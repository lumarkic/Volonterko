using Microsoft.EntityFrameworkCore;
using Volonterko.Data;
using Volonterko.Domain.Entities;
using Volonterko.Domain.Enums;

namespace Volonterko.Services;

public class OrganizationService
{
    private readonly ApplicationDbContext _db;

    public OrganizationService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Organization?> GetByOwnerUserIdAsync(string ownerUserId)
    {
        return await _db.Organizations
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.OwnerUserId == ownerUserId);
    }

    public async Task<Organization> CreateRequestAsync(string ownerUserId, string name, string city, string contactEmail, string? description)
    {
        // Enforce one org per owner (matches unique index)
        var existing = await _db.Organizations.FirstOrDefaultAsync(o => o.OwnerUserId == ownerUserId);
        if (existing is not null)
            return existing;

        var org = new Organization
        {
            OwnerUserId = ownerUserId,
            Name = name,
            City = city,
            ContactEmail = contactEmail,
            Description = description,
            Status = OrganizationStatus.Pending
        };

        _db.Organizations.Add(org);
        await _db.SaveChangesAsync();
        return org;
    }

    public async Task<List<Organization>> GetPendingAsync()
    {
        return await _db.Organizations
            .AsNoTracking()
            .Where(o => o.Status == OrganizationStatus.Pending)
            .OrderBy(o => o.Name)
            .ToListAsync();
    }

    public async Task ApproveAsync(int organizationId)
    {
        var org = await _db.Organizations.FirstOrDefaultAsync(o => o.Id == organizationId);
        if (org is null) return;

        org.Status = OrganizationStatus.Approved;
        await _db.SaveChangesAsync();
    }

    public async Task RejectAsync(int organizationId)
    {
        var org = await _db.Organizations.FirstOrDefaultAsync(o => o.Id == organizationId);
        if (org is null) return;

        org.Status = OrganizationStatus.Rejected;
        await _db.SaveChangesAsync();
    }
}
