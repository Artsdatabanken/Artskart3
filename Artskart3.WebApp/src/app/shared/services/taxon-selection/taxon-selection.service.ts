import { Injectable, computed, effect, inject, signal } from '@angular/core';
import { lastValueFrom } from 'rxjs';
import { TaxonTreeService } from '../taxon-tree/taxon-tree.service';
import { FilterStateService } from '../filter-state/filter-state.service';
import { TaxonTreeNodeDto } from '../../types/api.types';

export type TaxonCheckboxState = 'checked' | 'indeterminate' | 'unchecked';

/**
 * Avgjør checked/indeterminate-tilstand for avkrysningsboksene i taxon-treet.
 *
 * Seleksjonssemantikk:
 * - En node er checked om den selv eller en forgjenger er valgt.
 * - En node er indeterminate om minst én etterkommer er valgt, uten at noden selv er checked.
 * - Avhaking av en arvet checked node materialiserer utvalget: den valgte forgjengeren
 *   erstattes med søsknene langs stien ned til noden.
 * - Når alle (kjente) barn av en forelder er valgt, kollapses utvalget tilbake til forelderens id.
 *
 * Treet mater inn forfedrekjeder og søskenlister for alt som rendres; det som ikke er
 * rendret (f.eks. taxa valgt via artsøk) hentes fra GET /api/Lookup/TaxonAncestry.
 */
@Injectable({
  providedIn: 'root',
})
export class TaxonSelectionService {
  private readonly taxonTreeService = inject(TaxonTreeService);
  private readonly filterState = inject(FilterStateService);

  /** taxonId -> forfedre-id-er, fra rot til nærmeste forelder. */
  private readonly ancestryCache = signal<Map<number, number[]>>(new Map());

  /** parentTaxonId -> lastede barn-id-er. */
  private readonly childrenCache = signal<Map<number, number[]>>(new Map());

  private pendingAncestryIds = new Set<number>();
  private ancestryFetchPromise: Promise<void> | null = null;

  private readonly selectedIds = computed(() => new Set(this.filterState.selectedTaxonIds()));

  constructor() {
    // Hent forfedrekjeder for valgte id-er vi ikke kjenner ennå (f.eks. valg via artsøk),
    // uavhengig av om treet er lastet eller ikke.
    effect(() => {
      this.filterState.selectedTaxonIds();
      this.resolveAncestries();
    });
  }

  /**
   * Mengden av alle id-er som har en valgt etterkommer.
   * Brukt for O(1)-oppslag av indeterminate-tilstand.
   */
  private readonly ancestorsOfSelected = computed(() => {
    const cache = this.ancestryCache();
    const result = new Set<number>();
    for (const id of this.filterState.selectedTaxonIds()) {
      for (const ancestorId of cache.get(id) ?? []) {
        result.add(ancestorId);
      }
    }
    return result;
  });

  /** Registrerer forfedrekjeden til en rendret node (fra treets egen sti). */
  registerAncestry(taxonId: number, ancestorIds: number[]): void {
    this.registerAncestries([[taxonId, ancestorIds]]);
  }

  /** Bulk-variant: én cache-kloning uavhengig av antall oppføringer. */
  private registerAncestries(entries: Iterable<readonly [number, number[]]>): void {
    const cache = this.ancestryCache();
    const additions = new Map<number, number[]>();
    for (const [taxonId, ancestorIds] of entries) {
      if (!cache.has(taxonId) && !additions.has(taxonId)) additions.set(taxonId, ancestorIds);
    }
    if (additions.size === 0) return;
    this.ancestryCache.update((current) => new Map([...current, ...additions]));
  }

  /** Registrerer barna til en forelder slik materialisering/kollaps kan bruke dem. */
  registerChildren(parentTaxonId: number | undefined, children: TaxonTreeNodeDto[]): void {
    this.registerChildIds(parentTaxonId ?? 0, children.map((n) => n.id));
  }

  /** Registrerer et lastet tre-nivå: barna til forelder og forfedrekjeden til hvert barn. */
  registerTreeLevel(parentTaxonId: number | undefined, ancestorIds: number[], children: TaxonTreeNodeDto[]): void {
    this.registerChildren(parentTaxonId, children);
    this.registerAncestries(children.map((node) => [node.id, ancestorIds] as const));
  }

