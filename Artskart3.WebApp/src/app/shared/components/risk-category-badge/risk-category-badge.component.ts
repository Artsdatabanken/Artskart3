import { Component, input } from '@angular/core';

@Component({
  selector: 'app-risk-category-badge',
  styleUrl: './risk-category-badge.component.css',
  host: {
    '[attr.role]': 'label() ? "img" : null',
    '[attr.aria-label]': 'label() || null',
    '[attr.title]': 'label() || null',
  },
  template: `
    <span class="risk-category-circle-small" [class]="code()">
      <span class="risk-category-tag-label-small">{{ code() }}</span>
    </span>
  `,
})
export class RiskCategoryBadgeComponent {
  readonly code = input.required<string | null | undefined>();
  readonly label = input('');
}
