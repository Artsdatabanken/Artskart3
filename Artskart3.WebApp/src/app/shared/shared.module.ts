import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslateModule } from '@ngx-translate/core';
import { AlertComponent } from './components/alert/alert.component';
import { HeaderComponent } from './components/header/header.component';
import { ResizablePanelComponent } from './components/resizable-panel/resizable-panel.component';
import { MapComponent } from './components/map.component/map.component';
import { MapToolbarComponent } from './components/map.component/map-toolbar/map-toolbar.component';
import { MapTypeSelectorComponent } from './components/map.component/map-toolbar/map-type-selector';
import { ObservationList } from './components/observation-list/observation-list';

@NgModule({
  imports: [
    CommonModule,
    TranslateModule,
    AlertComponent,
    HeaderComponent,
    ResizablePanelComponent,
    MapComponent,
    MapToolbarComponent,
    ObservationList,
    MapTypeSelectorComponent,
  ],
  exports: [
    AlertComponent,
    HeaderComponent,
    ResizablePanelComponent,
    MapComponent,
    MapToolbarComponent,
    ObservationList,
    MapTypeSelectorComponent,
  ],
})
export class SharedModule {}
