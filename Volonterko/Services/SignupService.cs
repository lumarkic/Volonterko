using System.Data;
using Microsoft.EntityFrameworkCore;
using Volonterko.Data;
using Volonterko.Domain.Entities;
using Volonterko.Domain.Enums;

namespace Volonterko.Services;

public class SignupService
{
    private readonly ApplicationDbContext _db;

    public SignupService(ApplicationDbContext db)
    {
        _db = db;
    }

    private async Task<bool> IsOrganizationOwnerAsync(string userId)
    {
        return await _db.Organizations
            .AsNoTracking()
            .AnyAsync(o => o.OwnerUserId == userId);
    }

    public async Task<Signup?> GetMySignupForActionAsync(int actionId, string userId)
    {
        return await _db.Signups
            .AsNoTracking()
            .Where(s => s.VolunteerActionId == actionId && s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<List<Signup>> GetConflictingSignupsAsync(int actionId, string userId)
    {
        var action = await _db.VolunteerActions
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == actionId);

        if (action is null)
            return new List<Signup>();

        return await _db.Signups
            .AsNoTracking()
            .Include(s => s.VolunteerAction)
            .Where(s => s.UserId == userId &&
                        s.VolunteerActionId != actionId &&
                        (s.Status == SignupStatus.Applied || s.Status == SignupStatus.Accepted))
            .Where(s => s.VolunteerAction.StartDateTime < action.EndDateTime &&
                        action.StartDateTime < s.VolunteerAction.EndDateTime)
            .OrderBy(s => s.VolunteerAction.StartDateTime)
            .ToListAsync();
    }

    /// <summary>
    /// Create signup (Applied) with server-side capacity enforcement.
    /// Prevents race conditions with SERIALIZABLE transaction.
    /// </summary>
    public async Task<Signup?> CreateSignupAsync(int actionId, string userId)
    {
        // HARD RULE: org account cannot sign up
        if (await IsOrganizationOwnerAsync(userId))
            return null;

        var action = await _db.VolunteerActions
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == actionId);

        if (action is null)
            return null;

        if (action.Status != VolunteerActionStatus.Published)
            return null;

        if (action.EndDateTime <= DateTime.Now)
            return null;

        // conflict rule
        var conflicts = await GetConflictingSignupsAsync(actionId, userId);
        if (conflicts.Count > 0)
            return null;

        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        // Already exists?
        var existing = await _db.Signups
            .FirstOrDefaultAsync(s => s.VolunteerActionId == actionId && s.UserId == userId);

        // Capacity check (exclude Cancelled/Rejected) – IMPORTANT: pure SQL expression
        if (action.RequiredVolunteers > 0)
        {
            var countActive = await _db.Signups
                .Where(s => s.VolunteerActionId == actionId
                            && s.Status != SignupStatus.Cancelled
                            && s.Status != SignupStatus.Rejected)
                .CountAsync();

            if (countActive >= action.RequiredVolunteers)
            {
                await tx.RollbackAsync();
                return null;
            }
        }

        if (existing is not null)
        {
            // if already active -> no duplicate
            if (existing.Status != SignupStatus.Cancelled && existing.Status != SignupStatus.Rejected)
            {
                await tx.RollbackAsync();
                return null;
            }

            existing.Status = SignupStatus.Applied;
            existing.CreatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return existing;
        }

        var signup = new Signup
        {
            VolunteerActionId = actionId,
            UserId = userId,
            Status = SignupStatus.Applied,
            CreatedAt = DateTime.UtcNow
        };

        _db.Signups.Add(signup);
        await _db.SaveChangesAsync();

        await tx.CommitAsync();
        return signup;
    }

    public async Task<bool> CancelSignupAsync(int actionId, string userId)
    {
        var s = await _db.Signups
            .FirstOrDefaultAsync(x => x.VolunteerActionId == actionId && x.UserId == userId);

        if (s is null) return false;

        if (s.Status == SignupStatus.Rejected || s.Status == SignupStatus.Attended || s.Status == SignupStatus.NoShow)
            return false;

        if (s.Status == SignupStatus.Cancelled) return true;

        s.Status = SignupStatus.Cancelled;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SetStatusAsync(int signupId, SignupStatus status)
    {
        var s = await _db.Signups.FirstOrDefaultAsync(x => x.Id == signupId);
        if (s is null) return false;

        s.Status = status;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> MarkAttendedAsync(int signupId, decimal hours)
    {
        if (hours <= 0)
            return false;

        var s = await _db.Signups
            .Include(x => x.VolunteerAction)
            .FirstOrDefaultAsync(x => x.Id == signupId);

        if (s is null)
            return false;

        // Ne može se dodijeliti sate za nevažeće statuse
        if (s.Status == SignupStatus.Rejected || s.Status == SignupStatus.Cancelled)
            return false;

        // 🔒 KLJUČNA PROVJERA: akcija mora biti završena
        if (s.VolunteerAction.EndDateTime > DateTime.Now)
            return false;

        s.Status = SignupStatus.Attended;
        s.HoursAwarded = hours;

        await _db.SaveChangesAsync();
        return true;
    }

}
