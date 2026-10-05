import { DOCUMENT, NgTemplateOutlet } from '@angular/common';
import {
  afterNextRender,
  Component,
  CUSTOM_ELEMENTS_SCHEMA,
  effect,
  ElementRef,
  inject,
  Injector,
  input,
  linkedSignal,
  output,
} from '@angular/core';
import { LoggingService } from '@shared/logging.service';
import { Tree, TreeItem, TreeItemGroup } from '@angular/aria/tree';
import { TranslateModule } from '@ngx-translate/core';
import { ObservationTreeNode } from '../observation-list.component/observation-list.model';
import { RiskCategoryBadgeComponent } from '../risk-category-badge/risk-category-badge.component';

@Component({
  imports: [Tree, TreeItem, TreeItemGroup, NgTemplateOutlet, TranslateModule, RiskCategoryBadgeComponent],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  selector: 'app-observation-tree',
  styleUrl: './observation-tree.component.css',
  templateUrl: './observation-tree.component.html',
})
export class ObservationTreeComponent {
  readonly nodes = input<readonly ObservationTreeNode[]>([]);
  readonly resetKey = input('');
  readonly expandedNodeIds = linkedSignal({
    source: this.resetKey,
    computation: () => new Set<string>(),
  });
  readonly observationActivated = output<number>();
  private readonly logger = inject(LoggingService);

  openObservation(node: ObservationTreeNode): void {
    if (node.kind !== 'observation') return;
    if (node.observationId == null || node.observationId <= 0) {
      this.logger.error('Observation leaf has no valid ID', 'ObservationTree', node.id);
      return;
    }
    this.observationActivated.emit(node.observationId);
  }

  constructor() {
    const host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
    const document = inject(DOCUMENT);
    const injector = inject(Injector);
    effect(() => {
      this.resetKey();
      if (host.contains(document.activeElement)) {
        afterNextRender(() => host.querySelector<HTMLElement>('[role="treeitem"]')?.focus(), { injector });
      }
    });
  }

  onExpandedChange(node: ObservationTreeNode, item: TreeItem<string>, expanded: boolean): void {
    if (node.kind === 'group') {
      if (!expanded) item.expanded.set(true);
      return;
    }
    this.expandedNodeIds.update((ids) => {
      const next = new Set(ids);
      if (expanded) {
        next.add(node.id);
      } else {
        const collapse = (branch: ObservationTreeNode) => {
          next.delete(branch.id);
          branch.children.forEach(collapse);
        };
        collapse(node);
      }
      return next;
    });
  }

  activate(event: KeyboardEvent, node: ObservationTreeNode, item: TreeItem<string>): void {
    if (event.key !== 'Enter' && event.key !== ' ') return;
    event.preventDefault();
    event.stopPropagation();
    if (node.kind === 'status' || node.kind === 'species') item.expanded.update((value) => !value);
    else this.openObservation(node);
  }
}
