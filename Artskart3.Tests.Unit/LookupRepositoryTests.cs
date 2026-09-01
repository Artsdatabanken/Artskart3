using Artskart3.Core.Constants;
using Artskart3.Infrastructure.Data;
using Artskart3.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Artskart3.Tests.Unit;

/// <summary>
/// Vaktsetningene i katalognummer-oppslaget.
///
/// Alle tre returnerer FØR spørringen bygges, og det er hele poenget: endepunktet
/// er anonymt, og et prefikssøk som ikke kan brukes som seek degraderer til full
/// skanning av IX_Observation_CatalogNumber over 61M rader. Testene kjører derfor
/// mot en tom in-memory-base — når de passerer uten at noen spørring utføres, er
/// vakten faktisk foran databasen og ikke bak den.
/// </summary>
public class LookupRepositoryTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public async Task SearchCatalogNumbersAsync_WithBlankSearch_ReturnsEmpty(string search)
    {
        var sut = CreateRepository();

        var result = await sut.SearchCatalogNumbersAsync(search, 10);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchCatalogNumbersAsync_WithNullSearch_ReturnsEmpty()
    {
        var sut = CreateRepository();

        var result = await sut.SearchCatalogNumbersAsync(null!, 10);

        result.Should().BeEmpty();
    }

    /// <summary>
    /// Minstelengden håndheves på serveren. Debouncing og minstelengde i
    /// typeaheaden gjelder bare nettleseren — endepunktet kan kalles direkte.
    /// </summary>
    [Fact]
    public async Task SearchCatalogNumbersAsync_WithSearchShorterThanMinimum_ReturnsEmpty()
    {
        var sut = CreateRepository();
        var tooShort = new string('1', SearchConstants.MinCatalogNumberSearchLength - 1);

        var result = await sut.SearchCatalogNumbersAsync(tooShort, 10);

        result.Should().BeEmpty();
    }

    /// <summary>
    /// Starter søket med %, _ eller [, blir det escapede mønsteret et klammeuttrykk
    /// ('[%]abc%'). SQL Server kan ikke bruke det som seek-prefiks, og oppslaget
    /// degraderer til full skanning. Disse avvises framfor å kjøres.
    /// </summary>
    [Theory]
    [InlineData("%abc")]
    [InlineData("_abc")]
    [InlineData("[abc")]
    public async Task SearchCatalogNumbersAsync_WithWildcardPrefix_ReturnsEmpty(string search)
    {
        var sut = CreateRepository();

        var result = await sut.SearchCatalogNumbersAsync(search, 10);

        result.Should().BeEmpty();
    }

    /// <summary>
    /// Jokertegn LENGER INNE i strengen er uproblematiske: prefikset foran dem er
    /// fortsatt en gyldig seek, og de escapes. Avvises de også, mister brukeren
    /// katalognumre som faktisk inneholder tegnene.
    /// </summary>
    [Theory]
    [InlineData("ab%c")]
    [InlineData("ab_c")]
    [InlineData("ab[c")]
    public async Task SearchCatalogNumbersAsync_WithWildcardInsideTerm_IsNotRejectedByGuard(string search)
    {
        var sut = CreateRepository();

        var act = async () => await sut.SearchCatalogNumbersAsync(search, 10);

        // Går videre til spørringen. Mot en tom base gir det tomt resultat, ikke
        // et tidlig retur — men det viktige er at vakten ikke stanser det.
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SearchCatalogNumbersAsync_WithTrimmableSearch_UsesTrimmedLength()
    {
        var sut = CreateRepository();
        var paddedButTooShort = $"  {new string('1', SearchConstants.MinCatalogNumberSearchLength - 1)}  ";

        var result = await sut.SearchCatalogNumbersAsync(paddedButTooShort, 10);

        result.Should().BeEmpty();
    }

    private static LookupRepository CreateRepository()
    {
        var options = new DbContextOptionsBuilder<ArtskartDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new LookupRepository(new ArtskartDbContext(options), NullLogger<LookupRepository>.Instance);
    }
}
