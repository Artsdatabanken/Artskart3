namespace Artskart3.Core.Application.Import.Models;

/// <summary>
/// Resultatet av å lese én cache-rad: enten en post, eller en rad som ikke kunne
/// pakkes ut.
///
/// HVORFOR IKKE BARE KASTE: i Artskart 2 feller ett unntak på én post hele
/// nodeimporten — <c>RecordsProcessor</c> fanger ikke per post. Det er mønsteret
/// Artskart 3 ikke skal arve, og det begynner her: en ødelagt blob skal bli én
/// avvist post, ikke en avbrutt node. Fra fase 9 er det <c>RejectedRecord</c>-raden
/// <see cref="Error"/> ender opp i.
/// </summary>
public class CacheReadItem
{
    /// <summary>Primærnøkkelen til cache-raden. Alltid satt.</summary>
    public int CacheRowId { get; init; }

    /// <summary>Forretningsnøkkelen fra kolonnen <c>ProxyId</c>. Alltid satt.</summary>
    public string ProxyId { get; init; } = string.Empty;

    /// <summary>Posten, eller <c>null</c> hvis raden ikke kunne leses.</summary>
    public CacheRecord? Record { get; init; }

    /// <summary>Hvorfor raden ikke kunne leses. <c>null</c> når <see cref="Record"/> er satt.</summary>
    public string? Error { get; init; }

    public bool IsMalformed => Record is null;

    public static CacheReadItem Ok(int cacheRowId, string proxyId, CacheRecord record) =>
        new() { CacheRowId = cacheRowId, ProxyId = proxyId, Record = record };

    public static CacheReadItem Malformed(int cacheRowId, string proxyId, string error) =>
        new() { CacheRowId = cacheRowId, ProxyId = proxyId, Error = error };
}
