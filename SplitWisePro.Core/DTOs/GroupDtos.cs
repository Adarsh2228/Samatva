using System.ComponentModel.DataAnnotations;

namespace SplitWisePro.Core.DTOs;

// ── Create/Update Group ────────────────────────────────────────────

public class CreateGroupRequest
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(3)]
    public string DefaultCurrency { get; set; } = "INR";

    public string? ImageUrl { get; set; }
}

public class UpdateGroupRequest
{
    [MaxLength(200)]
    public string? Name { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(3)]
    public string? DefaultCurrency { get; set; }

    public string? ImageUrl { get; set; }
}

// ── Group Response ─────────────────────────────────────────────────

public class GroupDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public string DefaultCurrency { get; set; } = "INR";
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<GroupMemberDto> Members { get; set; } = new();
}

public class GroupMemberDto
{
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string Role { get; set; } = "Member";
    public DateTime JoinedAt { get; set; }
}

// ── Add Member ─────────────────────────────────────────────────────

public class AddMemberRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    public string Role { get; set; } = "Member";
}

// ── Guest Link Response ────────────────────────────────────────────

public class GuestLinkResponse
{
    public string GuestUrl { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}
