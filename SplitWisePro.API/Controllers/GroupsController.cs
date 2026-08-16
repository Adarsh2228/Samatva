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
public class GroupsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ITokenService _tokenService;
    private readonly IHubContext<ExpenseHub> _hubContext;

    public GroupsController(AppDbContext context, ITokenService tokenService, IHubContext<ExpenseHub> hubContext)
    {
        _context = context;
        _tokenService = tokenService;
        _hubContext = hubContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetMyGroups(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var groups = await _context.GroupMembers
            .Where(gm => gm.UserId == userId.Value && !gm.HasLeft)
            .Include(gm => gm.Group).ThenInclude(g => g.Members.Where(m => !m.HasLeft)).ThenInclude(m => m.User)
            .Select(gm => MapToGroupDto(gm.Group))
            .ToListAsync(ct);

        return Ok(groups);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetGroup(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var group = await _context.Groups
            .Include(g => g.Members.Where(m => !m.HasLeft)).ThenInclude(m => m.User)
            .FirstOrDefaultAsync(g => g.Id == id, ct);

        if (group is null) return NotFound();
        if (!group.Members.Any(m => m.UserId == userId.Value)) return Forbid();

        return Ok(MapToGroupDto(group));
    }

    [HttpPost]
    public async Task<IActionResult> CreateGroup([FromBody] CreateGroupRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var group = new Group { Id = Guid.NewGuid(), Name = request.Name, Description = request.Description, DefaultCurrency = request.DefaultCurrency, ImageUrl = request.ImageUrl };
        var membership = new GroupMember { Id = Guid.NewGuid(), GroupId = group.Id, UserId = userId.Value, Role = GroupRole.Owner };

        _context.Groups.Add(group);
        _context.GroupMembers.Add(membership);
        await _context.SaveChangesAsync(ct);

        var created = await _context.Groups.Include(g => g.Members).ThenInclude(m => m.User).FirstAsync(g => g.Id == group.Id, ct);
        return CreatedAtAction(nameof(GetGroup), new { id = group.Id }, MapToGroupDto(created));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateGroup(Guid id, [FromBody] UpdateGroupRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var group = await _context.Groups.Include(g => g.Members).ThenInclude(m => m.User).FirstOrDefaultAsync(g => g.Id == id, ct);
        if (group is null) return NotFound();

        var m = group.Members.FirstOrDefault(m => m.UserId == userId.Value);
        if (m is null || m.Role == GroupRole.Member) return Forbid();

        if (request.Name is not null) group.Name = request.Name;
        if (request.Description is not null) group.Description = request.Description;
        if (request.DefaultCurrency is not null) group.DefaultCurrency = request.DefaultCurrency;
        if (request.ImageUrl is not null) group.ImageUrl = request.ImageUrl;
        await _context.SaveChangesAsync(ct);

        return Ok(MapToGroupDto(group));
    }

    /// <summary>
    /// Add a member by email. Any group member can invite.
    /// If the user doesn't exist yet, returns a message telling them to share the invite link instead.
    /// </summary>
    [HttpPost("{id:guid}/members")]
    public async Task<IActionResult> AddMember(Guid id, [FromBody] AddMemberRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var group = await _context.Groups.Include(g => g.Members).FirstOrDefaultAsync(g => g.Id == id, ct);
        if (group is null) return NotFound();

        // Any member can invite (not just admin/owner)
        var reqMember = group.Members.FirstOrDefault(m => m.UserId == userId.Value && !m.HasLeft);
        if (reqMember is null) return Forbid();

        var target = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.ToLower(), ct);
        if (target is null) return NotFound(new { message = "User not registered yet. Share the invite link instead!" });

        var existing = group.Members.FirstOrDefault(m => m.UserId == target.Id);
        if (existing is not null && !existing.HasLeft) return Conflict(new { message = "Already a member." });
        if (existing is not null) { existing.HasLeft = false; existing.JoinedAt = DateTime.UtcNow; }
        else { _context.GroupMembers.Add(new GroupMember { Id = Guid.NewGuid(), GroupId = id, UserId = target.Id, Role = GroupRole.Member }); }

        await _context.SaveChangesAsync(ct);
        await _hubContext.Clients.Group($"group_{id}").SendAsync("MemberAdded", new { userId = target.Id, displayName = target.DisplayName }, ct);

        return Ok(new GroupMemberDto { UserId = target.Id, DisplayName = target.DisplayName, Email = target.Email, AvatarUrl = target.AvatarUrl, Role = "Member", JoinedAt = DateTime.UtcNow });
    }

    /// <summary>
    /// Generate invite link — any member can create one (not restricted to admins).
    /// This is a JOIN link (not guest/read-only).
    /// </summary>
    [HttpPost("{id:guid}/invite-link")]
    public async Task<IActionResult> GenerateInviteLink(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var group = await _context.Groups.Include(g => g.Members).FirstOrDefaultAsync(g => g.Id == id, ct);
        if (group is null) return NotFound();

        var mem = group.Members.FirstOrDefault(m => m.UserId == userId.Value && !m.HasLeft);
        if (mem is null) return Forbid();

        var token = _tokenService.GenerateGuestLinkToken(id, 7); // 7-day invite links
        group.GuestLinkToken = token;
        group.GuestLinkExpiresAt = DateTime.UtcNow.AddDays(7);
        
        // Ensure an active Invite Code exists so we can share it alongside the link
        if (string.IsNullOrEmpty(group.InviteCode) || group.InviteCodeExpiresAt < DateTime.UtcNow)
        {
            group.InviteCode = GenerateUniqueCode();
            group.InviteCodeExpiresAt = DateTime.UtcNow.AddHours(24);
        }
        
        await _context.SaveChangesAsync(ct);

        // Dynamically get the frontend URL from the Origin header, fallback to production URL
        var frontendUrl = Request.Headers["Origin"].FirstOrDefault() ?? "https://samatva-one.vercel.app";
        var inviteUrl = $"{frontendUrl}/join?token={Uri.EscapeDataString(token)}";

        return Ok(new
        {
            inviteUrl,
            expiresAt = group.GuestLinkExpiresAt.Value,
            groupName = group.Name,
            inviterName = (await _context.Users.FindAsync(new object[] { userId.Value }, ct))?.DisplayName ?? "Someone",
            qrData = inviteUrl, // frontend generates QR from this
            whatsappUrl = $"https://wa.me/?text={Uri.EscapeDataString($"Join my group \"{group.Name}\" on Samatva! ⚖️\n\n🔗 Link: {inviteUrl}\n🔢 Code: {group.InviteCode}")}",
            smsBody = $"Join my group \"{group.Name}\" on Samatva! Link: {inviteUrl} Code: {group.InviteCode}",
            emailSubject = $"Join \"{group.Name}\" on Samatva",
            emailBody = $"Hey! I've invited you to join our expense group \"{group.Name}\" on Samatva.\n\nClick here to join: {inviteUrl}\n\nOr enter this 6-digit code on the Join page: {group.InviteCode}\n\nThe link expires in 7 days, and the code expires in 24 hours."
        });
    }

    /// <summary>
    /// Join a group via invite token (self-add). Authenticated user joins the group.
    /// </summary>
    [HttpPost("join")]
    public async Task<IActionResult> JoinGroupViaInvite([FromBody] JoinViaTokenRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var groupId = _tokenService.ValidateGuestLinkToken(request.Token);
        if (groupId is null) return BadRequest(new { message = "Invalid or expired invite link." });

        var group = await _context.Groups.Include(g => g.Members).FirstOrDefaultAsync(g => g.Id == groupId.Value, ct);
        if (group is null) return NotFound();

        var existing = group.Members.FirstOrDefault(m => m.UserId == userId.Value);
        if (existing is not null && !existing.HasLeft) return Ok(new { message = "You are already a member.", groupId = group.Id });
        if (existing is not null) { existing.HasLeft = false; existing.JoinedAt = DateTime.UtcNow; }
        else { _context.GroupMembers.Add(new GroupMember { Id = Guid.NewGuid(), GroupId = group.Id, UserId = userId.Value, Role = GroupRole.Member }); }

        await _context.SaveChangesAsync(ct);

        var user = await _context.Users.FindAsync(new object[] { userId.Value }, ct);
        await _hubContext.Clients.Group($"group_{group.Id}").SendAsync("MemberAdded", new { userId = userId.Value, displayName = user?.DisplayName ?? "New Member" }, ct);

        return Ok(new { message = $"You joined \"{group.Name}\"!", groupId = group.Id });
    }

    /// <summary>
    /// POST /api/groups/{id}/invite-code — Generate a short 6-char alphanumeric code.
    /// </summary>
    [HttpPost("{id:guid}/invite-code")]
    public async Task<IActionResult> GenerateInviteCode(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var group = await _context.Groups.Include(g => g.Members).FirstOrDefaultAsync(g => g.Id == id, ct);
        if (group is null) return NotFound();

        var mem = group.Members.FirstOrDefault(m => m.UserId == userId.Value && !m.HasLeft);
        if (mem is null) return Forbid();

        // Generate unique 6-char code
        var code = GenerateUniqueCode();
        group.InviteCode = code;
        group.InviteCodeExpiresAt = DateTime.UtcNow.AddHours(24);
        await _context.SaveChangesAsync(ct);

        return Ok(new { code, expiresAt = group.InviteCodeExpiresAt, groupName = group.Name });
    }

    /// <summary>
    /// POST /api/groups/join-code — Join a group using a 6-char invite code.
    /// </summary>
    [HttpPost("join-code")]
    public async Task<IActionResult> JoinViaCode([FromBody] JoinViaCodeRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var group = await _context.Groups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.InviteCode == request.Code && g.InviteCodeExpiresAt > DateTime.UtcNow, ct);

        if (group is null) return BadRequest(new { message = "Invalid or expired code. Ask for a new one!" });

        var existing = group.Members.FirstOrDefault(m => m.UserId == userId.Value);
        if (existing is not null && !existing.HasLeft) return Ok(new { message = "You are already a member.", groupId = group.Id });
        if (existing is not null) { existing.HasLeft = false; existing.JoinedAt = DateTime.UtcNow; }
        else { _context.GroupMembers.Add(new GroupMember { Id = Guid.NewGuid(), GroupId = group.Id, UserId = userId.Value, Role = GroupRole.Member }); }

        await _context.SaveChangesAsync(ct);
        var user = await _context.Users.FindAsync(new object[] { userId.Value }, ct);
        await _hubContext.Clients.Group($"group_{group.Id}").SendAsync("MemberAdded", new { userId = userId.Value, displayName = user?.DisplayName ?? "New Member" }, ct);

        return Ok(new { message = $"You joined \"{group.Name}\"!", groupId = group.Id });
    }

    private static string GenerateUniqueCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no 0/O/1/I confusion
        var random = new Random();
        return new string(Enumerable.Range(0, 6).Select(_ => chars[random.Next(chars.Length)]).ToArray());
    }

    [HttpDelete("{groupId:guid}/members/{memberUserId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid groupId, Guid memberUserId, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var group = await _context.Groups.Include(g => g.Members).FirstOrDefaultAsync(g => g.Id == groupId, ct);
        if (group is null) return NotFound();

        var reqMember = group.Members.FirstOrDefault(m => m.UserId == userId.Value);
        if (reqMember is null) return Forbid();
        if (userId.Value != memberUserId && reqMember.Role == GroupRole.Member) return Forbid();

        var target = group.Members.FirstOrDefault(m => m.UserId == memberUserId);
        if (target is null) return NotFound();

        target.HasLeft = true;
        await _context.SaveChangesAsync(ct);
        await _hubContext.Clients.Group($"group_{groupId}").SendAsync("MemberRemoved", new { userId = memberUserId }, ct);

        return Ok(new { message = "Member removed." });
    }

    [HttpPost("{id:guid}/guest-link")]
    public async Task<IActionResult> GenerateGuestLink(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var group = await _context.Groups.Include(g => g.Members).FirstOrDefaultAsync(g => g.Id == id, ct);
        if (group is null) return NotFound();

        var mem = group.Members.FirstOrDefault(m => m.UserId == userId.Value);
        if (mem is null || mem.Role == GroupRole.Member) return Forbid();

        var token = _tokenService.GenerateGuestLinkToken(id, 30);
        group.GuestLinkToken = token;
        group.GuestLinkExpiresAt = DateTime.UtcNow.AddDays(30);
        await _context.SaveChangesAsync(ct);

        return Ok(new GuestLinkResponse { GuestUrl = $"{Request.Scheme}://{Request.Host}/guest?token={Uri.EscapeDataString(token)}", ExpiresAt = group.GuestLinkExpiresAt.Value });
    }

    [HttpGet("guest"), AllowAnonymous]
    public async Task<IActionResult> GetGroupByGuestLink([FromQuery] string token, CancellationToken ct)
    {
        var groupId = _tokenService.ValidateGuestLinkToken(token);
        if (groupId is null) return Unauthorized(new { message = "Invalid or expired guest link." });

        var group = await _context.Groups.Include(g => g.Members.Where(m => !m.HasLeft)).ThenInclude(m => m.User).FirstOrDefaultAsync(g => g.Id == groupId.Value, ct);
        if (group is null) return NotFound();

        return Ok(MapToGroupDto(group));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteGroup(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var group = await _context.Groups.Include(g => g.Members).FirstOrDefaultAsync(g => g.Id == id, ct);
        if (group is null) return NotFound();
        if (group.Members.FirstOrDefault(m => m.UserId == userId.Value)?.Role != GroupRole.Owner) return Forbid();

        group.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
        return Ok(new { message = "Group deleted." });
    }

    private Guid? GetUserId() { var c = User.FindFirst(ClaimTypes.NameIdentifier)?.Value; return c is not null ? Guid.Parse(c) : null; }

    private static GroupDto MapToGroupDto(Group g) => new()
    {
        Id = g.Id, Name = g.Name, Description = g.Description, ImageUrl = g.ImageUrl,
        DefaultCurrency = g.DefaultCurrency, IsArchived = g.IsArchived, CreatedAt = g.CreatedAt,
        Members = g.Members.Where(m => !m.HasLeft).Select(m => new GroupMemberDto
        {
            UserId = m.UserId, DisplayName = m.User?.DisplayName ?? "Unknown", Email = m.User?.Email ?? "",
            AvatarUrl = m.User?.AvatarUrl, Role = m.Role.ToString(), JoinedAt = m.JoinedAt
        }).ToList()
    };
}

// ── DTOs for new endpoints ──────────────────────────────────────────
public class JoinViaTokenRequest
{
    public string Token { get; set; } = string.Empty;
}

public class JoinViaCodeRequest
{
    public string Code { get; set; } = string.Empty;
}
