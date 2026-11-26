import { Component, CUSTOM_ELEMENTS_SCHEMA, effect, inject, input, signal, untracked, OnInit } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';
import { TaxonTreeService } from '../../services/taxon-tree/taxon-tree.service';
import { TaxonSelectionService, TaxonCheckboxState, taxonDisplayName } from '../../services/taxon-selection/taxon-selection.service';
import { TaxonTreeNodeDto } from '../../types/api.types';
import { FormatNumberPipe } from '../../pipes/format-number.pipe';
import { LoggingService } from '../../logging.service';

@Component({
  selector: 'app-taxon-tree',
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  imports: [FormatNumberPipe],
  templateUrl: './taxon-tree.component.html',
  styleUrl: './taxon-tree.component.css',
})
export class TaxonTreeComponent implements OnInit {
  private readonly taxonTreeService = inject(TaxonTreeService);
  private readonly taxonSelection = inject(TaxonSelectionService);
  private readonly logger = inject(LoggingService);
  protected readonly translate = inject(TranslateService);

  readonly parentTaxonId = input<number | undefined>(undefined);
  readonly autoLoad = input(true);
  /** Forfedrekjeden til denne tre-forekomsten, fra rot til nærmeste forelder. */
  readonly ancestorIds = input<number[]>([]);
  readonly showScientificNames = input(false);
  /** Each new value opens the nodes above the selected taxa (the first MAX_REVEALED_TAXA), level by level down the tree. */
  readonly revealSelection = input(0);
  readonly nodes = signal<TaxonTreeNodeDto[]>([]);
  readonly loaded = signal(false);
  /** Nodes whose subtree has been rendered. A closed node keeps it, so it isn't loaded again. */
  readonly expandedNodeIds = signal<Set<number>>(new Set());
  readonly openNodeIds = signal<Set<number>>(new Set());
  /** The nodes opened by the latest reveal. Only their subtrees carry it on. */
  private readonly revealedNodeIds = signal<Set<number>>(new Set());

  constructor() {
    effect(() => {
      if (this.revealSelection() === 0 || !this.loaded()) return;
      untracked(() => this.revealSelectedDescendants());
    });
  }

  ngOnInit(): void {
    if (this.autoLoad()) {
      this.loadChildren();
    }
  }

  loadChildren(): void {
    if (this.loaded()) return;
    this.taxonTreeService.getChildren(this.parentTaxonId()).subscribe({
      next: (nodes) => {
        this.nodes.set(nodes);
        this.loaded.set(true);
        this.taxonSelection.registerTreeLevel(this.parentTaxonId(), this.ancestorIds(), nodes);
      },
    });
  }

  onNodeToggle(nodeId: number, event: Event): void {
    // The accordion's toggle event bubbles, so nested nodes' toggles pass through here too.
    if (event.target !== event.currentTarget) return;
    const open = (event.currentTarget as HTMLElement & { open: boolean }).open;
    this.openNodeIds.update((ids) => {
      const next = new Set(ids);
      if (open) next.add(nodeId);
      else next.delete(nodeId);
      return next;
    });
    if (open) this.expandedNodeIds.update((ids) => withIds(ids, [nodeId]));
  }

  isNodeExpanded(nodeId: number): boolean {
    return this.expandedNodeIds().has(nodeId);
  }

  childRevealSelection(nodeId: number): number {
    return this.revealedNodeIds().has(nodeId) ? this.revealSelection() : 0;
  }

  private revealSelectedDescendants(): void {
    const ids = this.nodes()
      .filter((node) => node.hasChildren && this.taxonSelection.revealedAncestorIds().has(node.id))
      .map((node) => node.id);
    this.revealedNodeIds.set(new Set(ids));
    this.expandedNodeIds.update((current) => withIds(current, ids));
    this.openNodeIds.update((current) => withIds(current, ids));
  }

  private childAncestorIdsCache = new Map<number, number[]>();
  private childAncestorIdsForInput: number[] | null = null;

  childAncestorIds(nodeId: number): number[] {
    const ancestors = this.ancestorIds();
    // input() bruker Object.is — returner samme array-instans så lenge vår egen
    // ancestorIds-referanse er uendret, ellers får hele sub-treet unødvendig re-render
    if (this.childAncestorIdsForInput !== ancestors) {
      this.childAncestorIdsForInput = ancestors;
      this.childAncestorIdsCache.clear();
    }
    let chain = this.childAncestorIdsCache.get(nodeId);
    if (!chain) {
      chain = [...ancestors, nodeId];
      this.childAncestorIdsCache.set(nodeId, chain);
    }
    return chain;
  }

  displayName(node: TaxonTreeNodeDto): string | null | undefined {
    return taxonDisplayName(node, this.showScientificNames());
  }

  checkboxState(nodeId: number): TaxonCheckboxState {
    return this.taxonSelection.checkboxState(nodeId, this.ancestorIds());
  }

  onTaxonToggle(id: number): Promise<void> {
    // Fanger både synkrone unntak og avviste løfter — Angular forkaster
    // returverdien fra (change), så ingenting må slippe unhandled ut av her
    let action: Promise<void>;
    try {
      action =
        this.checkboxState(id) === 'checked'
          ? this.taxonSelection.deselect(id, this.ancestorIds())
          : Promise.resolve(this.taxonSelection.select(id));
    } catch (err) {
      this.logger.error('Klarte ikke oppdatere taksonvalg', 'TaxonTreeComponent', err);
      return Promise.resolve();
    }
    return action.catch((err: unknown) =>
      this.logger.error('Klarte ikke oppdatere taksonvalg', 'TaxonTreeComponent', err),
    );
  }
}

function withIds(ids: Set<number>, added: readonly number[]): Set<number> {
  return new Set([...ids, ...added]);
}
