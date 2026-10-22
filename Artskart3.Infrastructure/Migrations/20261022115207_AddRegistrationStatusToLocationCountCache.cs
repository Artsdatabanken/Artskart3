using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <summary>
/// Gir lokasjonsbufferen ytterpunktene for registreringsstatus.
///
/// HVA SOM ENDRES
/// LocationCountCacheLevel1 får MinRegistrationStatusId og MaxRegistrationStatusId.
/// Lokasjonssøket returnerer en status per lokalitet — funnet, ikke funnet eller
/// ikke gjenfunnet — og uten disse to kolonnene kunne bufferen ikke svare på
/// endepunktet i det hele tatt: en buffer som ikke kjenner statusen ville svart
/// «funnet» for alt, raskt og galt.
///
/// HVORFOR MIN OG MAX OG IKKE TELLERE
/// Statusen er «funnet hvis noe er funnet, ellers ikke funnet hvis alt er det,
/// ellers ikke gjenfunnet». Den leses rett ut av ytterpunktene, og ytterpunkter
/// lar seg slå sammen på tvers av bøtter og maskerader — MIN av MIN, MAX av MAX —
/// mens tellere måtte dedupliseres først.
///
/// BUFFEREN MÅ BYGGES PÅ NYTT
/// Kolonnene står tomme i rader bygget før denne migrasjonen, og 0 leses som en
/// gyldig verdi. Derfor er RowLayoutVersion i AreaCountCacheDimensions bumpet, slik
/// at oppslaget nekter å bruke en buffer bygget før endringen. Kjør
/// BuildLocationCountCache.sql og BuildAreaCountCache.sql — versjonen er delt, så
/// begge må bygges.
/// </summary>
public partial class AddRegistrationStatusToLocationCountCache : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<byte>(
            name: "MaxRegistrationStatusId",
            table: "LocationCountCacheLevel1",
            type: "tinyint",
            nullable: false,
            defaultValue: (byte)0);

        migrationBuilder.AddColumn<byte>(
            name: "MinRegistrationStatusId",
            table: "LocationCountCacheLevel1",
            type: "tinyint",
            nullable: false,
            defaultValue: (byte)0);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "MaxRegistrationStatusId",
            table: "LocationCountCacheLevel1");

        migrationBuilder.DropColumn(
            name: "MinRegistrationStatusId",
            table: "LocationCountCacheLevel1");
    }
}
