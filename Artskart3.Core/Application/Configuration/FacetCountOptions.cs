namespace Artskart3.Core.Application.Configuration;

public class FacetCountOptions
{
    public const string SectionName = "FacetCount";

    /// <summary>
    /// Hvor mange fasett-aggregeringer som får kjøre samtidig, hver på sin egen
    /// databasetilkobling.
    ///
    /// Gjelder bare fall-through-stien. Bufferoppslagene er 4-7 ms og kjøres
    /// sekvensielt på forespørselens egen context.
    ///
    /// HVORFOR 3, MÅLT OG IKKE GJETTET
    /// Endepunktet målt mot produksjonslik data, median av fem kall, alle seks
    /// gruppene åpne i filterpanelet:
    ///
    ///   tak   fugl + kategori 9 (4 aggregert)   fugl + fylke + kat (6 aggregert)
    ///    1              10 039 ms                        1 097 ms
    ///    3               6 421 ms                          598 ms
    ///    6               8 020 ms                          599 ms
    ///
    /// Seks samtidige er altså TREGERE enn tre på det tunge tilfellet. Arbeidet
    /// forsvinner ikke av å overlappes: fire COUNT(DISTINCT)-skanninger over titalls
    /// millioner rader konkurrerer om CPU og memory grants, og forbi et punkt taper
    /// man mer på kampen enn man vinner på overlappet. På det lette tilfellet, der
    /// hver spørring er kort, er 3 og 6 like.
    ///
    /// Taket er hele poenget med å gjøre dette serverside framfor å sende ett
    /// HTTP-kall per gruppe fra klienten: seks kall fra nettleseren ville gitt
    /// nøyaktig den samtidigheten som måler seg dårligst, uten noe sted å begrense
    /// den.
    ///
    /// Søke-endepunktene deler dessuten én GLOBAL concurrency limiter på 32 plasser,
    /// så et kall som holder mange tilkoblinger spiser mer av den enn tallene er verdt.
    ///
    /// 1 gir sekvensiell oppførsel igjen, uten deploy.
    /// </summary>
    public int MaxConcurrency { get; set; } = 3;
}
