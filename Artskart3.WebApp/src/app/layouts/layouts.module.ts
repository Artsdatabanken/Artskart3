import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { BaseLayoutComponent } from './base-layout/base-layout.component';
import { SharedModule } from '../shared/shared.module';

@NgModule({
  imports: [
    CommonModule,
    SharedModule,
    BaseLayoutComponent,
  ],
  exports: [
    BaseLayoutComponent,
  ],
})
export class LayoutsModule {}
