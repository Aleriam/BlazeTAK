using BlazeTAK.Data.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BlazeTAK.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<User> Profiles => Set<User>();
    public DbSet<VirtUser> VirtUsers => Set<VirtUser>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<Marker> Markers => Set<Marker>();
    public DbSet<PersistentMarker> PersistentMarkers => Set<PersistentMarker>();
    public DbSet<NonPersistentMarker> NonPersistentMarkers => Set<NonPersistentMarker>();
    public DbSet<Message> Messages => Set<Message>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Identity tables must be configured first.
        base.OnModelCreating(builder);

        builder.HasPostgresExtension("postgis");
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        CreateProfilesForNewAccounts();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        CreateProfilesForNewAccounts();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    // Every login account gets a domain profile, without touching the Identity (login/register) code.
    private void CreateProfilesForNewAccounts()
    {
        var newAccounts = ChangeTracker.Entries<ApplicationUser>()
            .Where(e => e.State == EntityState.Added && e.Entity.Profile is null)
            .Select(e => e.Entity)
            .ToList();

        foreach (var account in newAccounts)
        {
            account.Profile = new User
            {
                ID = Guid.NewGuid(),
                Name = account.UserName ?? account.Email ?? string.Empty
            };
        }
    }
}
