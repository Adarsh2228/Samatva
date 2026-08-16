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
public class ExpensesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IHubContext<ExpenseHub> _hubContext;

    public ExpensesController(AppDbContext context, IHubContext<ExpenseHub> hubContext)
    {
        _context = context;
        _hubContext = hubContext;
    }

    /// <summary>GET /api/expenses/group/{groupId} — Get all expenses for a group.</summary>
    [HttpGet("group/{groupId:guid}")]
    public async Task<IActionResult> GetGroupExpenses(Guid groupId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (!await IsMember(groupId, userId.Value, ct)) return Forbid();

        var expenses = await _context.Expenses
            .Where(e => e.GroupId == groupId)
            .Include(e => e.PaidByUser)
            .Include(e => e.Splits).ThenInclude(s => s.User)
            .OrderByDescending(e => e.ExpenseDate)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(e => MapToExpenseDto(e))
            .ToListAsync(ct);

        var total = await _context.Expenses.CountAsync(e => e.GroupId == groupId, ct);
        return Ok(new { data = expenses, total, page, pageSize });
    }

    /// <summary>GET /api/expenses/{id}</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetExpense(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var expense = await _context.Expenses.Include(e => e.PaidByUser).Include(e => e.Splits).ThenInclude(s => s.User).FirstOrDefaultAsync(e => e.Id == id, ct);
        if (expense is null) return NotFound();
        if (!await IsMember(expense.GroupId, userId.Value, ct)) return Forbid();

        return Ok(MapToExpenseDto(expense));
    }

    /// <summary>POST /api/expenses — Create a new expense with splits.</summary>
    [HttpPost]
    public async Task<IActionResult> CreateExpense([FromBody] CreateExpenseRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (!await IsMember(request.GroupId, userId.Value, ct)) return Forbid();

        var expense = new Expense
        {
            Id = Guid.NewGuid(), GroupId = request.GroupId, PaidByUserId = userId.Value,
            Description = request.Description, Amount = request.Amount, Currency = request.Currency,
            Category = request.Category, SplitType = request.SplitType,
            ExpenseDate = request.ExpenseDate ?? DateTime.UtcNow, Notes = request.Notes, ReceiptUrl = request.ReceiptUrl
        };

        // Calculate splits
        var splits = await CalculateSplits(expense, request.Splits, request.SplitType, ct);
        if (splits is null) return BadRequest(new { message = "Invalid split configuration." });

        _context.Expenses.Add(expense);
        _context.ExpenseSplits.AddRange(splits);
        await _context.SaveChangesAsync(ct);

        var created = await _context.Expenses.Include(e => e.PaidByUser).Include(e => e.Splits).ThenInclude(s => s.User).FirstAsync(e => e.Id == expense.Id, ct);
        var dto = MapToExpenseDto(created);

        await _hubContext.Clients.Group($"group_{request.GroupId}").SendAsync("ExpenseAdded", dto, ct);
        return CreatedAtAction(nameof(GetExpense), new { id = expense.Id }, dto);
    }

    /// <summary>PUT /api/expenses/{id}</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateExpense(Guid id, [FromBody] UpdateExpenseRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var expense = await _context.Expenses.Include(e => e.Splits).FirstOrDefaultAsync(e => e.Id == id, ct);
        if (expense is null) return NotFound();
        if (expense.PaidByUserId != userId.Value && !await IsAdmin(expense.GroupId, userId.Value, ct)) return Forbid();

        if (request.Description is not null) expense.Description = request.Description;
        if (request.Amount.HasValue) expense.Amount = request.Amount.Value;
        if (request.Currency is not null) expense.Currency = request.Currency;
        if (request.Category.HasValue) expense.Category = request.Category.Value;
        if (request.Notes is not null) expense.Notes = request.Notes;
        if (request.ExpenseDate.HasValue) expense.ExpenseDate = request.ExpenseDate.Value;

        if (request.SplitType.HasValue && request.Splits is not null)
        {
            _context.ExpenseSplits.RemoveRange(expense.Splits);
            expense.SplitType = request.SplitType.Value;
            var newSplits = await CalculateSplits(expense, request.Splits, request.SplitType.Value, ct);
            if (newSplits is null) return BadRequest(new { message = "Invalid split configuration." });
            _context.ExpenseSplits.AddRange(newSplits);
        }

        await _context.SaveChangesAsync(ct);
        var updated = await _context.Expenses.Include(e => e.PaidByUser).Include(e => e.Splits).ThenInclude(s => s.User).FirstAsync(e => e.Id == id, ct);
        var dto = MapToExpenseDto(updated);

        await _hubContext.Clients.Group($"group_{expense.GroupId}").SendAsync("ExpenseUpdated", dto, ct);
        return Ok(dto);
    }

    /// <summary>DELETE /api/expenses/{id}</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteExpense(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var expense = await _context.Expenses.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (expense is null) return NotFound();
        if (expense.PaidByUserId != userId.Value && !await IsAdmin(expense.GroupId, userId.Value, ct)) return Forbid();

        expense.IsDeleted = true;
        await _context.SaveChangesAsync(ct);

        await _hubContext.Clients.Group($"group_{expense.GroupId}").SendAsync("ExpenseDeleted", id, ct);
        return Ok(new { message = "Expense deleted." });
    }

    /// <summary>GET /api/expenses/group/{groupId}/balances — Get net balances for all members.</summary>
    [HttpGet("group/{groupId:guid}/balances")]
    public async Task<IActionResult> GetGroupBalances(Guid groupId, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (!await IsMember(groupId, userId.Value, ct)) return Forbid();

        var group = await _context.Groups.Include(g => g.Members.Where(m => !m.HasLeft)).ThenInclude(m => m.User).FirstOrDefaultAsync(g => g.Id == groupId, ct);
        if (group is null) return NotFound();

        var expenses = await _context.Expenses.Where(e => e.GroupId == groupId).Include(e => e.Splits).ToListAsync(ct);
        var settlements = await _context.Settlements.Where(s => s.GroupId == groupId && s.Status == SettlementStatus.Confirmed).ToListAsync(ct);

        var balanceMap = new Dictionary<Guid, decimal>();
        foreach (var m in group.Members) balanceMap[m.UserId] = 0;

        foreach (var exp in expenses)
        {
            balanceMap[exp.PaidByUserId] += exp.Amount;
            foreach (var split in exp.Splits)
            {
                if (balanceMap.ContainsKey(split.UserId))
                    balanceMap[split.UserId] -= split.OwedAmount;
            }
        }

        foreach (var stl in settlements)
        {
            if (balanceMap.ContainsKey(stl.PayerUserId)) balanceMap[stl.PayerUserId] += stl.Amount;
            if (balanceMap.ContainsKey(stl.ReceiverUserId)) balanceMap[stl.ReceiverUserId] -= stl.Amount;
        }

        var balances = group.Members.Select(m => new BalanceDto
        {
            UserId = m.UserId, DisplayName = m.User?.DisplayName ?? "Unknown",
            NetBalance = balanceMap.GetValueOrDefault(m.UserId), Currency = group.DefaultCurrency
        }).ToList();

        return Ok(balances);
    }

    /// <summary>GET /api/expenses/group/{groupId}/simplify — Get simplified debts.</summary>
    [HttpGet("group/{groupId:guid}/simplify")]
    public async Task<IActionResult> SimplifyDebts(Guid groupId, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (!await IsMember(groupId, userId.Value, ct)) return Forbid();

        var group = await _context.Groups.Include(g => g.Members.Where(m => !m.HasLeft)).ThenInclude(m => m.User).FirstOrDefaultAsync(g => g.Id == groupId, ct);
        if (group is null) return NotFound();

        var expenses = await _context.Expenses.Where(e => e.GroupId == groupId).Include(e => e.Splits).ToListAsync(ct);
        var settlements = await _context.Settlements.Where(s => s.GroupId == groupId && s.Status == SettlementStatus.Confirmed).ToListAsync(ct);

        var balanceMap = new Dictionary<Guid, decimal>();
        foreach (var m in group.Members) balanceMap[m.UserId] = 0;

        foreach (var exp in expenses)
        {
            balanceMap[exp.PaidByUserId] += exp.Amount;
            foreach (var split in exp.Splits)
                if (balanceMap.ContainsKey(split.UserId))
                    balanceMap[split.UserId] -= split.OwedAmount;
        }
        foreach (var stl in settlements)
        {
            if (balanceMap.ContainsKey(stl.PayerUserId)) balanceMap[stl.PayerUserId] += stl.Amount;
            if (balanceMap.ContainsKey(stl.ReceiverUserId)) balanceMap[stl.ReceiverUserId] -= stl.Amount;
        }

        // Greedy debt simplification
        var creditors = balanceMap.Where(b => b.Value > 0.01m).OrderByDescending(b => b.Value).ToList();
        var debtors = balanceMap.Where(b => b.Value < -0.01m).OrderBy(b => b.Value).ToList();
        var memberMap = group.Members.ToDictionary(m => m.UserId, m => m.User?.DisplayName ?? "Unknown");
        var simplified = new List<DebtSimplificationDto>();

        int i = 0, j = 0;
        while (i < creditors.Count && j < debtors.Count)
        {
            var credit = creditors[i].Value;
            var debt = Math.Abs(debtors[j].Value);
            var amount = Math.Min(credit, debt);

            simplified.Add(new DebtSimplificationDto
            {
                FromUserId = debtors[j].Key, FromDisplayName = memberMap.GetValueOrDefault(debtors[j].Key, "Unknown"),
                ToUserId = creditors[i].Key, ToDisplayName = memberMap.GetValueOrDefault(creditors[i].Key, "Unknown"),
                Amount = Math.Round(amount, 2), Currency = group.DefaultCurrency
            });

            creditors[i] = new(creditors[i].Key, credit - amount);
            debtors[j] = new(debtors[j].Key, debtors[j].Value + amount);
            if (creditors[i].Value < 0.01m) i++;
            if (Math.Abs(debtors[j].Value) < 0.01m) j++;
        }

        return Ok(simplified);
    }

    /// <summary>GET /api/expenses/group/{groupId}/analytics — Get spending analytics.</summary>
    [HttpGet("group/{groupId:guid}/analytics")]
    public async Task<IActionResult> GetSpendingAnalytics(Guid groupId, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (!await IsMember(groupId, userId.Value, ct)) return Forbid();

        var group = await _context.Groups
            .Include(g => g.Members.Where(m => !m.HasLeft)).ThenInclude(m => m.User)
            .FirstOrDefaultAsync(g => g.Id == groupId, ct);
        if (group is null) return NotFound();

        var expenses = await _context.Expenses
            .Where(e => e.GroupId == groupId)
            .Include(e => e.PaidByUser)
            .Include(e => e.Splits)
            .ToListAsync(ct);

        var settlements = await _context.Settlements
            .Where(s => s.GroupId == groupId && s.Status == SettlementStatus.Confirmed)
            .ToListAsync(ct);

        // Category breakdown
        var categoryGroups = expenses
            .GroupBy(e => e.Category.ToString())
            .Select(g => new CategoryBreakdownDto
            {
                Category = g.Key,
                Amount = g.Sum(e => e.Amount),
                Count = g.Count(),
                Percentage = expenses.Sum(e => e.Amount) > 0
                    ? Math.Round(g.Sum(e => e.Amount) / expenses.Sum(e => e.Amount) * 100, 1)
                    : 0
            })
            .OrderByDescending(c => c.Amount)
            .ToList();

        // Daily spending (last 30 days)
        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
        var dailySpending = expenses
            .Where(e => e.ExpenseDate >= thirtyDaysAgo)
            .GroupBy(e => e.ExpenseDate.Date)
            .Select(g => new DailySpendDto
            {
                Date = g.Key.ToString("yyyy-MM-dd"),
                Amount = g.Sum(e => e.Amount),
                Count = g.Count()
            })
            .OrderBy(d => d.Date)
            .ToList();

        // Fill missing days
        for (var d = thirtyDaysAgo.Date; d <= DateTime.UtcNow.Date; d = d.AddDays(1))
        {
            var dateStr = d.ToString("yyyy-MM-dd");
            if (!dailySpending.Any(ds => ds.Date == dateStr))
                dailySpending.Add(new DailySpendDto { Date = dateStr, Amount = 0, Count = 0 });
        }
        dailySpending = dailySpending.OrderBy(d => d.Date).ToList();

        // Member spending
        var balanceMap = new Dictionary<Guid, decimal>();
        foreach (var m in group.Members) balanceMap[m.UserId] = 0;
        foreach (var exp in expenses)
        {
            balanceMap[exp.PaidByUserId] += exp.Amount;
            foreach (var split in exp.Splits)
                if (balanceMap.ContainsKey(split.UserId))
                    balanceMap[split.UserId] -= split.OwedAmount;
        }
        foreach (var stl in settlements)
        {
            if (balanceMap.ContainsKey(stl.PayerUserId)) balanceMap[stl.PayerUserId] += stl.Amount;
            if (balanceMap.ContainsKey(stl.ReceiverUserId)) balanceMap[stl.ReceiverUserId] -= stl.Amount;
        }

        var memberSpending = group.Members.Select(m =>
        {
            var paid = expenses.Where(e => e.PaidByUserId == m.UserId).Sum(e => e.Amount);
            var owed = expenses.SelectMany(e => e.Splits).Where(s => s.UserId == m.UserId).Sum(s => s.OwedAmount);
            return new MemberSpendDto
            {
                UserId = m.UserId,
                DisplayName = m.User?.DisplayName ?? "Unknown",
                TotalPaid = paid,
                TotalOwed = owed,
                NetBalance = balanceMap.GetValueOrDefault(m.UserId)
            };
        }).ToList();

        var myOwed = expenses.SelectMany(e => e.Splits).Where(s => s.UserId == userId.Value).Sum(s => s.OwedAmount);
        var myBalance = balanceMap.GetValueOrDefault(userId.Value);

        return Ok(new SpendingAnalyticsDto
        {
            TotalSpent = expenses.Sum(e => e.Amount),
            TotalOwed = myOwed,
            TotalOwedToYou = myBalance > 0 ? myBalance : 0,
            Currency = group.DefaultCurrency,
            CategoryBreakdown = categoryGroups,
            DailySpending = dailySpending,
            MemberSpending = memberSpending,
            TopCategory = categoryGroups.FirstOrDefault()?.Category ?? "None",
            AverageExpense = expenses.Count > 0 ? Math.Round(expenses.Average(e => e.Amount), 2) : 0,
            TotalTransactions = expenses.Count
        });
    }

    // ── Helpers ────────────────────────────────────────────────────────

    private async Task<List<ExpenseSplit>?> CalculateSplits(Expense expense, List<SplitDetailRequest> splitDetails, SplitType splitType, CancellationToken ct)
    {
        var splits = new List<ExpenseSplit>();

        if (splitType == SplitType.Equal)
        {
            List<Guid> participantIds;
            if (splitDetails.Count > 0)
                participantIds = splitDetails.Select(s => s.UserId).ToList();
            else
                participantIds = await _context.GroupMembers.Where(gm => gm.GroupId == expense.GroupId && !gm.HasLeft).Select(gm => gm.UserId).ToListAsync(ct);

            if (participantIds.Count == 0) return null;
            var share = Math.Round(expense.Amount / participantIds.Count, 4);
            foreach (var uid in participantIds)
                splits.Add(new ExpenseSplit { Id = Guid.NewGuid(), ExpenseId = expense.Id, UserId = uid, OwedAmount = share });
        }
        else if (splitType == SplitType.Exact)
        {
            if (splitDetails.Count == 0) return null;
            var total = splitDetails.Sum(s => s.Value ?? 0);
            if (Math.Abs(total - expense.Amount) > 0.01m) return null;
            foreach (var sd in splitDetails)
                splits.Add(new ExpenseSplit { Id = Guid.NewGuid(), ExpenseId = expense.Id, UserId = sd.UserId, OwedAmount = sd.Value ?? 0, ShareValue = sd.Value });
        }
        else if (splitType == SplitType.Percentage)
        {
            if (splitDetails.Count == 0) return null;
            var totalPct = splitDetails.Sum(s => s.Value ?? 0);
            if (Math.Abs(totalPct - 100) > 0.01m) return null;
            foreach (var sd in splitDetails)
                splits.Add(new ExpenseSplit { Id = Guid.NewGuid(), ExpenseId = expense.Id, UserId = sd.UserId, OwedAmount = Math.Round(expense.Amount * (sd.Value ?? 0) / 100, 4), ShareValue = sd.Value });
        }
        else if (splitType == SplitType.Shares)
        {
            if (splitDetails.Count == 0) return null;
            var totalShares = splitDetails.Sum(s => s.Value ?? 0);
            if (totalShares <= 0) return null;
            foreach (var sd in splitDetails)
                splits.Add(new ExpenseSplit { Id = Guid.NewGuid(), ExpenseId = expense.Id, UserId = sd.UserId, OwedAmount = Math.Round(expense.Amount * (sd.Value ?? 0) / totalShares, 4), ShareValue = sd.Value });
        }

        return splits;
    }

    private async Task<bool> IsMember(Guid groupId, Guid userId, CancellationToken ct) =>
        await _context.GroupMembers.AnyAsync(gm => gm.GroupId == groupId && gm.UserId == userId && !gm.HasLeft, ct);

    private async Task<bool> IsAdmin(Guid groupId, Guid userId, CancellationToken ct) =>
        await _context.GroupMembers.AnyAsync(gm => gm.GroupId == groupId && gm.UserId == userId && !gm.HasLeft && (gm.Role == GroupRole.Admin || gm.Role == GroupRole.Owner), ct);

    private Guid? GetUserId() { var c = User.FindFirst(ClaimTypes.NameIdentifier)?.Value; return c is not null ? Guid.Parse(c) : null; }

    private static ExpenseDto MapToExpenseDto(Expense e) => new()
    {
        Id = e.Id, GroupId = e.GroupId, PaidByUserId = e.PaidByUserId,
        PaidByDisplayName = e.PaidByUser?.DisplayName ?? "Unknown", Description = e.Description,
        Amount = e.Amount, Currency = e.Currency, ExchangeRate = e.ExchangeRate,
        Category = e.Category.ToString(), SplitType = e.SplitType.ToString(),
        ExpenseDate = e.ExpenseDate, ReceiptUrl = e.ReceiptUrl, Notes = e.Notes,
        IsAiGenerated = e.IsAiGenerated, CreatedAt = e.CreatedAt,
        Splits = e.Splits.Select(s => new ExpenseSplitDto
        {
            Id = s.Id, UserId = s.UserId, UserDisplayName = s.User?.DisplayName ?? "Unknown",
            OwedAmount = s.OwedAmount, ShareValue = s.ShareValue, IsSettled = s.IsSettled
        }).ToList()
    };
}
