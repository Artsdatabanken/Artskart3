import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, shareReplay } from 'rxjs';
import { BasisOfRecordDto } from '../../types/api.types';

@Injectable({
  providedIn: 'root',
})
export class BasisOfRecordService {
  private readonly http = inject(HttpClient);
  private readonly endpoint = '/api/Lookup/BasisOfRecords';
  private readonly basisOfRecords$ = this.http.get<BasisOfRecordDto[]>(this.endpoint).pipe(shareReplay(1));

  getBasisOfRecords(): Observable<BasisOfRecordDto[]> {
    return this.basisOfRecords$;
  }
}

export function basisOfRecordDisplayName(basisOfRecord: BasisOfRecordDto, translate: (key: string) => string): string {
  if (!basisOfRecord.name) return basisOfRecord.description ?? '';
  const key = 'sidebar.basisOfRecordName.' + basisOfRecord.name;
  const translated = translate(key);
  return translated !== key ? translated : (basisOfRecord.description ?? basisOfRecord.name);
}
