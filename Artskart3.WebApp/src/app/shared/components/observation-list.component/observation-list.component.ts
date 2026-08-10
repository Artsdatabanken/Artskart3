import {Component, computed, CUSTOM_ELEMENTS_SCHEMA, effect, input, signal} from '@angular/core';
import {ObservationListInfoDto} from '@shared/types/api.types';
import {TranslateModule} from '@ngx-translate/core';

type ObservationFilterKey =  'Artsgruppe' | 'Kategori' | 'Lokasjon';

type ObservationFilter = {
  groupKey: string;
  observations: ObservationListInfoDto[];
}

@Component({
  selector: 'app-observation-list',
  imports: [TranslateModule],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  templateUrl: './observation-list.component.html',
  styleUrl: './observation-list.component.css',
})
export class ObservationListComponent {
  private localityNumbers = new Map<string, number>();
  observationList = input<ObservationListInfoDto[]>([]);
  filterByInput = input<ObservationFilterKey>('Artsgruppe');

  filterBy = signal<ObservationFilterKey>('Artsgruppe');
  filterByOptions: ObservationFilterKey[] = ['Artsgruppe', 'Lokasjon', 'Kategori'];

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
      case "Kategori":
        return observation.categoryName ?? 'Ukjent'
      case "Lokasjon":
        const locality = observation.locality ?? 'Ukjent';
        const localityIndex = this.localityNumbers.get(locality);
        if (localityIndex !== undefined) {
          return `location ${localityIndex}`;
        }

        const nextIndex = this.localityNumbers.size + 1;
        this.localityNumbers.set(locality, nextIndex);
        return `location ${nextIndex}`;
      case "Artsgruppe":
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
