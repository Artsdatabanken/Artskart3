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
  untracked,
} from '@angular/core';
import { LoggingService } from '@shared/logging.service';
import { Tree, TreeItem, TreeItemGroup } from '@angular/aria/tree';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { MeterUnitPipe } from '@shared/pipes/meter-unit.pipe';
import { ObservationTreeNode } from '../observation-list.component/observation-list.model';
import { RegistrationMarkerComponent } from '../registration-marker/registration-marker.component';
import { RiskCategoryBadgeComponent } from '../risk-category-badge/risk-category-badge.component';

@Component({
  imports: [
    Tree,
    TreeItem,
    TreeItemGroup,
    NgTemplateOutlet,
    TranslateModule,
    RegistrationMarkerComponent,
    RiskCategoryBadgeComponent,
    MeterUnitPipe,
  ],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  selector: 'app-observation-tree',
  styleUrl: './observation-tree.component.css',
  templateUrl: './observation-tree.component.html',
})
export class ObservationTreeComponent {
  readonly nodes = input<readonly ObservationTreeNode[]>([]);
  readonly resetKey = input('');
  readonly openFirstSection = input(false);
  readonly expandedNodeIds = linkedSignal({
    source: this.resetKey,
    // Untracked so only resetKey resets expansions, not rebuilt nodes (e.g. after a language change).
    computation: () =>
      untracked(
        () =>
          new Set<string>(
            this.openFirstSection() ? this.nodes().flatMap((group) => group.children.slice(0, 1).map((section) => section.id)) : [],
          ),
      ),
  });
  readonly observationActivated = output<number>();
  protected readonly translate = inject(TranslateService);
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
