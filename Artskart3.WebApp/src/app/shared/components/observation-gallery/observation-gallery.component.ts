import { Component, computed, CUSTOM_ELEMENTS_SCHEMA, inject, input, linkedSignal } from '@angular/core';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { ObservationMediaDto } from '@shared/types/api.types';
import { LoggingService } from '@shared/logging.service';
import { ImageInfoComponent } from '../image-info/image-info.component';
import { ImageViewerComponent } from '../image-viewer/image-viewer.component';
import { imageSource, neighbour, SpeciesNames, ViewerImage, webUrl } from '../image-viewer/image-source';

@Component({
  imports: [TranslateModule, ImageViewerComponent, ImageInfoComponent],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  selector: 'app-observation-gallery',
  styleUrl: './observation-gallery.component.css',
  templateUrl: './observation-gallery.component.html',
})
export class ObservationGalleryComponent {
  readonly observationId = input.required<number>();
  readonly images = input<ObservationMediaDto[]>([]);
  readonly species = input('');
  readonly names = input<SpeciesNames>({});
  private readonly logger = inject(LoggingService);
  private readonly translate = inject(TranslateService);
  readonly selected = linkedSignal<number | null>(() => {
    this.observationId();
    return null;
  });
  readonly failedSources = linkedSignal<Map<number, string[]>>(() => {
    this.images();
    return new Map();
  });
  private readonly loadedSources = linkedSignal<ReadonlySet<string>>(() => {
    this.images();
    return new Set();
  });
  private readonly selectedImage = computed(() => this.images().find((item) => item.id === this.selected()) ?? null);
  readonly viewer = computed<ViewerImage | null>(() => {
    const image = this.selectedImage();
    const src = image && this.original(image);
    if (!image || !src) return null;
    const { popularName, scientificName, author } = this.names();
    return { ...image, src, alt: this.alt(image), popularName, scientificName, author };
  });
  readonly previousImage = computed(() => this.neighbourOf(this.selectedImage(), -1));
  readonly nextImage = computed(() => this.neighbourOf(this.selectedImage(), 1));
  private lastStep: 1 | -1 = 1;

  source(image: ObservationMediaDto): string | null {
    return imageSource(this.observationId(), image, this.failedSources().get(image.id ?? -1));
  }

  /** The viewer only shows originals, so it skips images whose origin is missing or failed. */
  private original(image: ObservationMediaDto): string | null {
    const origin = webUrl(image.origin);
    return origin && !(this.failedSources().get(image.id ?? -1) ?? []).includes(origin) ? origin : null;
  }

  private neighbourOf(image: ObservationMediaDto | null, step: 1 | -1): ObservationMediaDto | null {
    return image && neighbour(this.images(), image, step, (other) => this.original(other) != null);
  }

  step(step: 1 | -1): void {
    const target = this.neighbourOf(this.selectedImage(), step);
    if (!target) return;
    this.lastStep = step;
    this.selected.set(target.id ?? null);
  }

  /** Only a loaded original is worth enlarging; the stored copy is a 100 × 80 thumbnail. */
  canEnlarge(image: ObservationMediaDto, source: string): boolean {
    return source === webUrl(image.origin) && this.loadedSources().has(source);
  }

  loaded(source: string): void {
    this.loadedSources.update((loaded) => new Set(loaded).add(source));
  }

  alt(image: ObservationMediaDto): string {
    return image.description || this.species() || this.translate.instant('observationDetails.image');
  }

  fail(image: ObservationMediaDto, source: string): void {
    const id = image.id ?? -1;
    if ((this.failedSources().get(id) ?? []).includes(source)) return;
    this.logger.warn('Observation image unavailable', 'ObservationGallery', { observationId: this.observationId(), mediaId: id });
    this.failedSources.update((failed) => new Map(failed).set(id, [...(failed.get(id) ?? []), source]));
  }

  /** Moves on in the direction the user was going, so a broken original doesn't end the browsing. */
  viewerFailed(): void {
    const image = this.selectedImage();
    const origin = image && this.original(image);
    if (!image || !origin) return;
    const onward = this.neighbourOf(image, this.lastStep) ?? this.neighbourOf(image, this.lastStep === 1 ? -1 : 1);
    this.fail(image, origin);
    this.selected.set(onward?.id ?? null);
  }
}
