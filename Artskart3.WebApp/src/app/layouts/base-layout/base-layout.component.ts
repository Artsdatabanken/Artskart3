import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { AlertComponent } from '../../shared/components/alert/alert.component';
import { FeedbackPanelComponent } from '../../shared/components/feedback-panel/feedback-panel.component';
import { HeaderComponent } from '../../shared/components/header/header.component';
import { NotificationsComponent } from '../../shared/components/notifications/notifications.component';

@Component({
  selector: 'app-base-layout',
  imports: [
    RouterOutlet,
    AlertComponent,
    FeedbackPanelComponent,
    HeaderComponent,
    NotificationsComponent,
  ],
  templateUrl: './base-layout.component.html',
  styleUrls: ['./base-layout.component.css'],
})
export class BaseLayoutComponent {}
