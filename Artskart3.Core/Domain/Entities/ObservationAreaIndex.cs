namespace Artskart3.Core.Domain.Entities;

/// <summary>
/// Flat projeksjon av observasjon-til-område-relasjonen.
/// AreaTypeId: 1=Kommune, 2=Fylke, 3=Verneområde, 4=Havområde, 5=Institusjon.
/// </summary>
public class ObservationAreaIndex
{
    public int ObservationId { get; set; }
    public int AreaTypeId { get; set; }
    public string AreaFid { get; set; } = null!;
}
