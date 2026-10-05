import { Component, signal, OnInit, inject } from '@angular/core';
import { LoggingService } from './shared/logging.service';
import { BaseLayoutComponent } from './layouts/base-layout/base-layout.component';

@Component({
  selector: 'app-root',
  imports: [BaseLayoutComponent],
  templateUrl: './app.html',
})
export class App implements OnInit {
  private readonly loggingService = inject(LoggingService);

  protected readonly title = signal('artskart3.webapp');

  ngOnInit() {
    this.loggingService.logEvent('App Initialized');
  }
}

