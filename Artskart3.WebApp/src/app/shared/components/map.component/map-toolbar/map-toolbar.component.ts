import { Component, Output, EventEmitter, Input } from '@angular/core';
import { NbicMapComponent } from '@artsdatabanken/nbic-map-component';
import { ToolbarAction } from './map-toolbar.constants';

type ActionHandler = () => void;

@Component({
  selector: 'app-map-toolbar',
  standalone: false,
  templateUrl: './map-toolbar.component.html',
  styleUrl: './map-toolbar.component.css',
})
export class MapToolbarComponent {
  @Input() protected map!: NbicMapComponent;
  isGeolocating = false;
  @Input() mapEl!: HTMLDivElement;
  @Output() iconClick = new EventEmitter<string>();

  // Expose actions for template
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

  onButtonClick(iconName: string): void {
    this.handleIconClick(iconName);
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
    if (this.isGeolocating) return;

    this.isGeolocating = true;

    if (!navigator.geolocation) {
      this.resetGeolocationState();
      return;
    }

    navigator.geolocation.getCurrentPosition(
      () => {
        if (this.map) {
          this.map.zoomToGeolocation();
        }
        this.resetGeolocationState();
      },
      (error) => {
        this.resetGeolocationState();
      },
      {
        enableHighAccuracy: false,
        timeout: 1000,
        maximumAge: 300000,
      }
    );
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
      mapContainer.requestFullscreen().catch((err: any) => {
        console.error('Error attempting to enable fullscreen:', err);
      });
    } else {
      document.exitFullscreen().catch((err: any) => {
        console.error('Error attempting to exit fullscreen:', err);
      });
    }
  }
}
