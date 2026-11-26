import { Service, inject } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';
import { switchMap } from 'rxjs';
import { AlertService } from '../alert/alert.service';
import { MapViewService } from '../map-view/map-view.service';
import { SearchFilterService } from '../search-filter/search-filter.service';
import { EXTENT_LINK_PARAM, parseExtent, parseFilterLink } from '../../utils/filter-link';

/** Applies `?filter=<json>&extent=<minX,minY,maxX,maxY>` links to the filter panel and the map. */
@Service()
export class FilterLinkService {
  private readonly searchFilter = inject(SearchFilterService);
  private readonly mapView = inject(MapViewService);
  private readonly alertService = inject(AlertService);
  private readonly translate = inject(TranslateService);

  /** Warnings about the current link, which the next link replaces. */
  private warnings: number[] = [];
  /** What the Artskart 2 guard left out of the `?filter=` link it redirected to; null for a link opened directly. */
  private carried: string[] | null = null;

  /** Starts a new link: warnings about the previous one no longer apply. */
  startLink(): void {
    this.warnings.forEach((id) => this.alertService.dismiss(id));
    this.warnings = [];
    this.carried = null;
  }

  /** Hands what a translated link left out to the `?filter=` link it becomes, so the reader gets one warning. */
  carry(dropped: string[]): void {
    this.carried = dropped;
  }

  async apply(filterJson: string, extentValue: string | null): Promise<void> {
    const carried = this.carried;
    if (carried === null) this.startLink();
    this.carried = null;

    const link = parseFilterLink(filterJson);
    if (!link) {
      this.warnUnreadable();
      return;
    }

    await this.searchFilter.applyFilter(link.filter);

    const dropped = [...(carried ?? []), ...link.dropped];
    if (extentValue !== null) {
      const extent = parseExtent(extentValue);
      if (extent) {
        this.mapView.requestFit(extent);
      } else {
        dropped.push(EXTENT_LINK_PARAM);
      }
    }
    this.warnAboutDropped(dropped);
  }

  /**
   * Names the parts of a link that weren't fully applied, so the reader knows the results may differ. Known
   * fields get the name the sidebar uses (Artskart 2's for its links). Unknown fields keep their name, which
   * helps whoever built the link.
   */
  warnAboutDropped(names: string[]): void {
    if (names.length === 0) return;
    const keys = names.map((name) => `filterLink.fields.${name}`);
    this.translate
      .get(keys)
      .pipe(
        switchMap((labels: Record<string, unknown>) => {
          const readable = names.map((name, i) => (typeof labels[keys[i]] === 'string' && labels[keys[i]] !== keys[i] ? labels[keys[i]] : name));
          return this.translate.get('filterLink.dropped', { names: [...new Set(readable)].join(', ') });
        }),
      )
      .subscribe((message: string) => this.show(message));
  }

  warnUnreadable(): void {
    this.warn('filterLink.unreadable');
  }

  // Translations may still be loading when a link opens the app, so this waits for them.
  warn(key: string, params?: Record<string, string>): void {
    this.translate.get(key, params).subscribe((message: string) => this.show(message));
  }

  private show(message: string): void {
    this.warnings.push(this.alertService.showWarning(message, { autoDismissMs: 0 }));
  }
}
