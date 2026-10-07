import { Component, computed, inject, input, linkedSignal } from '@angular/core';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { ObservationMediaDto } from '@shared/types/api.types';
import { LoggingService } from '@shared/logging.service';
import { ImageViewerComponent } from '../image-viewer/image-viewer.component';
import { ViewerImage, webUrl } from '../image-viewer/image-source';

@Component({
  imports: [TranslateModule, ImageViewerComponent],
  selector: 'app-observation-gallery',
  styleUrl: './observation-gallery.component.css',
  templateUrl: './observation-gallery.component.html',
})
export class ObservationGalleryComponent {
  readonly observationId = input.required<number>();
  readonly images = input<ObservationMediaDto[]>([]);
  readonly species = input('');
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
  readonly viewer = computed<ViewerImage | null>(() => {
    const image = this.images().find((item) => item.id === this.selected());
    const src = image && this.source(image);
    return image && src ? { ...image, src, alt: this.alt(image) } : null;
  });
  readonly webUrl = webUrl;

  source(image: ObservationMediaDto): string | null {
    const failed = this.failedSources().get(image.id ?? -1) ?? [];
    const origin = webUrl(image.origin);
    if (origin && !failed.includes(origin)) return origin;
    const fallback = `/api/observations/${this.observationId()}/media/${image.id}/image`;
    return image.id != null && image.hasStoredImage && !failed.includes(fallback) ? fallback : null;
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

  viewerFailed(): void {
    const image = this.images().find((item) => item.id === this.selected());
    const source = image && this.source(image);
    if (image && source) this.fail(image, source);
  }
}
