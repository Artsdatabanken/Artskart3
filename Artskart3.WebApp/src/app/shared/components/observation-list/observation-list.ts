import {Component, CUSTOM_ELEMENTS_SCHEMA, input} from '@angular/core';
import {ObservationDto} from '@shared/types/api.types';
import {TranslateModule} from '@ngx-translate/core';

@Component({
  selector: 'app-observation-list',
  imports: [TranslateModule],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  templateUrl: './observation-list.html',
  styleUrl: './observation-list.css',
})
export class ObservationList {
  observationList = input<ObservationDto[]>([]);
}
