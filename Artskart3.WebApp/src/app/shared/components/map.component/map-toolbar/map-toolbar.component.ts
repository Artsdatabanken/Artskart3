import { Component, CUSTOM_ELEMENTS_SCHEMA, inject, output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslateModule } from '@ngx-translate/core';
import { ToolbarAction } from './map-toolbar.constants';
import { MapTypeSelectorComponent } from './map-type-selector/map-type-selector.component';
import { LoggingService } from '@shared/logging.service';

type ActionHandler = () => void;

@Component({
  selector: 'app-map-toolbar',
  imports: [CommonModule, TranslateModule, MapTypeSelectorComponent],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  templateUrl: './map-toolbar.component.html',
  styleUrl: './map-toolbar.component.css',
})
export class MapToolbarComponent {
  readonly iconClick = output<string>();

  private readonly logger = inject(LoggingService);
  protected readonly toolbarActions = ToolbarAction;

  private readonly actionHandlers: Record<ToolbarAction, ActionHandler> = {
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

  private emitAction(action: ToolbarAction): void {
    this.iconClick.emit(action);
  }
}
