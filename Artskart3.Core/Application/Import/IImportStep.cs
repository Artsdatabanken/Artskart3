using Artskart3.Core.Application.Import.Models;

namespace Artskart3.Core.Application.Import;

/// <summary>
/// Ett steg i importkjeden. Port av Artskart 2s <c>IImportServiceTask</c>.
///
/// Et steg leser fra cache-posten, skriver til observasjonen og konteksten, og sier
/// med returverdien om posten kan fortsette. Det kaster ikke: et
/// unntak fra ett steg feller hele nodeimporten i Artskart 2, og det er mønsteret
/// Artskart 3 ikke skal arve. Noe gikk galt uttrykkes som en avvisning.
///
/// STEG MUTERER OGSÅ <c>record</c>. Flere steg skriver til <c>record.Notes</c> før
/// <c>ImportSimpleProperties</c> kopierer feltet, og taksonomisteget setter
/// <c>record.IdentificationPrecision</c>. Det er tilsiktet oppførsel, ikke en
/// bieffekt å rydde bort.
///
/// Artskart 2s <c>ImportFailureIsSevere</c> er ikke portert — den leses ingen steder.
/// </summary>
public interface IImportStep
{
    /// <summary>Når i kjeden steget kjører.</summary>
    ImportStepType StepType { get; }

    ImportRecordResult ValidateAndImport(CacheRecord record, ImportedObservation observation, ImportContext context);
}

/// <summary>
/// Markerer steget som etablerer identiteten til posten (<c>ProxyId</c>).
///
/// Kjeden trenger å kjenne det igjen fordi en slettet post bare skal gjennom dette
/// ene steget: identiteten er alt som trengs for å finne raden som skal fjernes.
/// Artskart 2 slår opp på typen <c>ImportRecordId</c> direkte.
/// </summary>
public interface IImportIdentityStep : IImportStep;
