using Artskart3.Core.Application.Services;
using Artskart3.Infrastructure.Persistence.Services;
using FluentAssertions;

namespace Artskart3.Tests.Unit;

/// <summary>
/// Lokasjonsbufferen låner hele dimensjonsregisteret fra områdebufferen og legger
/// bare til én egen dimensjon: «ingen filter». Disse testene vokter skjøten.
/// </summary>
public class LocationCountCacheDimensionsTests
{
    /// <summary>
    /// Dimensjon 0 er lokasjonsbufferens egen. Fikk en dimensjon i registeret id
    /// 0 en dag, ville de to delt bøtterom: et ufiltrert oppslag hadde lest den
    /// andre dimensjonens rader og svart med et vilkårlig utvalg lokasjoner.
    /// Ingen feilmelding, bare feil kart.
    /// </summary>
    [Fact]
    public void Ufiltrert_dimensjon_kolliderer_ikke_med_registeret()
    {
        AreaCountCacheDimensions.All.Select(d => d.Id)
            .Should().NotContain(LocationCountCacheService.NoFilterDimensionId);
    }

    /// <summary>
    /// Bufferen bruker områdebufferens SchemaVersion som gyldighetsstempel, siden
    /// den er bygget på nøyaktig de samme bøttene. Er den 0, virker
    /// versjonssjekken uten å sjekke noe.
    /// </summary>
    [Fact]
    public void Skjemaversjonen_er_ikke_null()
    {
        AreaCountCacheDimensions.SchemaVersion.Should().NotBe(0);
    }
}
