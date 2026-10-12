using Microsoft.EntityFrameworkCore;
using RouteWise.Api.Models;

namespace RouteWise.Api.Data;

public sealed class RouteWiseDbContext(
    DbContextOptions<RouteWiseDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Journey> Journeys => Set<Journey>();
    public DbSet<JourneyPoint> JourneyPoints => Set<JourneyPoint>();
    public DbSet<JourneySegment> JourneySegments => Set<JourneySegment>();
    public DbSet<Invitation> Invitations => Set<Invitation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var user = modelBuilder.Entity<User>();

        user.HasKey(u => u.Id);

        user.Property(u => u.Username)
            .IsRequired()
            .HasMaxLength(20);

        user.Property(u => u.NormalizedUsername)
            .IsRequired()
            .HasMaxLength(20);

        user.HasIndex(u => u.NormalizedUsername)
            .IsUnique();

        user.Property(u => u.PasswordHash)
            .IsRequired();

        var journey = modelBuilder.Entity<Journey>();

        journey.HasKey(j => j.Id);
        journey.Property(j => j.Name).IsRequired();

        journey.Property(j => j.TravelMode).HasConversion<string>();
        journey.Property(j => j.Objective).HasConversion<string>();
        journey.Property(j => j.EndMode).HasConversion<string>();

        journey.HasOne(j => j.Owner).WithMany()
            .HasForeignKey(j => j.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);

        journey.HasMany(j => j.Points).WithOne()
            .HasForeignKey(p => p.JourneyId)
            .OnDelete(DeleteBehavior.Cascade);

        journey.HasMany(j => j.Segments).WithOne()
            .HasForeignKey(s => s.JourneyId)
            .OnDelete(DeleteBehavior.Cascade);

        var point = modelBuilder.Entity<JourneyPoint>();

        point.HasKey(p => new { p.JourneyId, p.Id });
        point.HasIndex(p => new { p.JourneyId, p.Position });

        var segment = modelBuilder.Entity<JourneySegment>();

        segment.HasKey(s => new { s.JourneyId, s.Id });
        segment.HasIndex(s => new { s.JourneyId, s.Position });

        segment.HasOne<JourneyPoint>().WithMany()
            .HasForeignKey(s => new { s.JourneyId, s.FromPointId })
            .OnDelete(DeleteBehavior.NoAction);

        segment.HasOne<JourneyPoint>().WithMany()
            .HasForeignKey(s => new { s.JourneyId, s.ToPointId })
            .OnDelete(DeleteBehavior.NoAction);

        segment.OwnsMany(s => s.Path, path => path.ToJson());

        var invitation = modelBuilder.Entity<Invitation>();

        invitation.HasKey(i => i.Id);
        invitation.Property(i => i.Status).HasConversion<string>();

        invitation.HasOne(i => i.Journey).WithMany()
            .HasForeignKey(i => i.JourneyId)
            .OnDelete(DeleteBehavior.Cascade);

        invitation.HasOne(i => i.Sender).WithMany()
            .HasForeignKey(i => i.SenderId)
            .OnDelete(DeleteBehavior.Cascade);

        invitation.HasOne(i => i.Recipient).WithMany()
            .HasForeignKey(i => i.RecipientId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}