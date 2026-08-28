import '@artsdatabanken/components';
import { ChangeDetectionStrategy, Component, CUSTOM_ELEMENTS_SCHEMA, inject } from '@angular/core';
import { Router } from '@angular/router';
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
  private readonly router = inject(Router);

  onLinkClick(event: Event, route: string, alertId: number): void {
    event.preventDefault();
    this.alertService.dismiss(alertId);
    this.router.navigateByUrl(route);
  }

  protected formatDateRange(startDate?: string, endDate?: string): string {
  if (!startDate && !endDate) return '';

  const options: Intl.DateTimeFormatOptions = {
    day: '2-digit',
    month: 'long',        // Full month name: "juli" instead of "07"
    year: 'numeric'
  };

  const norwegianFormatter = new Intl.DateTimeFormat('no-NO', options);

  const start = startDate ? norwegianFormatter.format(new Date(startDate)) : '';
  const end = endDate ? norwegianFormatter.format(new Date(endDate)) : '';

  return start && end ? `${start} – ${end}` : (start || end || '');
  }
}
