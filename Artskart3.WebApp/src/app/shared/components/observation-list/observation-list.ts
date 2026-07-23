import {Component, computed, CUSTOM_ELEMENTS_SCHEMA, input} from '@angular/core';
import {ObservationDto} from '@shared/types/api.types';
import {TranslateModule} from '@ngx-translate/core';

type ObservationGroup = {
  locationId: string;
  observations: ObservationDto[];
}

@Component({
  selector: 'app-observation-list',
  imports: [TranslateModule],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  templateUrl: './observation-list.html',
  styleUrl: './observation-list.css',
})
export class ObservationList {
  observationList = input<ObservationDto[]>([]);
  groupedObservations = computed<ObservationGroup[]>(() => {
    const observationGroups = new Map<string, ObservationDto[]>();
    for (const observation of this.observationList()) {
      const locationId = observation.locality ?? 'unknown';
      const groupList = observationGroups.get(locationId) ?? [];
      groupList.push(observation);
      observationGroups.set(locationId, groupList);
    }

    return Array.from(observationGroups.entries()).map(([locationId, observations]) => ({
      locationId,
      observations
    }));
  });
}
