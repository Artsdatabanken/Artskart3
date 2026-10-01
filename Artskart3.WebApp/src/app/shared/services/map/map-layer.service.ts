import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClientService } from '@core/services/api-client.service';
import { MapLayerDto } from '../../types/map-layer.types';

@Injectable({ providedIn: 'root' })
export class MapLayerService {
  private readonly apiClient = inject(ApiClientService);

  getMapLayers(): Observable<MapLayerDto[]> {
    return this.apiClient.fetchJson<MapLayerDto[]>('/api/MapLayer');
  }
}
