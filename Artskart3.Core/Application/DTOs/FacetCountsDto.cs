namespace Artskart3.Core.Application.DTOs;

/// <summary>
/// Ett alternativ i filterpanelet med antallet observasjoner det ville gitt.
///
/// <see cref="Id"/> er bøtte-id-en for dimensjonsfasetter — kategori-id, taksongruppe-id
/// og så videre — altså den samme id-en klienten sender i filteret. Bool-filtrene har
/// ingen bøtte-id og bruker <see cref="FacetBoolCountsDto"/> i stedet.
/// </summary>
public class FacetBucketDto
{
    public int Id { get; set; }

    public int Count { get; set; }
}

/// <summary>
/// Antallene for ett bool-filter: hvor mange observasjoner som ville vært igjen med
/// alternativet på, og hvor mange med det av.
///
/// Begge regnes med brukerens eget valg på samme bit overstyrt — tallet svarer på «hva
/// får du hvis du velger denne i stedet».
/// </summary>
public class FacetBoolCountsDto
{
    public required string Key { get; set; }

    public int WhenTrue { get; set; }

    public int WhenFalse { get; set; }
}

/// <summary>
/// Fasett-tallene for én dimensjon.
///
/// <see cref="Buckets"/> inneholder bare bøtter med treff. Et alternativ som ikke står
/// der har null observasjoner — klienten skal vise 0, ikke tomt. Bøtter uten verdi
/// («uten kategori») er aldri med: et filter peker alltid ut konkrete verdier.
/// </summary>
public class FacetCountsDto
{
    public required string Key { get; set; }

    public required List<FacetBucketDto> Buckets { get; set; }

    /// <summary>
    /// Om tallene kom fra bufferen eller ble aggregert. Bare til feilsøking og måling;
    /// klienten viser det ikke.
    /// </summary>
    public required string Source { get; set; }
}

/// <summary>
/// Antallet for ett område i filterpanelets områdelister.
///
/// Nøkkelen er <see cref="Fid"/> og ikke en int, fordi områder identifiseres med Fid
/// gjennom hele løsningen — fylke «50», kommune «4601», verneområde
/// «Naturbase VV123».
/// </summary>
public class FacetAreaCountDto
{
    public required string Fid { get; set; }

    public int Count { get; set; }
}

/// <summary>
/// Svaret fra fasett-endepunktet: ett innslag per dimensjon klienten spurte om, ett
/// per bool-filter, og eventuelt områdelistene.
/// </summary>
public class FacetCountsResponseDto
{
    public required List<FacetCountsDto> Dimensions { get; set; }

    public required List<FacetBoolCountsDto> BoolFilters { get; set; }

    /// <summary>
    /// Fylker, kommuner, havområder og Svalbard. Tom når klienten ikke ba om dem.
    /// Områder uten treff er ikke med — klienten viser 0 for det som mangler.
    /// </summary>
    public required List<FacetAreaCountDto> Areas { get; set; }
}
