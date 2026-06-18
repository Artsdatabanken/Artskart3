import { Component, Output, EventEmitter, Input, CUSTOM_ELEMENTS_SCHEMA, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslateModule } from '@ngx-translate/core';
import { NbicMapComponent } from '@artsdatabanken/nbic-map-component';
import { ToolbarAction } from './map-toolbar.constants';
import { MapTypeSelectorComponent } from './map-type-selector/map-type-selector.component';
import { LoggingService } from '@shared/logging.service';

type ActionHandler = () => void;

@Component({
  selector: 'app-map-toolbar',
  standalone: true,
  imports: [CommonModule, TranslateModule, MapTypeSelectorComponent],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  templateUrl: './map-toolbar.component.html',
  styleUrl: './map-toolbar.component.css',
})
export class MapToolbarComponent {
  @Input() public map!: NbicMapComponent;
  @Output() iconClick = new EventEmitter<string>();

  private readonly logger = inject(LoggingService);
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

  onMapTypeSelected(layerId: string): void {
    this.iconClick.emit(`map-type:${layerId}`);
  }

  private handleIconClick(actionName: string): void {
    const action = actionName as ToolbarAction;
    const handler = this.actionHandlers[action];

    if (handler) {
      try {
        handler();
      } catch (error: unknown) {
        this.logger.error(`Error executing action '${actionName}':`, 'MapToolbar', error);
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
    if (!this.map) return;
    this.map.zoomToGeolocation(14);
  }

  private emitAction(action: ToolbarAction): void {
    this.iconClick.emit(action);
  }

  private toggleFullscreen(): void {
    if (!this.map) return;
    if (!document.fullscreenElement) {
      this.map.enterFullScreen();
    } else {
      this.map.leaveFullScreen();
    }
  }
}
