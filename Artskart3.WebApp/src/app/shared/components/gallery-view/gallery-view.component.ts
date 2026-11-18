import { Component, computed, CUSTOM_ELEMENTS_SCHEMA, inject, input, linkedSignal, output, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { LoggingService } from '@shared/logging.service';
import { MapViewService } from '../../services/map-view/map-view.service';
import { ObservationService } from '../../services/observation/observation.service';
import { SearchFilterService } from '../../services/search-filter/search-filter.service';
import { ObservationImageDto, ObservationSearchFilter, PagedObservationImageResponse } from '../../types/api.types';
import { ImageInfoComponent } from '../image-info/image-info.component';
import { ImageViewerComponent } from '../image-viewer/image-viewer.component';
import { imageSource, neighbour, ViewerImage, webUrl } from '../image-viewer/image-source';

@Component({
  selector: 'app-gallery-view',
  imports: [TranslateModule, ImageViewerComponent, ImageInfoComponent],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  templateUrl: './gallery-view.component.html',
  styleUrl: './gallery-view.component.css',
})
export class GalleryViewComponent {
  private readonly observationService = inject(ObservationService);
  private readonly searchFilter = inject(SearchFilterService);
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);
  private readonly logger = inject(LoggingService);
  private readonly mapView = inject(MapViewService);

  /** While hidden the gallery keeps its last result and sends no searches. */
  readonly active = input(true);

  /** Needed besides the URL change, which doesn't happen when the observation is already open. */
  readonly observationShown = output<void>();

  readonly pageSizeOptions = [10, 25, 50];
  readonly resultsPerPage = signal(this.pageSizeOptions[1]);
  readonly pageNumber = linkedSignal(() => {
    this.searchFilter.observationFilter();
    this.resultsPerPage();
    return 1;
  });

  private readonly searchParams = computed<ObservationSearchFilter>(
    () => ({
      ...this.searchFilter.observationFilter(),
      pageNumber: this.pageNumber(),
      resultsPerPage: this.resultsPerPage(),
    }),
    { equal: (a, b) => JSON.stringify(a) === JSON.stringify(b) },
  );

  private readonly activeParams = linkedSignal<ObservationSearchFilter | null, ObservationSearchFilter | undefined>({
    source: () => (this.active() ? this.searchParams() : null),
    computation: (params, previous) => params ?? previous?.value,
  });

  readonly imagesResource = rxResource<PagedObservationImageResponse, ObservationSearchFilter | undefined>({
    params: () => this.activeParams(),
    stream: ({ params }) => this.observationService.searchObservationImages(params),
  });

  readonly totalVisiblePages = computed(() => {
    const lookahead = this.imagesResource.hasValue() ? (this.imagesResource.value().lookaheadCount ?? 0) : 0;
    return this.pageNumber() + lookahead;
  });

  readonly hasMorePages = computed(() => this.imagesResource.hasValue() && (this.imagesResource.value().hasMorePages ?? false));

  readonly selected = linkedSignal<ObservationImageDto | null>(() => {
    this.searchParams();
    this.active();
    return null;
  });

  private readonly failedSources = linkedSignal<Map<number, string[]>>(() => {
    this.searchParams();
    return new Map();
  });

  private readonly loadedSources = linkedSignal<ReadonlySet<string>>(() => {
    this.searchParams();
    return new Set();
  });

  private readonly expandedImages = linkedSignal<ReadonlySet<number>>(() => {
    this.searchParams();
    return new Set();
  });

  readonly viewer = computed<ViewerImage | null>(() => {
    const item = this.selected();
    const src = item && this.original(item);
    if (!item || !src) return null;
    const { popularName, scientificName, author } = item;
    return { ...item.image, src, alt: this.alt(item), popularName, scientificName, author };
  });

  private readonly items = computed(() => (this.imagesResource.hasValue() ? (this.imagesResource.value().items ?? []) : []));
  readonly previousItem = computed(() => this.neighbourOf(this.selected(), -1));
  readonly nextItem = computed(() => this.neighbourOf(this.selected(), 1));
  private lastStep: 1 | -1 = 1;

  source(item: ObservationImageDto): string | null {
    if (item.observationId == null || !item.image) return null;
    return imageSource(item.observationId, item.image, this.failedSources().get(item.image.id ?? -1));
  }

  /** The viewer only shows originals, so it skips images whose origin is missing or failed. */
  private original(item: ObservationImageDto): string | null {
    const origin = webUrl(item.image?.origin);
    return origin && !(this.failedSources().get(item.image?.id ?? -1) ?? []).includes(origin) ? origin : null;
  }

  private neighbourOf(item: ObservationImageDto | null, step: 1 | -1): ObservationImageDto | null {
    return item && neighbour(this.items(), item, step, (other) => this.original(other) != null);
  }

  step(step: 1 | -1): void {
    const target = this.neighbourOf(this.selected(), step);
    if (!target) return;
    this.lastStep = step;
    this.selected.set(target);
  }

  isExpanded(item: ObservationImageDto): boolean {
    return this.expandedImages().has(item.image?.id ?? -1);
  }

  toggleInfo(item: ObservationImageDto): void {
    const id = item.image?.id ?? -1;
    this.expandedImages.update((expanded) => {
      const next = new Set(expanded);
      if (!next.delete(id)) next.add(id);
      return next;
    });
  }

  /** Only a loaded original is worth enlarging; the stored copy is a 100 × 80 thumbnail. */
  canEnlarge(item: ObservationImageDto, source: string): boolean {
    return source === webUrl(item.image?.origin) && this.loadedSources().has(source);
  }

  loaded(source: string): void {
    this.loadedSources.update((loaded) => new Set(loaded).add(source));
  }

  alt(item: ObservationImageDto): string {
    const species = [item.popularName, item.scientificName].filter(Boolean).join(' ');
    return item.image?.description || species || this.translate.instant('observationDetails.image');
  }

  fail(item: ObservationImageDto, source: string): void {
    const id = item.image?.id ?? -1;
    if ((this.failedSources().get(id) ?? []).includes(source)) return;
    this.logger.warn('Observation image unavailable', 'GalleryView', { observationId: item.observationId, mediaId: id });
    this.failedSources.update((failed) => new Map(failed).set(id, [...(failed.get(id) ?? []), source]));
  }

  /** Moves on in the direction the user was going, so a broken original doesn't end the browsing. */
  viewerFailed(): void {
    const item = this.selected();
    const origin = item && this.original(item);
    if (!item || !origin) return;
    const onward = this.neighbourOf(item, this.lastStep) ?? this.neighbourOf(item, this.lastStep === 1 ? -1 : 1);
    this.fail(item, origin);
    this.selected.set(onward);
  }

  showObservation(item: ObservationImageDto): void {
    if (item.observationId == null) return;
    const { east, north } = item.point ?? {};
    if (east != null && north != null) this.mapView.requestFitPoint(east, north, item.coordinatePrecision);
    void this.router.navigate([], { queryParams: { observationId: item.observationId, occurrenceId: null }, queryParamsHandling: 'merge' });
    this.observationShown.emit();
  }

  onPageChange(page: number): void {
    this.pageNumber.set(page);
  }

  onPageSizeChange(size: number): void {
    this.resultsPerPage.set(size);
  }
}
