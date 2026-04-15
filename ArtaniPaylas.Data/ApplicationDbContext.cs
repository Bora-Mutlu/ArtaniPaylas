using ArtaniPaylas.Core.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ArtaniPaylas.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Listing> Listings => Set<Listing>();

    public DbSet<Request> Requests => Set<Request>();

    public DbSet<Review> Reviews => Set<Review>();

    public DbSet<Report> Reports => Set<Report>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Listing>(entity =>
        {
            entity.HasOne(x => x.OwnerUser)
                .WithMany(x => x.Listings)
                .HasForeignKey(x => x.OwnerUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Request>(entity =>
        {
            entity.HasOne(x => x.Listing)
                .WithMany(x => x.Requests)
                .HasForeignKey(x => x.ListingId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.RequesterUser)
                .WithMany(x => x.RequestsMade)
                .HasForeignKey(x => x.RequesterUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Review>(entity =>
        {
            entity.HasOne(x => x.FromUser)
                .WithMany(x => x.ReviewsGiven)
                .HasForeignKey(x => x.FromUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ToUser)
                .WithMany(x => x.ReviewsReceived)
                .HasForeignKey(x => x.ToUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Request)
                .WithMany()
                .HasForeignKey(x => x.RequestId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => new { x.RequestId, x.FromUserId }).IsUnique();
        });

        builder.Entity<Report>(entity =>
        {
            entity.HasOne(x => x.Reporter)
                .WithMany()
                .HasForeignKey(x => x.ReporterId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.ReportedUser)
                .WithMany()
                .HasForeignKey(x => x.ReportedUserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(x => x.ReportedListing)
                .WithMany()
                .HasForeignKey(x => x.ReportedListingId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
