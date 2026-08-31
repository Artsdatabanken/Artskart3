namespace Artskart3.Core.Domain.Entities;

/// <summary>
/// Kobling mellom observasjon og datasett (Organization med OrganizationTypeId = 3).
///
/// Egen tabell, ikke en kolonne på Observation: datasett er det eneste av de tre
/// organisasjonsforholdene som ikke er 1:1. Målt august 2026 har 13 739 242
/// observasjoner minst ett datasett, og 745 066 av dem har flere enn ett (maks 5).
/// En enkelt kolonne ville stille droppet tilknytningen for de ~750 000.
///
/// Erstatter datasett-skiven av OrganizationRelation (136M rader). Denne tabellen
/// er 14,5M rader med to int-kolonner — filteret seeker på DatasetOrgId, får i snitt
/// ~1 455 ObservationId-er, og de blir seek mot den clustered indeksen på
/// ObservationEntityIndex.
/// </summary>
public class ObservationDataset
{
    public int ObservationId { get; set; }

    public int DatasetOrgId { get; set; }
}
