import { HttpClient } from '@angular/common/http';
import { Component, signal } from '@angular/core';

@Component({
  selector: 'app-root',
  standalone: false,
  templateUrl: './app.html',
})
export class App {

  constructor(private http: HttpClient) {}

  

  protected readonly title = signal('artskart3.client');
}
