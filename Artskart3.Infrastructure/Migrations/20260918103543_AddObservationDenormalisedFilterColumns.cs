using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <summary>
/// Tre denormaliserte filterkolonner på Observation, speilet fra
/// ObservationEntityIndex der de allerede fantes.
///
/// HVORFOR
/// Områdetellingene filtrerte på kolonner mens listevisningen utledet det samme
/// fra tagger, mediefiler og atferdstabellen — per rad, hver gang. Målt på
/// RegistrationStatusId = 2 (193 510 observasjoner, 0,32 % av tabellen):
/// 1548 ms med semi-join mot taggene, 89 ms med kolonnen. Kostnaden lå i de
/// 193 510 oppslagene i klyngeindeksen, ikke i predikatet.
///
/// ALLE TRE I ÉN ALTER, MED VILJE
/// Tabellen skrives om én gang i stedet for tre. Målt på 61M rader: 475 sekunder
/// for to kolonner, 428 for én.
///
/// HVORFOR NOT NULL MED DEFAULT, I MOTSETNING TIL AddCompleteFilterColumns
/// Den migrasjonen la kolonnene til som nullable fordi det er en ren
/// metadataoperasjon, og satte NOT NULL etter backfillen. Her er avveiningen
/// motsatt: defaultverdiene dekker nesten alt — 99,67 % av radene har
/// RegistrationStatusId 1, og 93,5 % har ingen mediefiler. Med nullable måtte
/// backfillen oppdatert alle 61M rader; med default gjenstår ~201 000 for status
/// og ~4M for mediefiler.
///
/// Prisen er at ALTER-en ikke blir metadata-only her. Det burde den vært fra og
/// med SQL Server 2012, men ble det ikke — antakelig fordi Observation har
/// beregnede kolonner (MonthCollected, YearCollected). Åtte minutter på 61M
/// rader er likevel billigere enn 61M oppdateringer.
///
/// BehaviorId er nullable fordi NULL er riktig sluttilstand: ~73 % av
/// observasjonene har ingen atferd.
///
/// suppressTransaction ER AVGJØRENDE
/// ALTER-en tar minutter. Ligger den i migrasjonstransaksjonen, ruller en timeout
/// tilbake hele arbeidet — det skjedde under utviklingen, og migrasjonen
/// konvergerer aldri uansett hvor mange forsøk den får.
///
/// Kolonnene er TOMME etter denne migrasjonen, bortsett fra defaultverdiene.
/// BackfillObservationDenormalisedFilterColumns fyller resten.
/// </summary>
public partial class AddObservationDenormalisedFilterColumns : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Idempotent: feiler kjøringen på timeout, skal neste forsøk kunne gå
        // videre i stedet for å stoppe på «column already exists».
        migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Observation', 'RegistrationStatusId') IS NULL
    ALTER TABLE dbo.Observation ADD
        RegistrationStatusId TINYINT NOT NULL
            CONSTRAINT DF_Observation_RegistrationStatusId DEFAULT (1),
        HasMediaFiles        BIT     NOT NULL
            CONSTRAINT DF_Observation_HasMediaFiles DEFAULT (0),
        BehaviorId           TINYINT NULL;
", suppressTransaction: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Observation', 'RegistrationStatusId') IS NOT NULL
    ALTER TABLE dbo.Observation
        DROP CONSTRAINT DF_Observation_RegistrationStatusId,
                        DF_Observation_HasMediaFiles;

IF COL_LENGTH('dbo.Observation', 'RegistrationStatusId') IS NOT NULL
    ALTER TABLE dbo.Observation
        DROP COLUMN RegistrationStatusId, HasMediaFiles, BehaviorId;
", suppressTransaction: true);
    }
}