  /** Som registerChildren, men med kjente id-er (f.eks. fra TaxonAncestry-nivåer). */
  private registerChildIds(parentId: number, childIds: number[]): void {
    if (this.childrenCache().has(parentId)) return;
    this.childrenCache.update((cache) => {
      const next = new Map(cache);
      next.set(parentId, childIds);
      return next;
    });
  }

  isSelected(id: number): boolean {
    return this.selectedIds().has(id);
  }

  isInheritedSelected(ancestorIds: number[]): boolean {
    return ancestorIds.some((id) => this.selectedIds().has(id));
  }

  isIndeterminate(id: number): boolean {
    return this.ancestorsOfSelected().has(id);
  }

  checkboxState(nodeId: number, ancestorIds: number[]): TaxonCheckboxState {
    if (this.isSelected(nodeId) || this.isInheritedSelected(ancestorIds)) return 'checked';
    if (this.isIndeterminate(nodeId)) {
      return this.isFullyCovered(nodeId) ? 'checked' : 'indeterminate';
    }
    return 'unchecked';
  }

  /** Sann om alle (laste­de) barn av noden er fullt dekket av utvalget, rekursivt. */
  private isFullyCovered(id: number): boolean {
    return this.isCoveredBy(id, this.selectedIds());
  }

  /** Sjekker inn en node: legger den til, fjerner overflødige etterkommere, kollapser oppover. */
  select(taxonId: number, ancestorIds: number[]): void {
    const knownDescendants = this.knownDescendantsOf(taxonId);
    const ids = this.filterState
      .selectedTaxonIds()
      .filter((id) => !knownDescendants.has(id) && id !== taxonId);
    ids.push(taxonId);
    this.filterState.setTaxons(this.normaliseUpwards(ids, ancestorIds));
  }

  /**
   * Sjekker ut en node. Hvis noden var arvet checked materialiseres utvalget:
   * nærmeste valgte forgjenger byttes ut med søsknene langs stien ned til noden.
   */
  async deselect(taxonId: number, ancestorIds: number[]): Promise<void> {
    const selected = this.selectedIds();

    if (selected.has(taxonId)) {
      this.filterState.setTaxons(
        this.normaliseUpwards(this.filterState.selectedTaxonIds().filter((id) => id !== taxonId), ancestorIds),
      );
      return;
    }

    // Arvet checked: finn nærmeste valgte forgjenger (lengst ned i kjeden)
    let ancestorIndex = -1;
    for (let i = ancestorIds.length - 1; i >= 0; i--) {
      if (selected.has(ancestorIds[i])) {
        ancestorIndex = i;
        break;
      }
    }
    if (ancestorIndex < 0) {
      // Noden er verken valgt eller arvet — men kan være checked via full barne­dekning.
      // Avhaking fjerner da alle kjente valgte etterkommere.
      if (this.isFullyCovered(taxonId)) {
        const descendants = this.knownDescendantsOf(taxonId);
        this.filterState.setTaxons(
          this.filterState.selectedTaxonIds().filter((id) => !descendants.has(id)),
        );
      }
      return;
    }

    const selectedAncestor = ancestorIds[ancestorIndex];
    const path = [...ancestorIds.slice(ancestorIndex + 1), taxonId];

    const added: number[] = [];
    const chainsToRegister: [number, number[]][] = [];
    let current = selectedAncestor;
    for (const step of path) {
      const siblings = await this.getChildrenIds(current);
      // Søsknenes kjede er kjent — registrer den så de ikke trenger TaxonAncestry-oppslag
      const childChain = ancestorIds.slice(0, ancestorIds.indexOf(current) + 1);
      for (const siblingId of siblings) {
        chainsToRegister.push([siblingId, childChain]);
        if (siblingId !== step && !added.includes(siblingId)) added.push(siblingId);
      }
      current = step;
    }
    this.registerAncestries(chainsToRegister);

    // Les utvalget på nytt etter awaits slik at samtidige select() ikke overskrives
    const ids = this.filterState.selectedTaxonIds().filter((id) => id !== selectedAncestor);
    for (const id of added) {
      if (!ids.includes(id)) ids.push(id);
    }

    this.filterState.setTaxons(this.normaliseUpwards(ids, ancestorIds));
  }

