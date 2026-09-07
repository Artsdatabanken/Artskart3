import { ChangeDetectionStrategy, Component, DestroyRef, effect, inject, input, signal } from '@angular/core';

@Component({
  selector: 'app-loading-indicator',
  templateUrl: './loading-indicator.component.html',
  styleUrl: './loading-indicator.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LoadingIndicatorComponent {
  readonly active = input(false);
  readonly label = input('');
  readonly ariaLive = input<'polite' | 'assertive'>('polite');
  readonly variant = input<'overlay' | 'inline'>('overlay');
  readonly showDelayMs = input(150);
  readonly minVisibleDurationMs = input(300);

  private readonly visibleState = signal(false);
  readonly visible = this.visibleState.asReadonly();

  private showTimer: ReturnType<typeof setTimeout> | undefined;
  private hideTimer: ReturnType<typeof setTimeout> | undefined;
  private shownAtMs = 0;

  constructor() {
    effect(() => {
      const isActive = this.active();
      this.clearPendingTimers();

      if (isActive) {
        this.showTimer = setTimeout(() => {
          this.visibleState.set(true);
          this.shownAtMs = Date.now();
        }, this.showDelayMs());
        return;
      }

      if (!this.visibleState()) {
        return;
      }

      const remainingVisibleMs = this.minVisibleDurationMs() - (Date.now() - this.shownAtMs);
      if (remainingVisibleMs > 0) {
        this.hideTimer = setTimeout(() => this.visibleState.set(false), remainingVisibleMs);
      } else {
        this.visibleState.set(false);
      }
    });

    inject(DestroyRef).onDestroy(() => this.clearPendingTimers());
  }

  private clearPendingTimers(): void {
    clearTimeout(this.showTimer);
    clearTimeout(this.hideTimer);
  }
}
