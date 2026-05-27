import { Component, ChangeDetectionStrategy, CUSTOM_ELEMENTS_SCHEMA, signal } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';
import { SharedModule } from '../../shared/shared.module';
import { ListViewComponent } from '../../shared/components/list-view/list-view.component';

@Component({
  selector: 'app-home',
  imports: [SharedModule, TranslateModule, ListViewComponent],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './home.component.html',
  styleUrl: './home.component.css',
})
export class HomeComponent {
  activeTab = signal(0);

  onTabChange(event: Event) {
    const customEvent = event as CustomEvent<{ index: number }>;
    this.activeTab.set(customEvent.detail.index);
  }
}
