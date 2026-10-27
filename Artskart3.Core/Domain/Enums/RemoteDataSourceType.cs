namespace Artskart3.Core.Domain.Enums;

/// <summary>
/// Protokollen harvest henter en datakilde over. Verdiene er de samme tallene som
/// Artskart 2s <c>RemoteDataSourceTypeEnum</c>, som lagrer dem usatte — rekkefølgen er
/// derfor lastbærende og må ikke endres.
/// </summary>
public enum RemoteDataSourceType
{
    ArtskartDataDeliveryWebServiceV1 = 0,
    GbifIpt = 1,
    Artsobservasjoner20Api = 2,
    GbifJson = 3,
    GbifApi = 4
}
