import { Component, computed, inject, input, linkedSignal, model, output } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { LoggingService } from '@shared/logging.service';
import { ObservationService } from '@shared/services/observation/observation.service';
import { DetailState, ObservationDetailsComponent } from '../observation-details/observation-details.component';
import { loadObservationDetail } from '../observation-details/observation-detail-load';

@Component({
  selector: 'app-list-observation-details',
  imports: [ObservationDetailsComponent],
  template: `
    <app-observation-details
      [observationId]="observationId()"
      [detail]="detail()"
      [state]="state()"
      [position]="position() + 1 || 1"
      [count]="count()"
      [fillHeight]="true"
      [(expanded)]="expanded"
      (previous)="move(-1)"
      (next)="move(1)"
      (dismiss)="closed.emit()"
      (retry)="load.reload()"
    />
  `,
})
export class ListObservationDetailsComponent {
  readonly observationId = model.required<number>();
  /** The observations the pager steps through, in list order. */
  readonly observationIds = input<readonly number[]>([]);
  readonly active = input(true);
  readonly closed = output<void>();
  /** A modal left open on a hidden tab blocks the whole page, so hiding the tab collapses it. */
  readonly expanded = linkedSignal({ source: this.active, computation: () => false });
  private readonly api = inject(ObservationService);
  private readonly logger = inject(LoggingService);
  readonly load = rxResource({
    params: () => this.observationId(),
    stream: ({ params }) => loadObservationDetail(this.api, this.logger, params, 'ListObservationDetails'),
  });
  readonly state = computed<DetailState>(() => (this.load.isLoading() ? 'loading' : (this.load.value()?.state ?? 'loading')));
  readonly detail = computed(() => (this.load.isLoading() ? null : (this.load.value()?.detail ?? null)));
  readonly position = computed(() => this.observationIds().indexOf(this.observationId()));
  readonly count = computed(() => (this.position() >= 0 ? this.observationIds().length : 1));

  move(direction: number): void {
    const ids = this.observationIds();
    if (ids.length < 2 || this.position() < 0) return;
    this.observationId.set(ids[(this.position() + direction + ids.length) % ids.length]);
  }
}
