namespace Volonterko.Domain.Entities;

public class VolunteerActionTag
{
    public int VolunteerActionId { get; set; }
    public VolunteerAction VolunteerAction { get; set; } = null!;

    public int TagId { get; set; }
    public Tag Tag { get; set; } = null!;
}
