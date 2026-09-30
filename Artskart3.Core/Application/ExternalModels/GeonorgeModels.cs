using System.Text.Json.Serialization;

namespace Artskart3.Core.Application.ExternalModels;

/// <summary>
/// Modeller for responsen fra Geonorge Stedsnavn API sitt /sted-endepunkt.
/// Se https://ws.geonorge.no/stedsnavn/v1/ (schema "ReturSted"/"Sok") for full kontrakt.
/// </summary>
public class GeonorgeStedResponse
{
    [JsonPropertyName("metadata")]
    public GeonorgeMetadata? Metadata { get; set; }

    [JsonPropertyName("navn")]
    public List<GeonorgeSted> Navn { get; set; } = [];
}

public class GeonorgeMetadata
{
    [JsonPropertyName("side")]
    public int Side { get; set; }

    [JsonPropertyName("sokeStreng")]
    public string? SokeStreng { get; set; }

    [JsonPropertyName("totaltAntallTreff")]
    public int TotaltAntallTreff { get; set; }

    [JsonPropertyName("treffPerSide")]
    public int TreffPerSide { get; set; }

    [JsonPropertyName("viserFra")]
    public int? ViserFra { get; set; }

    [JsonPropertyName("viserTil")]
    public int? ViserTil { get; set; }
}

public class GeonorgeSted
{
    [JsonPropertyName("stedsnummer")]
    public int Stedsnummer { get; set; }

    [JsonPropertyName("stedstatus")]
    public string? Stedstatus { get; set; }

    [JsonPropertyName("navneobjekttype")]
    public string? Navneobjekttype { get; set; }

    [JsonPropertyName("representasjonspunkt")]
    public GeonorgeRepresentasjonspunkt? Representasjonspunkt { get; set; }

    [JsonPropertyName("stedsnavn")]
    public List<GeonorgeSkrivemate> Stedsnavn { get; set; } = [];

    [JsonPropertyName("kommuner")]
    public List<GeonorgeKommune> Kommuner { get; set; } = [];

    [JsonPropertyName("fylker")]
    public List<GeonorgeFylke> Fylker { get; set; } = [];
}

public class GeonorgeRepresentasjonspunkt
{
    [JsonPropertyName("øst")]
    public double Ost { get; set; }

    [JsonPropertyName("nord")]
    public double Nord { get; set; }

    [JsonPropertyName("koordsys")]
    public int Koordsys { get; set; }
}

public class GeonorgeSkrivemate
{
    [JsonPropertyName("skrivemåte")]
    public string? Skrivemate { get; set; }

    [JsonPropertyName("skrivemåtestatus")]
    public string? SkrivemateStatus { get; set; }

    [JsonPropertyName("navnestatus")]
    public string? Navnestatus { get; set; }

    [JsonPropertyName("språk")]
    public string? Sprak { get; set; }
}

public class GeonorgeKommune
{
    [JsonPropertyName("kommunenavn")]
    public string? Kommunenavn { get; set; }

    [JsonPropertyName("kommunenummer")]
    public string? Kommunenummer { get; set; }
}

public class GeonorgeFylke
{
    [JsonPropertyName("fylkesnavn")]
    public string? Fylkesnavn { get; set; }

    [JsonPropertyName("fylkesnummer")]
    public string? Fylkesnummer { get; set; }
}
