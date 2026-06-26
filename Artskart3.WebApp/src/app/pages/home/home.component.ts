import { Component, ChangeDetectionStrategy, CUSTOM_ELEMENTS_SCHEMA, signal, inject } from '@angular/core';
import { DOCUMENT } from '@angular/common';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { SharedModule } from '../../shared/shared.module';
import { ListViewComponent } from '../../shared/components/list-view/list-view.component';
import { SidebarComponent } from '../../shared/components/sidebar/sidebar.component';
import { FilterStateService } from '../../shared/services/filter-state/filter-state.service';
import { AreaService } from '../../shared/services/area/area.service';
import { ExportService } from '../../shared/services/export/export.service';
import { AlertService } from '../../shared/services/alert/alert.service';
import { AuthService } from '../../shared/services/auth/auth.service';
import { ObservationSearchFilter } from '../../shared/types/api.types';

@Component({
  selector: 'app-home',
  imports: [SharedModule, TranslateModule, ListViewComponent, SidebarComponent],
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
  private readonly translate = inject(TranslateService);
  readonly alertService = inject(AlertService);
  readonly authService = inject(AuthService);

  readonly minWidth = this.getCSSVar('--panel-min-width', 300);
  readonly maxWidth = this.getCSSVar('--panel-max-width', 500);
  readonly filterPanelWidth = signal(this.minWidth);

  activeTab = signal(0);
  exporting = signal(false);

  onTabChange(event: Event) {
    const customEvent = event as CustomEvent<{ index: number }>;
    this.activeTab.set(customEvent.detail.index);
  }

  onExport() {
    if (this.exporting()) return;

    const { countyIds, municipalityIds } = this.areaService.resolvedAreaFilter();
    const coordinatePrecisionFrom = this.filterState.coordinatePrecisionFrom();
    const coordinatePrecisionTo = this.filterState.coordinatePrecisionTo();
    const periodFrom = this.filterState.periodFrom();
    const periodTo = this.filterState.periodTo();
    const hasCoordinatePrecision = coordinatePrecisionFrom != null || coordinatePrecisionTo != null;
    const hasPeriod = periodFrom != null || periodTo != null;

    const filter: ObservationSearchFilter = {
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
      period: hasPeriod ? { from: periodFrom, to: periodTo } : undefined,
    };

    this.exporting.set(true);
    this.exportService.startExport(filter).subscribe({
      next: (response) => {
        this.exporting.set(false);
        this.alertService.showInfo(
          this.translate.instant('export.started', { jobId: response.jobId }),
        );
        this.exportService.trackExport(response.jobId);
      },
      error: () => {
        this.exporting.set(false);
        this.alertService.showError(this.translate.instant('export.startFailed'));
      },
    });
  }

  onFilterPanelResize(newWidth: number) {
    const validatedWidth = Math.max(this.minWidth, Math.min(newWidth, this.maxWidth));
    this.filterPanelWidth.set(validatedWidth);
  }

  private getCSSVar(name: string, fallback: number): number {
    const value = this.document.documentElement
      ? getComputedStyle(this.document.documentElement).getPropertyValue(name).trim()
      : '';
    return parseInt(value) || fallback;
  }
}
