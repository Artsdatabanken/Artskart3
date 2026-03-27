import { Component, signal, OnInit } from '@angular/core';
import { LoggingService } from './shared/logging.service';
import { HttpClient } from '@angular/common/http';

@Component({
  selector: 'app-root',
  standalone: false,
  templateUrl: './app.html',
})
export class App implements OnInit {
  constructor(private loggingService: LoggingService, private http: HttpClient) {

  }

  ngOnInit() {
    this.loggingService.logEvent('App Initialized');
  }

  protected readonly title = signal('artskart3.client');
}

