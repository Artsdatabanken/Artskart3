import {Component, computed, CUSTOM_ELEMENTS_SCHEMA, effect, input, signal} from '@angular/core';
import {ObservationListInfoDto} from '@shared/types/api.types';
import {TranslateModule} from '@ngx-translate/core';

type ObservationFilterKey = 'locality' | 'taxonGroupId' | 'categoryId';

type ObservationFilter = {
  groupKey: string;
  observations: ObservationListInfoDto[];
}

@Component({
  selector: 'app-observation-list',
  imports: [TranslateModule],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  templateUrl: './observation-list.html',
  styleUrl: './observation-list.css',
})
export class ObservationList {
  private localityNumbers = new Map<string, number>();
  observationList = input<ObservationListInfoDto[]>([]);
  filterByInput = input<ObservationFilterKey>('locality');

  filterBy = signal<ObservationFilterKey>('locality');
  filterByOptions: ObservationFilterKey[] = ['locality', 'taxonGroupId', 'categoryId'];

  constructor() {
    effect(() => {
      this.filterBy.set(this.filterByInput());
    });
  }

  public setFilter(filter: ObservationFilterKey) {
    this.filterBy.set(filter);
  }

  private groupValue(observation: ObservationListInfoDto): string {
    switch (this.filterBy()) {
      case "categoryId":
        return observation.categoryName ?? 'Ukjent'
      case "locality":
        const locality = observation.locality ?? 'Ukjent';
        const localityIndex = this.localityNumbers.get(locality);
        if (localityIndex !== undefined) {
          return `location ${localityIndex}`;
        }

        const nextIndex = this.localityNumbers.size + 1;
        this.localityNumbers.set(locality, nextIndex);
        return `location ${nextIndex}`;
      case "taxonGroupId":
        return observation.taxonGroupName ?? 'Ukjent'
      default:
        return observation.locality ?? 'Ukjent';
    }
  }

  groupedObservations = computed<ObservationFilter[]>(() => {
    const observationGroups = new Map<string, ObservationListInfoDto[]>();
    this.localityNumbers = new Map<string, number>();
    for (const observation of this.observationList()) {
      const groupKey = this.groupValue(observation);
      const groupList = observationGroups.get(groupKey) ?? [];
      groupList.push(observation);
      observationGroups.set(groupKey, groupList);
    }

    return Array.from(observationGroups.entries()).map(([groupKey, observations]) => ({
      groupKey,
      observations
    }));
  });
}
