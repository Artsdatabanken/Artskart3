import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ObservationListComponent } from './observation-list.component';

describe('ObservationList', () => {
  let component: ObservationListComponent;
  let fixture: ComponentFixture<ObservationListComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ObservationListComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ObservationListComponent);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