  /**
   * Kollapser fullt dekkede forgjengere til forelderens id, fra nærmeste forelder og oppover.
   * En forgjenger er dekket om alle dens (laste­de) barn er dekket, rekursivt — den øverste
   * fullt dekkede noden ender dermed opp som filterets taxonId.
   */
  private normaliseUpwards(ids: number[], ancestorIds: number[]): number[] {
    let result = [...ids];
    for (let i = ancestorIds.length - 1; i >= 0; i--) {
      const parentId = ancestorIds[i];
      if (!this.isCoveredBy(parentId, new Set(result))) continue;
      const descendants = this.knownDescendantsOf(parentId);
      result = result.filter((id) => !descendants.has(id) && id !== parentId);
      result.push(parentId);
    }
    return result;
  }

  /**
   * Sann om noden er valgt eller alle dens (laste­de) barn er dekket, rekursivt.
   * Noder med ulastede barn kan ikke verifiseres og regnes ikke som dekket.
   */
  private isCoveredBy(id: number, ids: ReadonlySet<number>, visited = new Set<number>()): boolean {
    if (ids.has(id)) return true;
    if (!visited.add(id)) return false; // syklusvakt
    const children = this.childrenCache().get(id);
    if (!children || children.length === 0) return false;
    return children.every((childId) => this.isCoveredBy(childId, ids, visited));
  }

  /** Alle kjente etterkommere av taxonId, basert på hva som er registrert fra treet. */
  private knownDescendantsOf(taxonId: number): Set<number> {
    const result = new Set<number>();
    const cache = this.ancestryCache();
    for (const [id, ancestors] of cache) {
      if (ancestors.includes(taxonId)) result.add(id);
    }
    return result;
  }

  private async getChildrenIds(parentTaxonId: number): Promise<number[]> {
    const cached = this.childrenCache().get(parentTaxonId);
    if (cached) return cached;
    const nodes = await lastValueFrom(this.taxonTreeService.getChildren(parentTaxonId));
    this.registerChildren(parentTaxonId, nodes);
    return nodes.map((n) => n.id);
  }

  /** Maks antall id-er per TaxonAncestry-request — tilsvarer grensen i API-et. */
  private static readonly ANCESTRY_BATCH_SIZE = 100;

  /**
   * Henter forfedrekjeder for valgte id-er vi ikke kjenner ennå (f.eks. fra artsøk).
   * Batch i requester på inntil ANCESTRY_BATCH_SIZE; id-er som mangler i svaret
   * (f.eks. ukjente hos backend) markeres som kjente med tom kjede slik at de
   * ikke hentes på nytt i evig løkke.
   */
  resolveAncestries(): void {
    if (this.ancestryFetchPromise) return;

    const cache = this.ancestryCache();
    const missing = this.filterState
      .selectedTaxonIds()
      .filter((id) => !cache.has(id) && !this.pendingAncestryIds.has(id));
    if (missing.length === 0) return;

    const batch = missing.slice(0, TaxonSelectionService.ANCESTRY_BATCH_SIZE);
    for (const id of batch) this.pendingAncestryIds.add(id);

    let succeeded = false;
    this.ancestryFetchPromise = lastValueFrom(this.taxonTreeService.getAncestry(batch))
      .then((ancestries) => {
        succeeded = true;
        // Id-er som ikke er i svaret (f.eks. ukjente hos backend) markeres som
        // kjente med tom kjede — ellers ville de hentes på nytt i evig løkke
        const entries = new Map<number, number[]>();
        for (const ancestry of ancestries) {
          if (ancestry.id != null) entries.set(ancestry.id, ancestry.parentIds ?? []);
        }
        for (const id of batch) {
          if (!entries.has(id)) entries.set(id, []);
        }
        this.ancestryCache.update((cache) => new Map([...cache, ...entries]));
        // Registrer kjente barn per nivå slik full dekning kan avgjøres uten
        // at treet er ekspandert ned til nivået
        for (const ancestry of ancestries) {
          for (const level of ancestry.levels ?? []) {
            if (level.parentId == null) continue;
            this.registerChildIds(level.parentId, level.childIds ?? []);
          }
        }
      })
      .catch(() => {
        // Lar batchen gå ut av pending slik at neste utvalgsendring kan prøve på nytt.
        // Ingen rekursiv oppfølging ved feil — ellers ville en vedvarende feil gitt evig retry.
      })
      .finally(() => {
        for (const id of batch) this.pendingAncestryIds.delete(id);
        this.ancestryFetchPromise = null;
        // Plukk opp id-er som ble valgt mens requesten pågikk,
        // og neste batch dersom det var flere enn ANCESTRY_BATCH_SIZE
        if (succeeded) this.resolveAncestries();
      });
  }
}
