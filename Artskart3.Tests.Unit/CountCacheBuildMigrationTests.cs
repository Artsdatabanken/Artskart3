using Microsoft.EntityFrameworkCore.Migrations;
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
/// den er den SISTE migrasjonen.
///
/// Det gikk galt én gang alt: byggingen lå opprinnelig i 20261007134334, skriptene
/// vokste til å referere Mask, UncertainIdentification, Introduced og
/// Min/MaxRegistrationStatusId fra senere migrasjoner, og en tom database stoppet
/// midt i migreringen med «Invalid column name 'Mask'» — hvorpå ingenting etter den
/// ble anvendt. Eksisterende miljøer hadde historikkraden og merket det aldri, så
/// feilen traff bare nye miljøer og gjenoppbygde testdatabaser.
///
/// NÅR DENNE FEILER
/// Noen har lagt til en migrasjon med et senere tidsstempel. Flytt
/// byggemigrasjonen bakerst igjen — gi den et nytt tidsstempel — ikke endre denne
/// testen. Alternativet er at neste tomme database stopper på samme måte.
/// </summary>
public class CountCacheBuildMigrationTests
{
    [Fact]
    public void Byggemigrasjonen_er_den_siste()
    {
        var migrasjoner = typeof(RebuildCountCaches).Assembly
            .GetTypes()
            .Where(t => t.IsSubclassOf(typeof(Migration)) && !t.IsAbstract)
            .Select(t => new
            {
                Navn = t.Name,
                Id = t.GetCustomAttributes(typeof(MigrationAttribute), inherit: false)
                      .Cast<MigrationAttribute>()
                      .Select(a => a.Id)
                      .SingleOrDefault(),
            })
            .Where(m => m.Id is not null)
            .OrderBy(m => m.Id, StringComparer.Ordinal)
            .ToList();

        migrasjoner.Should().NotBeEmpty("migrasjonene skal ligge i Infrastructure-assemblyet");

        var siste = migrasjoner[^1];

        siste.Navn.Should().Be(
            nameof(RebuildCountCaches),
            "byggemigrasjonen leser byggeskriptene som de ser ut nå, så alle kolonnene de " +
            $"bruker må finnes når den kjører. Nå er «{siste.Navn}» sist. Flytt " +
            "RebuildCountCaches bakerst med et nytt tidsstempel.");
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
            "Skal bufferne bygges av en migrasjon, hører det i RebuildCountCaches, som er sist.");
    }
}
