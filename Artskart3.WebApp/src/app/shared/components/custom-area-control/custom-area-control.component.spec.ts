import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TranslateModule } from '@ngx-translate/core';
import { CustomAreaControlComponent } from './custom-area-control.component';
import { CustomAreaDrawingCommand, CustomAreaDrawingService } from '../../services/custom-area-drawing/custom-area-drawing.service';
import { FilterStateService } from '../../services/filter-state/filter-state.service';

describe('CustomAreaControlComponent', () => {
  let fixture: ComponentFixture<CustomAreaControlComponent>;
  let drawing: CustomAreaDrawingService;
  let commands: CustomAreaDrawingCommand[];

  const buttons = () => [...fixture.nativeElement.querySelectorAll('adb-button, adb-icon-button')] as HTMLElement[];
  const mainButton = () => fixture.nativeElement.querySelector('adb-button') as HTMLElement & { disabled: boolean };
  const click = (element: HTMLElement) => {
    element.click();
    fixture.detectChanges();
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CustomAreaControlComponent, TranslateModule.forRoot()],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(CustomAreaControlComponent);
    drawing = TestBed.inject(CustomAreaDrawingService);
    commands = [];
    drawing.commands.subscribe((command) => commands.push(command));
    fixture.detectChanges();
  });

  it('viser bare knappen for å tegne før tegningen er startet', () => {
    expect(buttons()).toHaveLength(1);
    expect(mainButton().textContent).toContain('sidebar.customArea.draw');
  });

  it('starter og fullfører tegningen med samme knapp', () => {
    click(mainButton());

    expect(commands).toEqual(['start']);
    expect(mainButton().textContent).toContain('sidebar.customArea.finish');
    expect(mainButton().disabled).toBe(true);

    drawing.setPointCount(3);
    fixture.detectChanges();
    click(mainButton());

    expect(mainButton().disabled).toBe(false);
    expect(commands).toEqual(['start', 'finish']);
  });

  it('kan angre et hjørne og avbryte mens tegningen pågår', () => {
    click(mainButton());
    const [undo, cancel] = [...fixture.nativeElement.querySelectorAll('adb-icon-button')] as HTMLElement[];

    expect(undo.getAttribute('aria-label')).toBe('sidebar.customArea.undo');
    expect(cancel.getAttribute('aria-label')).toBe('sidebar.customArea.cancel');

    click(undo);
    click(cancel);

    expect(commands).toEqual(['start', 'undo', 'cancel']);
    expect(buttons()).toHaveLength(1);
  });

  const checkPolygon = (tooManyLocations: boolean) => {
    drawing.submit('POLYGON((0 0,10 0,10 10,0 0))');
    TestBed.inject(HttpTestingController)
      .expectOne('/api/Lookup/CustomArea')
      .flush({ tooManyLocations, maxLocationCount: 200000 });
    fixture.detectChanges();
  };

  it('fjerner et tegnet område i filteret med hovedknappen, i farefarge', () => {
    checkPolygon(false);

    expect(buttons()).toHaveLength(1);
    expect(mainButton().textContent).toContain('sidebar.customArea.remove');
    expect(mainButton().getAttribute('color')).toBe('danger');

    click(mainButton());

    expect(TestBed.inject(FilterStateService).customAreaWkt()).toBeNull();
    expect(commands).toEqual([]);
    expect(mainButton().textContent).toContain('sidebar.customArea.draw');
    expect(mainButton().getAttribute('color')).toBeNull();
  });

  it('forklarer et avvist polygon og fjerner det med hovedknappen', () => {
    checkPolygon(true);

    const message = fixture.nativeElement.querySelector('[role="status"]') as HTMLElement;
    expect(message.textContent?.trim()).toBe('sidebar.customArea.tooManyLocations');
    expect(buttons()).toHaveLength(1);
    expect(mainButton().textContent).toContain('sidebar.customArea.remove');

    click(mainButton());

    expect(drawing.rejected()).toBeNull();
    expect(message.textContent?.trim()).toBe('');
    expect(mainButton().textContent).toContain('sidebar.customArea.draw');
  });
});
