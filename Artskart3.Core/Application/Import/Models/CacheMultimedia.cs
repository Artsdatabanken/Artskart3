namespace Artskart3.Core.Application.Import.Models;

/// <summary>
/// Simple Multimedia-utvidelsen på en cache-post. Port av
/// <c>DarwinCoreProxy.Multimedia</c>.
///
/// Harvest kan ha lagret objekter av subklassen <c>TrackableMultimedia</c>, som har
/// <c>Id</c> og <c>DateLastModified</c> i tillegg. De feltene står i JSON-en, men
/// Artskart 2 deserialiserer også til basisklassen og ignorerer dem. Vi gjør det
/// samme — ikke av latskap, men fordi importen ellers ville sett felt Artskart 2
/// aldri har sett.
/// </summary>
public class CacheMultimedia
{
    public string? Type { get; set; }

    public string? Format { get; set; }

    /// <summary>Direkte URL til mediefilen, ikke til siden den vises på.</summary>
    public string? Identifier { get; set; }

    public string? References { get; set; }

    public string? Title { get; set; }

    public string? Description { get; set; }

    public DateTime Created { get; set; }

    public string? Creator { get; set; }

    public string? Contributor { get; set; }

    public string? Publisher { get; set; }

    public string? Audience { get; set; }

    public string? Source { get; set; }

    public string? License { get; set; }

    public string? RightsHolder { get; set; }

    public string? DatasetID { get; set; }
}
