import { Component, ChangeDetectionStrategy, inject, signal } from '@angular/core';
import { DOCUMENT } from '@angular/common';
import { CommonModule } from '@angular/common';
import { RouterOutlet } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { SharedModule } from '../../shared/shared.module';
import { SidebarComponent } from '../../shared/components/sidebar/sidebar.component';

@Component({
  selector: 'app-base-layout',
  imports: [
    CommonModule,
    RouterOutlet,
    SharedModule,
    TranslateModule,
    SidebarComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './base-layout.component.html',
  styleUrls: ['./base-layout.component.css'],
})
export class BaseLayoutComponent {
  private readonly document = inject(DOCUMENT);

  readonly minWidth = this.getCSSVar('--panel-min-width', 300);
  readonly maxWidth = this.getCSSVar('--panel-max-width', 500);
  readonly filterPanelWidth = signal(this.minWidth);
  readonly showFilterPanel = signal(true);

  toggleFilterPanel() {
    this.showFilterPanel.update((val) => !val);
  }

  onFilterPanelResize(newWidth: number) {
    const validatedWidth = Math.max(this.minWidth, Math.min(newWidth, this.maxWidth));
    this.filterPanelWidth.set(validatedWidth);
  }

  private getCSSVar(name: string, fallback: number): number {
    const value = this.document.documentElement
      ? getComputedStyle(this.document.documentElement).getPropertyValue(name).trim()
      : '';
    return parseInt(value) || fallback;
  }
}
