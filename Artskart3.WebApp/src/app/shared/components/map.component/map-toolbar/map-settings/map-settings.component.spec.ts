import { TestBed } from '@angular/core/testing';
import { provideTranslateService } from '@ngx-translate/core';
import { MapSettingsComponent } from './map-settings.component';

describe('MapSettingsComponent', () => {
  it('turns clustering off and on from the switch', async () => {
    await TestBed.configureTestingModule({
      imports: [MapSettingsComponent],
      providers: [provideTranslateService()],
    }).compileComponents();
    const fixture = TestBed.createComponent(MapSettingsComponent);
    const element: HTMLElement = fixture.nativeElement;
    fixture.detectChanges();
    expect(element.querySelector('adb-switch')).toBeNull();

    fixture.componentRef.setInput('isOpen', true);
    fixture.detectChanges();
    const toggle = element.querySelector<HTMLElement & { checked: boolean }>('adb-switch')!;
    expect(toggle.checked).toBe(true);

    toggle.checked = false;
    toggle.dispatchEvent(new Event('change'));
    expect(fixture.componentInstance.clusteringEnabled()).toBe(false);

    toggle.checked = true;
    toggle.dispatchEvent(new Event('change'));
    expect(fixture.componentInstance.clusteringEnabled()).toBe(true);
  });

  it('requests the opposite open state when the button is clicked', async () => {
    await TestBed.configureTestingModule({
      imports: [MapSettingsComponent],
      providers: [provideTranslateService()],
    }).compileComponents();
    const fixture = TestBed.createComponent(MapSettingsComponent);
    const openChanges: boolean[] = [];
    fixture.componentInstance.mapSettingsOpenChange.subscribe((isOpen) => openChanges.push(isOpen));
    fixture.detectChanges();
    const button = (fixture.nativeElement as HTMLElement).querySelector('adb-icon-button')!;

    button.dispatchEvent(new Event('click'));
    fixture.componentRef.setInput('isOpen', true);
    fixture.detectChanges();
    button.dispatchEvent(new Event('click'));

    expect(openChanges).toEqual([true, false]);
  });
});
