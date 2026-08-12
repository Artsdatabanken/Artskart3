import { Component, ChangeDetectionStrategy, CUSTOM_ELEMENTS_SCHEMA, signal, computed, inject, DestroyRef, HostListener } from '@angular/core';
import { DOCUMENT } from '@angular/common';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subject, takeUntil } from 'rxjs';
import { SharedModule } from '../../shared/shared.module';
import { ListViewComponent } from '../../shared/components/list-view/list-view.component';
import { SidebarComponent } from '../../shared/components/sidebar/sidebar.component';
import { ModalComponent } from '../../shared/components/modal/modal.component';
import { FilterStateService, imageFilterToWithImages } from '../../shared/services/filter-state/filter-state.service';
import { AreaService } from '../../shared/services/area/area.service';
import { ExportService } from '../../shared/services/export/export.service';
import { AlertService } from '../../shared/services/alert/alert.service';
import { AuthService } from '../../shared/services/auth/auth.service';
import { ObservationSearchFilter } from '../../shared/types/api.types';
import { FormatNumberPipe } from '../../shared/pipes/format-number.pipe';
import { FormatFileSizePipe } from '../../shared/pipes/format-file-size.pipe';
import { FormsModule } from '@angular/forms';

const SKIP_EXPORT_INFO_KEY = 'artskart.export.skipInfoModal';

@Component({
  selector: 'app-home',
  imports: [SharedModule, TranslateModule, ListViewComponent, SidebarComponent, ModalComponent, FormsModule, FormatNumberPipe, FormatFileSizePipe],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './home.component.html',
  styleUrl: './home.component.css',
})
export class HomeComponent {
  private readonly document = inject(DOCUMENT);
  private readonly filterState = inject(FilterStateService);
  private readonly areaService = inject(AreaService);
  private readonly exportService = inject(ExportService);
  protected readonly translate = inject(TranslateService);
  readonly alertService = inject(AlertService);
  readonly authService = inject(AuthService);

  readonly minWidth = this.getCSSVar('--panel-min-width', 300);
  readonly maxWidth = this.getCSSVar('--panel-max-width', 500);
  readonly filterPanelWidth = signal(this.minWidth);

  private readonly HEADER_HEIGHT = 80;
  private readonly HANDLE_HEIGHT = 56;
  private readonly MOBILE_BREAKPOINT = 768;
  readonly isFilterOpen = signal(false);
  readonly isDragging = signal(false);
  private readonly dragTranslatePx = signal<number | null>(null);
  private readonly viewportTick = signal(0);
  private dragStartY = 0;
  private dragStartTranslate = 0;

  readonly sheetTransform = computed(() => {
    this.viewportTick();
    if (!this.isMobileViewport()) {
      return 'none';
    }
    const dragValue = this.dragTranslatePx();
    if (dragValue !== null) {
      return `translateY(${dragValue}px)`;
    }
    return `translateY(${this.isFilterOpen() ? 0 : this.getCollapsedTranslateY()}px)`;
  });

  activeTab = signal(0);
  exporting = signal(false);

  // Modal state
  showNameModal = signal(false);
  showInfoModal = signal(false);
  showLimitModal = signal(false);
  exportName = signal('');
  estimatedFileSizeBytes = signal(0);
  limitTotalRows = signal(0);
  summaryLoading = signal(false);
  limitHardLimit = signal(0);
  dontShowAgain = signal(false);

  private readonly destroyRef = inject(DestroyRef);
  private readonly cancelSummary$ = new Subject<void>();

  onTabChange(event: Event) {
    const customEvent = event as CustomEvent<{ index: number }>;
    this.activeTab.set(customEvent.detail.index);
  }

  onExport() {
    if (this.exporting()) return;
    this.exportName.set('');
    this.showNameModal.set(true);
  }

