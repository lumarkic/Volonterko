using Volonterko.Domain.Enums;

namespace Volonterko.Domain.Entities;

public class Signup
{
    public int Id { get; set; }

    public int VolunteerActionId { get; set; }
    public VolunteerAction VolunteerAction { get; set; } = null!;

    // Identity user
    public string UserId { get; set; } = string.Empty;

    public SignupStatus Status { get; set; } = SignupStatus.Applied;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Filled when attended
    public decimal? HoursAwarded { get; set; }
}
