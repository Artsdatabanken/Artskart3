namespace Artskart3.Core.Application.Maintenance;

/// <summary>
/// Fyller <c>ObservationCount</c>-kolonnene på oppslagstabellene. Etterarbeid etter
/// importen, ikke del av den.
/// </summary>
public interface IObservationCountService
{
    /// <summary>
    /// Oppdaterer alle tellingene.
    ///
    /// EN TABELL SOM FEILER STOPPER IKKE DE ANDRE, slik Artskart 2s orkestrerende
    /// prosedyre gjør det: hver av de åtte ligger i sin egen <c>TRY/CATCH</c>.
    /// Alternativet — å avbryte — ville latt én feil fryse alle tallene.
    /// </summary>
    Task<IReadOnlyList<CountResult>> UpdateAllAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Utfallet for én tabell.
/// </summary>
/// <param name="Table">Tabellen.</param>
/// <param name="Elapsed">Tiden den tok.</param>
/// <param name="Error">Feilmeldingen, eller <c>null</c> om den gikk gjennom.</param>
public sealed record CountResult(string Table, TimeSpan Elapsed, string? Error)
{
    public bool Succeeded => Error is null;
}
