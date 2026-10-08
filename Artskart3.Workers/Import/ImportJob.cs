namespace Artskart3.Workers.Import;

/// <summary>
/// Hangfire continuation/recurring job — entry point for import.
/// Leser fra cache-databasen, prosesserer og skriver til index-databasen.
/// TODO: Implementeres i neste fase.
///
/// VIKTIG — ObservationEntityIndex har en columnstore-indeks (IX_OEI_Columnstore).
/// Det legger noen føringer på hvordan daglige batcher må skrives:
///
/// - Bruk bulk-insert med minst 102 400 rader per setning. Da går radene rett inn i
///   en komprimert rowgroup. Mindre batcher havner i delta store (en vanlig b-tre)
///   og venter til ~1M rader har samlet seg før de komprimeres.
/// - Foretrekk append fremfor UPDATE. En UPDATE mot columnstore markerer den gamle
///   raden i delete-bitmappen og setter inn en ny versjon i delta store — den gamle
///   raden opptar fortsatt plass til neste rebuild. Skal eksisterende observasjoner
///   revideres, er bulk delete + insert billigere enn UPDATE-in-place.
/// - Kjør DatabaseMaintenanceJob (REORGANIZE) etter importen, se den klassen.
///
/// MERK: ObservationTaxonHierarchy har i dag ingen oppdateringssti — den populeres
/// kun én gang i migrasjon 20260702113806. Den må også vedlikeholdes herfra,
/// ellers blir taksonfiltrering stående på et frosset øyeblikksbilde.
///
/// TELLEBUFFERNE MÅ UGYLDIGGJØRES FØR FØRSTE RAD SKRIVES, IKKE BARE BYGGES ETTERPÅ
/// Område- og lokasjonsbufferen er førstegangsbygget av migrasjonen BuildCountCaches.
/// Herfra er det importen som eier dem, og rekkefølgen er det som avgjør om de er
/// trege eller FEIL:
///
///   1. Sett Status bort fra 'Ready' i AreaCountCacheState og LocationCountCacheState
///   2. Importer
///   3. Bygg bufferne på nytt (se Scripts/BuildAreaCountCache.sql, deretter
///      BuildLocationCountCache.sql — rekkefølgen er et krav)
///
/// Uten steg 1 svarer bufferne fra gamle tall i hele import- og byggevinduet.
/// Kontrakten er at bufferen skal gi NØYAKTIG det tellingen gir (se
/// ILocationCountCacheService): avviker den, er den feil, og feilen vises som gale
/// tall på kartet uten en eneste feilmelding.
///
/// OG NEI, SourceRows-VAKTEN ERSTATTER IKKE STEG 1
/// Oppslaget sammenligner SourceRows med radantallet i ObservationEntityIndex og
/// nekter å bruke en buffer der de spriker (se AreaCountCacheService.IsReadyAsync).
/// Den fanger en import som ENDRER RADANTALLET, og gjør da bufferen inert —
/// tregere, aldri galt. Den fanger ikke:
///
///   - en UPDATE som bare bytter verdier: RegistrationStatusId, HasMediaFiles,
///     MonthCollected, CoordinatePrecisionInMeters, DatasetOrgId — nøyaktig de
///     denormaliserte kolonnene importen vedlikeholder
///   - en sletting og innsetting som går opp i null
///   - en koordinatretting: LocationCountCacheLevel1 nøkles på East/North fra
///     Location, så innholdet endres uten at en indeksrad kommer eller går
///
/// Vakten er altså et sikkerhetsnett for det ene tilfellet, ikke en erstatning for
/// rekkefølgen over. Steg 1 står.
///
/// En ikke-klar buffer er derimot ufarlig: oppslaget faller tilbake til aggregering
/// og svarer riktig, bare tregere.
///
/// Skal bufferne bygges uten nedetid, trengs blue/green — byggeskriptene tømmer og
/// bygger på nytt, så bufferen er borte i minuttene det tar.
/// </summary>
public class ImportJob
{
    private readonly ILogger<ImportJob> _logger;

    public ImportJob(ILogger<ImportJob> logger)
    {
        _logger = logger;
    }

    public Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("ImportJob er ikke implementert ennå");
        return Task.CompletedTask;
    }
}
