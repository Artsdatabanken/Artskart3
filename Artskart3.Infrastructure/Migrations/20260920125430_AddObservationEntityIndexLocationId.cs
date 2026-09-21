using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <summary>
/// LocationId på ObservationEntityIndex, denormalisert fra Observation.
///
/// HVORFOR
/// Lokasjonssøket grupperer på LocationId. Kjørt mot Observation gikk
/// aggregeringen i row mode — målt på Oslo-utsnittet 11 166 ms og 84 sekunder
/// CPU for å gruppere 57M rader. Indekstabellen har en columnstore og alle
/// filterkolonnene fra før, men manglet grupperingsnøkkelen, så spørringen måtte
/// joine tilbake til Observation og falt dermed ut av batch mode.
///
/// Målt på samme datamengde og gruppeantall:
///   dagens sti, via Observation                 11 166 ms
///   indekstabellen + join for LocationId          5 116 ms
///   ren columnstore-aggregering, 400 000 grupper  1 415 ms
///
/// Joinen kostet altså 3,7 av de 5,1 sekundene. Denne kolonnen fjerner den.
///
/// Kolonnen er nullable: en observasjon uten lokalitet har ingen LocationId, og
/// NULL er riktig sluttilstand for dem. Nullable uten default er dessuten en ren
/// metadataoperasjon — se AddCompleteFilterColumns.
///
/// Kolonnen fylles av BackfillObservationEntityIndexLocationId, og legges inn i
/// IX_OEI_Columnstore av samme migrasjon. Før den er kjørt er kolonnen NULL, og
/// lokasjonssøket returnerer tomt — de to hører sammen og må deployes sammen.
/// </summary>
public partial class AddObservationEntityIndexLocationId : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.ObservationEntityIndex', 'LocationId') IS NULL
    ALTER TABLE dbo.ObservationEntityIndex ADD LocationId INT NULL;
", suppressTransaction: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.ObservationEntityIndex', 'LocationId') IS NOT NULL
    ALTER TABLE dbo.ObservationEntityIndex DROP COLUMN LocationId;
", suppressTransaction: true);
    }
}
