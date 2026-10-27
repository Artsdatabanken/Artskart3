using Artskart3.Core.Application.Import;
using Artskart3.Core.Application.Import.Models;
using FluentAssertions;

namespace Artskart3.Tests.Unit;

public class ImportPropertyOverridesTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Ingen_overstyringer_for_en_tom_konfigurasjon(string? json)
    {
        var overstyringer = ImportPropertyOverrides.Parse(json);

        overstyringer.Count.Should().Be(0);
        overstyringer.Should().BeSameAs(ImportPropertyOverrides.None);
    }

    [Fact]
    public void Setter_et_felt_pa_posten()
    {
        var record = new CacheRecord { BasisOfRecord = "Ukjent" };

        ImportPropertyOverrides.Parse("""{"BasisOfRecord":"HumanObservation"}""").ApplyTo(record);

        record.BasisOfRecord.Should().Be("HumanObservation");
    }

    [Fact]
    public void Setter_flere_felt()
    {
        var record = new CacheRecord();

        var overstyringer = ImportPropertyOverrides.Parse(
            """{"InstitutionCode":"NINA","CollectionCode":"Feltdata"}""");

        overstyringer.ApplyTo(record);

        overstyringer.Count.Should().Be(2);
        record.InstitutionCode.Should().Be("NINA");
        record.CollectionCode.Should().Be("Feltdata");
    }

    [Fact]
    public void Setter_overstyringene_pa_hver_post()
    {
        var overstyringer = ImportPropertyOverrides.Parse("""{"Country":"Norway"}""");

        var poster = new[] { new CacheRecord(), new CacheRecord { Country = "Noreg" } };

        foreach (var post in poster) overstyringer.ApplyTo(post);

        poster.Should().OnlyContain(x => x.Country == "Norway");
    }

    /// <summary>
    /// <c>JavaScriptSerializer.Deserialize&lt;Dictionary&lt;string, string&gt;&gt;</c>
    /// tar imot tall og bool i JSON-en og gjør dem til tekst. Det gjør denne også, så
    /// en konfigurasjon skrevet mot Artskart 2 leses likt.
    /// </summary>
    [Fact]
    public void Tar_imot_tall_i_json()
    {
        var record = new CacheRecord();

        ImportPropertyOverrides.Parse("""{"UTMsone":33}""").ApplyTo(record);

        record.UTMsone.Should().Be(33);
    }

    [Fact]
    public void Tar_imot_bool_i_json()
    {
        var record = new CacheRecord();

        ImportPropertyOverrides.Parse("""{"Antropokor":true}""").ApplyTo(record);

        record.Antropokor.Should().BeTrue();
    }

    [Fact]
    public void Tar_imot_null_i_json_pa_et_tekstfelt()
    {
        var record = new CacheRecord { OwnerInstitutionCode = "Noe" };

        ImportPropertyOverrides.Parse("""{"OwnerInstitutionCode":null}""").ApplyTo(record);

        record.OwnerInstitutionCode.Should().BeNull();
    }

    // --- Feil skal stoppe noden, og stoppe den tidlig ----------------------

    /// <summary>
    /// Artskart 2 kaster for et ukjent propertynavn, og det er riktig: en skrivefeil i
    /// registeret skal ikke gi en node som ser ut til å gå fint mens overstyringen
    /// aldri slår inn.
    /// </summary>
    [Fact]
    public void Kaster_for_et_ukjent_feltnavn()
    {
        var handling = () => ImportPropertyOverrides.Parse("""{"Lokalitet":"Oslo"}""");

        handling.Should().Throw<InvalidOperationException>()
            .WithMessage("*Lokalitet*");
    }

    [Fact]
    public void Kaster_for_feil_bokstavstorrelse_i_feltnavnet()
    {
        var handling = () => ImportPropertyOverrides.Parse("""{"locality":"Oslo"}""");

        handling.Should().Throw<InvalidOperationException>()
            .WithMessage("*locality*");
    }

    /// <summary>
    /// Alle ukjente navn skal stå i meldingen, ikke bare det første — ellers må den som
    /// retter registeret kjøre noden på nytt per skrivefeil.
    /// </summary>
    [Fact]
    public void Navngir_alle_ukjente_felt()
    {
        var handling = () => ImportPropertyOverrides.Parse("""{"Lokalitet":"Oslo","Fylke":"Oslo"}""");

        handling.Should().Throw<InvalidOperationException>()
            .WithMessage("*Lokalitet*Fylke*");
    }

    /// <summary>
    /// Verdien konverteres når overstyringene leses, ikke på første post. Artskart 2
    /// oppdager dette midt i en nodeimport; her stopper noden før den begynner.
    /// </summary>
    [Fact]
    public void Kaster_for_en_verdi_som_ikke_passer_feltets_type()
    {
        var handling = () => ImportPropertyOverrides.Parse("""{"UTMsone":"trettitre"}""");

        handling.Should().Throw<InvalidOperationException>()
            .WithMessage("*UTMsone*trettitre*");
    }

    [Fact]
    public void Kaster_for_null_pa_et_felt_som_ikke_kan_vaere_null()
    {
        var handling = () => ImportPropertyOverrides.Parse("""{"UTMsone":null}""");

        handling.Should().Throw<InvalidOperationException>()
            .WithMessage("*UTMsone*");
    }

    [Fact]
    public void Kaster_for_ugyldig_json()
    {
        var handling = () => ImportPropertyOverrides.Parse("{ikke json");

        handling.Should().Throw<InvalidOperationException>()
            .WithMessage("*gyldig JSON-objekt*");
    }

    /// <summary>
    /// Et nøstet objekt kan ikke bli en tekstverdi. <c>JavaScriptSerializer</c> ville
    /// kastet her også.
    /// </summary>
    [Fact]
    public void Kaster_for_en_nostet_verdi()
    {
        var handling = () => ImportPropertyOverrides.Parse("""{"Locality":{"navn":"Oslo"}}""");

        handling.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Lister_feltene_som_overstyres()
    {
        var overstyringer = ImportPropertyOverrides.Parse("""{"Country":"Norway","County":"Oslo"}""");

        overstyringer.Fields.Should().BeEquivalentTo(["Country", "County"]);
    }
}
