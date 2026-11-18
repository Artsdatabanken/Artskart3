using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using FluentAssertions;
using RebuildCountCaches = Artskart3.Infrastructure.Migrations.RebuildCountCaches;

namespace Artskart3.Tests.Unit;

/// <summary>
/// Vakt for byggemigrasjonen til tellebufferne.
///
/// HVA DEN BESKYTTER MOT
/// RebuildCountCaches kjører Scripts/BuildAreaCountCache.sql og
/// BuildLocationCountCache.sql som innebygde ressurser — altså skriptene slik de ser
/// ut NÅ, ikke slik de så ut da migrasjonen ble skrevet. Det går bare så lenge alle
/// kolonnene skriptene bruker finnes når migrasjonen kjører, og det er sant bare hvis
/// ingen senere migrasjon endrer skjemaet på annen måte enn med indekser.
///
/// Det gikk galt én gang alt: byggingen lå opprinnelig i 20261007134334, skriptene
/// vokste til å referere Mask, UncertainIdentification, Introduced og
/// Min/MaxRegistrationStatusId fra senere migrasjoner, og en tom database stoppet
/// midt i migreringen med «Invalid column name 'Mask'» — hvorpå ingenting etter den
/// ble anvendt. Eksisterende miljøer hadde historikkraden og merket det aldri, så
/// feilen traff bare nye miljøer og gjenoppbygde testdatabaser.
///
/// INDEKSER ER UNNTATT
/// Skriptene leser kolonner, ikke indekser, så en indeksmigrasjon kan ligge bak
/// byggemigrasjonen. Å flytte den koster et nytt bygg i hvert miljø der den alt har
/// kjørt, og det er bortkastet når ingenting den trenger er endret.
///
/// NÅR DENNE FEILER
/// Noen har lagt til en senere migrasjon som gjør mer enn å endre indekser. Rå SQL
/// teller med, siden testen ikke kan se hva den gjør. Flytt byggemigrasjonen bakerst
/// igjen — gi den et nytt tidsstempel — ikke endre denne testen. Alternativet er at
/// neste tomme database stopper på samme måte.
/// </summary>
public class CountCacheBuildMigrationTests
{
    [Fact]
    public void Etter_byggemigrasjonen_kommer_bare_indeksendringer()
    {
        var migrasjoner = typeof(RebuildCountCaches).Assembly
            .GetTypes()
            .Where(t => t.IsSubclassOf(typeof(Migration)) && !t.IsAbstract)
            .Select(t => new
            {
                Type = t,
                Id = t.GetCustomAttributes(typeof(MigrationAttribute), inherit: false)
                      .Cast<MigrationAttribute>()
                      .Select(a => a.Id)
                      .SingleOrDefault(),
            })
            .Where(m => m.Id is not null)
            .OrderBy(m => m.Id, StringComparer.Ordinal)
            .Select(m => m.Type)
            .ToList();

        migrasjoner.Should().Contain(typeof(RebuildCountCaches), "migrasjonene skal ligge i Infrastructure-assemblyet");

        var senere = migrasjoner.SkipWhile(t => t != typeof(RebuildCountCaches)).Skip(1);

        senere.SelectMany(AndreEndringerEnnIndekser).Should().BeEmpty(
            "byggemigrasjonen leser byggeskriptene som de ser ut nå, så alle kolonnene de " +
            "bruker må finnes når den kjører. Flytt RebuildCountCaches bakerst med et nytt tidsstempel.");
    }

    [Fact]
    public void Vakten_slipper_gjennom_indekser_men_ikke_kolonner_eller_raa_sql()
    {
        AndreEndringerEnnIndekser(typeof(LagerIndeks)).Should().BeEmpty();
        AndreEndringerEnnIndekser(typeof(LeggerTilKolonne)).Should().ContainSingle();
        AndreEndringerEnnIndekser(typeof(KjorerRaaSql)).Should().ContainSingle();
    }

    /// <summary>
    /// Den gamle byggemigrasjonen skal bli stående tom. Fylles den igjen, er feilen
    /// over tilbake — den ligger midt i rekken, før kolonnene skriptene bruker.
    /// </summary>
    [Fact]
    public void Den_gamle_byggemigrasjonen_bygger_ingenting()
    {
        var gammel = typeof(RebuildCountCaches).Assembly
            .GetTypes()
            .Single(t => t.Name == "BuildCountCaches");

        var migrasjon = (Migration)Activator.CreateInstance(gammel)!;
        var operasjoner = migrasjon.UpOperations;

        operasjoner.Should().BeEmpty(
            "20261007134334_BuildCountCaches ligger før kolonnene byggeskriptene bruker. " +
            "Skal bufferne bygges av en migrasjon, hører det i RebuildCountCaches.");
    }

    private static IEnumerable<string> AndreEndringerEnnIndekser(Type migrasjon) =>
        ((Migration)Activator.CreateInstance(migrasjon)!).UpOperations
            .Where(o => o is not (CreateIndexOperation or DropIndexOperation))
            .Select(o => $"{migrasjon.Name}: {o.GetType().Name}");

    private sealed class LagerIndeks : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder) =>
            migrationBuilder.CreateIndex("IX_Test", "Observation", "Id");
    }

    private sealed class LeggerTilKolonne : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder) =>
            migrationBuilder.AddColumn<int>("NyKolonne", "Observation", nullable: false, defaultValue: 0);
    }

    private sealed class KjorerRaaSql : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql("UPDATE dbo.Observation SET HasMediaFiles = 0");
    }
}
