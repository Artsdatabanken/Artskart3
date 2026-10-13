using Artskart3.Core.Application.Import;
using Artskart3.Core.Application.Import.Models;
using Artskart3.Core.Application.Import.Steps;
using Artskart3.Core.Domain.Enums;
using FluentAssertions;

namespace Artskart3.Tests.Unit;

public class ImportMediaStepTests
{
    private readonly ImportMediaStep _steg = new();
    private readonly ImportedObservation _observasjon = new();
    private readonly ImportContext _context = new();

    private ImportRecordResult Kjør(CacheRecord record) =>
        _steg.ValidateAndImport(record, _observasjon, _context);

    [Fact]
    public void Er_et_parallell_steg()
    {
        // Planen fører ImportMedia som PostTask, men konstruktøren i Artskart 2 setter
        // ParallellTask. Steget kjører altså før ImportSimpleProperties.
        _steg.StepType.Should().Be(ImportStepType.Parallel);
    }

    [Fact]
    public void Gjor_ingenting_uten_media()
    {
        Kjør(new CacheRecord()).ValidationCode.Should().Be(RecordValidationCode.Ok);

        _observasjon.MediaFiles.Should().BeEmpty();
        _observasjon.Tags.Should().BeEmpty();
        _observasjon.AssociatedReferences.Should().BeEmpty();
        _observasjon.HasMediaFiles.Should().BeFalse();
    }

    [Fact]
    public void Lager_bildefil_og_tagg_av_associatedMedia()
    {
        Kjør(new CacheRecord { AssociatedMedia = "https://example.org/bilde.jpg" });

        _observasjon.MediaFiles.Should().ContainSingle();
        _observasjon.MediaFiles[0].Origin.Should().Be("https://example.org/bilde.jpg");
        _observasjon.MediaFiles[0].MediaFileTypeId.Should().Be(1, "1 er Jpeg");
        _observasjon.HasTag(Tag.HasImage).Should().BeTrue();
        _observasjon.HasMediaFiles.Should().BeTrue();
    }

    [Theory]
    [InlineData("jpg")]
    [InlineData("jpeg")]
    [InlineData("png")]
    [InlineData("gif")]
    [InlineData("jp2")]
    [InlineData("dng")]
    [InlineData("tiff")]
    public void Kjenner_igjen_bildetypene(string type)
    {
        Kjør(new CacheRecord { AssociatedMedia = $"https://example.org/fil.{type}" });

        _observasjon.MediaFiles.Should().ContainSingle();
        _observasjon.MediaFiles[0].MediaFileTypeId.Should().Be(1);
    }

    /// <summary>
    /// Typegjenkjenningen er <c>Contains</c>, ikke filendelse. En URL mot et domene med
    /// «png» i navnet teller som bilde — dagens oppførsel.
    /// </summary>
    [Fact]
    public void Regner_url_med_bildetype_i_domenet_som_bilde()
    {
        Kjør(new CacheRecord { AssociatedMedia = "https://pngwing.example/side" });

        _observasjon.MediaFiles.Should().ContainSingle();
        _observasjon.MediaFiles[0].MediaFileTypeId.Should().Be(1);
    }

    /// <summary>
    /// Betingelsen for å lese <c>associatedMedia</c> er «ikke tom OG inneholder ikke
    /// loddrett strek», mens splittingen like etter deler på både semikolon og
    /// loddrett strek. Flere URL-er skilt med strek faller derfor ut.
    /// </summary>
    [Fact]
    public void Hopper_over_associatedMedia_med_loddrett_strek()
    {
        Kjør(new CacheRecord
        {
            AssociatedMedia = "https://example.org/a.jpg|https://example.org/b.jpg"
        });

        _observasjon.MediaFiles.Should().BeEmpty();
        _observasjon.AssociatedReferences.Should().BeEmpty();
    }

    [Fact]
    public void Deler_associatedMedia_paa_semikolon()
    {
        Kjør(new CacheRecord
        {
            AssociatedMedia = "https://example.org/a.jpg; https://example.org/b.jpg"
        });

        _observasjon.MediaFiles.Should().HaveCount(2);
        _observasjon.MediaFiles.Select(x => x.Origin).Should()
            .Equal(["https://example.org/a.jpg", "https://example.org/b.jpg"]);
    }

    [Fact]
    public void Lager_lydfil_uten_bildetagg()
    {
        Kjør(new CacheRecord { AssociatedMedia = "https://example.org/lyd.mp3" });

        _observasjon.MediaFiles.Should().ContainSingle();
        _observasjon.MediaFiles[0].MediaFileTypeId.Should().Be(2, "2 er Audio");
        _observasjon.HasTag(Tag.HasImage).Should().BeFalse();
    }

    /// <summary>
    /// Det som verken er bilde eller lyd blir en referanse. For node 40 og 211 blir den
    /// senere overskrevet av <c>ImportSimplePropertiesStep</c>.
    /// </summary>
    [Fact]
    public void Legger_annet_enn_bilde_og_lyd_i_referanser()
    {
        Kjør(new CacheRecord { AssociatedMedia = "https://example.org/artikkel.pdf" });

        _observasjon.MediaFiles.Should().BeEmpty();
        _observasjon.AssociatedReferences.Should().Equal(["https://example.org/artikkel.pdf"]);
    }

    [Fact]
    public void Bruker_format_naar_url_ikke_avslorer_typen()
    {
        Kjør(new CacheRecord
        {
            Multimedias = [new CacheMultimedia { Identifier = "https://example.org/hent?id=5", Format = "image/jpeg" }]
        });

        _observasjon.MediaFiles.Should().ContainSingle();
        _observasjon.MediaFiles[0].MediaFileTypeId.Should().Be(1);
        _observasjon.HasTag(Tag.HasImage).Should().BeTrue();
    }

