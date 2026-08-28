import { Component, ChangeDetectionStrategy, CUSTOM_ELEMENTS_SCHEMA, inject, input, signal, OnInit } from '@angular/core';
import { TaxonTreeService } from '../../services/taxon-tree/taxon-tree.service';
import { FilterStateService } from '../../services/filter-state/filter-state.service';
import { TaxonTreeNodeDto } from '../../types/api.types';
import { FormatNumberPipe } from '../../pipes/format-number.pipe';

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
  private readonly filterState = inject(FilterStateService);

  readonly parentTaxonId = input<number | undefined>(undefined);
  readonly autoLoad = input(true);
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

  isTaxonSelected(id: number): boolean {
    return this.filterState.selectedTaxonIds().includes(id);
  }

  onTaxonToggle(id: number): void {
    this.filterState.toggleTaxon(id);
  }
}
