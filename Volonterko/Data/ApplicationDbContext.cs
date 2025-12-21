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

        // ===== DbSets (domenski entiteti) =====

        public DbSet<Organization> Organizations => Set<Organization>();
        public DbSet<VolunteerAction> VolunteerActions => Set<VolunteerAction>();
        public DbSet<Signup> Signups => Set<Signup>();
        public DbSet<Tag> Tags => Set<Tag>();
        public DbSet<VolunteerActionTag> VolunteerActionTags => Set<VolunteerActionTag>();

        // ===== Fluent konfiguracija =====

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // -------- Organization --------
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

                // Organization → VolunteerActions (1:N)
                e.HasMany(x => x.Actions)
                    .WithOne(x => x.Organization)
                    .HasForeignKey(x => x.OrganizationId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Jedan user = jedna organizacija (za MVP)
                e.HasIndex(x => x.OwnerUserId)
                    .IsUnique();
            });

            // -------- VolunteerAction --------
            builder.Entity<VolunteerAction>(e =>
            {
                e.HasKey(x => x.Id);

                e.Property(x => x.Title)
                    .HasMaxLength(200)
                    .IsRequired();

                e.Property(x => x.City)
                    .HasMaxLength(120)
                    .IsRequired();

                // VolunteerAction → Signups (1:N)
                e.HasMany(x => x.Signups)
                    .WithOne(x => x.VolunteerAction)
                    .HasForeignKey(x => x.VolunteerActionId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // -------- Signup --------
            builder.Entity<Signup>(e =>
            {
                e.HasKey(x => x.Id);

                // Jedan user se može prijaviti na jednu akciju samo jednom
                e.HasIndex(x => new { x.UserId, x.VolunteerActionId })
                    .IsUnique();

                // Explicit precision for SQL Server
                e.Property(x => x.HoursAwarded)
                    .HasPrecision(6, 2);
            });

            // -------- Tag --------
            builder.Entity<Tag>(e =>
            {
                e.HasKey(x => x.Id);

                e.Property(x => x.Name)
                    .HasMaxLength(80)
                    .IsRequired();

                e.HasIndex(x => x.Name)
                    .IsUnique();
            });

            // -------- VolunteerActionTag (M:N join) --------
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
