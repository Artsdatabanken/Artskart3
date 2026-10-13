using Artskart3.Core.Application.Import;
using Artskart3.Core.Application.Import.Models;
using Artskart3.Core.Application.Import.Steps;
using FluentAssertions;

namespace Artskart3.Tests.Unit;

public class ImportCheckUniqueIdStepTests
{
    private readonly ImportCheckUniqueIdStep _steg = new();

    private ImportRecordResult Kjør(string? proxyId) =>
        _steg.ValidateAndImport(
            new CacheRecord(),
            new ImportedObservation { ProxyId = proxyId },
            new ImportContext());

    [Fact]
    public void Er_et_pre_steg()
    {
        _steg.StepType.Should().Be(ImportStepType.Pre);
    }

    [Fact]
    public void Godtar_en_identitet_forste_gang()
    {
        Kjør("uio/o/o-1").ValidationCode.Should().Be(RecordValidationCode.Ok);
    }

    [Fact]
    public void Avviser_samme_identitet_andre_gang()
    {
        Kjør("uio/o/o-1");

        var resultat = Kjør("uio/o/o-1");

        resultat.IsAccepted.Should().BeFalse();
        resultat.ValidationCode.Should().Be(RecordValidationCode.DuplicateId);
        resultat.ErrorContext.Should().Be("uio/o/o-1", "avvisningen skal si hvilken identitet som kolliderte");
    }

    [Fact]
    public void Godtar_ulike_identiteter()
    {
        Kjør("uio/o/o-1").IsAccepted.Should().BeTrue();
        Kjør("uio/o/o-2").IsAccepted.Should().BeTrue();
        Kjør("nina/b/b-1").IsAccepted.Should().BeTrue();

        _steg.SeenCount.Should().Be(3);
    }

    /// <summary>
    /// Identiteten skiller på bokstavstørrelse, som i Artskart 2. To poster som bare
    /// avviker i store og små bokstaver regnes derfor som to.
    /// </summary>
    [Fact]
    public void Skiller_paa_bokstavstorrelse()
    {
        Kjør("uio/o/o-1").IsAccepted.Should().BeTrue();
        Kjør("UIO/O/O-1").IsAccepted.Should().BeTrue();
    }

    /// <summary>
    /// En post uten identitet legges i settet som alle andre, så den andre avvises. En
    /// node som leverer poster uten identitet skal se det i avvisningene.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Avviser_den_andre_posten_uten_identitet(string? proxyId)
    {
        Kjør(proxyId).IsAccepted.Should().BeTrue();

        Kjør(proxyId).ValidationCode.Should().Be(RecordValidationCode.DuplicateId);
    }

    /// <summary>
    /// Tilstanden hører til én nodekjøring. En ny instans skal ikke huske noe —
    /// kjeden bygges per node, aldri som singleton.
    /// </summary>
    [Fact]
    public void Ny_instans_husker_ingenting()
    {
        Kjør("uio/o/o-1");

        var nyttSteg = new ImportCheckUniqueIdStep();
        var resultat = nyttSteg.ValidateAndImport(
            new CacheRecord(),
            new ImportedObservation { ProxyId = "uio/o/o-1" },
            new ImportContext());

        resultat.IsAccepted.Should().BeTrue();
        nyttSteg.SeenCount.Should().Be(1);
    }
}
