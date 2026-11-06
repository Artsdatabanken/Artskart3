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

  it('holds the legend open while the other menus are toggled', () => {
    fixture = TestBed.createComponent(MapToolbarComponent);
    fixture.detectChanges();
    const element: HTMLElement = fixture.nativeElement;
    const buttonFor = (label: string) => element.querySelector(`adb-icon-button[aria-label="${label}"]`)!;
    const legendOpen = () => element.querySelector('.map-legend-dropdown') !== null;

    buttonFor('mapToolbar.legendHeading').dispatchEvent(new Event('click'));
    fixture.detectChanges();
    buttonFor('mapToolbar.layersAriaLabel').dispatchEvent(new Event('click'));
    fixture.detectChanges();
    expect(element.querySelector('adb-checkbox, .map-layers-empty')).not.toBeNull();
    expect(legendOpen()).toBe(true);

    buttonFor('mapToolbar.settingsAriaLabel').dispatchEvent(new Event('click'));
    fixture.detectChanges();
    expect(legendOpen()).toBe(true);

    buttonFor('mapToolbar.legendHeading').dispatchEvent(new Event('click'));
    fixture.detectChanges();
    expect(legendOpen()).toBe(false);
  });
});
