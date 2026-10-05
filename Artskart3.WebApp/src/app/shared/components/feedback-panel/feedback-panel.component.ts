import { Component, ElementRef, signal, viewChild } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';

@Component({
  selector: 'app-feedback-panel',
  imports: [TranslateModule],
  templateUrl: './feedback-panel.component.html',
  styleUrl: './feedback-panel.component.css',
  host: {
    '[class.open]': 'isOpen()',
    '(document:keydown.escape)': 'onEscape()',
  },
})
export class FeedbackPanelComponent {
  readonly panelId = 'feedback-panel';
  readonly headingId = 'feedback-panel-heading';

  readonly feedbackFormUrl = 'https://forms.cloud.microsoft/e/Ns51ygm4rB?origin=lprLink';
  readonly githubUrl = 'https://github.com/orgs/Artsdatabanken/projects/53';

  readonly isOpen = signal(false);

  private readonly tabButton = viewChild.required<ElementRef<HTMLButtonElement>>('tabButton');
  private readonly heading = viewChild.required<ElementRef<HTMLElement>>('heading');

  toggle(): void {
    if (this.isOpen()) {
      this.isOpen.set(false);
    } else {
      this.open();
    }
  }

  open(): void {
    this.isOpen.set(true);
    // Wait for inert to be removed before moving focus into the panel.
    // preventScroll stops the browser from jumping the layout while the panel is still sliding in.
    setTimeout(() => this.heading().nativeElement.focus({ preventScroll: true }));
  }

  close(): void {
    this.isOpen.set(false);
    this.tabButton().nativeElement.focus({ preventScroll: true });
  }

  onEscape(): void {
    if (this.isOpen()) {
      this.close();
    }
  }
}