    [Fact]
    public void Tar_med_multimedia_utvidelsen()
    {
        Kjør(new CacheRecord
        {
            Multimedias =
            [
                new CacheMultimedia { Identifier = "https://example.org/a.jpg" },
                new CacheMultimedia { Identifier = "https://example.org/b.jpg" }
            ]
        });

        _observasjon.MediaFiles.Should().HaveCount(2);
    }

    [Fact]
    public void Hopper_over_multimedia_uten_identifikator()
    {
        Kjør(new CacheRecord
        {
            Multimedias = [new CacheMultimedia { Identifier = "  ", Format = "image/jpeg" }],
            AssociatedMedia = "https://example.org/a.jpg"
        });

        _observasjon.MediaFiles.Should().ContainSingle();
    }

    /// <summary>
    /// Samme fil oppgitt både i <c>associatedMedia</c> og i utvidelsen skal bare bli én
    /// mediefil. Sammenligningen ser bort fra bokstavstørrelse og mellomrom rundt.
    /// </summary>
    [Fact]
    public void Dubletter_mellom_de_to_kildene_legges_bare_til_en_gang()
    {
        Kjør(new CacheRecord
        {
            AssociatedMedia = "https://example.org/a.jpg",
            Multimedias = [new CacheMultimedia { Identifier = "  HTTPS://EXAMPLE.ORG/A.JPG " }]
        });

        _observasjon.MediaFiles.Should().ContainSingle();
    }

    [Fact]
    public void Setter_rettighetshaver_fra_forste_utfylte_felt()
    {
        Kjør(new CacheRecord
        {
            Multimedias =
            [
                new CacheMultimedia { Identifier = "a.jpg", Creator = "Fotograf", Publisher = "Utgiver" }
            ]
        });

        _observasjon.MediaFiles[0].RightsHolder.Should().Be("Fotograf");
    }

    [Fact]
    public void Foretrekker_rettighetshaver_framfor_fotograf()
    {
        Kjør(new CacheRecord
        {
            Multimedias =
            [
                new CacheMultimedia { Identifier = "a.jpg", RightsHolder = "Eier", Creator = "Fotograf" }
            ]
        });

        _observasjon.MediaFiles[0].RightsHolder.Should().Be("Eier");
    }

    [Fact]
    public void Legger_kilden_bak_rettighetshaver()
    {
        Kjør(new CacheRecord
        {
            Multimedias = [new CacheMultimedia { Identifier = "a.jpg", RightsHolder = "Eier", Source = "Samlingen" }]
        });

        _observasjon.MediaFiles[0].RightsHolder.Should().Be("Eier, Samlingen");
    }

    /// <summary>
    /// Er ingen av de tre eierfeltene utfylt, blir verdien stående som «, kilde». Slik
    /// er det i Artskart 2.
    /// </summary>
    [Fact]
    public void Gir_bare_kilden_med_komma_foran_naar_eier_mangler()
    {
        Kjør(new CacheRecord
        {
            Multimedias = [new CacheMultimedia { Identifier = "a.jpg", Source = "Samlingen" }]
        });

        _observasjon.MediaFiles[0].RightsHolder.Should().Be(", Samlingen");
    }

    [Fact]
    public void Trunkerer_lisens_rettighetshaver_og_beskrivelse_til_500()
    {
        var lang = new string('x', 600);

        Kjør(new CacheRecord
        {
            Multimedias =
            [
                new CacheMultimedia { Identifier = "a.jpg", License = lang, RightsHolder = lang, Description = lang }
            ]
        });

        var fil = _observasjon.MediaFiles[0];
        fil.License.Should().HaveLength(500);
        fil.RightsHolder.Should().HaveLength(500);
        fil.Description.Should().HaveLength(500);
        fil.License.Should().EndWith("...");
    }

    [Fact]
    public void Lager_en_mediefil_per_url_i_en_sammensatt_identifikator()
    {
        Kjør(new CacheRecord
        {
            Multimedias = [new CacheMultimedia { Identifier = "https://example.org/a.jpg|https://example.org/b.jpg" }]
        });

        _observasjon.MediaFiles.Should().HaveCount(2);
        _observasjon.MediaFiles.Select(x => x.Origin).Should()
            .Equal(["https://example.org/a.jpg", "https://example.org/b.jpg"]);
    }

    /// <summary>
    /// Typen avgjøres av hele identifikatorstrengen. Inneholder den «jpg» én gang,
    /// behandles alle URL-ene i den som bilder.
    /// </summary>
    [Fact]
    public void Typen_avgjores_av_hele_strengen_ikke_per_url()
    {
        Kjør(new CacheRecord
        {
            Multimedias = [new CacheMultimedia { Identifier = "https://example.org/a.jpg|https://example.org/b.pdf" }]
        });

        _observasjon.MediaFiles.Should().HaveCount(2);
        _observasjon.MediaFiles.Should().OnlyContain(x => x.MediaFileTypeId == 1);
    }

    /// <summary>
    /// For en sammensatt identifikator som ikke er bilde eller lyd blir bare den første
    /// URL-en referanse — og den legges inn én gang per URL i strengen, altså dedupet.
    /// </summary>
    [Fact]
    public void Bruker_forste_url_som_referanse_for_sammensatt_identifikator()
    {
        Kjør(new CacheRecord
        {
            Multimedias = [new CacheMultimedia { Identifier = "https://example.org/a.pdf|https://example.org/b.pdf" }]
        });

        _observasjon.AssociatedReferences.Should().Equal(["https://example.org/a.pdf"]);
    }
}
