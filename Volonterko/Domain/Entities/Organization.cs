using Volonterko.Domain.Enums;

namespace Volonterko.Domain.Entities;

public class Organization
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string City { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;

    public OrganizationStatus Status { get; set; } = OrganizationStatus.Pending;

    // Identity user who owns/manages this organization
    public string OwnerUserId { get; set; } = string.Empty;

    public List<VolunteerAction> Actions { get; set; } = new();
}
