using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using FluentAssertions;
using Byggemigrasjonen = Artskart3.Infrastructure.Migrations.BuildFacetCountCache;

namespace Artskart3.Tests.Unit;

/// <summary>
/// Vakt for byggemigrasjonen til tellebufferne.
///
/// HVA DEN BESKYTTER MOT
/// Byggemigrasjonen kjører Scripts/BuildAreaCountCache.sql,
/// BuildLocationCountCache.sql og BuildFacetCountCache.sql som innebygde ressurser —
/// altså skriptene slik de ser ut NÅ, ikke slik de så ut da migrasjonen ble skrevet.
/// Det går bare så lenge alle kolonnene og tabellene skriptene bruker finnes når
/// migrasjonen kjører, og det er sant bare hvis ingen senere migrasjon endrer
/// skjemaet på annen måte enn med indekser.
///
/// <c>Byggemigrasjonen</c> er et alias, så testen følger med når byggingen flyttes.
/// Den har flyttet fire ganger: 20261007134334_BuildCountCaches →
/// 20261111112446_RebuildCountCaches → 20261116143428_RebuildCountCachesWithFacets
/// (da fasett-tabellene kom bak den) → 20261117151539_RebuildCountCachesAfterTaxonRanks
/// (da de 19 taksonrangkolonnene kom bak den) → 20261125185806_BuildFacetCountCache.
///
/// DEN SISTE FLYTTINGEN HAR EN ANNEN ÅRSAK ENN DE TRE FØRSTE
/// Den kom av en sammenslåing med develop, som hadde flyttet SIN byggemigrasjon bak
/// 20261118095554_UpdateMdirWmsAdresses. To byggemigrasjoner havnet i samme kjede.
///
/// MERK at det riktige svaret da IKKE var å legge fasett-skriptet i develops
/// 20261118131407_RebuildCountCaches, selv om den er den nyeste byggemigrasjonen. Den
/// er alt anvendt i miljøene, og en migrasjonskropp som har en historikkrad kjører
/// aldri igjen: skriptet ville bare virket på ferske databaser. Når den nyeste
/// byggemigrasjonen tilhører en annen gren og er deployert, skal byggingen i en NY
/// migrasjon — ikke inn i hennes.
///
/// Derfor bygger develops migrasjon fortsatt område- og lokasjonsbufferen, mens
/// denne aliasets migrasjon bygger fasett-bufferen. To aktive byggemigrasjoner er
/// greit: vakten krever bare at ingenting skjemaendrende kommer etter den SISTE.
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
        var migrasjoner = typeof(Byggemigrasjonen).Assembly
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

        migrasjoner.Should().Contain(typeof(Byggemigrasjonen), "migrasjonene skal ligge i Infrastructure-assemblyet");

        var senere = migrasjoner.SkipWhile(t => t != typeof(Byggemigrasjonen)).Skip(1);

        senere.SelectMany(AndreEndringerEnnIndekser).Should().BeEmpty(
            "byggemigrasjonen leser byggeskriptene som de ser ut nå, så alle kolonnene de " +
            "bruker må finnes når den kjører. Flytt byggemigrasjonen bakerst med et nytt " +
            "tidsstempel, tøm den gamle, og pek aliaset Byggemigrasjonen på den nye.");
    }

    [Fact]
    public void Vakten_slipper_gjennom_indekser_men_ikke_kolonner_eller_raa_sql()
    {
        AndreEndringerEnnIndekser(typeof(LagerIndeks)).Should().BeEmpty();
        AndreEndringerEnnIndekser(typeof(LeggerTilKolonne)).Should().ContainSingle();
        AndreEndringerEnnIndekser(typeof(KjorerRaaSql)).Should().ContainSingle();
    }

    /// <summary>
    /// De gamle byggemigrasjonene skal bli stående tomme. Fylles én av dem igjen, er
    /// feilen over tilbake — de ligger midt i rekken, før skjemaet skriptene trenger.
    ///
    /// 20261007134334 lå før kolonnene skriptene bruker, 20261116143428 før
    /// fasett-tabellene, og 20261117151539 før develops migrasjoner fra 18. november.
    ///
    /// MERK at develops 20261118131407_RebuildCountCaches med vilje IKKE står i listen.
    /// Den er ikke tom — den bygger fortsatt område- og lokasjonsbufferen, og er alt
    /// anvendt i miljøene.
    /// </summary>
    [Theory]
    [InlineData("BuildCountCaches")]
    [InlineData("RebuildCountCachesWithFacets")]
    [InlineData("RebuildCountCachesAfterTaxonRanks")]
    public void De_gamle_byggemigrasjonene_bygger_ingenting(string navn)
    {
        var gammel = typeof(Byggemigrasjonen).Assembly
            .GetTypes()
            .Single(t => t.Name == navn);

        var migrasjon = (Migration)Activator.CreateInstance(gammel)!;

        migrasjon.UpOperations.Should().BeEmpty(
            $"{navn} ligger før skjemaet byggeskriptene trenger. Skal bufferne bygges av " +
            "en migrasjon, hører det i den nyeste byggemigrasjonen.");
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
