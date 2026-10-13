using Artskart3.Core.Application.Import.Models;

namespace Artskart3.Core.Application.Import;

/// <summary>
/// Det stegene deler seg imellom mens én post behandles. Port av Artskart 2s
/// <c>ImportTasksContext</c>.
///
/// Konteksten lever per post, ikke per batch. Den er samtidig den eneste kanalen
/// mellom steg som ikke går via <see cref="ImportedObservation"/> — og de koblingene
/// er rekkefølgeavhengige: <c>ImportStatusForRegion</c> leser
/// <see cref="TaxonProperties"/> som <c>ImportTaxonomy</c> fylte.
/// </summary>
public class ImportContext
{
    /// <summary>
    /// Prefikset på nøkler i <see cref="DynamicProperties"/> som bærer kildens
    /// opprinnelige verdi etter at importen har tolket om et felt. Verdien havner i
    /// <c>ObservationDetail.DynamicProperties</c> og er det eneste sporet tilbake til
    /// det kilden faktisk sendte.
    /// </summary>
    public const string OriginalValuePrefix = "_original_";

    /// <summary>Satt av <c>ImportTaxonomy</c>, lest av <c>ImportStatusForRegion</c>.</summary>
    public List<TaxonPropertyInfo> TaxonProperties { get; set; } = [];

    /// <summary>
    /// Nøkler og verdier som serialiseres til <c>ObservationDetail.DynamicProperties</c>
    /// til slutt — sortert på nøkkel, slik Artskart 2 gjør det.
    /// </summary>
    public Dictionary<string, string?> DynamicProperties { get; } = [];

    /// <summary>
    /// Verdier som er maskert bort for en sensitiv observasjon. Holdes utenfor
    /// <see cref="DynamicProperties"/> nettopp fordi de ikke skal lagres (fase 8).
    /// </summary>
    public Dictionary<string, string?> MaskedProperties { get; } = [];

    /// <summary>Feltmerknader fra tolkningen, f.eks. et tall som ikke kunne leses.</summary>
    public IList<ImportIssue> RecordIssues { get; } = [];

    /// <summary>
    /// Om hele datakilden er merket sensitiv. Avgjør maskering uavhengig av art
    /// (fase 8).
    /// </summary>
    public bool IsDatasetSensitive { get; set; }

    /// <summary>
    /// Sensitive observasjoner som er publisert åpent, formatert for varsling. Fase 8
    /// sender dem videre; her samles de bare opp.
    /// </summary>
    public List<string> SensitivePublicObservationsFormatted { get; } = [];

    /// <summary>Alle navne-ID-ene taksonet har, satt av taksonomioppslaget.</summary>
    public int[]? AllScientificNameIds { get; set; }

    /// <summary>
    /// Legger til en verdi. Finnes nøkkelen fra før, føyes verdien på med <c>|</c> —
    /// men bare hvis den ikke står der allerede. Nøkler med <c>null</c>-verdi legges
    /// ikke inn i det hele tatt.
    ///
    /// Semantikken er Artskart 2s <c>AppendDynamicProperty</c>, inkludert at en
    /// eksisterende nøkkel med <c>null</c> blir overskrevet av den nye verdien.
    /// </summary>
    public void AppendDynamicProperty(string key, string? value)
    {
        if (DynamicProperties.TryGetValue(key, out var eksisterende))
        {
            if (eksisterende is null)
            {
                DynamicProperties[key] = value;
            }
            else if (!eksisterende.Split('|').Contains(value))
            {
                DynamicProperties[key] = eksisterende + "|" + value;
            }
        }
        else if (value is not null)
        {
            DynamicProperties.Add(key, value);
        }
    }

    /// <summary>
    /// Lagrer kildens opprinnelige verdi for et felt importen har tolket om, under
    /// <see cref="OriginalValuePrefix"/>.
    /// </summary>
    public void SaveOriginalValue(string propertyName, string? originalValue) =>
        AppendDynamicProperty(OriginalValuePrefix + propertyName, originalValue);

    public bool HasOriginalValue(string propertyName) =>
        DynamicProperties.ContainsKey(OriginalValuePrefix + propertyName);

    public void RemoveDynamicProperty(string key) => DynamicProperties.Remove(key);
}
