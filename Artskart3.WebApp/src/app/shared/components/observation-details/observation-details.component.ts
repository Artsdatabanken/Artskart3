import {
  afterRenderEffect,
  Component,
  computed,
  CUSTOM_ELEMENTS_SCHEMA,
  ElementRef,
  inject,
  input,
  linkedSignal,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { ObservationDetailDto } from '@shared/types/api.types';
import { LocaleDatePipe } from '@shared/pipes/locale-date.pipe';
import { LoggingService } from '@shared/logging.service';
import { RiskCategoryBadgeComponent } from '../risk-category-badge/risk-category-badge.component';
import { ObservationGalleryComponent } from '../observation-gallery/observation-gallery.component';
import { ImageViewerComponent } from '../image-viewer/image-viewer.component';
import { ViewerImage } from '../image-viewer/image-source';

export type DetailState = 'loading' | 'ready' | 'notFound' | 'error';
type MapView = 'local' | 'overview';

@Component({
  imports: [TranslateModule, RiskCategoryBadgeComponent, ObservationGalleryComponent, ImageViewerComponent],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  selector: 'app-observation-details',
  styleUrl: './observation-details.component.css',
  templateUrl: './observation-details.component.html',
})
export class ObservationDetailsComponent {
  readonly observationId = input<number | null>(null);
  readonly detail = input<ObservationDetailDto | null>(null);
  readonly state = input<DetailState>('loading');
  readonly count = input(1);
  readonly position = input(1);
  readonly navigationError = input(false);
  readonly hasList = input(false);
  readonly previous = output<void>();
  readonly next = output<void>();
  readonly dismiss = output<void>();
  readonly retry = output<void>();
  readonly expanded = signal(false);
  readonly sectionsOpen = signal<Record<string, boolean>>({ observation: true, place: false, dataset: false });
  readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  readonly body = viewChild.required<ElementRef<HTMLElement>>('body');
  private readonly backButton = viewChild.required<ElementRef<HTMLElement>>('backButton');
  private readonly expandButton = viewChild.required<ElementRef<HTMLElement>>('expandButton');
  readonly viewer = linkedSignal<ViewerImage | null>(() => {
    this.observationId();
    return null;
  });
  readonly failedMaps = linkedSignal<Set<MapView>>(() => {
    this.observationId();
    return new Set();
  });
  readonly mapAttempts = linkedSignal<Record<MapView, number>>(() => {
    this.observationId();
    return { local: 0, overview: 0 };
  });
  readonly mapViews: MapView[] = ['local', 'overview'];
  private readonly translate = inject(TranslateService);
  private readonly logger = inject(LoggingService);
  private readonly language = toSignal(this.translate.onLangChange);
  private modal = false;
  readonly title = computed(() => {
    this.language();
    return this.detail()?.popularName || this.detail()?.scientificName || this.translate.instant('observationDetails.title');
  });
  readonly sections = computed(() => {
    const d = this.detail();
    const language = this.language()?.lang ?? this.translate.getCurrentLang();
    const row = (label: string, text: unknown, emptyKey = 'notProvided') =>
      text == null || text === ''
        ? { label, value: this.translate.instant(`observationDetails.${emptyKey}`), empty: true }
        : { label, value: String(text), empty: false };
    const quality =
      d?.quality != null && d.quality >= 0 && d.quality <= 3 ? this.translate.instant(`observationDetails.qualities.${d.quality}`) : '';
    const validated = d?.tags?.includes('Validated') ? this.translate.instant('observationDetails.validated') : '';
    const warning =
      d?.hasErrors || d?.tags?.some((tag) => ['QualityIssue', 'Blocked'].includes(tag))
        ? this.translate.instant('observationDetails.qualityWarning')
        : '';
    return [
      {
        id: 'observation',
        rows: [
          row('date', new LocaleDatePipe().transform(d?.collected, language)),
          row('collector', d?.collector),
          row('basis', this.lookup('basisOfRecordName', d?.basisOfRecord)),
          row('activity', d?.behaviors?.map((name) => this.lookup('behaviorName', name)).join(', ')),
          row(
            'quality',
            [quality, validated, warning].filter(Boolean).join(' · ') || this.translate.instant('observationDetails.noKnownIssues'),
          ),
        ],
      },
      {
        id: 'place',
        rows: [
          row('county', d?.counties?.join(', ')),
          row('municipality', d?.municipalities?.join(', ')),
          row('locality', d?.locality),
          row('latitude', d?.point?.latitude),
          row('longitude', d?.point?.longitude),
          row('precision', d?.coordinatePrecision != null ? `${d.coordinatePrecision} m` : null),
        ],
      },
      {
        id: 'dataset',
        rows: [
          row('institution', d?.institution, 'noInstitution'),
          row('projects', d?.projects?.join(', '), 'noProjects'),
          row('dataset', d?.dataset, 'noDataset'),
          row('catalogNumber', d?.catalogNumber),
        ],
      },
    ];
  });

  constructor() {
    afterRenderEffect(() => {
      const dialog = this.dialog().nativeElement;
      const expanded = this.expanded();
      if (!dialog.open || this.modal !== expanded) {
        const changingLayout = dialog.open;
        const active = dialog.ownerDocument.activeElement;
        let restore = changingLayout && active instanceof HTMLElement && dialog.contains(active) && !active.hidden ? active : null;
        while (restore?.shadowRoot?.activeElement instanceof HTMLElement) {
          restore = restore.shadowRoot.activeElement;
        }
        if (dialog.open) dialog.close();
        this.modal = expanded;
        if (expanded) dialog.showModal();
        else dialog.show();
        if (restore) {
          restore.focus({ preventScroll: true });
        } else if (changingLayout) {
          const control = expanded ? this.backButton() : this.expandButton();
          control.nativeElement.shadowRoot?.querySelector('button')?.focus({ preventScroll: true });
        }
      }
    });
    afterRenderEffect(() => {
      this.observationId();
      this.body().nativeElement.scrollTop = 0;
    });
  }

  setSection(id: string, event: Event): void {
    const item = event.currentTarget;
    if (!(item instanceof HTMLElement) || !('open' in item) || typeof item.open !== 'boolean') {
      this.logger.error('Invalid accordion toggle event', 'ObservationDetails', id);
      return;
    }
    const open = item.open;
    this.sectionsOpen.update((sections) => ({ ...sections, [id]: open }));
  }

  cancel(event: Event): void {
    event.preventDefault();
    this.dismiss.emit();
  }

  toggleExpanded(): void {
    this.expanded.update((value) => !value);
  }

  mapUrl(view: MapView): string {
    return `/api/observations/${this.observationId()}/maps/${view}?attempt=${this.mapAttempts()[view]}`;
  }

  mapFailed(view: MapView): void {
    this.logger.warn('Observation map unavailable', 'ObservationDetails', { id: this.observationId(), view });
    this.failedMaps.update((failed) => new Set(failed).add(view));
    this.viewer.set(null);
  }

  retryMap(view: MapView): void {
    this.mapAttempts.update((attempts) => ({ ...attempts, [view]: attempts[view] + 1 }));
    this.failedMaps.update((failed) => {
      const next = new Set(failed);
      next.delete(view);
      return next;
    });
  }

  enlargeMap(view: MapView): void {
    this.viewer.set({ src: this.mapUrl(view), alt: this.translate.instant(`observationDetails.maps.${view}`) });
  }

  viewerFailed(): void {
    const src = this.viewer()?.src;
    const view = this.mapViews.find((view) => this.mapUrl(view) === src);
    if (view) this.mapFailed(view);
  }

  private lookup(group: string, name: string | null | undefined): string {
    if (!name) return '';
    const key = `sidebar.${group}.${name.toLowerCase()}`;
    const label: string = this.translate.instant(key);
    return label === key ? name : label;
  }
}
