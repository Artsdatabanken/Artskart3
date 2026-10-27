using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <summary>
/// Gjør havområdene til en påkrevd områdetype, slik fylker, kommuner og
/// Svalbard-områdene er.
///
/// HVORFOR: importen avviser en post som ikke treffer noen <c>IsRequired</c>-områdetype
/// (<c>GeographyService.GetAreasForCoordinate</c> returnerer tom liste, og posten felles
/// med <c>CoordinateOutOfBounds</c>). Områdeinndelingen er bygget om — fylker for
/// fastlandet, type 6 for Svalbard, Bjørnøya og Jan Mayen, og type 4 for havområdene,
/// alle tre på zoomnivå 1 — men bare type 4 fikk ikke flagget. En observasjon i havet
/// traff derfor ingen påkrevd type og ble avvist.
///
/// Det tømmer havområdefilteret av konstruksjon: <c>OceanAreaIds</c> finnes i
/// <c>ObservationQueryBuilder</c> og <c>SearchRepository</c>, API-et eksponerer
/// <c>OceanAreas</c>, og frontend har egen filtergruppe for det — men importen avviste
/// postene som skulle fylt det.
///
/// MÅLT på gullsett medium (node 39, 43, 59, 101, 135, 194): flagget henter inn 100
/// poster som Artskart 2 importerte og Artskart 3 avviste. Node 39 gikk fra 74 952 til
/// 74 954 og treffer da Artskart 2 eksakt. 24 poster ligger fortsatt i havet uten
/// kommune og avvises av en annen grunn i tillegg.
///
/// MERK at dette ikke rører <c>LocationAreas</c>-koblingene. Importen skrev alt til
/// type 4 for kystnære lokaliteter som også traff en kommune — oppslaget returnerer alle
/// treff når først ett påkrevd er med. Det er bare avvisningen som endrer seg.
/// </summary>
public partial class RequireOceanAreaForCoordinates : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
                UPDATE [dbo].[AreaType]
                SET IsRequired = 1,
                    UpdatedAt = SYSUTCDATETIME()
                WHERE Id = 4;
            ");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
                UPDATE [dbo].[AreaType]
                SET IsRequired = 0,
                    UpdatedAt = SYSUTCDATETIME()
                WHERE Id = 4;
            ");
    }
}
