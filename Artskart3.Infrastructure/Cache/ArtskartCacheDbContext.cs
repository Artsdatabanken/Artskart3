using Artskart3.Infrastructure.Cache.Entities;
using Microsoft.EntityFrameworkCore;

namespace Artskart3.Infrastructure.Cache;

/// <summary>
/// Read-only kontekst mot <c>Artskart2Cache</c>, cache-databasen Artskart 2s harvest
/// eier.
///
/// LESER, SKRIVER ALDRI. Harvest er ikke portert, så cachen har fortsatt én eier.
/// Konteksten står på <c>NoTracking</c> og skal ha en tilkoblingsstreng med
/// leserettigheter (<c>ConnectionStrings:ArtskartCache</c>). At Artskart 2 og
/// Artskart 3 kan lese samme cache samtidig er det som gjør paritetstesting mulig i
/// det hele tatt.
///
/// INGEN MIGRASJONER. Skjemaet eies av EF6-modellen i Artskart 2, som i tillegg har
/// <c>AutomaticMigrationDataLossAllowed = true</c>. Denne konteksten skal aldri
/// migrere noe — mappingen er skrevet for å lese det som står der.
/// </summary>
public class ArtskartCacheDbContext : DbContext
{
    public ArtskartCacheDbContext(DbContextOptions<ArtskartCacheDbContext> options)
        : base(options)
    {
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        ChangeTracker.AutoDetectChangesEnabled = false;
    }

    public virtual DbSet<CacheDarwinCoreProxy> DarwinCoreProxies => Set<CacheDarwinCoreProxy>();

    public virtual DbSet<CacheInstitutionCode> InstitutionCodes => Set<CacheInstitutionCode>();

    public virtual DbSet<CacheCollectionCode> CollectionCodes => Set<CacheCollectionCode>();

    public virtual DbSet<CacheHarvestState> HarvestStates => Set<CacheHarvestState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CacheDarwinCoreProxy>(entity =>
        {
            entity.ToTable("DarwinCoreProxies");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ProxyId).HasMaxLength(255).IsRequired();
            entity.Property(e => e.OccurenceId).HasMaxLength(250);
            entity.Property(e => e.CatalogNumber).HasMaxLength(200);
            entity.Property(e => e.DateLastModified).HasColumnType("datetime2");
            entity.Property(e => e.TrackInfoDateImported).HasColumnType("datetime2");

            // Indeksene finnes i cachen fra før og er oppgitt her slik at EF kan bruke
            // dem i spørringsplanleggingen. De opprettes ikke herfra.
            entity.HasIndex(e => new { e.NodeId, e.ProxyId }, "IX_Nid_Pid");
            entity.HasIndex(e => new { e.NodeId, e.TrackInfoDateImported, e.IsDeleted }, "IX_Nid_Deleted");
            entity.HasIndex(e => e.TrackInfoDateImported, "IX_TrackInfoDateImported");
        });

        modelBuilder.Entity<CacheInstitutionCode>(entity =>
        {
            entity.ToTable("InstitutionCodes");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Code).HasMaxLength(100);
        });

        modelBuilder.Entity<CacheCollectionCode>(entity =>
        {
            entity.ToTable("CollectionCodes");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Code).HasMaxLength(100);
        });

        modelBuilder.Entity<CacheHarvestState>(entity =>
        {
            entity.ToTable("HarvestStates");
            entity.HasKey(e => new { e.Id, e.Reprocess });
            entity.Property(e => e.NodeName).HasMaxLength(250);
        });
    }
}
