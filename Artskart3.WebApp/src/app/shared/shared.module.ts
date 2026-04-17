import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslateModule } from '@ngx-translate/core';
import { HeaderComponent } from './components/header/header.component';
import { ResizablePanelComponent } from './components/resizable-panel/resizable-panel.component';
import { LanguageDropdown } from './components/language-dropdown/language-dropdown';

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

  
    LanguageDropdown
  ],
})
export class SharedModule {}
