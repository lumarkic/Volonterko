using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Volonterko.Data;
using Volonterko.Domain.Entities;
using Volonterko.Domain.Enums;

namespace Volonterko.Services;

public class OrganizationService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public OrganizationService(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<Organization?> GetByOwnerUserIdAsync(string ownerUserId)
    {
        return await _db.Organizations
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.OwnerUserId == ownerUserId);
    }

    public async Task<Organization?> GetByIdAsync(int id)
    {
        return await _db.Organizations
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id);
    }

    public async Task<Organization> CreateRequestAsync(string ownerUserId, string name, string city, string contactEmail, string? description)
    {
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

    /// <summary>
    /// Approves organization and assigns Organization role to the owner user.
    /// Returns false if org/user not found or operation fails.
    /// </summary>
    public async Task<bool> ApproveAndAssignRoleAsync(int organizationId, string organizationRoleName)
    {
        var org = await _db.Organizations.FirstOrDefaultAsync(o => o.Id == organizationId);
        if (org is null) return false;

        // already approved -> still ensure role (idempotent)
        var ownerUser = await _userManager.FindByIdAsync(org.OwnerUserId);
        if (ownerUser is null) return false;

        // set status
        org.Status = OrganizationStatus.Approved;
        await _db.SaveChangesAsync();

        // assign role if missing
        if (!await _userManager.IsInRoleAsync(ownerUser, organizationRoleName))
        {
            var res = await _userManager.AddToRoleAsync(ownerUser, organizationRoleName);
            if (!res.Succeeded)
                return false;
        }

        return true;
    }

    public async Task<bool> RejectAsync(int organizationId)
    {
        var org = await _db.Organizations.FirstOrDefaultAsync(o => o.Id == organizationId);
        if (org is null) return false;

        org.Status = OrganizationStatus.Rejected;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateProfileAsync(int organizationId, string city, string contactEmail, string? description)
    {
        var org = await _db.Organizations.FirstOrDefaultAsync(o => o.Id == organizationId);
        if (org is null) return false;

        org.City = city;
        org.ContactEmail = contactEmail;
        org.Description = description;

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SetLogoUrlAsync(int organizationId, string? logoUrl)
    {
        var org = await _db.Organizations.FirstOrDefaultAsync(o => o.Id == organizationId);
        if (org is null) return false;

        org.LogoUrl = logoUrl;
        await _db.SaveChangesAsync();
        return true;
    }
}
