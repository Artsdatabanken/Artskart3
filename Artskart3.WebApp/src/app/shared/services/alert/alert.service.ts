import { Injectable, signal, computed } from '@angular/core';

export type AlertVariant = 'danger' | 'warning' | 'success' | 'info';

export interface AlertOptions {
  heading?: string;
  closable?: boolean;
  /** Auto-dismiss delay in milliseconds. Defaults to 5000. Set to 0 to disable. */
  autoDismissMs?: number;
  /** Optional display-only validity period, e.g. from a scheduled notification. */
  startDisplayDate?: string;
  endDisplayDate?: string;
}

export interface AlertItem {
  id: number;
  message: string;
  variant: AlertVariant;
  heading?: string;
  closable: boolean;
  autoDismissMs: number;
  startDisplayDate?: string;
  endDisplayDate?: string;
}

const DEFAULT_AUTO_DISMISS_MS = 5000;
const MAX_VISIBLE = 3;

@Injectable({
  providedIn: 'root',
})
export class AlertService {
  private nextId = 0;
  private readonly items = signal<AlertItem[]>([]);
  private readonly timers = new Map<number, ReturnType<typeof setTimeout>>();

  /** The currently visible alerts (max 3). Remaining are queued. */
  readonly visibleAlerts = computed(() => this.items().slice(0, MAX_VISIBLE));

  show(message: string, variant: AlertVariant, options?: AlertOptions): void {
    const autoDismissMs = options?.autoDismissMs ?? DEFAULT_AUTO_DISMISS_MS;
    const id = this.nextId++;

    const item: AlertItem = {
      id,
      message,
      variant,
      heading: options?.heading,
      closable: options?.closable ?? true,
      autoDismissMs,
      startDisplayDate: options?.startDisplayDate,
      endDisplayDate: options?.endDisplayDate,
    };

    this.items.update(list => [...list, item]);

    if (autoDismissMs > 0) {
      const timer = setTimeout(() => this.dismiss(id), autoDismissMs);
      this.timers.set(id, timer);
    }
  }

  showError(message: string, options?: AlertOptions): void {
    this.show(message, 'danger', options);
  }

  showWarning(message: string, options?: AlertOptions): void {
    this.show(message, 'warning', options);
  }

  showSuccess(message: string, options?: AlertOptions): void {
    this.show(message, 'success', options);
  }

  showInfo(message: string, options?: AlertOptions): void {
    this.show(message, 'info', options);
  }

  dismiss(id: number): void {
    const timer = this.timers.get(id);
    if (timer != null) {
      clearTimeout(timer);
      this.timers.delete(id);
    }
    this.items.update(list => list.filter(item => item.id !== id));
  }

  dismissAll(): void {
    for (const timer of this.timers.values()) {
      clearTimeout(timer);
    }
    this.timers.clear();
    this.items.set([]);
  }
}
