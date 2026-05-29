import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { CategoryTypeDto } from '../../types/api.types';

@Injectable({
  providedIn: 'root',
})
export class CategoryService {
  private readonly http = inject(HttpClient);
  private readonly endpoint = '/api/Lookup/Categories';

  getCategories(): Observable<CategoryTypeDto[]> {
    return this.http.get<CategoryTypeDto[]>(this.endpoint);
  }
}
