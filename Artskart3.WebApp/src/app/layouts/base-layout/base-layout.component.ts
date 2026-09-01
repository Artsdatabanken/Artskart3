import { Component, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterOutlet } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { SharedModule } from '../../shared/shared.module';

@Component({
  selector: 'app-base-layout',
  imports: [
    CommonModule,
    RouterOutlet,
    SharedModule,
    TranslateModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './base-layout.component.html',
  styleUrls: ['./base-layout.component.css'],
})
export class BaseLayoutComponent {}
