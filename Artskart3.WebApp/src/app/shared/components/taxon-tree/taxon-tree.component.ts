import { Component, ChangeDetectionStrategy, CUSTOM_ELEMENTS_SCHEMA, inject, input, signal, OnInit } from '@angular/core';
import { TaxonTreeService } from '../../services/taxon-tree/taxon-tree.service';
import { TaxonSelectionService, TaxonCheckboxState } from '../../services/taxon-selection/taxon-selection.service';
import { TaxonTreeNodeDto } from '../../types/api.types';
import { FormatNumberPipe } from '../../pipes/format-number.pipe';
import { LoggingService } from '../../logging.service';

@Component({
  selector: 'app-taxon-tree',
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  imports: [FormatNumberPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './taxon-tree.component.html',
  styleUrl: './taxon-tree.component.css',
})
export class TaxonTreeComponent implements OnInit {
  private readonly taxonTreeService = inject(TaxonTreeService);
  private readonly taxonSelection = inject(TaxonSelectionService);
  private readonly logger = inject(LoggingService);

  readonly parentTaxonId = input<number | undefined>(undefined);
  readonly autoLoad = input(true);
  /** Forfedrekjeden til denne tre-forekomsten, fra rot til nærmeste forelder. */
  readonly ancestorIds = input<number[]>([]);
  readonly nodes = signal<TaxonTreeNodeDto[]>([]);
  readonly loaded = signal(false);
  readonly expandedNodeIds = signal<Set<number>>(new Set());

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

  onNodeToggle(nodeId: number): void {
    if (this.expandedNodeIds().has(nodeId)) return;
    this.expandedNodeIds.update((ids) => {
      const next = new Set(ids);
      next.add(nodeId);
      return next;
    });
  }

  isNodeExpanded(nodeId: number): boolean {
    return this.expandedNodeIds().has(nodeId);
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
          : Promise.resolve(this.taxonSelection.select(id, this.ancestorIds()));
    } catch (err) {
      this.logger.error('Klarte ikke oppdatere taksonvalg', 'TaxonTreeComponent', err);
      return Promise.resolve();
    }
    return action.catch((err: unknown) =>
      this.logger.error('Klarte ikke oppdatere taksonvalg', 'TaxonTreeComponent', err),
    );
  }
}
