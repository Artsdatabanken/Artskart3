import {Component, computed, CUSTOM_ELEMENTS_SCHEMA, effect, input, signal} from '@angular/core';
import {ObservationDto} from '@shared/types/api.types';
import {TranslateModule} from '@ngx-translate/core';

type ObservationFilterKey = 'locality' | 'taxonGroupId' | 'categoryId';

type ObservationFilter = {
  groupKey: string;
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

  private groupValue(observation: ObservationDto): string {
    switch (this.filterBy()) {
      case "categoryId":
        return String(observation.categoryId ?? 'unknown')
      case "locality":
        return String(observation.locality ?? 'unknown')
      case "taxonGroupId":
        return String(observation.taxonGroupId ?? 'unknown')
      default:
        return observation.locality ?? 'unknown';
    }
  }

  groupedObservations = computed<ObservationFilter[]>(() => {
    const observationGroups = new Map<string, ObservationDto[]>();
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
