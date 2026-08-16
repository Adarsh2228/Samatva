using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SplitWisePro.Core.DTOs;

namespace SplitWisePro.API.Hubs;

/// <summary>
/// SignalR hub for real-time expense updates across web and mobile clients.
/// Authenticated users join group-specific channels for targeted broadcasts.
/// </summary>
[Authorize]
public class ExpenseHub : Hub
{
    /// <summary>
    /// Join a group channel to receive real-time updates for that group.
    /// Called by the client after loading a group.
    /// </summary>
    public async Task JoinGroup(string groupId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"group_{groupId}");
        await Clients.Caller.SendAsync("JoinedGroup", groupId);
    }

    /// <summary>
    /// Leave a group channel (e.g., when navigating away from a group view).
    /// </summary>
    public async Task LeaveGroup(string groupId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"group_{groupId}");
    }

    /// <summary>
    /// Notify all group members that a new expense was added.
    /// </summary>
    public async Task NotifyExpenseAdded(string groupId, ExpenseDto expense)
    {
        await Clients.OthersInGroup($"group_{groupId}")
            .SendAsync("ExpenseAdded", expense);
    }

    /// <summary>
    /// Notify all group members that an expense was updated.
    /// </summary>
    public async Task NotifyExpenseUpdated(string groupId, ExpenseDto expense)
    {
        await Clients.OthersInGroup($"group_{groupId}")
            .SendAsync("ExpenseUpdated", expense);
    }

    /// <summary>
    /// Notify all group members that an expense was deleted.
    /// </summary>
    public async Task NotifyExpenseDeleted(string groupId, Guid expenseId)
    {
        await Clients.OthersInGroup($"group_{groupId}")
            .SendAsync("ExpenseDeleted", expenseId);
    }

    /// <summary>
    /// Notify all group members about a settlement.
    /// </summary>
    public async Task NotifySettlement(string groupId, SettlementDto settlement)
    {
        await Clients.OthersInGroup($"group_{groupId}")
            .SendAsync("SettlementRecorded", settlement);
    }

    /// <summary>
    /// Notify all group members about a balance update.
    /// </summary>
    public async Task NotifyBalancesUpdated(string groupId, List<BalanceDto> balances)
    {
        await Clients.Group($"group_{groupId}")
            .SendAsync("BalancesUpdated", balances);
    }

    /// <summary>
    /// Notify a specific user about an activity/notification.
    /// </summary>
    public async Task NotifyUser(string userId, string message, string activityType)
    {
        await Clients.User(userId)
            .SendAsync("Notification", new { message, activityType, timestamp = DateTime.UtcNow });
    }

    // ── Connection Lifecycle ───────────────────────────────────────────

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is not null)
        {
            // Add user to their personal channel for direct notifications
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is not null)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId}");
        }
        await base.OnDisconnectedAsync(exception);
    }
}
