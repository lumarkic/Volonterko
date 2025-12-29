using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Volonterko.Domain.Entities;

namespace Volonterko.Data
{
    public class ApplicationDbContext
        : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Organization> Organizations => Set<Organization>();
        public DbSet<VolunteerAction> VolunteerActions => Set<VolunteerAction>();
        public DbSet<Signup> Signups => Set<Signup>();
        public DbSet<Tag> Tags => Set<Tag>();
        public DbSet<VolunteerActionTag> VolunteerActionTags => Set<VolunteerActionTag>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Organization>(e =>
            {
                e.HasKey(x => x.Id);

                e.Property(x => x.Name)
                    .HasMaxLength(200)
                    .IsRequired();

                e.Property(x => x.City)
                    .HasMaxLength(120)
                    .IsRequired();

                e.Property(x => x.ContactEmail)
                    .HasMaxLength(256)
                    .IsRequired();

                // ✅ logo url
                e.Property(x => x.LogoUrl)
                    .HasMaxLength(600);

                e.HasMany(x => x.Actions)
                    .WithOne(x => x.Organization)
                    .HasForeignKey(x => x.OrganizationId)
                    .OnDelete(DeleteBehavior.Cascade);

                e.HasIndex(x => x.OwnerUserId)
                    .IsUnique();
            });

            builder.Entity<VolunteerAction>(e =>
            {
                e.HasKey(x => x.Id);

                e.Property(x => x.Title)
                    .HasMaxLength(200)
                    .IsRequired();

                e.Property(x => x.City)
                    .HasMaxLength(120)
                    .IsRequired();

                // ✅ action image url
                e.Property(x => x.ImageUrl)
                    .HasMaxLength(600);

                e.HasMany(x => x.Signups)
                    .WithOne(x => x.VolunteerAction)
                    .HasForeignKey(x => x.VolunteerActionId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<Signup>(e =>
            {
                e.HasKey(x => x.Id);

                e.HasIndex(x => new { x.UserId, x.VolunteerActionId })
                    .IsUnique();

                e.Property(x => x.HoursAwarded)
                    .HasPrecision(6, 2);
            });

            builder.Entity<Tag>(e =>
            {
                e.HasKey(x => x.Id);

                e.Property(x => x.Name)
                    .HasMaxLength(80)
                    .IsRequired();

                e.HasIndex(x => x.Name)
                    .IsUnique();
            });

            builder.Entity<VolunteerActionTag>(e =>
            {
                e.HasKey(x => new { x.VolunteerActionId, x.TagId });

                e.HasOne(x => x.VolunteerAction)
                    .WithMany(x => x.ActionTags)
                    .HasForeignKey(x => x.VolunteerActionId);

                e.HasOne(x => x.Tag)
                    .WithMany(x => x.ActionTags)
                    .HasForeignKey(x => x.TagId);
            });
        }
    }
}
