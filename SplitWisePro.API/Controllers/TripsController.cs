using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SplitWisePro.Core.DTOs;
using SplitWisePro.Core.Entities;
using SplitWisePro.Infrastructure.Data;

namespace SplitWisePro.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TripsController : ControllerBase
{
    private readonly AppDbContext _db;

    public TripsController(AppDbContext db) => _db = db;

    // ── GET /api/trips — My trips ──────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> GetMyTrips(CancellationToken ct)
    {
        var userId = GetUserId(); if (userId is null) return Unauthorized();
        var trips = await _db.Trips
            .Include(t => t.AdminUser)
            .Include(t => t.Members).ThenInclude(m => m.User)
            .Where(t => t.Members.Any(m => m.UserId == userId.Value) || t.AdminUserId == userId.Value)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);
        var currentUserId = userId.Value;
        return Ok(trips.Select(t => MapToDto(t, currentUserId)));
    }

    // ── GET /api/trips/{id} — Get one trip ────────────────────────
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetTrip(Guid id, CancellationToken ct)
    {
        var userId = GetUserId(); if (userId is null) return Unauthorized();
        var trip = await LoadTrip(id, ct);
        if (trip is null) return NotFound();
        if (!IsMember(trip, userId.Value)) return Forbid();
        return Ok(MapToDto(trip, userId.Value));
    }

    // ── POST /api/trips — Create trip ─────────────────────────────
    [HttpPost]
    public async Task<IActionResult> CreateTrip([FromBody] CreateTripRequest req, CancellationToken ct)
    {
        var userId = GetUserId(); if (userId is null) return Unauthorized();
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var code = GenerateTripCode();
        // Ensure uniqueness
        while (await _db.Trips.AnyAsync(t => t.TripCode == code, ct))
            code = GenerateTripCode();

        var trip = new Trip
        {
            Id = Guid.NewGuid(), Name = req.Name, Description = req.Description,
            Destination = req.Destination, Budget = req.Budget, Currency = req.Currency,
            StartDate = req.StartDate, EndDate = req.EndDate,
            TripCode = code, AdminUserId = userId.Value, IsActive = true
        };

        // Auto-add admin as member
        var adminMember = new TripMember { Id = Guid.NewGuid(), TripId = trip.Id, UserId = userId.Value };
        _db.Trips.Add(trip);
        _db.TripMembers.Add(adminMember);
        await _db.SaveChangesAsync(ct);

        var created = await LoadTrip(trip.Id, ct);
        return CreatedAtAction(nameof(GetTrip), new { id = trip.Id }, MapToDto(created!, userId.Value));
    }

    // ── POST /api/trips/join — Join via code ──────────────────────
    [HttpPost("join")]
    public async Task<IActionResult> JoinTrip([FromBody] JoinTripRequest req, CancellationToken ct)
    {
        var userId = GetUserId(); if (userId is null) return Unauthorized();
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var trip = await _db.Trips
            .Include(t => t.Members)
            .FirstOrDefaultAsync(t => t.TripCode == req.TripCode.ToUpper().Trim(), ct);
        if (trip is null) return NotFound(new { message = "Invalid trip code." });
        if (!trip.IsActive) return BadRequest(new { message = "This trip is no longer active." });
        if (trip.Members.Any(m => m.UserId == userId.Value))
            return BadRequest(new { message = "You are already a member of this trip." });

        _db.TripMembers.Add(new TripMember { Id = Guid.NewGuid(), TripId = trip.Id, UserId = userId.Value });
        await _db.SaveChangesAsync(ct);
        var updated = await LoadTrip(trip.Id, ct);
        return Ok(MapToDto(updated!, userId.Value));
    }

    // ── PUT /api/trips/{id}/budget — Update budget (admin only) ───
    [HttpPut("{id:guid}/budget")]
    public async Task<IActionResult> UpdateBudget(Guid id, [FromBody] UpdateTripBudgetRequest req, CancellationToken ct)
    {
        var userId = GetUserId(); if (userId is null) return Unauthorized();
        var trip = await LoadTrip(id, ct);
        if (trip is null) return NotFound();
        if (trip.AdminUserId != userId.Value) return Forbid();
        trip.Budget = req.Budget;
        await _db.SaveChangesAsync(ct);
        return Ok(MapToDto(trip, userId.Value));
    }

    // ── GET /api/trips/{id}/expenses — Get all expenses ───────────
    [HttpGet("{id:guid}/expenses")]
    public async Task<IActionResult> GetExpenses(Guid id, CancellationToken ct)
    {
        var userId = GetUserId(); if (userId is null) return Unauthorized();
        var trip = await LoadTrip(id, ct);
        if (trip is null) return NotFound();
        if (!IsMember(trip, userId.Value)) return Forbid();

        // Load ALL expenses including rejected — rejected stay visible
        var expenses = await _db.TripExpenses
            .IgnoreQueryFilters()
            .Where(e => e.TripId == id && !e.IsDeleted)
            .Include(e => e.AddedByUser)
            .Include(e => e.RejectedByUser)
            .OrderByDescending(e => e.SpentAt)
            .ToListAsync(ct);

        return Ok(expenses.Select(MapToExpenseDto));
    }

    // ── POST /api/trips/{id}/expenses — Add expense ───────────────
    [HttpPost("{id:guid}/expenses")]
    public async Task<IActionResult> AddExpense(Guid id, [FromBody] AddTripExpenseRequest req, CancellationToken ct)
    {
        var userId = GetUserId(); if (userId is null) return Unauthorized();
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var trip = await LoadTrip(id, ct);
        if (trip is null) return NotFound();
        if (!IsMember(trip, userId.Value)) return Forbid();
        if (!trip.IsActive) return BadRequest(new { message = "Trip is closed." });

        var expense = new TripExpense
        {
            Id = Guid.NewGuid(), TripId = id, AddedByUserId = userId.Value,
            Description = req.Description, Reason = req.Reason,
            Amount = req.Amount, Currency = req.Currency,
            SpentAt = req.SpentAt, ScreenshotData = req.ScreenshotData,
            Category = req.Category
        };
        _db.TripExpenses.Add(expense);
        await _db.SaveChangesAsync(ct);

        var created = await _db.TripExpenses
            .IgnoreQueryFilters()
            .Include(e => e.AddedByUser)
            .Include(e => e.RejectedByUser)
            .FirstAsync(e => e.Id == expense.Id, ct);
        return CreatedAtAction(nameof(GetExpenses), new { id }, MapToExpenseDto(created));
    }

    // ── PUT /api/trips/{id}/expenses/{expId}/reject — Admin rejects ─
    [HttpPut("{id:guid}/expenses/{expId:guid}/reject")]
    public async Task<IActionResult> RejectExpense(Guid id, Guid expId, [FromBody] RejectTripExpenseRequest req, CancellationToken ct)
    {
        var userId = GetUserId(); if (userId is null) return Unauthorized();
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var trip = await _db.Trips.FindAsync(new object[] { id }, ct);
        if (trip is null) return NotFound();
        if (trip.AdminUserId != userId.Value) return Forbid();

        var expense = await _db.TripExpenses
            .IgnoreQueryFilters()
            .Include(e => e.AddedByUser)
            .Include(e => e.RejectedByUser)
            .FirstOrDefaultAsync(e => e.Id == expId && e.TripId == id, ct);
        if (expense is null) return NotFound();
        if (expense.IsRejected) return BadRequest(new { message = "Expense is already rejected." });

        expense.IsRejected = true;
        expense.RejectionReason = req.Reason;
        expense.RejectedByUserId = userId.Value;
        expense.RejectedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(MapToExpenseDto(expense));
    }

    // ── PUT /api/trips/{id}/expenses/{expId}/restore — Admin restores ─
    [HttpPut("{id:guid}/expenses/{expId:guid}/restore")]
    public async Task<IActionResult> RestoreExpense(Guid id, Guid expId, CancellationToken ct)
    {
        var userId = GetUserId(); if (userId is null) return Unauthorized();
        var trip = await _db.Trips.FindAsync(new object[] { id }, ct);
        if (trip is null) return NotFound();
        if (trip.AdminUserId != userId.Value) return Forbid();

        var expense = await _db.TripExpenses
            .IgnoreQueryFilters()
            .Include(e => e.AddedByUser)
            .Include(e => e.RejectedByUser)
            .FirstOrDefaultAsync(e => e.Id == expId && e.TripId == id, ct);
        if (expense is null) return NotFound();

        expense.IsRejected = false;
        expense.RejectionReason = null;
        expense.RejectedByUserId = null;
        expense.RejectedAt = null;
        await _db.SaveChangesAsync(ct);
        return Ok(MapToExpenseDto(expense));
    }

    // ── DELETE /api/trips/{id}/expenses/{expId} — Hard delete (admin) ─
    [HttpDelete("{id:guid}/expenses/{expId:guid}")]
    public async Task<IActionResult> DeleteExpense(Guid id, Guid expId, [FromBody] RejectTripExpenseRequest req, CancellationToken ct)
    {
        var userId = GetUserId(); if (userId is null) return Unauthorized();
        var trip = await _db.Trips.FindAsync(new object[] { id }, ct);
        if (trip is null) return NotFound();
        if (trip.AdminUserId != userId.Value) return Forbid();

        var expense = await _db.TripExpenses
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.Id == expId && e.TripId == id, ct);
        if (expense is null) return NotFound();

        // Mark as rejected first (stays visible), then soft-delete
        expense.IsRejected = true;
        expense.RejectionReason = req.Reason;
        expense.RejectedByUserId = userId.Value;
        expense.RejectedAt = DateTime.UtcNow;
        expense.IsDeleted = true;
        await _db.SaveChangesAsync(ct);
        return Ok(new { message = "Expense deleted." });
    }

    // ── GET /api/trips/join-info/{code} — Preview trip before joining ─
    [HttpGet("join-info/{code}")]
    public async Task<IActionResult> GetJoinInfo(string code, CancellationToken ct)
    {
        var trip = await _db.Trips
            .Include(t => t.AdminUser)
            .Include(t => t.Members).ThenInclude(m => m.User)
            .FirstOrDefaultAsync(t => t.TripCode == code.ToUpper().Trim(), ct);
        if (trip is null) return NotFound(new { message = "Invalid trip code." });
        return Ok(new
        {
            id = trip.Id, name = trip.Name, destination = trip.Destination,
            budget = trip.Budget, currency = trip.Currency,
            adminName = trip.AdminUser.DisplayName,
            memberCount = trip.Members.Count,
            isActive = trip.IsActive
        });
    }

    // ── GET /api/trips/{id}/join-info redundant with top — skip ──
    // ── POST /api/trips/{id}/chat — Trip AI chatbot ───────────────
    [HttpPost("{id:guid}/chat")]
    public async Task<IActionResult> TripChat(Guid id, [FromBody] TripChatRequest req, CancellationToken ct)
    {
        var userId = GetUserId(); if (userId is null) return Unauthorized();
        var trip = await LoadTrip(id, ct);
        if (trip is null) return NotFound();
        if (!IsMember(trip, userId.Value)) return Forbid();

        var msg = (req.Message ?? "").ToLower().Trim();

        // Load all expenses
        var expenses = await _db.TripExpenses
            .IgnoreQueryFilters()
            .Where(e => e.TripId == id && !e.IsDeleted)
            .Include(e => e.AddedByUser)
            .ToListAsync(ct);

        var validExpenses = expenses.Where(e => !e.IsRejected).ToList();
        var rejectedExpenses = expenses.Where(e => e.IsRejected).ToList();
        var totalSpent = validExpenses.Sum(e => e.Amount);
        var remaining = trip.Budget - totalSpent;
        var sym = trip.Currency == "INR" ? "₹" : trip.Currency;
        var currentUser = await _db.Users.FindAsync(new object[] { userId.Value }, ct);
        var userName = currentUser?.DisplayName ?? "You";

        string answer;

        // ── Pattern Matching AI ─────────────────────────────────
        bool Has(params string[] keywords) => keywords.Any(k => msg.Contains(k));

        if (Has("budget", "how much left", "remaining", "bacha", "kitna bacha", "balance"))
        {
            var pct = trip.Budget > 0 ? (totalSpent / trip.Budget * 100) : 0;
            if (remaining >= 0)
                answer = $"💰 **Budget Status**\n\nYou've used **{sym}{totalSpent:N0}** out of **{sym}{trip.Budget:N0}** ({pct:N0}% used).\n\n✅ **{sym}{remaining:N0} remaining** — you're within budget!";
            else
                answer = $"⚠️ **Over Budget!**\n\nYou've spent **{sym}{totalSpent:N0}** against a budget of **{sym}{trip.Budget:N0}**.\n\n🚨 You are **{sym}{Math.Abs(remaining):N0} over budget!** Consider reviewing expenses.";
        }
        else if (Has("category", "categories", "most spent", "kitne category", "kahan kharch"))
        {
            if (!validExpenses.Any())
            { answer = "📊 No expenses logged yet to categorize!"; }
            else
            {
                var cats = validExpenses.GroupBy(e => e.Category)
                    .OrderByDescending(g => g.Sum(e => e.Amount))
                    .Select(g => $"• **{g.Key}**: {sym}{g.Sum(e => e.Amount):N0} ({g.Count()} entries)")
                    .ToList();
                answer = $"📊 **Spending by Category**\n\n{string.Join("\n", cats)}";
            }
        }
        else if (Has("who paid", "sabse jyada", "most", "top spender", "biggest"))
        {
            if (!validExpenses.Any())
            { answer = "🔍 No expenses yet to analyze!"; }
            else
            {
                var byMember = validExpenses.GroupBy(e => e.AddedByUser?.DisplayName ?? "Unknown")
                    .OrderByDescending(g => g.Sum(e => e.Amount))
                    .Select((g, i) => $"{(i == 0 ? "🥇" : i == 1 ? "🥈" : "🥉")} **{g.Key}**: {sym}{g.Sum(e => e.Amount):N0} ({g.Count()} spends)")
                    .Take(5).ToList();
                answer = $"👥 **Member Spending Leaderboard**\n\n{string.Join("\n", byMember)}";
            }
        }
        else if (Has("total", "kitna kharch", "how much spent", "total spend", "overall"))
        {
            answer = $"💸 **Total Spending**\n\nThe trip has **{validExpenses.Count} expense(s)** totalling **{sym}{totalSpent:N0}**.\n\nAverage spend per entry: **{sym}{(validExpenses.Any() ? validExpenses.Average(e => e.Amount) : 0):N0}**\n\nBudget utilization: **{(trip.Budget > 0 ? totalSpent / trip.Budget * 100 : 0):N0}%**";
        }
        else if (Has("rejected", "reject", "removed", "deleted", "invalid"))
        {
            if (!rejectedExpenses.Any())
                answer = "✅ No entries have been rejected on this trip!";
            else
            {
                var rejList = rejectedExpenses.Select(e => $"• {e.Description} ({sym}{e.Amount:N0}) — Reason: *{e.RejectionReason}*").ToList();
                answer = $"🚫 **Rejected Entries ({rejectedExpenses.Count})**\n\n{string.Join("\n", rejList)}\n\nRejected amounts are **not** counted in total spent.";
            }
        }
        else if (Has("member", "who", "log", "person", "kaun"))
        {
            var memberList = trip.Members.Select(m => $"• {m.User?.DisplayName ?? "?"}{(m.UserId == trip.AdminUserId ? " 👑 Admin" : "")}").ToList();
            answer = $"👥 **Trip Members ({trip.Members.Count})**\n\n{string.Join("\n", memberList)}";
        }
        else if (Has("recent", "last", "latest", "aakhri"))
        {
            var recent = validExpenses.OrderByDescending(e => e.SpentAt).Take(3).ToList();
            if (!recent.Any())
            { answer = "📋 No expenses logged yet!"; }
            else
            {
                var items = recent.Select(e => $"• **{e.Description}** — {sym}{e.Amount:N0} by {e.AddedByUser?.DisplayName} ({e.SpentAt:dd MMM, h:mm tt})").ToList();
                answer = $"🕐 **Recent Expenses**\n\n{string.Join("\n", items)}";
            }
        }
        else if (Has("food", "khana", "eat", "restaurant", "lunch", "dinner", "breakfast"))
        {
            var foodExp = validExpenses.Where(e => e.Category.Equals("Food", StringComparison.OrdinalIgnoreCase)).ToList();
            if (!foodExp.Any())
                answer = "🍽️ No food expenses logged yet!";
            else
                answer = $"🍽️ **Food Expenses**\n\n{foodExp.Count} entries totalling **{sym}{foodExp.Sum(e => e.Amount):N0}**.\n\n" + string.Join("\n", foodExp.Take(5).Select(e => $"• {e.Description}: {sym}{e.Amount:N0}"));
        }
        else if (Has("transport", "travel", "bus", "train", "flight", "cab", "ticket"))
        {
            var tExp = validExpenses.Where(e => e.Category.Equals("Transport", StringComparison.OrdinalIgnoreCase)).ToList();
            if (!tExp.Any())
                answer = "🚗 No transport expenses logged yet!";
            else
                answer = $"🚗 **Transport Expenses**\n\n{tExp.Count} entries totalling **{sym}{tExp.Sum(e => e.Amount):N0}**.\n\n" + string.Join("\n", tExp.Take(5).Select(e => $"• {e.Description}: {sym}{e.Amount:N0}"));
        }
        else if (Has("summary", "brief", "overview", "report", "all"))
        {
            var cats = validExpenses.GroupBy(e => e.Category)
                .OrderByDescending(g => g.Sum(e => e.Amount))
                .Select(g => $"  • {g.Key}: {sym}{g.Sum(e => e.Amount):N0}");
            answer = $"📋 **Trip Summary: {trip.Name}**\n\n" +
                     $"📍 Destination: {trip.Destination ?? "Not set"}\n" +
                     $"👥 Members: {trip.Members.Count}\n" +
                     $"💰 Budget: {sym}{trip.Budget:N0}\n" +
                     $"💸 Spent: {sym}{totalSpent:N0} ({(trip.Budget > 0 ? totalSpent / trip.Budget * 100 : 0):N0}%)\n" +
                     $"✅ Remaining: {sym}{remaining:N0}\n" +
                     $"🧾 Expenses: {validExpenses.Count} ({rejectedExpenses.Count} rejected)\n\n" +
                     $"**By Category:**\n{string.Join("\n", cats)}";
        }
        else if (Has("help", "what can", "kya kar", "options", "commands"))
        {
            answer = "🤖 **I can help you with:**\n\n• 💰 Budget & remaining amount\n• 📊 Category breakdown\n• 👥 Who spent the most\n• 💸 Total spending\n• 🚫 Rejected entries\n• 🕐 Recent expenses\n• 📋 Full trip summary\n• 🍽️ Food / 🚗 Transport breakdown\n\nJust ask naturally! e.g. *\"How much is left in budget?\"*, *\"Who paid the most?\"*, *\"Show summary\"*";
        }
        else
        {
            // Generic fallback with context
            answer = $"🤔 I'm not sure about that specific question, but here's a quick snapshot:\n\n💰 Budget: {sym}{trip.Budget:N0} | Spent: {sym}{totalSpent:N0} | Left: {sym}{remaining:N0}\n🧾 {validExpenses.Count} expense(s) logged by {trip.Members.Count} member(s)\n\nTry asking: *\"Show summary\"*, *\"Who paid most?\"*, or *\"Budget status\"*";
        }

        return Ok(new { answer, timestamp = DateTime.UtcNow });
    }

    // ── Helpers ────────────────────────────────────────────────────
    private static string GenerateTripCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var rng = new Random();
        return new string(Enumerable.Range(0, 8).Select(_ => chars[rng.Next(chars.Length)]).ToArray());
    }

    private async Task<Trip?> LoadTrip(Guid id, CancellationToken ct) =>
        await _db.Trips
            .Include(t => t.AdminUser)
            .Include(t => t.Members).ThenInclude(m => m.User)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    private static bool IsMember(Trip trip, Guid userId) =>
        trip.AdminUserId == userId || trip.Members.Any(m => m.UserId == userId);

    private Guid? GetUserId()
    {
        var c = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return c is not null ? Guid.Parse(c) : null;
    }

    private TripDto MapToDto(Trip t, Guid currentUserId)
    {
        var validExpenses = _db.TripExpenses
            .IgnoreQueryFilters()
            .Where(e => e.TripId == t.Id && !e.IsDeleted && !e.IsRejected)
            .Sum(e => (decimal?)e.Amount) ?? 0;

        var code = t.TripCode;
        var qrData = $"splitwisepro://join-trip?code={code}";
        var qrUrl = $"https://api.qrserver.com/v1/create-qr-code/?size=300x300&data={Uri.EscapeDataString(qrData)}";

        return new TripDto
        {
            Id = t.Id, Name = t.Name, Description = t.Description,
            Destination = t.Destination, Budget = t.Budget,
            TotalSpent = validExpenses,
            RemainingBudget = t.Budget - validExpenses,
            Currency = t.Currency,
            StartDate = t.StartDate, EndDate = t.EndDate,
            TripCode = t.TripCode, QrCodeUrl = qrUrl,
            AdminUserId = t.AdminUserId,
            AdminDisplayName = t.AdminUser?.DisplayName ?? "Unknown",
            IsActive = t.IsActive,
            IsAdmin = t.AdminUserId == currentUserId,
            CreatedAt = t.CreatedAt,
            Members = t.Members.Select(m => new TripMemberDto
            {
                UserId = m.UserId,
                DisplayName = m.User?.DisplayName ?? "Unknown",
                AvatarUrl = m.User?.AvatarUrl,
                JoinedAt = m.JoinedAt,
                IsAdmin = m.UserId == t.AdminUserId
            }).ToList()
        };
    }

    private static TripExpenseDto MapToExpenseDto(TripExpense e) => new()
    {
        Id = e.Id, TripId = e.TripId, AddedByUserId = e.AddedByUserId,
        AddedByDisplayName = e.AddedByUser?.DisplayName ?? "Unknown",
        Description = e.Description, Reason = e.Reason,
        Amount = e.Amount, Currency = e.Currency,
        SpentAt = e.SpentAt, ScreenshotData = e.ScreenshotData,
        Category = e.Category, IsRejected = e.IsRejected,
        RejectionReason = e.RejectionReason,
        RejectedByDisplayName = e.RejectedByUser?.DisplayName,
        RejectedAt = e.RejectedAt, CreatedAt = e.CreatedAt
    };
}
