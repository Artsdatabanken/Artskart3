using Artskart3.Core.Application.Import.Models;

namespace Artskart3.Core.Application.Import.Steps;

/// <summary>
/// Avviser en post hvis identiteten alt er sett i denne kjøringen. Port av Artskart 2s
/// <c>ImportCheckUniqueId</c>.
///
/// HVORFOR DET TRENGS: <c>ProxyId</c> er nøkkelen observasjoner slås sammen på. Kommer
/// samme identitet to ganger i én node, ville den andre posten overskrevet den første
/// ved skriving — og hvilken av dem som vant hadde vært avhengig av rekkefølgen i
/// batchen. Her avvises den andre i stedet, med <c>DuplicateId</c>, slik at det blir
/// synlig i avvisningsrapporten at kilden leverer dubletter.
///
/// STEGET HAR TILSTAND, OG DEN HØRER TIL ÉN NODEKJØRING. Artskart 2 oppretter
/// taskene på nytt i <c>InitializeImportTasks</c> for hver node, så settet gjelder per
/// node. En gjenbrukt instans ville både vokst i minnet og begynt å avvise poster som
/// bare er «duplikater» på tvers av noder. Kjeden må derfor bygges per nodekjøring —
/// aldri registreres som singleton.
///
/// MERK at steget kjører etter <c>ImportRecordIdStep</c> og leser identiteten den satte.
/// </summary>
public class ImportCheckUniqueIdStep : IImportStep
{
    private readonly HashSet<string> _sett = [];

    public ImportStepType StepType => ImportStepType.Pre;

    /// <summary>Antall identiteter sett så langt. Til logging og test.</summary>
    public int SeenCount => _sett.Count;

    public ImportRecordResult ValidateAndImport(
        CacheRecord record,
        ImportedObservation observation,
        ImportContext context)
    {
        ArgumentNullException.ThrowIfNull(observation);

        // Tom nøkkel er ikke en gyldig identitet, men Artskart 2 legger den i settet
        // som alle andre og avviser nummer to. Beholdt: en node som leverer poster uten
        // identitet skal se det i avvisningene, ikke få dem slått sammen i stillhet.
        var identitet = observation.ProxyId ?? string.Empty;

        if (!_sett.Add(identitet))
        {
            return ImportRecordResult.Rejected(
                RecordValidationCode.DuplicateId,
                RecordNotificationCode.Ok,
                identitet);
        }

        return ImportRecordResult.Ok();
    }
}
