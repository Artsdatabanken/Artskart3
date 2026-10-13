using Artskart3.Core.Application.Import.Helpers;
using FluentAssertions;

namespace Artskart3.Tests.Unit;

/// <summary>
/// <c>CleanDarwinCoreId</c> bestemmer <c>Observation.ProxyId</c> — nøkkelen som binder
/// en cache-post til en eksisterende observasjon. Testene her låser formen, ikke bare
/// oppførselen.
/// </summary>
public class DarwinCoreHelperTests
{
    [Fact]
    public void Bruker_occurrenceId_naar_den_er_en_ekte_identifikator()
    {
        DarwinCoreHelper.CleanDarwinCoreId("UiO", "O", "O-1", "urn:uuid:ABC-123")
            .Should().Be("urn:uuid:abc-123");
    }

    [Theory]
    [InlineData("1")]
    [InlineData("1234567890")]
    public void Bruker_ikke_occurrenceId_som_er_et_rent_loepenummer(string occurrenceId)
    {
        DarwinCoreHelper.CleanDarwinCoreId("UiO", "O", "O-1", occurrenceId)
            .Should().Be("UiO/O/o-1");
    }

    [Fact]
    public void Bruker_occurrenceId_med_elleve_siffer()
    {
        DarwinCoreHelper.CleanDarwinCoreId("UiO", "O", "O-1", "12345678901")
            .Should().Be("12345678901");
    }

    [Theory]
    [InlineData("O-1")]
    [InlineData("o-1")]
    [InlineData("  O-1  ")]
    public void Bruker_ikke_occurrenceId_som_er_den_samme_som_katalognummeret(string occurrenceId)
    {
        DarwinCoreHelper.CleanDarwinCoreId("UiO", "O", "O-1", occurrenceId)
            .Should().Be("UiO/O/o-1");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Faller_tilbake_til_sammensatt_id_uten_occurrenceId(string? occurrenceId)
    {
        DarwinCoreHelper.CleanDarwinCoreId("UiO", "O", "O-1", occurrenceId)
            .Should().Be("UiO/O/o-1");
    }

    /// <summary>
    /// Bare katalognummeret lowercases. Institusjon og samling står som de kom — de er
    /// allerede uppercaset av <c>ImportRecordId</c> i den ekte flyten.
    /// </summary>
    [Fact]
    public void Lowercaser_bare_katalognummeret()
    {
        DarwinCoreHelper.CleanDarwinCoreId("UiO", "Vascular", "AbC-1", null)
            .Should().Be("UiO/Vascular/abc-1");
    }

    [Fact]
    public void Bytter_skraastrek_i_alle_tre_leddene()
    {
        DarwinCoreHelper.CleanDarwinCoreId("A/B", "C/D", "E/F", null)
            .Should().Be("A_B/C_D/e_f");
    }

    /// <summary>Loddrett strek byttes bare i katalognummeret.</summary>
    [Fact]
    public void Bytter_loddrett_strek_bare_i_katalognummeret()
    {
        DarwinCoreHelper.CleanDarwinCoreId("A|B", "C|D", "E|F", null)
            .Should().Be("A|B/C|D/e_f");
    }

    [Fact]
    public void Trimmer_leddene_i_den_sammensatte_iden()
    {
        DarwinCoreHelper.CleanDarwinCoreId("  UiO ", " O ", "  O-1 ", null)
            .Should().Be("UiO/O/o-1");
    }

    [Fact]
    public void Taaler_at_alt_er_tomt()
    {
        DarwinCoreHelper.CleanDarwinCoreId(null, null, null, null).Should().Be("//");
    }

    /// <summary>
    /// Returverdien trimmes ikke, selv om sammenligningen mot katalognummeret gjør det.
    /// Slik er det i Artskart 2, og ProxyId-ene i basen er skrevet med den formen.
    /// </summary>
    [Fact]
    public void Trimmer_ikke_occurrenceId_i_returverdien()
    {
        DarwinCoreHelper.CleanDarwinCoreId("UiO", "O", "O-1", " urn:X ")
            .Should().Be(" urn:x ");
    }
}
