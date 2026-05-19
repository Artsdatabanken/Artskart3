import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterOutlet } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { SharedModule } from '../../shared/shared.module';

@Component({
  selector: 'app-base-layout',
  standalone: true,
  imports: [
    CommonModule,
    RouterOutlet,
    SharedModule,
    TranslateModule,
  ],
  templateUrl: './base-layout.component.html',
  styleUrls: ['./base-layout.component.css'],
})
export class BaseLayoutComponent {
  minWidth = this.getPanelMinWidth();
  maxWidth = this.getPanelMaxWidth();
  filterPanelWidth = signal(this.minWidth);
  showFilterPanel = signal(true);

  private getPanelMinWidth(): number {
    const value = getComputedStyle(document.documentElement).getPropertyValue('--panel-min-width').trim();
    return parseInt(value) || 280;
  }

  private getPanelMaxWidth(): number {
    const value = getComputedStyle(document.documentElement).getPropertyValue('--panel-max-width').trim();
    return parseInt(value) || 500;
  }

  toggleFilterPanel() {
    this.showFilterPanel.update((val) => !val);
  }

  onFilterPanelResize(newWidth: number) {
    const validatedWidth = Math.max(this.minWidth, Math.min(newWidth, this.maxWidth));
    this.filterPanelWidth.set(validatedWidth);
  }
}
