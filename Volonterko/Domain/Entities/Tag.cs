namespace Volonterko.Domain.Entities;

public class Tag
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public List<VolunteerActionTag> ActionTags { get; set; } = new();
}
