import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Service, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { translateArtskart2Filter } from '../../guards/artskart2-filter';
import { ObservationSearchFilter } from '../../types/api.types';
import { AreaService } from '../area/area.service';
import { OrganizationService } from '../organization/organization.service';

type AreaFilterKey = 'countyIds' | 'municipalityIds' | 'restrictedAreaIds' | 'oceanAreaIds';
type Artskart2Area = [AreaFilterKey, ...string[]] | ['historical', areaTypeId: number, code: string, name: string];
type OrganizationFilterKey = 'organizationIds' | 'datasetOrgIds' | 'projectOrgIds';

/** Artskart 2 called organisation type 2 a collection and type 3 a dataset; Artskart 3 calls them dataset and project. */
const ORGANIZATION_FILTERS: Record<number, OrganizationFilterKey> = { 1: 'organizationIds', 2: 'datasetOrgIds', 3: 'projectOrgIds' };

/** Artskart 2 production `Area.Id` to Artskart 3 filter, written by scripts/generate-artskart2-areas.mjs. */
const loadArtskart2Areas = () =>
  import('../../../../assets/artskart2-areas.json').then((module) => module.default as unknown as Record<string, Artskart2Area>);

/** Import source to the datasets that hold exactly its observations, written by Scripts/Artskart2NodeDatasets.sql. */
const loadArtskart2Nodes = () =>
  import('../../../../assets/artskart2-nodes.json').then((module) => module.default as unknown as Record<string, number[]>);

export interface Artskart2Translation {
  /** The `?filter=` value. `{}` when nothing carries over, so the link still replaces the filter that was set. */
  filter: string;
  /** Artskart 2 fields that weren't fully applied. */
  dropped: string[];
}

/** Translates Artskart 2 filters, including the parts that need a lookup. */
@Service()
export class Artskart2LinkService {
  private readonly http = inject(HttpClient);
  private readonly areaService = inject(AreaService);
  private readonly organizationService = inject(OrganizationService);

  /** Null when the filter isn't a JSON object at all. */
  async translate(source: unknown): Promise<Artskart2Translation | null> {
    if (typeof source !== 'object' || source === null || Array.isArray(source)) return null;

    const { filter, dropped, areaIds, catalogNumbers, collectionIds, nodeIds } = translateArtskart2Filter(
      source as Record<string, unknown>,
      new Date().getFullYear(),
    );
    const [allAreas, allCatalogNumbers, allCollections] = await Promise.all([
      this.addAreas(filter, areaIds),
      this.addCatalogNumbers(filter, catalogNumbers),
      this.addCollections(filter, collectionIds),
    ]);
    // After the collections, which may already have set the dataset filter.
    const allNodes = await this.addNodes(filter, nodeIds);

    if (!allAreas) dropped.push('AreaIds');
    if (!allCatalogNumbers) dropped.push('CatalogNumbers');
    if (!allCollections) dropped.push('CollectionIds');
    if (!allNodes) dropped.push('NodeIds');

    return { filter: JSON.stringify(filter), dropped: [...new Set(dropped)] };
  }

  /** A bookmark stored by Artskart 2, or null when it doesn't exist. Other errors are passed on. */
  loadBookmark(id: string): Promise<unknown> {
    return firstValueFrom(this.http.get<unknown>(`/api/Lookup/Artskart2Bookmarks/${id}`)).catch((error: unknown) => {
      if (error instanceof HttpErrorResponse && error.status === 404) return null;
      throw error;
    });
  }

  /** Returns false when an area has no Artskart 3 equivalent. */
  private async addAreas(filter: ObservationSearchFilter, areaIds: number[]): Promise<boolean> {
    if (areaIds.length === 0) return true;
    // The table is a separate file, which a tab left open across a deploy may fail to load.
    const areas = await loadArtskart2Areas().catch(() => null);
    if (!areas) return false;

    let allFound = true;
    const historical: Promise<number | null>[] = [];
    for (const id of areaIds) {
      const area = areas[id];
      if (!area) {
        allFound = false;
      } else if (area[0] === 'historical') {
        historical.push(this.findHistoricalArea(area));
      } else {
        const [key, ...codes] = area;
        filter[key] = [...new Set([...(filter[key] ?? []), ...codes])];
      }
    }

    const historicalIds = await Promise.all(historical);
    const found = historicalIds.filter((id) => id !== null);
    if (found.length > 0) filter.historicalAreaIds = [...new Set(found)];
    return allFound && found.length === historicalIds.length;
  }

  /** Artskart 3 filters historical areas on its own ID, so they are found by name and matched on type and code. */
  private findHistoricalArea([, areaTypeId, code, name]: Artskart2Area): Promise<number | null> {
    return firstValueFrom(this.areaService.searchAreas(name))
      .then((areas) => areas.find((area) => !area.isCurrent && area.areaTypeId === areaTypeId && area.fid === code)?.id ?? null)
      .catch(() => null);
  }

  /** Artskart 2 matched catalogue numbers exactly. Returns false when one of them has no match. */
  private async addCatalogNumbers(filter: ObservationSearchFilter, catalogNumbers: string[]): Promise<boolean> {
    const matches = await Promise.all(
      catalogNumbers.map((catalogNumber) =>
        firstValueFrom(this.organizationService.searchCatalogNumbers(catalogNumber))
          .then((found) => found.filter((match) => match.catalogNumber?.toLowerCase() === catalogNumber.toLowerCase()))
          .catch(() => []),
      ),
    );
    const observationIds = [...new Set(matches.flat().flatMap((match) => match.observationIds ?? []))];
    if (observationIds.length > 0) filter.observationIds = observationIds;
    return matches.every((found) => found.length > 0);
  }

  /**
   * Artskart 2 matched any of the organisations; Artskart 3 combines its organisation fields with AND. So only
   * organisations of one type carry over, and only into a field the link hasn't already set.
   */
  private async addCollections(filter: ObservationSearchFilter, collectionIds: number[]): Promise<boolean> {
    if (collectionIds.length === 0) return true;
    const types = await Promise.all(
      collectionIds.map((id) =>
        firstValueFrom(this.organizationService.getOrganization(id))
          .then((organization) => organization.organizationTypeId ?? null)
          .catch(() => null),
      ),
    );
    const found = collectionIds.flatMap((id, i) => {
      const key = types[i] === null ? undefined : ORGANIZATION_FILTERS[types[i]];
      return key ? [{ id, key }] : [];
    });

    const keys = new Set(found.map(({ key }) => key));
    const [key] = keys;
    if (keys.size !== 1 || filter[key]) return false;
    filter[key] = found.map(({ id }) => id);
    return found.length === collectionIds.length;
  }

  /** Import sources that share a dataset with another source aren't in the table, and leave the whole filter out. */
  private async addNodes(filter: ObservationSearchFilter, nodeIds: number[]): Promise<boolean> {
    if (nodeIds.length === 0) return true;
    if (filter.datasetOrgIds) return false;
    const nodes = await loadArtskart2Nodes().catch(() => null);
    if (!nodes) return false;
    const datasets = nodeIds.map((id) => nodes[id]);
    if (datasets.some((ids) => !ids)) return false;
    filter.datasetOrgIds = [...new Set(datasets.flat())];
    return true;
  }
}
