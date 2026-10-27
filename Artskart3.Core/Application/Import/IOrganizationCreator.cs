namespace Artskart3.Core.Application.Import;

/// <summary>
/// Oppretter organisasjonene importen møter men ikke finner. Port av Artskart 2s
/// <c>ImportRecordId.AddNewCollection</c> og <c>AddNewDataset</c>, som opprettet dem i
/// RavenDB og speilet dem til SQL gjennom <c>OrganizationMaintainer</c>. Her er
/// <c>Organization</c> primærkilden, så raden opprettes direkte.
///
/// DEN KAN SLÅS AV, og det er et krav, ikke en bekvemmelighet. Under
/// parallellverifiseringen (plan 4.8) kjører Artskart 2 og Artskart 3 samtidig mot hver
/// sin skrivesti. Opprettet begge nye datasett fra ukjente koder, divergerte registrene
/// fra første natt. Artskart 3 kjører derfor i «opprett-ikke-modus»: steget får ingen
/// <see cref="IOrganizationCreator"/>, møter en ukjent kode, avviser posten med
/// <see cref="RecordValidationCode.OrganizationNotCreated"/> og logger avviket.
/// Sammenligningen skal vise null slike avvik — gjør den ikke det, har speilingen et
/// hull, og det er nettopp det vi er ute etter å finne.
///
/// METODENE ER «FINN ELLER OPPRETT», ikke «opprett». Artskart 2 laster dokumentet
/// før den lager det, og implementasjonen her slår opp i databasen av samme grunn:
/// registerets øyeblikksbilde lastes én gang per prosess, så en rad opprettet under en
/// tidligere nodekjøring står ikke i det.
///
/// RADEN MÅ FINNES FØR OBSERVASJONEN SKRIVES, for den er et fremmednøkkelmål. Derfor
/// returnerer metodene en ferdig <see cref="OrganizationRef"/> med en ekte
/// <c>Organization.Id</c> — ikke et løfte om en.
/// </summary>
public interface IOrganizationCreator
{
    /// <summary>
    /// Oppretter et datasett (type 2) under utgiveren.
    ///
    /// KODEN LAGRES I SMÅ BOKSTAVER. Artskart 2 kaller <c>AddCollection</c> med
    /// <c>collectionCode.ToLowerInvariant()</c> mens oppslaget skjer på den versalerte
    /// kilden, så en samling som opprettes av importen får små bokstaver, og en som er
    /// opprettet i admin har det den ble gitt. Oppslaget tåler begge; det er bare
    /// verdien som havner på observasjonen som arver forskjellen.
    /// </summary>
    OrganizationRef CreateDataset(OrganizationRef publisher, string code);

    /// <summary>
    /// Oppretter et prosjekt (type 3) under datasettet.
    /// </summary>
    /// <param name="dataset">Datasettet prosjektet hører under.</param>
    /// <param name="sourceDatasetId">
    /// DwC <c>datasetID</c>, eller <c>null</c> når kilden bare oppgav et navn. Lagres i
    /// <c>Organization.SourceDatasetId</c>.
    /// </param>
    /// <param name="name">DwC <c>datasetName</c>. Lagres som <c>Organization.Name</c>.</param>
    OrganizationRef CreateProject(OrganizationRef dataset, string? sourceDatasetId, string name);
}
