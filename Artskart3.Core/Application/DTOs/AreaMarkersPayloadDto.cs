namespace Artskart3.Core.Application.DTOs;

/// <summary>
/// Ferdig serialisert svar for det ufiltrerte områdesøket.
///
/// HVORFOR FERDIG SERIALISERT
/// Frontend henter AreaMarkers nøyaktig to ganger per sidelast — ufiltrert, ett kall
/// per zoomnivå (<c>prefetchAreaGeometries</c>). Geometrien legges så i en klientside
/// cache som aldri tømmes, og alle senere filterendringer går til AreaCounts. Svaret
/// er altså identisk hver gang, men ble serialisert på nytt ved hvert kall: målt til
/// ~297 ms for zoomnivå 2.
///
/// STØRRELSE (målt)
///   zoomnivå 1:  3,9 MB rå  ->  1,38 MB gzip
///   zoomnivå 2: 20,7 MB rå  ->  7,26 MB gzip
///
/// Begge varianter holdes i minnet — til sammen ~33 MB for begge zoomnivåer. Å holde
/// den gzippede unngår at komprimeringsmellomvaren pakker de samme bytene på nytt ved
/// hvert kall.
/// </summary>
/// <param name="Raw">UTF-8 JSON.</param>
/// <param name="Gzip">Samme innhold, gzippet. Serveres med Content-Encoding: gzip.</param>
public sealed record AreaMarkersPayloadDto(byte[] Raw, byte[] Gzip);
