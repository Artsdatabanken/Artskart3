namespace Artskart3.Core.Application.Import.Writing;

/// <summary>
/// Skriver en ferdig tolket bunt til indexdatabasen.
///
/// ÉN RUNDE ER ÉN TRANSAKSJON. Går noe galt, er hele bunten uskrevet — ikke halve.
/// Det er også grunnen til at bunten er stor: columnstore-indeksen komprimerer bare
/// radgrupper på minst 102 400 rader, og Artskart 2s <c>SaveChanges()</c> hver 100.
/// post ville etterlatt alt i delta-lageret.
/// </summary>
public interface IObservationWriter
{
    /// <summary>
    /// Skriver bunten og tømmer den ikke — det er kallerens valg.
    /// </summary>
    /// <param name="batch">Postene som skal skrives.</param>
    /// <param name="locationIds">
    /// Bufferet som fylles med lokalitets-Id-ene runden skrev, slik neste bunt slipper
    /// å skrive de samme lokalitetene igjen. Kan være <c>null</c>.
    /// </param>
    /// <param name="cancellationToken">Avbrytes mellom setningene, ikke midt i en.</param>
    Task<PublishResult> PublishAsync(
        ImportBatch batch,
        LocationIdCache? locationIds = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Hva en publisering gjorde. Tallene er til logg og til paritetsriggen, ikke til
/// kontrollflyt.
/// </summary>
/// <param name="Observations">Antall observasjoner i bunten.</param>
/// <param name="Locations">Antall lokaliteter som ble skrevet.</param>
/// <param name="StagedRows">Antall rader bulk-kopiert, summert over alle tabellene.</param>
/// <param name="Elapsed">Tiden hele runden tok, staging inkludert.</param>
public sealed record PublishResult(int Observations, int Locations, int StagedRows, TimeSpan Elapsed);
