namespace Artskart3.Core.Application.Import;

/// <summary>
/// Når i kjeden et steg kjører. Port av Artskart 2s <c>ImportServiceTaskType</c>.
///
/// REKKEFØLGEN MELLOM FASENE ER DEL AV TOLKNINGEN, ikke en implementasjonsdetalj.
/// <c>ImportStatusForRegion</c> leser taksonegenskapene <c>ImportTaxonomy</c> satte og
/// områdene <c>ImportGeometry</c> satte; <c>ImportSimpleProperties</c> kopierer
/// <c>record.Notes</c> etter at tre tidligere steg har skrevet til feltet. Flyttes et
/// steg mellom fasene, endres resultatet uten at noe feiler.
/// </summary>
public enum ImportStepType
{
    /// <summary>Kjører sekvensielt, først. Et avvisende resultat stopper posten.</summary>
    Pre,

    /// <summary>
    /// Fasen mellom <see cref="Pre"/> og <see cref="Post"/>. Alle steg i den kjører
    /// ferdig før posten eventuelt avvises.
    ///
    /// Navnet er Artskart 2s: der startes disse med <c>Task.Run</c> og ventes på med
    /// <c>WaitAll</c>. Artskart 3 kjører dem etter hverandre — se
    /// <c>ImportRecordProcessor.RunParallel</c> for hvorfor. To tasks har denne typen i
    /// Artskart 2: <c>ImportGeometry</c> og <c>ImportMedia</c>.
    /// </summary>
    Parallel,

    /// <summary>Kjører sekvensielt, sist.</summary>
    Post
}
