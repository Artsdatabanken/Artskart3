import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslateModule } from '@ngx-translate/core';
import { HeaderComponent } from './components/header/header.component';
import { ResizablePanelComponent } from './components/resizable-panel/resizable-panel.component';
import { MapComponent } from './components/map.component/map.component';
import { MapToolbarComponent } from './components/map.component/map-toolbar/map-toolbar.component';
import { MapTypeSelectorComponent } from './components/map.component/map-toolbar/map-type-selector';

@NgModule({
  imports: [
    CommonModule,
    TranslateModule,
    HeaderComponent,
    ResizablePanelComponent,
    MapComponent,
    MapToolbarComponent,
    MapTypeSelectorComponent,
  ],
  exports: [
    HeaderComponent,
    ResizablePanelComponent,
    MapComponent,
    MapToolbarComponent,
    MapTypeSelectorComponent,
  ],
})
export class SharedModule {}
