import {Component, input} from '@angular/core';
import {ObservationDto} from '@shared/types/api.types';

@Component({
  selector: 'app-observation-list',
  templateUrl: './observation-list.html',
  styleUrl: './observation-list.css',
})
export class ObservationList {
  observationList = input<ObservationDto[]>();
}
