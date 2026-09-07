namespace Artskart3.Core.Application.DTOs;

/// <summary>
/// Ett treff i katalognummer-typeaheaden.
///
/// ObservationIds følger med i treffet med vilje. Katalognummer er tilnærmet
/// unikt — 54 687 713 distinkte verdier over 61 052 216 observasjoner — og
/// flest observasjoner på samme verdi er 675. Listen er derfor alltid kort, og
/// frontend kan sende den rett inn som filter uten et ekstra oppslag.
///
/// Det er dette som gjør at filterspørringen slipper strengsammenligning:
/// oppslaget skjer her, mot IX_Observation_CatalogNumber, og selve filteret blir
/// et seek på klyngeindeksen. Delstrengsøk mot 61M rader var det som gjorde
/// katalognummer-filteret 18-21 sekunder.
/// </summary>
public class CatalogNumberMatchDto
{
    public string CatalogNumber { get; set; } = null!;

    public int[] ObservationIds { get; set; } = [];

    public int ObservationCount => ObservationIds.Length;
}
