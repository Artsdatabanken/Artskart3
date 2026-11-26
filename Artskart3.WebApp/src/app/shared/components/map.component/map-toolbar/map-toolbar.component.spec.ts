import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { provideTranslateService } from '@ngx-translate/core';

import { MapToolbarComponent } from './map-toolbar.component';

describe('MapToolbarComponent', () => {
  let component: MapToolbarComponent;
  let fixture: ComponentFixture<MapToolbarComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [MapToolbarComponent],
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
      providers: [provideTranslateService()],
    }).compileComponents();
  });

  it('should create', () => {
    fixture = TestBed.createComponent(MapToolbarComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  it('gives each toolbar button a tooltip', () => {
    fixture = TestBed.createComponent(MapToolbarComponent);
    fixture.detectChanges();
    const tooltips = Array.from<HTMLElement>(fixture.nativeElement.querySelectorAll('.map-control-tooltip-anchor > .map-control-tooltip'));
    expect(tooltips.map((tooltip) => tooltip.textContent?.trim())).toEqual([
      'mapToolbar.mapTypeTooltip',
      'mapToolbar.layersTooltip',
      'mapToolbar.settingsTooltip',
    ]);
  });
});
