import { Component, signal, OnInit, inject } from '@angular/core';
import { LoggingService } from './shared/logging.service';

@Component({
  selector: 'app-root',
  standalone: false,
  templateUrl: './app.html',
})
export class App implements OnInit {
  private readonly loggingService = inject(LoggingService);

  protected readonly title = signal('artskart3.webapp');

  ngOnInit() {
    this.loggingService.logEvent('App Initialized');
  }
}

