import {Component, computed, CUSTOM_ELEMENTS_SCHEMA, effect, input, signal} from '@angular/core';
import {ObservationListInfoDto} from '@shared/types/api.types';
import {TranslateModule} from '@ngx-translate/core';

enum Filters {
  TaxonGroup = "TaxonGroup",
  Category = "Category",
  Location= "Location"
}

type TopLevelFilter = {
  groupKeyId: number,
  registrationTypes: RegistrationTypeGroup[]
};

type RegistrationTypeGroup = {
  registrationKeyId: string,
  species: SpeciesGroup[]
};

type SpeciesGroup = {
  speciesKeyId: string,
  registrations: string[]
};

@Component({
  selector: 'app-observation-list',
  imports: [TranslateModule],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  templateUrl: './observation-list.component.html',
  styleUrl: './observation-list.component.css',
})
export class ObservationListComponent {
  observationList = input<ObservationListInfoDto[]>([]);
  currentFilter: Filters = Filters.TaxonGroup;
  topLevelFilter = signal<TopLevelFilter[]>([]);

  ngOnInit() {
    this.topLevelFilter.set(this.getTopLevelGroups(this.observationList()));
  }

  public getTopLevelGroups(observationList: ObservationListInfoDto[]) {
    const topLevelMap = new Map<number, Map<string, Map<string, string[]>>>();

    for (const obs of observationList) {
      const topKey = this.getFilterKey(obs);
      const regType = this.getRegistrationType(obs);
      const speciesName = (obs.displayName ?? "Ukjent");
      const registration = obs.identifiedBy ?? '';

      let regTypeMap = topLevelMap.get(topKey);
      if(!regTypeMap) {
        regTypeMap = new Map<string, Map<string,string[]>>();
        topLevelMap.set(topKey, regTypeMap);
      }

      let speciesMap = regTypeMap.get(regType);
      if(!speciesMap) {
        speciesMap = new Map<string, string[]>();
        regTypeMap.set(regType, speciesMap);
      }

      const regs = speciesMap.get(speciesName) ?? [];
      regs.push(registration);
      speciesMap.set(speciesName, regs);
    }

    const result: TopLevelFilter[] = Array.from(topLevelMap.entries()).map(([groupKeyId, regTypeMap]) => ({
      groupKeyId,
      registrationTypes: Array.from(regTypeMap.entries()).map(([registrationKeyId, speciesMap]) => ({
        registrationKeyId,
        species: Array.from(speciesMap.entries()).map(([speciesKeyId, registrations]) => ({
          speciesKeyId,
          registrations: registrations.map(r => String(r))
        }))
      }))
    }));
    console.log(result);
    return result;
  }

  private getRegistrationType(observation: ObservationListInfoDto): string {
    switch (observation.registrationType) {
      case observation.registrationType?.includes("Absent"):
        return "Absent";
      case observation.registrationType?.includes("NotRecovered"):
        return "NotRecovered";
      default:
        return "Found";
    }
  }

  private getFilterKey(observation: ObservationListInfoDto): number {
    if (!observation) return -1;
    switch (this.currentFilter) {
      case Filters.TaxonGroup:
        return observation.taxonGroupId ?? -1;
      case Filters.Category:
        return observation.categoryId ?? -1;
      case Filters.Location:
        return observation.locationId ?? -1;
      default:
        return -1;
    }
  }
}
