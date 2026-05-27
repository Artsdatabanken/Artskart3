import { Component, Output, EventEmitter, Input, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NbicMapComponent } from '@artsdatabanken/nbic-map-component';
import { ToolbarAction } from './map-toolbar.constants';
import { MapTypeSelectorComponent } from './map-type-selector/map-type-selector.component';

type ActionHandler = () => void;

@Component({
  selector: 'app-map-toolbar',
  standalone: true,
  imports: [CommonModule, MapTypeSelectorComponent],
  templateUrl: './map-toolbar.component.html',
  styleUrl: './map-toolbar.component.css',
})
export class MapToolbarComponent implements OnInit, OnDestroy {
  @Input() public map!: NbicMapComponent;
  isGeolocating = false;
  @Input() mapEl!: HTMLDivElement;

  private cachedPosition: GeolocationPosition | null = null;
  private watchId: number | null = null;
  @Output() iconClick = new EventEmitter<string>();

  protected readonly toolbarActions = ToolbarAction;


  private readonly actionHandlers: Record<ToolbarAction, ActionHandler> = {
    [ToolbarAction.ZOOM_IN]: () => this.zoomIn(),
    [ToolbarAction.ZOOM_OUT]: () => this.zoomOut(),
    [ToolbarAction.GEOLOCATION]: () => this.geolocation(),
    [ToolbarAction.FULLSCREEN]: () => this.toggleFullscreen(),
    [ToolbarAction.MAP]: () => this.emitAction(ToolbarAction.MAP),
    [ToolbarAction.LAYERS]: () => this.emitAction(ToolbarAction.LAYERS),
    [ToolbarAction.FILTER]: () => this.emitAction(ToolbarAction.FILTER),
    [ToolbarAction.POLYGON]: () => this.emitAction(ToolbarAction.POLYGON),
  };

  ngOnInit(): void {
    if (navigator.geolocation) {
      this.watchId = navigator.geolocation.watchPosition(
        (pos) => { this.cachedPosition = pos; },
        // eslint-disable-next-line @typescript-eslint/no-empty-function
        () => {},
        { enableHighAccuracy: false, maximumAge: 300000 }
      );
    }
  }

  ngOnDestroy(): void {
    if (this.watchId !== null) {
      navigator.geolocation.clearWatch(this.watchId);
    }
  }

  onButtonClick(iconName: string): void {
    this.handleIconClick(iconName);
  }

  onMapTypeSelected(layerId: string): void {
    this.iconClick.emit(`map-type:${layerId}`);
  }

  private handleIconClick(actionName: string): void {
    const action = actionName as ToolbarAction;
    const handler = this.actionHandlers[action];

    if (handler) {
      try {
        handler();
      } catch (error) {
        console.error(`Error executing action '${actionName}':`, error);
      }
    } else {
      this.iconClick.emit(actionName);
    }
  }

  private zoomIn(): void {
    if (!this.map) return;
    const { zoom } = this.map.getCamera();
    this.map.setZoom(zoom + 1);
  }

  private zoomOut(): void {
    if (!this.map) return;
    const { zoom } = this.map.getCamera();
    this.map.setZoom(zoom - 1);
  }

  private geolocation(): void {
    if (this.isGeolocating || !this.map) return;

    this.isGeolocating = true;


    if (this.cachedPosition) {
      const { longitude, latitude } = this.cachedPosition.coords;
      const coord = this.map.transformCoordsFrom([longitude, latitude], 'EPSG:4326', 'EPSG:25833');
      this.map.setCenter(coord);
      this.map.setZoom(14);
    } else {
      this.map.zoomToGeolocation();
    }
    setTimeout(() => {
      this.resetGeolocationState();
    }, 100);
  }

  private resetGeolocationState(): void {
    this.isGeolocating = false;
  }

  private emitAction(action: ToolbarAction): void {
    this.iconClick.emit(action);
  }

  private toggleFullscreen(): void {
    const mapContainer = this.mapEl;
    if (!document.fullscreenElement) {
      // eslint-disable-next-line @typescript-eslint/no-unused-vars, @typescript-eslint/no-explicit-any, @typescript-eslint/no-empty-function
      mapContainer.requestFullscreen().catch((err: any) => {
      });
    } else {
      // eslint-disable-next-line @typescript-eslint/no-unused-vars, @typescript-eslint/no-explicit-any, @typescript-eslint/no-empty-function
      document.exitFullscreen().catch((err: any) => {
      });
    }
  }
}
