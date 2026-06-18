import { Component, Output, EventEmitter, Input, CUSTOM_ELEMENTS_SCHEMA, inject, signal, OnInit, OnDestroy } from '@angular/core';
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
export class MapToolbarComponent implements OnInit, OnDestroy {
  @Input() public map!: NbicMapComponent;
  @Output() iconClick = new EventEmitter<string>();

  private readonly logger = inject(LoggingService);
  protected readonly toolbarActions = ToolbarAction;
  protected readonly geolocationDenied = signal(false);

  private permissionStatus: PermissionStatus | null = null;

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

  ngOnInit(): void {
    this.queryGeolocationPermission();
  }

  ngOnDestroy(): void {
    if (this.permissionStatus) {
      this.permissionStatus.onchange = null;
    }
  }

  private queryGeolocationPermission(): void {
    if (!navigator.permissions) return;

    navigator.permissions
      .query({ name: 'geolocation' })
      .then((status) => {
        this.permissionStatus = status;

        if (status.state === 'denied') {
          this.geolocationDenied.set(true);
        }

        status.onchange = () => {
          this.geolocationDenied.set(status.state === 'denied');
        };
      })
      .catch(() => {
        // Permissions API not supported for geolocation in this browser
      });
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

  private async geolocation(): Promise<void> {
    if (!this.map) return;

    const permissionGranted = await new Promise<boolean>((resolve) => {
      navigator.geolocation.getCurrentPosition(
        () => resolve(true),
        (error) => {
          if (error.code === 1) {
            // PERMISSION_DENIED
            resolve(false);
          } else {
            // Other errors (timeout, position unavailable) — still allowed
            resolve(true);
          }
        },
        { timeout: 5000, maximumAge: Infinity },
      );
    });

    if (!permissionGranted) {
      this.geolocationDenied.set(true);
      return;
    }

    try {
      await this.map.zoomToGeolocation(14);
    } catch {
      // Map zoom failed but permission was granted — don't disable button
    }
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
