using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <summary>
/// Legger DateTimeCollected som nøkkelkolonne nummer to på filterindeksene, slik
/// at listevisningen kan sortere nyeste først uten å sortere treffmengden.
///
/// BAKGRUNN: listevisningen sorterte tidligere på Observation.Id. Den sorteringen
/// er byttet til DateTimeCollected DESC, Id DESC (SearchRepository). For hvert
/// filter som brukes alene må det da finnes en indeks på
/// (filterkolonne, DateTimeCollected) — ellers må planen hente HELE treffmengden
/// og sortere den for å plukke 40 rader.
///
/// Uten støtteindeks er utslaget dramatisk. Målt på 61 052 216 rader, ett filter
/// alene, nyeste først:
///   Havforskningsinstituttet (480 402 obs)   23 294 ms -> 1 ms
///   Norges miljø- og biovitensk. (149 720)    9 465 ms -> 3 ms
///   Universitetsmuseet i Bergen (602 804)     2 687 ms -> 4 ms
/// Museumssamlinger er historiske, så et bakoverskann fra i dag passerer år med
/// nyere data før det finner deres rader. Med datoen i nøkkelen seeker planen rett
/// inn i filterverdien og går bakover derfra.
///
/// Institusjon og datasett får det samme, men i sine egne migrasjoner
/// (20260904114820 og 20260904122248) fordi de indeksene ble opprettet der.
///
/// OMRÅDEFILTRENE ER IKKE MED HER, OG DET ER PRØVD UT.
/// Det ble bygget en indeks IX_OEI_Entity_Date på
/// ObservationEntityIndex (EntityTypeId, EntityId, DateTimeCollected, ObservationId)
/// for at planen skulle kunne seeke et område og gå bakover i den denormaliserte
/// datoen der. Selve formen virker — målt isolert ga den 2 ms mot 169 ms for
/// EXISTS-formen — men optimalisereren valgte den aldri: EF skriver områdefilteret
/// som EXISTS med Observation som drivende tabell, og både EXISTS og
/// IN (SELECT ...) normaliseres til samme semi-join.
///
/// Indeksen sto med 2 seeks over 181 benchmark-case, og de to ble absorbert av
/// IX_ObservationEntityIndex_EntityLookup da den ble droppet. Å droppe den
/// endret ingenting: Geografi-gruppen lå på 32 ms både med og uten, og
/// «Kommune lettest (Sørreisa)» på 39 ms begge veier. Den kostet 1 358,9 MB.
///
/// Skal områdefilteret senere skrives om til å drive fra indekstabellen — et
/// eksplisitt totrinns oppslag, ikke en LINQ-omskriving — må indeksen bygges på
/// nytt. Det tar ~5,5 minutter. Merk at et slikt oppslag må håndtere duplikater:
/// en observasjon som ligger både i en valgt kommune og i fylket over, treffer to
/// rader i indekstabellen.
///
/// MERK OGSÅ: MonthCollected-indeksen hjelper bare delvis. Månedsfilteret bruker
/// DATEPART på DateTimeCollected og treffer derfor ikke kolonnen; selv om det
/// endres til å bruke MonthCollected, koster IN (6,7,8) ~320 ms fordi tre atskilte
/// områder må flettes til én datorekkefølge. Én enkelt måned er 2-7 ms.
/// </summary>
public partial class AddListViewDateSortIndexes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Alle bygges med DROP_EXISTING: indeksene finnes fra før som
        // énkolonneindekser, og DROP_EXISTING bygger dem om i én operasjon i
        // stedet for drop + create.
        //
        // suppressTransaction av samme grunn som de andre indeksmigrasjonene:
        // hver av disse går over 61M rader og tar omtrent ett minutt. En så lang
        // operasjon inne i migrasjonstransaksjonen hindrer avkorting av
        // transaksjonsloggen mens den pågår.
        //
        // PAGE-komprimering beholdes — det er slik indeksene var fra før.
        migrationBuilder.Sql(@"
CREATE NONCLUSTERED INDEX IX_Observation_CategoryId
ON dbo.Observation (CategoryId, DateTimeCollected)
WITH (DROP_EXISTING = ON, DATA_COMPRESSION = PAGE, MAXDOP = 4);
", suppressTransaction: true);

        migrationBuilder.Sql(@"
CREATE NONCLUSTERED INDEX IX_Observation_TaxonGroupId
ON dbo.Observation (TaxonGroupId, DateTimeCollected)
WITH (DROP_EXISTING = ON, DATA_COMPRESSION = PAGE, MAXDOP = 4);
", suppressTransaction: true);

        migrationBuilder.Sql(@"
CREATE NONCLUSTERED INDEX IX_Observation_BasisOfRecordId
ON dbo.Observation (BasisOfRecordId, DateTimeCollected)
WITH (DROP_EXISTING = ON, DATA_COMPRESSION = PAGE, MAXDOP = 4);
", suppressTransaction: true);

        migrationBuilder.Sql(@"
CREATE NONCLUSTERED INDEX IX_Observation_MonthCollected
ON dbo.Observation (MonthCollected, DateTimeCollected)
WITH (DROP_EXISTING = ON, DATA_COMPRESSION = PAGE, MAXDOP = 4);
", suppressTransaction: true);

        migrationBuilder.Sql(@"
CREATE NONCLUSTERED INDEX IX_Observation_TaxonId
ON dbo.Observation (TaxonId, DateTimeCollected)
WITH (DROP_EXISTING = ON, DATA_COMPRESSION = PAGE, MAXDOP = 4);
", suppressTransaction: true);

    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Tilbake til énkolonneindeksene slik de var før — med PAGE-komprimering,
        // som er slik de faktisk lå i basen.
        migrationBuilder.Sql(@"
CREATE NONCLUSTERED INDEX IX_Observation_CategoryId
ON dbo.Observation (CategoryId)
WITH (DROP_EXISTING = ON, DATA_COMPRESSION = PAGE, MAXDOP = 4);
", suppressTransaction: true);

        migrationBuilder.Sql(@"
CREATE NONCLUSTERED INDEX IX_Observation_TaxonGroupId
ON dbo.Observation (TaxonGroupId)
WITH (DROP_EXISTING = ON, DATA_COMPRESSION = PAGE, MAXDOP = 4);
", suppressTransaction: true);

        migrationBuilder.Sql(@"
CREATE NONCLUSTERED INDEX IX_Observation_BasisOfRecordId
ON dbo.Observation (BasisOfRecordId)
WITH (DROP_EXISTING = ON, DATA_COMPRESSION = PAGE, MAXDOP = 4);
", suppressTransaction: true);

        migrationBuilder.Sql(@"
CREATE NONCLUSTERED INDEX IX_Observation_MonthCollected
ON dbo.Observation (MonthCollected)
WITH (DROP_EXISTING = ON, DATA_COMPRESSION = PAGE, MAXDOP = 4);
", suppressTransaction: true);

        migrationBuilder.Sql(@"
CREATE NONCLUSTERED INDEX IX_Observation_TaxonId
ON dbo.Observation (TaxonId)
WITH (DROP_EXISTING = ON, DATA_COMPRESSION = PAGE, MAXDOP = 4);
", suppressTransaction: true);

    }
}
