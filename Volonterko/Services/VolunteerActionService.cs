using Microsoft.EntityFrameworkCore;
using Volonterko.Data;
using Volonterko.Domain.Entities;
using Volonterko.Domain.Enums;

namespace Volonterko.Services;

public class VolunteerActionService
{
    private readonly ApplicationDbContext _db;

    public VolunteerActionService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<VolunteerAction>> GetForOrganizationAsync(int organizationId)
    {
        return await _db.VolunteerActions
            .AsNoTracking()
            .Where(a => a.OrganizationId == organizationId)
            .OrderByDescending(a => a.StartDateTime)
            .ToListAsync();
    }

    public async Task<VolunteerAction?> GetByIdAsync(int id)
    {
        return await _db.VolunteerActions
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<int> CreateAsync(VolunteerAction action)
    {
        _db.VolunteerActions.Add(action);
        await _db.SaveChangesAsync();
        return action.Id;
    }

    public async Task UpdateAsync(VolunteerAction action)
    {
        _db.VolunteerActions.Update(action);
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Delete is blocked if action has ANY signups.
    /// This preserves history (hours, attendance, etc.) without DB migration.
    /// </summary>
    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _db.VolunteerActions.FirstOrDefaultAsync(a => a.Id == id);
        if (entity is null) return true;

        var hasAnySignups = await _db.Signups
            .AsNoTracking()
            .AnyAsync(s => s.VolunteerActionId == id);

        if (hasAnySignups)
            return false;

        _db.VolunteerActions.Remove(entity);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task SetStatusAsync(int id, VolunteerActionStatus status)
    {
        var entity = await _db.VolunteerActions.FirstOrDefaultAsync(a => a.Id == id);
        if (entity is null) return;

        entity.Status = status;
        await _db.SaveChangesAsync();
    }
}
