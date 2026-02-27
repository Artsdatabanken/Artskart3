import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HeaderComponent } from './components/header/header.component';
import { ResizablePanelComponent } from './components/resizable-panel/resizable-panel.component';

@NgModule({
  imports: [
    CommonModule,
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
