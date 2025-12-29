using Volonterko.Domain.Enums;

namespace Volonterko.Domain.Entities;

public class VolunteerAction
{
    public int Id { get; set; }

    public int OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    public string City { get; set; } = string.Empty;
    public string? Address { get; set; }

    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }

    public int RequiredVolunteers { get; set; } = 1;

    public VolunteerActionStatus Status { get; set; } = VolunteerActionStatus.Draft;

    // ✅ NEW: optional image for action (relative URL under wwwroot, e.g. "uploads/actions/..png")
    public string? ImageUrl { get; set; }

    public List<Signup> Signups { get; set; } = new();
    public List<VolunteerActionTag> ActionTags { get; set; } = new();
}
