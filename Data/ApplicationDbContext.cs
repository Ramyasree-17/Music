using Microsoft.EntityFrameworkCore;
using TunewaveAPIDB1.Models;

namespace TunewaveAPIDB1.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Branding> Brandings { get; set; }
        public DbSet<MailLog> MailLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Branding table mapping
            modelBuilder.Entity<Branding>(entity =>
            {
                entity.ToTable("Branding");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("Id");
                entity.Property(e => e.SiteName).HasColumnName("SiteName");
                entity.Property(e => e.DomainName).HasColumnName("DomainName");
                entity.Property(e => e.ContactEmail).HasColumnName("ContactEmail");
                entity.Property(e => e.IsActive)
                    .HasColumnName("IsActive")
                    .HasColumnType("bit"); // Explicitly map BIT type
            });

            // MailLogs table mapping
            modelBuilder.Entity<MailLog>(entity =>
            {
                entity.ToTable("MailLogs");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("Id");
                entity.Property(e => e.BrandingId).HasColumnName("BrandingId");
                entity.Property(e => e.FromEmail).HasColumnName("FromEmail");
                entity.Property(e => e.ToEmail).HasColumnName("ToEmail");
                entity.Property(e => e.Subject).HasColumnName("Subject");
                entity.Property(e => e.Status).HasColumnName("Status");
                entity.Property(e => e.ErrorMessage).HasColumnName("ErrorMessage");
                entity.Property(e => e.SentAt).HasColumnName("SentAt");
            });

        }
    }
}

