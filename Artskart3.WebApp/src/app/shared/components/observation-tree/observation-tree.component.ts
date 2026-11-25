import { DOCUMENT, NgTemplateOutlet } from '@angular/common';
import {
  afterNextRender,
  Component,
  CUSTOM_ELEMENTS_SCHEMA,
  DestroyRef,
  effect,
  ElementRef,
  inject,
  Injector,
  input,
  linkedSignal,
  output,
  signal,
  untracked,
} from '@angular/core';
import { LoggingService } from '@shared/logging.service';
import { Tree, TreeItem, TreeItemGroup } from '@angular/aria/tree';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { MeterUnitPipe } from '@shared/pipes/meter-unit.pipe';
import { ObservationTreeNode } from '../observation-list.component/observation-list.model';
import { RegistrationMarkerComponent } from '../registration-marker/registration-marker.component';
import { RiskCategoryBadgeComponent } from '../risk-category-badge/risk-category-badge.component';
import { LoadingIndicatorComponent } from '../loading-indicator/loading-indicator.component';

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
    LoadingIndicatorComponent,
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
  readonly expandingNodeIds = signal(new Set<string>());
  protected readonly translate = inject(TranslateService);
  private readonly logger = inject(LoggingService);
  private readonly injector = inject(Injector);
  private readonly expansionFrames = new Map<string, number>();

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
    effect(() => {
      this.resetKey();
      this.cancelPendingExpansions();
      if (host.contains(document.activeElement)) {
        afterNextRender(() => host.querySelector<HTMLElement>('[role="treeitem"]')?.focus(), { injector: this.injector });
      }
    });
    inject(DestroyRef).onDestroy(() => this.cancelPendingExpansions());
  }

  onExpandedChange(node: ObservationTreeNode, item: TreeItem<string>, expanded: boolean): void {
    if (node.kind === 'group') {
      if (!expanded) item.expanded.set(true);
      return;
    }

    if (expanded) {
      item.expanded.set(false);
      this.expandingNodeIds.update((ids) => new Set(ids).add(node.id));
      const firstFrame = requestAnimationFrame(() => {
        const secondFrame = requestAnimationFrame(() => {
          this.expansionFrames.delete(node.id);
          item.expanded.set(true);
          this.expandedNodeIds.update((ids) => new Set(ids).add(node.id));
          afterNextRender(() => this.removeExpandingNodeId(node.id), { injector: this.injector });
        });
        this.expansionFrames.set(node.id, secondFrame);
      });
      this.expansionFrames.set(node.id, firstFrame);
      return;
    }

    const frame = this.expansionFrames.get(node.id);
    if (frame !== undefined) cancelAnimationFrame(frame);
    this.expansionFrames.delete(node.id);
    this.removeExpandingNodeId(node.id);
    this.expandedNodeIds.update((ids) => {
      const next = new Set(ids);
      const collapse = (branch: ObservationTreeNode) => {
        next.delete(branch.id);
        branch.children.forEach(collapse);
      };
      collapse(node);
      return next;
    });
  }

  private removeExpandingNodeId(nodeId: string): void {
    this.expandingNodeIds.update((ids) => {
      const next = new Set(ids);
      next.delete(nodeId);
      return next;
    });
  }

  private cancelPendingExpansions(): void {
    this.expansionFrames.forEach((frame) => cancelAnimationFrame(frame));
    this.expansionFrames.clear();
    this.expandingNodeIds.set(new Set());
  }

  activate(event: KeyboardEvent, node: ObservationTreeNode, item: TreeItem<string>): void {
    if (event.key !== 'Enter' && event.key !== ' ') return;
    event.preventDefault();
    event.stopPropagation();
    if (node.kind === 'status' || node.kind === 'species') item.expanded.update((value) => !value);
    else this.openObservation(node);
  }
}
