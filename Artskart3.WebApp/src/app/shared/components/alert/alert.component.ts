import '@artsdatabanken/components';
import { ChangeDetectionStrategy, Component, CUSTOM_ELEMENTS_SCHEMA, inject } from '@angular/core';
import { AlertService } from '../../services/alert/alert.service';

@Component({
  selector: 'app-alert',
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './alert.component.html',
  styleUrl: './alert.component.css',
})
export class AlertComponent {
  protected readonly alertService = inject(AlertService);

  protected formatDateRange(startDate?: string, endDate?: string): string {
    if (startDate && endDate && startDate === endDate) return startDate;
    if (startDate && endDate) return `${startDate} - ${endDate}`;
    if (startDate) return `From ${startDate}`;
    if (endDate) return `Until ${endDate}`;
    return '';
  }
}
