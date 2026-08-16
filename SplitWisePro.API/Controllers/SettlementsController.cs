using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SplitWisePro.API.Hubs;
using SplitWisePro.Core.DTOs;
using SplitWisePro.Core.Entities;
using SplitWisePro.Core.Enums;
using SplitWisePro.Infrastructure.Data;

namespace SplitWisePro.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SettlementsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IHubContext<ExpenseHub> _hubContext;

    public SettlementsController(AppDbContext context, IHubContext<ExpenseHub> hubContext)
    {
        _context = context;
        _hubContext = hubContext;
    }

    /// <summary>GET /api/settlements/group/{groupId}</summary>
    [HttpGet("group/{groupId:guid}")]
    public async Task<IActionResult> GetGroupSettlements(Guid groupId, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (!await IsMember(groupId, userId.Value, ct)) return Forbid();

        var settlements = await _context.Settlements
            .Where(s => s.GroupId == groupId)
            .Include(s => s.PayerUser).Include(s => s.ReceiverUser)
            .OrderByDescending(s => s.SettlementDate)
            .Select(s => MapToDto(s)).ToListAsync(ct);

        return Ok(settlements);
    }

    /// <summary>POST /api/settlements — Record a settlement.</summary>
    [HttpPost]
    public async Task<IActionResult> CreateSettlement([FromBody] CreateSettlementRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (!await IsMember(request.GroupId, userId.Value, ct)) return Forbid();

        var settlement = new Settlement
        {
            Id = Guid.NewGuid(), GroupId = request.GroupId, PayerUserId = userId.Value,
            ReceiverUserId = request.ReceiverUserId, Amount = request.Amount,
            Currency = request.Currency, Notes = request.Notes,
            PaymentMethod = request.PaymentMethod, UpiTransactionId = request.UpiTransactionId
        };

        _context.Settlements.Add(settlement);
        await _context.SaveChangesAsync(ct);

        var created = await _context.Settlements.Include(s => s.PayerUser).Include(s => s.ReceiverUser).FirstAsync(s => s.Id == settlement.Id, ct);
        var dto = MapToDto(created);

        await _hubContext.Clients.Group($"group_{request.GroupId}").SendAsync("SettlementRecorded", dto, ct);
        return CreatedAtAction(nameof(GetGroupSettlements), new { groupId = request.GroupId }, dto);
    }

    /// <summary>PUT /api/settlements/{id}/confirm — Confirm a settlement (receiver only).</summary>
    [HttpPut("{id:guid}/confirm")]
    public async Task<IActionResult> ConfirmSettlement(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var settlement = await _context.Settlements.Include(s => s.PayerUser).Include(s => s.ReceiverUser).FirstOrDefaultAsync(s => s.Id == id, ct);
        if (settlement is null) return NotFound();
        if (settlement.ReceiverUserId != userId.Value) return Forbid();

        settlement.Status = SettlementStatus.Confirmed;
        await _context.SaveChangesAsync(ct);

        var dto = MapToDto(settlement);
        await _hubContext.Clients.Group($"group_{settlement.GroupId}").SendAsync("SettlementConfirmed", dto, ct);
        return Ok(dto);
    }

    /// <summary>PUT /api/settlements/{id}/reject — Reject a settlement (receiver only).</summary>
    [HttpPut("{id:guid}/reject")]
    public async Task<IActionResult> RejectSettlement(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var settlement = await _context.Settlements.Include(s => s.PayerUser).Include(s => s.ReceiverUser).FirstOrDefaultAsync(s => s.Id == id, ct);
        if (settlement is null) return NotFound();
        if (settlement.ReceiverUserId != userId.Value) return Forbid();

        settlement.Status = SettlementStatus.Rejected;
        await _context.SaveChangesAsync(ct);

        return Ok(MapToDto(settlement));
    }

    /// <summary>GET /api/settlements/upi-link — Generate UPI deep link for a settlement.</summary>
    [HttpGet("upi-link")]
    public async Task<IActionResult> GenerateUpiLink([FromQuery] Guid receiverUserId, [FromQuery] decimal amount, [FromQuery] Guid groupId, CancellationToken ct)
    {
        var receiver = await _context.Users.FindAsync(new object[] { receiverUserId }, ct);
        if (receiver is null) return NotFound(new { message = "Receiver not found." });
        if (string.IsNullOrWhiteSpace(receiver.UpiId))
            return BadRequest(new { message = "Receiver has not set up a UPI ID." });

        var group = await _context.Groups.FindAsync(new object[] { groupId }, ct);
        var note = $"SplitWisePro - {group?.Name ?? "Settlement"}";
        var upiUrl = $"upi://pay?pa={Uri.EscapeDataString(receiver.UpiId)}&pn={Uri.EscapeDataString(receiver.DisplayName)}&am={amount:F2}&cu=INR&tn={Uri.EscapeDataString(note)}";

        var qrCodeUrl = $"https://api.qrserver.com/v1/create-qr-code/?size=300x300&data={Uri.EscapeDataString(upiUrl)}";

        return Ok(new UpiDeepLinkResponse
        {
            UpiIntentUrl = upiUrl, PayeeName = receiver.DisplayName,
            PayeeUpiId = receiver.UpiId, Amount = amount, TransactionNote = note,
            QrCodeUrl = qrCodeUrl
        });
    }

    /// <summary>
    /// GET /api/settlements/breakdown/{groupId}/{receiverUserId}
    /// Returns a breakdown of why payer owes receiver money — lists all relevant expenses.
    /// </summary>
    [HttpGet("breakdown/{groupId:guid}/{receiverUserId:guid}")]
    public async Task<IActionResult> GetSettlementBreakdown(Guid groupId, Guid receiverUserId, CancellationToken ct)
    {
        var payerUserId = GetUserId();
        if (payerUserId is null) return Unauthorized();
        if (!await IsMember(groupId, payerUserId.Value, ct)) return Forbid();

        // Get all expenses in this group
        var allExpenses = await _context.Expenses
            .Where(e => e.GroupId == groupId && !e.IsDeleted)
            .Include(e => e.PaidByUser)
            .Include(e => e.Splits)
            .ToListAsync(ct);

        // Find expenses where receiver paid and payer owes a share
        var breakdown = allExpenses
            .Where(e => e.PaidByUserId == receiverUserId)
            .Select(e => {
                var payerSplit = e.Splits.FirstOrDefault(s => s.UserId == payerUserId.Value);
                return new SettlementBreakdownItem
                {
                    ExpenseId = e.Id,
                    Description = e.Description,
                    TotalAmount = e.Amount,
                    YourShare = payerSplit?.OwedAmount ?? 0,
                    Currency = e.Currency,
                    PaidBy = e.PaidByUser?.DisplayName ?? "Unknown",
                    Date = e.ExpenseDate,
                    Category = e.Category.ToString()
                };
            })
            .Where(b => b.YourShare > 0)
            .OrderByDescending(b => b.Date)
            .ToList();

        // Get receiver's UPI info
        var receiver = await _context.Users.FindAsync(new object[] { receiverUserId }, ct);
        var note = $"SplitWise Settlement";
        var totalOwed = breakdown.Sum(b => b.YourShare);
        string? upiLink = null;
        string? qrUrl = null;
        string? whatsappLink = null;

        if (receiver?.UpiId is not null)
        {
            upiLink = $"upi://pay?pa={Uri.EscapeDataString(receiver.UpiId)}&pn={Uri.EscapeDataString(receiver.DisplayName)}&am={totalOwed:F2}&cu=INR&tn={Uri.EscapeDataString(note)}";
            qrUrl = $"https://api.qrserver.com/v1/create-qr-code/?size=300x300&data={Uri.EscapeDataString(upiLink)}";
        }
        if (receiver?.PhoneNumber is not null)
        {
            var phone = receiver.PhoneNumber.Replace("+", "").Replace("-", "").Replace(" ", "");
            if (!phone.StartsWith("91")) phone = "91" + phone;
            whatsappLink = $"https://wa.me/{phone}?text={Uri.EscapeDataString($"Hi! Sending ₹{totalOwed:F2} for our expense settlement via SplitWise Pro 💸")}";
        }

        return Ok(new SettlementBreakdownResponse
        {
            ReceiverUserId = receiverUserId,
            ReceiverName = receiver?.DisplayName ?? "Unknown",
            ReceiverUpiId = receiver?.UpiId,
            ReceiverPhone = receiver?.PhoneNumber,
            TotalOwed = totalOwed,
            Currency = "INR",
            Breakdown = breakdown,
            UpiIntentUrl = upiLink,
            QrCodeUrl = qrUrl,
            WhatsAppUrl = whatsappLink
        });
    }

    private async Task<bool> IsMember(Guid groupId, Guid userId, CancellationToken ct) =>
        await _context.GroupMembers.AnyAsync(gm => gm.GroupId == groupId && gm.UserId == userId && !gm.HasLeft, ct);

    private Guid? GetUserId() { var c = User.FindFirst(ClaimTypes.NameIdentifier)?.Value; return c is not null ? Guid.Parse(c) : null; }

    private static SettlementDto MapToDto(Settlement s) => new()
    {
        Id = s.Id, GroupId = s.GroupId, PayerUserId = s.PayerUserId,
        PayerDisplayName = s.PayerUser?.DisplayName ?? "Unknown",
        ReceiverUserId = s.ReceiverUserId, ReceiverDisplayName = s.ReceiverUser?.DisplayName ?? "Unknown",
        Amount = s.Amount, Currency = s.Currency, Status = s.Status.ToString(),
        SettlementDate = s.SettlementDate, Notes = s.Notes,
        PaymentMethod = s.PaymentMethod, UpiTransactionId = s.UpiTransactionId, CreatedAt = s.CreatedAt
    };
}
