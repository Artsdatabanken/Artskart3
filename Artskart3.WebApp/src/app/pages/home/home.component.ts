import { Component, ChangeDetectionStrategy, CUSTOM_ELEMENTS_SCHEMA, signal, inject } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';
import { switchMap } from 'rxjs';
import { SharedModule } from '../../shared/shared.module';
import { ListViewComponent } from '../../shared/components/list-view/list-view.component';
import { FilterStateService } from '../../shared/services/filter-state/filter-state.service';
import { AreaService } from '../../shared/services/area/area.service';
import { ExportService, ExportJobStatus } from '../../shared/services/export/export.service';
import { ObservationSearchFilter } from '../../shared/types/api.types';

@Component({
  selector: 'app-home',
  imports: [SharedModule, TranslateModule, ListViewComponent],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './home.component.html',
  styleUrl: './home.component.css',
})
export class HomeComponent {
  private readonly filterState = inject(FilterStateService);
  private readonly areaService = inject(AreaService);
  private readonly exportService = inject(ExportService);

  activeTab = signal(0);
  exporting = signal(false);
  // TODO: Fjern når permanent nedlastingsløsning er på plass
  downloading = signal(false);
  downloadingExcel = signal(false);

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
      categoryIds: this.filterState.selectedCategoryIds().length ? this.filterState.selectedCategoryIds() : undefined,
      organizationIds: this.filterState.selectedInstitutionIds().length ? this.filterState.selectedInstitutionIds() : undefined,
      behaviorIds: this.filterState.selectedBehaviorIds().length ? this.filterState.selectedBehaviorIds() : undefined,
      basisOfRecordIds: this.filterState.selectedBasisOfRecordIds().length ? this.filterState.selectedBasisOfRecordIds() : undefined,
      taxonGroupIds: this.filterState.selectedTaxonGroupIds().length ? this.filterState.selectedTaxonGroupIds() : undefined,
      countyIds: countyIds.length ? countyIds : undefined,
      municipalityIds: municipalityIds.length ? municipalityIds : undefined,
      oceanAreaIds: this.filterState.selectedOceanAreaIds().length ? this.filterState.selectedOceanAreaIds() : undefined,
      coordinatePrecision: hasCoordinatePrecision ? { from: coordinatePrecisionFrom, to: coordinatePrecisionTo } : undefined,
      period: hasPeriod ? { from: periodFrom, to: periodTo } : undefined,
    };

    this.exporting.set(true);
    this.exportService.startExport(filter).subscribe({
      next: (response) => {
        this.exporting.set(false);
        alert(`Eksport startet (jobb-ID: ${response.jobId}). Bruk "Hent siste eksport" for å laste ned når den er ferdig.`);
      },
      error: () => {
        this.exporting.set(false);
        alert('Kunne ikke starte eksport. Prøv igjen senere.');
      },
    });
  }

  // TODO: Fjern når permanent nedlastingsløsning er på plass
  onDownloadLatest() {
    if (this.downloading()) return;

    this.downloading.set(true);
    this.exportService.getHistory().pipe(
      switchMap(jobs => {
        const completedJob = jobs.find(j => j.status === ExportJobStatus.Complete); // 2 = Complete
        if (!completedJob) {
          throw new Error('Ingen ferdig eksport funnet.');
        }
        return this.exportService.getDownloadUrl(completedJob.id);
      }),
    ).subscribe({
      next: (response) => {
        this.downloading.set(false);
        window.open(response.url, '_blank');
      },
      error: (err) => {
        this.downloading.set(false);
        alert(err.message || 'Kunne ikke hente eksport.');
      },
    });
  }

  // TODO: Fjern når permanent nedlastingsløsning er på plass
  onDownloadLatestExcel() {
    if (this.downloadingExcel()) return;

    this.downloadingExcel.set(true);
    this.exportService.getHistory().pipe(
      switchMap(jobs => {
        const completedJob = jobs.find(j => j.status === ExportJobStatus.Complete);
        if (!completedJob) {
          throw new Error('Ingen ferdig eksport funnet.');
        }
        return this.exportService.downloadExcel(completedJob.id);
      }),
    ).subscribe({
      next: (blob) => {
        this.downloadingExcel.set(false);
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = 'eksport.xlsx';
        a.click();
        URL.revokeObjectURL(url);
      },
      error: (err) => {
        this.downloadingExcel.set(false);
        alert(err.message || 'Kunne ikke hente eksport.');
      },
    });
  }
}
