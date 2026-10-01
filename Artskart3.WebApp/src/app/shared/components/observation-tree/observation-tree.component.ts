import { DOCUMENT, NgTemplateOutlet } from '@angular/common';
import { afterNextRender, Component, CUSTOM_ELEMENTS_SCHEMA, effect, ElementRef, inject, Injector, input } from '@angular/core';
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

  keepHeadingOpen(node: ObservationTreeNode, item: TreeItem<string>, expanded: boolean): void {
    if (node.kind === 'group' && !expanded) item.expanded.set(true);
  }

  activate(event: KeyboardEvent, node: ObservationTreeNode, item: TreeItem<string>): void {
    if (event.key !== 'Enter' && event.key !== ' ') return;
    event.preventDefault();
    event.stopPropagation();
    if (node.kind === 'status' || node.kind === 'species') item.expanded.update((value) => !value);
  }
}
