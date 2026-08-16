using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SplitWisePro.API.Hubs;
using SplitWisePro.Core.DTOs;
using SplitWisePro.Core.Entities;
using SplitWisePro.Core.Enums;
using SplitWisePro.Core.Interfaces;
using SplitWisePro.Infrastructure.Data;

namespace SplitWisePro.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AiAssistantController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IAiParserService _parser;
    private readonly IHubContext<ExpenseHub> _hubContext;

    public AiAssistantController(AppDbContext context, IAiParserService parser, IHubContext<ExpenseHub> hubContext)
    {
        _context = context;
        _parser = parser;
        _hubContext = hubContext;
    }

    /// <summary>
    /// POST /api/aiassistant/parse — Parse a natural language message into structured expense data.
    /// Does NOT create the expense yet (preview mode).
    /// </summary>
    [HttpPost("parse")]
    public async Task<IActionResult> ParseMessage([FromBody] AiParseRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (!await IsMember(request.GroupId, userId.Value, ct)) return Forbid();

        var result = await _parser.ParseExpenseMessageAsync(request.Message, request.GroupId, userId.Value);
        return Ok(result);
    }

    /// <summary>
    /// POST /api/aiassistant/create — Parse AND create the expense in one step.
    /// </summary>
    [HttpPost("create")]
    public async Task<IActionResult> ParseAndCreate([FromBody] AiParseRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (!await IsMember(request.GroupId, userId.Value, ct)) return Forbid();

        var parseResult = await _parser.ParseExpenseMessageAsync(request.Message, request.GroupId, userId.Value);
        if (!parseResult.Success || parseResult.ParsedExpense is null)
            return BadRequest(parseResult);

        var parsed = parseResult.ParsedExpense;

        // Resolve payer
        var payerUserId = userId.Value;
        if (parsed.PayerIdentifier is not null && !parsed.PayerIdentifier.Equals("me", StringComparison.OrdinalIgnoreCase))
        {
            var payer = await _context.Users.FirstOrDefaultAsync(u => u.DisplayName.ToLower().Contains(parsed.PayerIdentifier.ToLower()), ct);
            if (payer is not null) payerUserId = payer.Id;
        }

        // Resolve participants
        List<Guid> participantIds;
        if (parsed.Participants.Count > 0)
        {
            participantIds = new List<Guid> { payerUserId };
            foreach (var name in parsed.Participants)
            {
                var user = await _context.GroupMembers
                    .Where(gm => gm.GroupId == request.GroupId && !gm.HasLeft)
                    .Include(gm => gm.User)
                    .Where(gm => gm.User.DisplayName.ToLower().Contains(name.ToLower()))
                    .Select(gm => gm.UserId).FirstOrDefaultAsync(ct);

                if (user != Guid.Empty) participantIds.Add(user);
            }
            participantIds = participantIds.Distinct().ToList();
        }
        else
        {
            participantIds = await _context.GroupMembers
                .Where(gm => gm.GroupId == request.GroupId && !gm.HasLeft)
                .Select(gm => gm.UserId).ToListAsync(ct);
        }

        if (participantIds.Count == 0) return BadRequest(new { message = "No participants found." });

        var expense = new Expense
        {
            Id = Guid.NewGuid(), GroupId = request.GroupId, PaidByUserId = payerUserId,
            Description = parsed.Description, Amount = parsed.Amount, Currency = parsed.Currency,
            Category = parsed.Category, SplitType = parsed.SplitType,
            IsAiGenerated = true, AiRawInput = request.Message
        };

        var share = Math.Round(parsed.Amount / participantIds.Count, 4);
        var splits = participantIds.Select(uid => new ExpenseSplit
        {
            Id = Guid.NewGuid(), ExpenseId = expense.Id, UserId = uid, OwedAmount = share
        }).ToList();

        _context.Expenses.Add(expense);
        _context.ExpenseSplits.AddRange(splits);
        await _context.SaveChangesAsync(ct);

        var created = await _context.Expenses.Include(e => e.PaidByUser).Include(e => e.Splits).ThenInclude(s => s.User).FirstAsync(e => e.Id == expense.Id, ct);

        var dto = new ExpenseDto
        {
            Id = created.Id, GroupId = created.GroupId, PaidByUserId = created.PaidByUserId,
            PaidByDisplayName = created.PaidByUser?.DisplayName ?? "Unknown",
            Description = created.Description, Amount = created.Amount, Currency = created.Currency,
            Category = created.Category.ToString(), SplitType = created.SplitType.ToString(),
            ExpenseDate = created.ExpenseDate, IsAiGenerated = true, CreatedAt = created.CreatedAt,
            Splits = created.Splits.Select(s => new ExpenseSplitDto
            {
                Id = s.Id, UserId = s.UserId, UserDisplayName = s.User?.DisplayName ?? "Unknown",
                OwedAmount = s.OwedAmount, IsSettled = s.IsSettled
            }).ToList()
        };

        await _hubContext.Clients.Group($"group_{request.GroupId}").SendAsync("ExpenseAdded", dto, ct);

        return Ok(new { parseResult, createdExpense = dto });
    }

    // ════════════════════════════════════════════════════════════════
    // ── SMART AI ANALYZER — answers questions about expenses ──────
    // ════════════════════════════════════════════════════════════════
    /// <summary>
    /// POST /api/aiassistant/analyze — Answers user questions about expenses,
    /// balances, spending patterns by analyzing all group data locally (zero-cost).
    /// </summary>
    [HttpPost("analyze")]
    public async Task<IActionResult> AnalyzeAndAnswer([FromBody] AiParseRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (!await IsMember(request.GroupId, userId.Value, ct)) return Forbid();

        var msg = request.Message.ToLower().Trim();

        // Load all group data
        var group = await _context.Groups
            .Include(g => g.Members.Where(m => !m.HasLeft)).ThenInclude(m => m.User)
            .FirstOrDefaultAsync(g => g.Id == request.GroupId, ct);
        if (group is null) return NotFound();

        var expenses = await _context.Expenses
            .Where(e => e.GroupId == request.GroupId)
            .Include(e => e.PaidByUser)
            .Include(e => e.Splits).ThenInclude(s => s.User)
            .OrderByDescending(e => e.ExpenseDate)
            .ToListAsync(ct);

        var settlements = await _context.Settlements
            .Where(s => s.GroupId == request.GroupId && s.Status == SettlementStatus.Confirmed)
            .ToListAsync(ct);

        var currentUser = await _context.Users.FindAsync(new object[] { userId.Value }, ct);
        var userName = currentUser?.DisplayName ?? "You";

        // Build balance map
        var balanceMap = new Dictionary<Guid, decimal>();
        foreach (var m in group.Members) balanceMap[m.UserId] = 0;
        foreach (var exp in expenses)
        {
            if (balanceMap.ContainsKey(exp.PaidByUserId)) balanceMap[exp.PaidByUserId] += exp.Amount;
            foreach (var split in exp.Splits)
                if (balanceMap.ContainsKey(split.UserId)) balanceMap[split.UserId] -= split.OwedAmount;
        }
        foreach (var stl in settlements)
        {
            if (balanceMap.ContainsKey(stl.PayerUserId)) balanceMap[stl.PayerUserId] += stl.Amount;
            if (balanceMap.ContainsKey(stl.ReceiverUserId)) balanceMap[stl.ReceiverUserId] -= stl.Amount;
        }

        var myBalance = balanceMap.GetValueOrDefault(userId.Value);
        var memberNames = group.Members.ToDictionary(m => m.UserId, m => m.User?.DisplayName ?? "Unknown");

        string answer;

        // ── Pattern matching for questions ──────────────────────
        if (IsQuestion(msg, "how much", "do i owe", "i owe", "my balance", "what do i owe", "kya dena hai", "kitna dena"))
        {
            if (myBalance >= 0)
                answer = $"🎉 Great news! You don't owe anything — you actually get back ₹{myBalance:N0} from the group.";
            else
                answer = $"💸 You currently owe ₹{Math.Abs(myBalance):N0} in total.\n\nHere's the breakdown:";

            // Find who you owe
            var debts = CalculateSimplifiedDebts(balanceMap, memberNames);
            var myDebts = debts.Where(d => d.from == userId.Value).ToList();
            if (myDebts.Any())
            {
                answer += "\n";
                foreach (var d in myDebts)
                    answer += $"\n• You owe ₹{d.amount:N0} to {d.toName}";
            }
        }
        else if (IsQuestion(msg, "who owes me", "kaun dega", "mujhe kitna milega", "get back", "how much will i get"))
        {
            if (myBalance <= 0)
                answer = $"😅 No one owes you right now. Your balance is ₹{myBalance:N0}.";
            else
            {
                answer = $"💰 You get back ₹{myBalance:N0} in total!\n";
                var debts = CalculateSimplifiedDebts(balanceMap, memberNames);
                var owedToMe = debts.Where(d => d.to == userId.Value).ToList();
                foreach (var d in owedToMe)
                    answer += $"\n• {d.fromName} owes you ₹{d.amount:N0}";
            }
        }
        else if (IsQuestion(msg, "why", "kyu", "reason", "explain", "how come", "10", "₹"))
        {
            // Extract amount if mentioned
            var amountMatch = System.Text.RegularExpressions.Regex.Match(msg, @"[\₹]?\s?(\d+\.?\d*)");
            decimal? queriedAmount = amountMatch.Success ? decimal.Parse(amountMatch.Groups[1].Value) : null;

            answer = $"📊 Here's how your balance of ₹{myBalance:N0} was calculated:\n";

            // Find expenses I paid for
            var iPaid = expenses.Where(e => e.PaidByUserId == userId.Value).ToList();
            var totalPaid = iPaid.Sum(e => e.Amount);

            // Find my share in all expenses
            var myShares = expenses.SelectMany(e => e.Splits.Where(s => s.UserId == userId.Value)).ToList();
            var totalOwe = myShares.Sum(s => s.OwedAmount);

            answer += $"\n💳 You paid for {iPaid.Count} expense(s) totaling ₹{totalPaid:N0}";
            answer += $"\n🧾 Your share across all expenses: ₹{totalOwe:N0}";
            answer += $"\n📐 Net: ₹{totalPaid:N0} (paid) - ₹{totalOwe:N0} (owed) = ₹{(totalPaid - totalOwe):N0}";

            if (queriedAmount.HasValue)
            {
                // Find specific expenses matching that amount
                var matching = expenses.Where(e =>
                    e.Splits.Any(s => s.UserId == userId.Value && Math.Abs(s.OwedAmount - queriedAmount.Value) < 1) ||
                    Math.Abs(e.Amount - queriedAmount.Value) < 1
                ).ToList();

                if (matching.Any())
                {
                    answer += $"\n\n🔍 Expenses related to ₹{queriedAmount.Value:N0}:";
                    foreach (var e in matching.Take(5))
                    {
                        var myShare = e.Splits.FirstOrDefault(s => s.UserId == userId.Value)?.OwedAmount ?? 0;
                        answer += $"\n• \"{e.Description}\" — ₹{e.Amount:N0} (paid by {e.PaidByUser?.DisplayName}, your share: ₹{myShare:N0}) on {e.ExpenseDate:MMM d}";
                    }
                }
            }
        }
        else if (IsQuestion(msg, "total", "kitna kharcha", "total spent", "total expense", "group total"))
        {
            var total = expenses.Sum(e => e.Amount);
            var byCategory = expenses.GroupBy(e => e.Category).Select(g => new { Cat = g.Key, Sum = g.Sum(x => x.Amount) }).OrderByDescending(x => x.Sum).ToList();

            answer = $"📊 Group Total: ₹{total:N0} across {expenses.Count} expenses\n\nBy category:";
            foreach (var c in byCategory)
                answer += $"\n• {c.Cat}: ₹{c.Sum:N0}";
        }
        else if (IsQuestion(msg, "my expense", "my spending", "maine kitna", "i spent", "i paid"))
        {
            var iPaid = expenses.Where(e => e.PaidByUserId == userId.Value).ToList();
            var total = iPaid.Sum(e => e.Amount);

            answer = $"💳 You've paid for {iPaid.Count} expense(s) totaling ₹{total:N0}:\n";
            foreach (var e in iPaid.Take(10))
                answer += $"\n• \"{e.Description}\" — ₹{e.Amount:N0} ({e.Category}) on {e.ExpenseDate:MMM d}";
        }
        else if (IsQuestion(msg, "latest", "recent", "last expense", "last 5", "history"))
        {
            var recent = expenses.Take(5).ToList();
            answer = $"📋 Last {recent.Count} expenses:\n";
            foreach (var e in recent)
                answer += $"\n• \"{e.Description}\" — ₹{e.Amount:N0} (paid by {e.PaidByUser?.DisplayName}) {e.ExpenseDate:MMM d}";
        }
        else if (IsQuestion(msg, "summary", "overview", "status", "group summary"))
        {
            var total = expenses.Sum(e => e.Amount);
            var memberCount = group.Members.Count;

            answer = $"📊 **Group Summary: {group.Name}**\n";
            answer += $"\n👥 Members: {memberCount}";
            answer += $"\n🧾 Total expenses: {expenses.Count}";
            answer += $"\n💰 Total amount: ₹{total:N0}";
            answer += $"\n📐 Avg per expense: ₹{(expenses.Count > 0 ? total / expenses.Count : 0):N0}";
            answer += $"\n\n💳 Your balance: {(myBalance >= 0 ? $"You get back ₹{myBalance:N0}" : $"You owe ₹{Math.Abs(myBalance):N0}")}";
        }
        else if (IsQuestion(msg, "settle", "how to settle", "clear debt", "pay off"))
        {
            var debts = CalculateSimplifiedDebts(balanceMap, memberNames);
            if (!debts.Any())
                answer = "✅ All settled! No pending debts in this group.";
            else
            {
                answer = "🤝 Optimal settlement plan:\n";
                foreach (var d in debts)
                    answer += $"\n• {d.fromName} → pays ₹{d.amount:N0} → {d.toName}";
            }
        }
        else if (IsQuestion(msg, "help", "what can you do", "commands", "features"))
        {
            answer = "🤖 I can help you with:\n\n"
                + "📝 **Add expenses:** \"I paid 500 for dinner\"\n"
                + "💸 **Check balance:** \"How much do I owe?\"\n"
                + "🔍 **Explain charges:** \"Why do I owe ₹200?\"\n"
                + "👥 **Who owes you:** \"Who owes me?\"\n"
                + "📊 **Group stats:** \"Total expenses\", \"Summary\"\n"
                + "💳 **My spending:** \"My expenses\", \"I paid\"\n"
                + "📋 **History:** \"Last 5 expenses\"\n"
                + "🤝 **Settle up:** \"How to settle?\"\n"
                + "\nJust type naturally — I understand Hindi/English! 🇮🇳";
        }
        else
        {
            // Default: try to parse as expense first
            return await ParseAndCreate(request, ct);
        }

        return Ok(new { answer, type = "analysis" });
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static bool IsQuestion(string msg, params string[] keywords)
        => keywords.Any(k => msg.Contains(k));

    private static List<(Guid from, string fromName, Guid to, string toName, decimal amount)> CalculateSimplifiedDebts(
        Dictionary<Guid, decimal> balanceMap, Dictionary<Guid, string> memberNames)
    {
        var creditors = balanceMap.Where(b => b.Value > 0.01m).OrderByDescending(b => b.Value).ToList();
        var debtors = balanceMap.Where(b => b.Value < -0.01m).OrderBy(b => b.Value).ToList();
        var result = new List<(Guid from, string fromName, Guid to, string toName, decimal amount)>();

        int i = 0, j = 0;
        while (i < creditors.Count && j < debtors.Count)
        {
            var credit = creditors[i].Value;
            var debt = Math.Abs(debtors[j].Value);
            var amount = Math.Min(credit, debt);

            result.Add((debtors[j].Key, memberNames.GetValueOrDefault(debtors[j].Key, "?"),
                         creditors[i].Key, memberNames.GetValueOrDefault(creditors[i].Key, "?"),
                         Math.Round(amount, 2)));

            creditors[i] = new(creditors[i].Key, credit - amount);
            debtors[j] = new(debtors[j].Key, debtors[j].Value + amount);
            if (creditors[i].Value < 0.01m) i++;
            if (Math.Abs(debtors[j].Value) < 0.01m) j++;
        }
        return result;
    }

    private async Task<bool> IsMember(Guid groupId, Guid userId, CancellationToken ct) =>
        await _context.GroupMembers.AnyAsync(gm => gm.GroupId == groupId && gm.UserId == userId && !gm.HasLeft, ct);

    private Guid? GetUserId() { var c = User.FindFirst(ClaimTypes.NameIdentifier)?.Value; return c is not null ? Guid.Parse(c) : null; }
}
