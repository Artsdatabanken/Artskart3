using Artskart3.Core.Application.Import.Models;
using Artskart3.Core.Application.Import.Writing;
using FluentAssertions;

namespace Artskart3.Tests.Unit;

public class ImportBatchTests
{
    private readonly ImportBatch _bunt = new();

    private static ImportedObservation Observasjon(string proxyId, ImportedLocation? lokalitet = null) =>
        new() { ProxyId = proxyId, Location = lokalitet };

    private static ImportedLocation Lokalitet(string lookupId) =>
        new() { LookupId = lookupId, East = 600000, North = 6600000 };

    [Fact]
    public void Tom_bunt_har_ingenting()
    {
        _bunt.Count.Should().Be(0);
        _bunt.Observations.Should().BeEmpty();
        _bunt.Locations.Should().BeEmpty();
    }

    [Fact]
    public void Tar_imot_en_observasjon()
    {
        _bunt.Add(Observasjon("a"));

        _bunt.Count.Should().Be(1);
    }

    [Fact]
    public void Tar_med_lokaliteten()
    {
        _bunt.Add(Observasjon("a", Lokalitet("40_600000_6600000_100")));

        _bunt.Locations.Should().ContainSingle()
            .Which.LookupId.Should().Be("40_600000_6600000_100");
    }

    /// <summary>
    /// En bunt på 250 000 poster fra samme node har langt færre distinkte lokaliteter,
    /// og hver av dem skal bli én rad.
    /// </summary>
    [Fact]
    public void Tar_med_hver_lokalitet_en_gang()
    {
        var lokalitet = Lokalitet("40_600000_6600000_100");

        _bunt.Add(Observasjon("a", lokalitet));
        _bunt.Add(Observasjon("b", lokalitet));
        _bunt.Add(Observasjon("c", Lokalitet("40_600000_6600000_100")));

        _bunt.Count.Should().Be(3);
        _bunt.Locations.Should().ContainSingle();
    }

    [Fact]
    public void Taaler_en_observasjon_uten_lokalitet()
    {
        _bunt.Add(Observasjon("a"));

        _bunt.Locations.Should().BeEmpty();
    }

    [Fact]
    public void Taaler_en_lokalitet_uten_nokkel()
    {
        _bunt.Add(Observasjon("a", new ImportedLocation()));

        _bunt.Locations.Should().BeEmpty("uten LookupId finnes det ingen rad å slå opp");
    }

    // --- Bufferet ---------------------------------------------------------

    /// <summary>
    /// Er lokaliteten alt skrevet, skal den ikke skrives igjen — observasjonen får
    /// Id-en direkte.
    /// </summary>
    [Fact]
    public void Henter_kjent_lokalitetsid_fra_bufferet()
    {
        var buffer = new LocationIdCache();
        buffer.Set("40_600000_6600000_100", 4711);

        var observasjon = Observasjon("a", Lokalitet("40_600000_6600000_100"));

        _bunt.Add(observasjon, buffer);

        observasjon.LocationId.Should().Be(4711);
        _bunt.Locations.Should().BeEmpty("lokaliteten finnes alt i basen");
    }

    [Fact]
    public void Tar_med_lokaliteten_naar_bufferet_ikke_treffer()
    {
        var observasjon = Observasjon("a", Lokalitet("40_600000_6600000_100"));

        _bunt.Add(observasjon, new LocationIdCache());

        observasjon.LocationId.Should().BeNull();
        _bunt.Locations.Should().ContainSingle();
    }

    [Fact]
    public void Clear_tommer_begge_samlingene()
    {
        _bunt.Add(Observasjon("a", Lokalitet("x")));

        _bunt.Clear();

        _bunt.Count.Should().Be(0);
        _bunt.Locations.Should().BeEmpty();
    }
}

public class LocationIdCacheTests
{
    [Fact]
    public void Finner_det_som_er_lagt_inn()
    {
        var buffer = new LocationIdCache();

        buffer.Set("a", 1);

        buffer.TryGet("a", out var id).Should().BeTrue();
        id.Should().Be(1);
        buffer.Hits.Should().Be(1);
    }

    [Fact]
    public void Finner_ikke_det_som_ikke_er_lagt_inn()
    {
        var buffer = new LocationIdCache();

        buffer.TryGet("a", out var id).Should().BeFalse();
        id.Should().Be(0);
        buffer.Misses.Should().Be(1);
    }

    [Fact]
    public void Oppdaterer_en_eksisterende_nokkel()
    {
        var buffer = new LocationIdCache();

        buffer.Set("a", 1);
        buffer.Set("a", 2);

        buffer.TryGet("a", out var id);

        id.Should().Be(2);
        buffer.Count.Should().Be(1);
    }

    /// <summary>
    /// Artskart 2 tømmer hele bufferet når det blir fullt. Her kastes den eldste ut, så
    /// nøklene som brukes hele tiden blir liggende.
    /// </summary>
    [Fact]
    public void Kaster_ut_den_eldste_naar_det_er_fullt()
    {
        var buffer = new LocationIdCache(capacity: 2);

        buffer.Set("a", 1);
        buffer.Set("b", 2);
        buffer.Set("c", 3);

        buffer.Count.Should().Be(2);
        buffer.TryGet("a", out _).Should().BeFalse("a var eldst");
        buffer.TryGet("b", out _).Should().BeTrue();
        buffer.TryGet("c", out _).Should().BeTrue();
    }

    [Fact]
    public void Et_oppslag_gjor_nokkelen_nyest()
    {
        var buffer = new LocationIdCache(capacity: 2);

        buffer.Set("a", 1);
        buffer.Set("b", 2);
        buffer.TryGet("a", out _);
        buffer.Set("c", 3);

        buffer.TryGet("a", out _).Should().BeTrue("a ble brukt nettopp");
        buffer.TryGet("b", out _).Should().BeFalse("b var eldst da c kom");
    }

    [Fact]
    public void En_oppdatering_gjor_ogsaa_nokkelen_nyest()
    {
        var buffer = new LocationIdCache(capacity: 2);

        buffer.Set("a", 1);
        buffer.Set("b", 2);
        buffer.Set("a", 11);
        buffer.Set("c", 3);

        buffer.TryGet("a", out var id).Should().BeTrue();
        id.Should().Be(11);
        buffer.TryGet("b", out _).Should().BeFalse();
    }

    [Fact]
    public void Clear_tommer_bufferet()
    {
        var buffer = new LocationIdCache();

        buffer.Set("a", 1);
        buffer.Clear();

        buffer.Count.Should().Be(0);
        buffer.TryGet("a", out _).Should().BeFalse();
    }

    [Fact]
    public void Krever_en_kapasitet_over_null()
    {
        var handling = () => new LocationIdCache(capacity: 0);

        handling.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Krever_en_nokkel()
    {
        var buffer = new LocationIdCache();

        var handling = () => buffer.Set(string.Empty, 1);

        handling.Should().Throw<ArgumentException>();
    }
}