  onNameModalConfirm() {
    const name = this.exportName().trim();
    if (!name || this.summaryLoading()) return;

    this.summaryLoading.set(true);

    const filter = this.buildFilter();

    this.exportService.getSummary(filter, name).pipe(
      takeUntil(this.cancelSummary$),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe({
      next: (summary) => {
        this.summaryLoading.set(false);
        this.showNameModal.set(false);

        if (summary.exceedsHardLimit) {
          this.limitTotalRows.set(summary.totalRows ?? 0);
          this.limitHardLimit.set(summary.hardLimit ?? 0);
          this.showLimitModal.set(true);
          return;
        }

        this.estimatedFileSizeBytes.set(summary.estimatedFileSizeBytes ?? 0);
        this.exporting.set(true);
        this.startExportWithName(filter, name);
      },
      error: () => {
        this.summaryLoading.set(false);
        this.showNameModal.set(false);
        this.alertService.showError(this.translate.instant('export.summaryFailed'));
      },
    });
  }

  onNameModalCancel() {
    this.cancelSummary$.next();
    this.summaryLoading.set(false);
    this.showNameModal.set(false);
  }

  onInfoModalClose() {
    if (this.dontShowAgain()) {
      localStorage.setItem(SKIP_EXPORT_INFO_KEY, 'true');
    }
    this.showInfoModal.set(false);
    this.dontShowAgain.set(false);
  }

  onLimitModalClose() {
    this.showLimitModal.set(false);
  }

  onFilterPanelResize(newWidth: number) {
    const validatedWidth = Math.max(this.minWidth, Math.min(newWidth, this.maxWidth));
    this.filterPanelWidth.set(validatedWidth);
  }

  toggleFilter(): void {
    this.isFilterOpen.update((open) => !open);
  }

  @HostListener('window:resize')
  onWindowResize(): void {
    this.viewportTick.update((tick) => tick + 1);
  }

  onHandlePointerDown(event: PointerEvent): void {
    this.isDragging.set(true);
    this.dragStartY = event.clientY;
    this.dragStartTranslate = this.isFilterOpen() ? 0 : this.getCollapsedTranslateY();
    this.dragTranslatePx.set(this.dragStartTranslate);

    const handle = event.currentTarget as Element | null;
    handle?.setPointerCapture(event.pointerId);
  }

  onHandlePointerMove(event: PointerEvent): void {
    if (!this.isDragging()) return;
    const collapsedTranslateY = this.getCollapsedTranslateY();
    const deltaY = event.clientY - this.dragStartY;
    const nextTranslateY = Math.min(Math.max(this.dragStartTranslate + deltaY, 0), collapsedTranslateY);
    this.dragTranslatePx.set(nextTranslateY);
  }

  onHandlePointerUp(event: PointerEvent): void {
    if (!this.isDragging()) return;
    this.isDragging.set(false);
    const collapsedTranslateY = this.getCollapsedTranslateY();
    const currentTranslateY = this.dragTranslatePx() ?? this.dragStartTranslate;
    const movedDistance = Math.abs(currentTranslateY - this.dragStartTranslate);

    if (movedDistance < 5) {
      this.toggleFilter();
    } else {
      this.isFilterOpen.set(currentTranslateY < collapsedTranslateY / 2);
    }
    this.dragTranslatePx.set(null);
    const handle = event.currentTarget as Element | null;
    handle?.releasePointerCapture(event.pointerId);
  }

  private getCollapsedTranslateY(): number {
    const viewportHeight = this.document.defaultView?.innerHeight ?? 0;
    return Math.max(viewportHeight - this.HEADER_HEIGHT - this.HANDLE_HEIGHT, 0);
  }

  private isMobileViewport(): boolean {
    return (this.document.defaultView?.innerWidth ?? 0) <= this.MOBILE_BREAKPOINT;
  }

  private startExportWithName(filter: ObservationSearchFilter, name: string): void {
    this.exportService.startExport(filter, name).subscribe({
      next: (response) => {
        this.exporting.set(false);
        this.exportService.trackExport(response.jobId);

        const skipInfo = localStorage.getItem(SKIP_EXPORT_INFO_KEY) === 'true';
        if (!skipInfo) {
          this.showInfoModal.set(true);
        }
      },
      error: () => {
        this.exporting.set(false);
        this.alertService.showError(this.translate.instant('export.startFailed'));
      },
    });
  }

  private buildFilter(): ObservationSearchFilter {
    const { countyIds, municipalityIds } = this.areaService.resolvedAreaFilter();
    const coordinatePrecisionFrom = this.filterState.coordinatePrecisionFrom();
    const coordinatePrecisionTo = this.filterState.coordinatePrecisionTo();
    const periodFrom = this.filterState.periodFrom();
    const periodTo = this.filterState.periodTo();
    const periodMonths = this.filterState.selectedMonths();
    const hasCoordinatePrecision = coordinatePrecisionFrom != null || coordinatePrecisionTo != null;
    const projectName = this.filterState.projectName().trim();
    const collectionCode = this.filterState.collectionCode().trim();
    const catalogNumber = this.filterState.catalogNumber().trim();
    const withImages = imageFilterToWithImages(this.filterState.imageFilter());
    const hasPeriod = periodFrom != null || periodTo != null || periodMonths.length > 0;

    return {
      categoryIds: this.filterState.selectedCategoryIds().length
        ? this.filterState.selectedCategoryIds()
        : undefined,
      organizationIds: this.filterState.selectedInstitutionIds().length
        ? this.filterState.selectedInstitutionIds()
        : undefined,
      behaviorIds: this.filterState.selectedBehaviorIds().length
        ? this.filterState.selectedBehaviorIds()
        : undefined,
      basisOfRecordIds: this.filterState.selectedBasisOfRecordIds().length
        ? this.filterState.selectedBasisOfRecordIds()
        : undefined,
      taxonGroupIds: this.filterState.selectedTaxonGroupIds().length
        ? this.filterState.selectedTaxonGroupIds()
        : undefined,
      countyIds: countyIds.length ? countyIds : undefined,
      municipalityIds: municipalityIds.length ? municipalityIds : undefined,
      oceanAreaIds: this.filterState.selectedOceanAreaIds().length
        ? this.filterState.selectedOceanAreaIds()
        : undefined,
      coordinatePrecision: hasCoordinatePrecision
        ? { from: coordinatePrecisionFrom, to: coordinatePrecisionTo }
        : undefined,

      projectName: projectName ? projectName : undefined,
      collectionCode: collectionCode ? collectionCode : undefined,
      catalogNumber: catalogNumber ? catalogNumber : undefined,
      withImages: withImages,
      period: hasPeriod
        ? { from: periodFrom, to: periodTo, months: periodMonths.length ? periodMonths : undefined }
        : undefined,
    };
  }

  private getCSSVar(name: string, fallback: number): number {
    const value = this.document.documentElement
      ? getComputedStyle(this.document.documentElement).getPropertyValue(name).trim()
      : '';
    return parseInt(value) || fallback;
  }
}
