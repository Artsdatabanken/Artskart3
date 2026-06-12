import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ObservationSearchFilter } from '../../types/api.types';

export interface StartExportRequest {
  filter: ObservationSearchFilter;
  selectedColumns: string[];
}

export interface StartExportResponse {
  jobId: number;
}

export const ExportJobStatus = {
  Pending: 0,
  Processing: 1,
  Complete: 2,
  Failed: 3,
  Cancelled: 4,
} as const;

export interface CsvExportJobStatus {
  id: number;
  status: number;
  totalRows: number;
  rowsProcessed: number;
  fileSize: number;
  hasExcel: boolean;
  createdAt: string;
  startedAt: string | null;
  completedAt: string | null;
  expiresAt: string | null;
  errorMessage: string | null;
}

@Injectable({
  providedIn: 'root',
})
export class ExportService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/export/csv';

  startExport(filter: ObservationSearchFilter): Observable<StartExportResponse> {
    const request: StartExportRequest = {
      filter,
      selectedColumns: [],
    };
    return this.http.post<StartExportResponse>(`${this.baseUrl}/start`, request);
  }

  // TODO: Fjern når permanent nedlastingsløsning er på plass
  getHistory(): Observable<CsvExportJobStatus[]> {
    return this.http.get<CsvExportJobStatus[]>(`${this.baseUrl}/history`);
  }

  // TODO: Fjern når permanent nedlastingsløsning er på plass
  getDownloadUrl(jobId: number): Observable<{ url: string }> {
    return this.http.get<{ url: string }>(`${this.baseUrl}/${jobId}/download`);
  }

  // TODO: Fjern når permanent nedlastingsløsning er på plass
  getExcelDownloadUrl(jobId: number): Observable<{ url: string }> {
    return this.http.get<{ url: string }>(`${this.baseUrl}/${jobId}/download/excel`);
  }
}
