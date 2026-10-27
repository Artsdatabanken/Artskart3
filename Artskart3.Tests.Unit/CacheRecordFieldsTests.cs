using System.Globalization;
using System.Reflection;
using Artskart3.Core.Application.Import;
using Artskart3.Core.Application.Import.Models;
using FluentAssertions;

namespace Artskart3.Tests.Unit;

public class CacheRecordFieldsTests
{
    /// <summary>
    /// Feltene som bevisst ikke kan settes utenfra: samlingene, som Artskart 2 heller
    /// ikke kunne sette fra en tekstverdi, og de to kolonnene Artskart 3 har lagt til.
    /// </summary>
    private static readonly string[] BevisstUtenfor =
    [
        "Multimedias",
        "MeasurementOrFacts",
        "ExtendedMeasurementOrFacts",
        "UnknownFields",
        "CacheRowId",
        "HashCode"
    ];

    /// <summary>
    /// Legger noen et felt på <see cref="CacheRecord"/> uten å legge det i
    /// settertabellen, kan en annotasjon eller en overstyring på det feltet ikke
    /// anvendes — og da feiler importen på en konfigurasjon som ser riktig ut. Denne
    /// testen er gjerdet rundt det.
    /// </summary>
    [Fact]
    public void Dekker_alle_felt_pa_CacheRecord()
    {
        var mangler = typeof(CacheRecord)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(x => x.SetMethod is not null)
            .Select(x => x.Name)
            .Except(BevisstUtenfor, StringComparer.Ordinal)
            .Where(x => !CacheRecordFields.CanSet(x))
            .ToArray();

        mangler.Should().BeEmpty("alle felt som kan settes skal stå i settertabellen");
    }

    [Fact]
    public void Inneholder_ingenting_som_ikke_finnes_pa_CacheRecord()
    {
        var finnes = typeof(CacheRecord)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(x => x.Name)
            .ToHashSet(StringComparer.Ordinal);

        CacheRecordFields.Names.Should().OnlyContain(x => finnes.Contains(x));
    }

    [Fact]
    public void Tar_ikke_med_samlingene()
    {
        foreach (var felt in BevisstUtenfor)
        {
            CacheRecordFields.CanSet(felt).Should().BeFalse($"{felt} skal ikke kunne settes");
        }
    }

    /// <summary>
    /// Artskart 2 slår opp feltnavnet med <c>GetProperty</c>, som er
    /// bokstavstørrelsesfølsom. En konfigurasjon som ikke virker der skal ikke virke
    /// her heller.
    /// </summary>
    [Fact]
    public void Oppslaget_er_bokstavstorrelsesfolsomt()
    {
        CacheRecordFields.CanSet("Locality").Should().BeTrue();
        CacheRecordFields.CanSet("locality").Should().BeFalse();
        CacheRecordFields.CanSet("LOCALITY").Should().BeFalse();
    }

    // --- Konvertering ------------------------------------------------------

    private static CacheRecord Sett(string felt, string? verdi)
    {
        var record = new CacheRecord();

        CacheRecordFields.Setter(felt)!(record, verdi);

        return record;
    }

    [Fact]
    public void Setter_et_tekstfelt()
    {
        Sett("Locality", "Oslo").Locality.Should().Be("Oslo");
    }

    /// <summary>
    /// Settingen går gjennom modellens egen setter, så feltene som trimmer gjør det
    /// også her. Det gjør den i Artskart 2 også — <c>SetValue</c> kaller setteren.
    /// </summary>
    [Fact]
    public void Trimmer_feltene_som_trimmer()
    {
        Sett("Locality", "  Oslo  ").Locality.Should().Be("Oslo");
    }

    [Fact]
    public void Setter_null_pa_et_tekstfelt()
    {
        Sett("Locality", null).Locality.Should().BeNull();
    }

    [Fact]
    public void Setter_et_heltallsfelt()
    {
        Sett("UTMost", "600000").UTMost.Should().Be(600000);
    }

    [Fact]
    public void Setter_et_kort_heltallsfelt()
    {
        Sett("UTMsone", "33").UTMsone.Should().Be(33);
    }

    [Fact]
    public void Setter_et_nullbart_heltallsfelt()
    {
        Sett("Year", "1987").Year.Should().Be(1987);
    }

    /// <summary>
    /// Artskart 2 kan ikke sette de nullbare feltene i det hele tatt —
    /// <c>Convert.ChangeType</c> kaster for <c>int?</c>. Her virker de, og tom verdi
    /// blir <c>null</c>. Divergensen er bevisst: ingen konfigurasjon kan være avhengig
    /// av et unntak som feller hele noden.
    /// </summary>
    [Fact]
    public void Tom_verdi_pa_et_nullbart_felt_blir_null()
    {
        Sett("Year", "").Year.Should().BeNull();
    }

    [Fact]
    public void Setter_et_desimalfelt()
    {
        Sett("Latitude", "59.91273").Latitude.Should().Be(59.91273);
    }

    /// <summary>
    /// Artskart 2 bruker <c>Convert.ChangeType</c> uten kultur, altså trådens. Samme
    /// konfigurasjon ville gitt 5991273 på en norsk server og 59.91273 på en engelsk.
    /// Her leses tall alltid invariant.
    /// </summary>
    [Fact]
    public void Leser_tall_invariant_uansett_tradkultur()
    {
        var opprinnelig = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("nb-NO");

            Sett("Latitude", "59.91273").Latitude.Should().Be(59.91273);
        }
        finally
        {
            CultureInfo.CurrentCulture = opprinnelig;
        }
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("TRUE", true)]
    [InlineData("false", false)]
    [InlineData("False", false)]
    public void Setter_et_boolfelt(string verdi, bool forventet)
    {
        Sett("Antropokor", verdi).Antropokor.Should().Be(forventet);
    }

    /// <summary>«1» og «0» er ikke gyldige bool for <c>Convert.ToBoolean</c> heller.</summary>
    [Theory]
    [InlineData("1")]
    [InlineData("ja")]
    public void Kaster_for_en_ugyldig_bool(string verdi)
    {
        var handling = () => Sett("Antropokor", verdi);

        handling.Should().Throw<FormatException>();
    }

    [Fact]
    public void Setter_et_datofelt()
    {
        Sett("DateLastModified", "2026-10-14T08:30:00")
            .DateLastModified.Should().Be(new DateTime(2026, 10, 14, 8, 30, 0, DateTimeKind.Unspecified));
    }

    [Fact]
    public void Kaster_for_en_verdi_som_ikke_er_tall()
    {
        var handling = () => Sett("UTMost", "seks hundre tusen");

        handling.Should().Throw<FormatException>();
    }

    [Fact]
    public void Kaster_for_null_pa_et_felt_som_ikke_kan_vaere_null()
    {
        var handling = () => Sett("UTMost", null);

        handling.Should().Throw<FormatException>();
    }

    [Fact]
    public void Setter_er_null_for_et_ukjent_feltnavn()
    {
        CacheRecordFields.Setter("FinnesIkke").Should().BeNull();
    }
}
