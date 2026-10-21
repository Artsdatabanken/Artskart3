import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { TranslateModule } from '@ngx-translate/core';
import { CadastralUnitSearchComponent } from './cadastral-unit-search.component';
import { CadastralUnitDto } from '../../types/api.types';

describe('CadastralUnitSearchComponent', () => {
  let fixture: ComponentFixture<CadastralUnitSearchComponent>;
  let component: CadastralUnitSearchComponent;
  let httpTesting: HttpTestingController;
  let selected: CadastralUnitDto[];

  const cadastralUnit: CadastralUnitDto = {
    id: '1101-1/2',
    wktPolygon: 'MULTIPOLYGON (((0 0, 1 0, 1 1, 0 0)))',
    extent: { minX: 0, minY: 0, maxX: 1, maxY: 1 },
  };

  const type = (value: string) => component.onSearchInput(new CustomEvent('adb-input', { detail: { value } }) as unknown as Event);
  const message = () => fixture.nativeElement.querySelector('.message') as HTMLElement;
  const status = () => fixture.nativeElement.querySelector('[role="status"]') as HTMLElement;
  const submit = () => {
    component.onSearchSubmit();
    fixture.detectChanges();
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CadastralUnitSearchComponent, TranslateModule.forRoot()],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(CadastralUnitSearchComponent);
    fixture.componentRef.setInput('label', 'Gårds- og bruksnummer');
    component = fixture.componentInstance;
    httpTesting = TestBed.inject(HttpTestingController);
    selected = [];
    component.selected.subscribe((unit) => selected.push(unit));
    fixture.detectChanges();
  });

  afterEach(() => httpTesting.verify());

  it('viser formatet som hjelpetekst', () => {
    expect(message().textContent?.trim()).toBe('sidebar.cadastralUnitHint');
    expect(message().classList).not.toContain('message--error');
  });

  it('viser formatfeil uten å spørre API-et', () => {
    type('1101-1');
    submit();

    expect(message().textContent?.trim()).toBe('sidebar.cadastralUnitInvalidFormat');
    expect(message().classList).toContain('message--error');
    expect(status().textContent?.trim()).toBe('sidebar.cadastralUnitInvalidFormat');
  });

  it('gjør ingenting med et tomt felt', () => {
    type('  ');
    submit();

    expect(message().textContent?.trim()).toBe('sidebar.cadastralUnitHint');
  });

  it('slår opp det normaliserte nummeret og sender treffet videre', () => {
    type(' 1101-01 / 2 ');
    submit();

    httpTesting.expectOne((req) => req.url === '/api/Lookup/CadastralUnit' && req.params.get('id') === '1101-1/2').flush(cadastralUnit);
    fixture.detectChanges();

    expect(selected).toEqual([cadastralUnit]);
    expect(message().textContent?.trim()).toBe('sidebar.cadastralUnitHint');
  });

  it('sier fra når eiendommen ikke finnes', () => {
    type('1101-99999/1');
    submit();

    httpTesting.expectOne((req) => req.url === '/api/Lookup/CadastralUnit').flush(null, { status: 404, statusText: 'Not Found' });
    fixture.detectChanges();

    expect(selected).toEqual([]);
    expect(message().textContent?.trim()).toBe('sidebar.cadastralUnitNotFound');
  });

  it('skiller en feil hos Kartverket fra et ukjent nummer', () => {
    type('1101-1/2');
    submit();

    httpTesting.expectOne((req) => req.url === '/api/Lookup/CadastralUnit').flush(null, { status: 502, statusText: 'Bad Gateway' });
    fixture.detectChanges();

    expect(message().textContent?.trim()).toBe('sidebar.cadastralUnitLookupFailed');
  });

  it('forkaster svaret på et oppslag når brukeren skriver videre', () => {
    type('1101-1/2');
    submit();
    type('1101-1/3');

    httpTesting.expectOne((req) => req.url === '/api/Lookup/CadastralUnit').flush(cadastralUnit);
    fixture.detectChanges();

    expect(selected).toEqual([]);
  });

  it('fjerner feilen når brukeren skriver videre', () => {
    type('abc');
    submit();
    type('1101-1/2');
    fixture.detectChanges();

    expect(message().textContent?.trim()).toBe('sidebar.cadastralUnitHint');
  });
});
