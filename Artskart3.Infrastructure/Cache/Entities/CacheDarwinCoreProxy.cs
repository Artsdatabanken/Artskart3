namespace Artskart3.Infrastructure.Cache.Entities;

/// <summary>
/// Raden i <c>Artskart2Cache.DarwinCoreProxies</c>. Bare kolonnene som finnes
/// fysisk — de ~115 Darwin Core-feltene ligger i <see cref="SerializedObject"/>.
///
/// Tabellen eies av Artskart 2s harvest. Endres modellen der, må denne følge med.
/// </summary>
public class CacheDarwinCoreProxy
{
    public int Id { get; set; }

    /// <summary>Forretningsnøkkel generert ved harvest.</summary>
    public string ProxyId { get; set; } = string.Empty;

    public int NodeId { get; set; }

    public string? OccurenceId { get; set; }

    public int InstitutionCodeId { get; set; }

    public int CollectionCodeId { get; set; }

    public string? CatalogNumber { get; set; }

    /// <summary>Cachen sletter aldri fysisk.</summary>
    public bool IsDeleted { get; set; }

    public DateTime DateLastModified { get; set; }

    /// <summary>Satt av harvest. Nøkkelen inkrementell import leser på.</summary>
    public DateTime TrackInfoDateImported { get; set; }

    /// <summary>GZIP-komprimert JSON, kodet som UTF-16. Se <c>CacheBlobCompression</c>.</summary>
    public byte[] SerializedObject { get; set; } = [];

    /// <summary>
    /// Hash av det deserialiserte objektet, beregnet av harvest. Importen bruker den
    /// til endringsdeteksjon, og den leses herfra — ikke fra blobben.
    /// </summary>
    public int Hash { get; set; }
}
