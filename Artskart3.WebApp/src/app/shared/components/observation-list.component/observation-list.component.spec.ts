import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TranslateModule } from "@ngx-translate/core";
import {Filters, ObservationListComponent} from './observation-list.component';

describe('ObservationList', () => {
  let component: ObservationListComponent;
  let fixture: ComponentFixture<ObservationListComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ObservationListComponent, TranslateModule.forRoot()]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ObservationListComponent);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should group registrations by taxon group', () => {
    fixture.componentRef.setInput('observationList', [
      {
        taxonGroupName: "Fugler",
        displayName: "Kråke",
        categoryName: "Livskraftig",
        locationId: "47",
        registrationType: ["Funnet"],
        collector: "Collector 1"
      },
      {
        taxonGroupName: "Tovinger",
        displayName: "Humle",
        categoryName: "Sårbar",
        locationId: "3",
        registrationType: ["Funnet"],
        collector: "Collector 3"
      },
      {
        taxonGroupName: "Tovinger",
        displayName: "Honningbie",
        categoryName: "Nær truet",
        locationId: "36",
        registrationType: ["Ikke Funnet"],
        collector: "Collector 2"
      },
    ]);

    const group = component.topLevelFilter();
    expect(group.map(group => group.groupKeyId)).toEqual([
      'Fugler',
      'Tovinger'
    ]);

    expect(group[1].registrationTypes[0].species).toEqual([
      {
        speciesKeyId: "Honningbie",
        registrations: ["Collector 2"]
      },
      {
        speciesKeyId: "Humle",
        registrations: ["Collector 3"]
      }
    ])
  });

  it('should group by category when the filter changes', () => {
    fixture.componentRef.setInput('observationList', [
      {
        taxonGroupName: "Fugler",
        displayName: "Kråke",
        categoryName: "Livskraftig",
        locationId: "47",
        registrationType: ["Funnet"],
        collector: "Collector 1"
      },
      {
        taxonGroupName: "Tovinger",
        displayName: "Humle",
        categoryName: "Sårbar",
        locationId: "3",
        registrationType: ["Funnet"],
        collector: "Collector 3"
      }
    ]);

    component.currentFilter.set(Filters.Category);

    expect(component.topLevelFilter().map(group => group.groupKeyId)).toEqual([
      'Livskraftig',
      "Sårbar"
    ]);
  });
});
