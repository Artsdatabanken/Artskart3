import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslateModule } from '@ngx-translate/core';
import { HeaderComponent } from './components/header/header.component';
import { ResizablePanelComponent } from './components/resizable-panel/resizable-panel.component';

@NgModule({
  imports: [
    CommonModule,
    TranslateModule,
    HeaderComponent,
    ResizablePanelComponent,
  ],
  exports: [
    HeaderComponent,
    ResizablePanelComponent],
  declarations: [

  ],
})
export class SharedModule {}
