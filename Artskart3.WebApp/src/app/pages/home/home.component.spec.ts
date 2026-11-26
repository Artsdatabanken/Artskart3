import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CUSTOM_ELEMENTS_SCHEMA, signal } from '@angular/core';
import { ActivatedRoute, convertToParamMap, ParamMap, Router } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TranslateModule } from '@ngx-translate/core';
import { BehaviorSubject, of } from 'rxjs';
import { HomeComponent } from './home.component';
import { MapComponent } from '../../shared/components/map.component/map.component';
import { ListViewComponent } from '../../shared/components/list-view/list-view.component';
import { GalleryViewComponent } from '../../shared/components/gallery-view/gallery-view.component';
import { SidebarComponent } from '../../shared/components/sidebar/sidebar.component';
import { ResizablePanelComponent } from '../../shared/components/resizable-panel/resizable-panel.component';
import { SaveFilterDialogComponent } from '../../shared/components/save-filter-dialog/save-filter-dialog.component';
import { AuthService } from '../../shared/services/auth/auth.service';
import { SavedFilterService } from '../../shared/services/saved-filter/saved-filter.service';
import { AlertService } from '../../shared/services/alert/alert.service';
import { SavedFilterDto } from '../../shared/types/api.types';
import { CustomAreaDrawingService } from '../../shared/services/custom-area-drawing/custom-area-drawing.service';
import { FilterLinkService } from '../../shared/services/filter-link/filter-link.service';

describe('HomeComponent – aktivering av lagret filter fra URL', () => {
  let fixture: ComponentFixture<HomeComponent>;
  let queryParamMap: BehaviorSubject<ParamMap>;
  let session: unknown;

  const savedFilter: SavedFilterDto = { id: 'abc', name: 'Fugler', filter: { taxonGroupIds: [1] }, isDefault: false };
  const router = { navigate: vi.fn() };
  const authService = {
    isAuthenticated: signal(true),
    getSession: vi.fn(() => of(session)),
    login: vi.fn(),
  };
  const savedFilterService = {
    findById: vi.fn(),
    activate: vi.fn().mockResolvedValue(undefined),
  };
  const alertService = { showError: vi.fn(), showSuccess: vi.fn() };
  const filterLinkService = { apply: vi.fn().mockResolvedValue(undefined) };

  async function create(filterId: string | null): Promise<void> {
    await createWithParams(filterId ? { savedFilter: filterId } : {});
  }

  async function createWithParams(params: Record<string, string>): Promise<void> {
    queryParamMap = new BehaviorSubject(convertToParamMap(params));

    await TestBed.configureTestingModule({
      imports: [HomeComponent, TranslateModule.forRoot()],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: ActivatedRoute, useValue: { queryParamMap } },
        { provide: Router, useValue: router },
        { provide: AuthService, useValue: authService },
        { provide: SavedFilterService, useValue: savedFilterService },
        { provide: AlertService, useValue: alertService },
        { provide: FilterLinkService, useValue: filterLinkService },
      ],
    })
      .overrideComponent(HomeComponent, {
        remove: { imports: [MapComponent, ListViewComponent, GalleryViewComponent, SidebarComponent, ResizablePanelComponent, SaveFilterDialogComponent] },
        add: { schemas: [CUSTOM_ELEMENTS_SCHEMA] },
      })
      .compileComponents();

    fixture = TestBed.createComponent(HomeComponent);
    await fixture.whenStable();
  }

  beforeEach(() => {
    vi.clearAllMocks();
    sessionStorage.clear();
    session = [{ type: 'sub', value: '1' }];
  });

  it('aktiverer filteret og fjerner parameteren fra URL-en', async () => {
    savedFilterService.findById.mockReturnValue(of(savedFilter));

    await create('abc');

    expect(savedFilterService.findById).toHaveBeenCalledWith('abc');
    expect(savedFilterService.activate).toHaveBeenCalledWith(savedFilter);
    expect(router.navigate).toHaveBeenCalledWith([], expect.objectContaining({ queryParams: { savedFilter: null }, replaceUrl: true }));
  });

  it('viser en feil når filteret ikke finnes, og fjerner parameteren', async () => {
    savedFilterService.findById.mockReturnValue(of(null));

    await create('slettet');

    expect(savedFilterService.activate).not.toHaveBeenCalled();
    expect(alertService.showError).toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalled();
  });

  it('sender anonyme brukere til innlogging', async () => {
    session = null;

    await create('abc');

    expect(authService.login).toHaveBeenCalled();
    expect(savedFilterService.findById).not.toHaveBeenCalled();
  });

  it('sender ikke brukeren til innlogging på nytt hvis de kommer tilbake fortsatt utlogget', async () => {
    session = null;
    sessionStorage.setItem('artskart.savedFilter.loginAttempted', String(Date.now()));

    await create('abc');

    expect(authService.login).not.toHaveBeenCalled();
    expect(alertService.showError).toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalled();
  });

  it('tilbyr innlogging igjen når et tidligere forsøk ble avbrutt for en stund siden', async () => {
    session = null;
    sessionStorage.setItem('artskart.savedFilter.loginAttempted', String(Date.now() - 10 * 60 * 1000));

    await create('abc');

    expect(authService.login).toHaveBeenCalled();
    expect(alertService.showError).not.toHaveBeenCalled();
  });

  it('lukker filterpanelet og viser kartet når tegningen starter', async () => {
    await create(null);
    const component = fixture.componentInstance;
    component.isFilterOpen.set(true);
    component.activeTab.set(1);

    TestBed.inject(CustomAreaDrawingService).start();
    TestBed.tick();

    expect(component.isFilterOpen()).toBe(false);
    expect(component.activeTab()).toBe(0);
  });

  it('gjør ingenting uten parameter', async () => {
    await create(null);

    expect(savedFilterService.findById).not.toHaveBeenCalled();
    expect(filterLinkService.apply).not.toHaveBeenCalled();
    expect(router.navigate).not.toHaveBeenCalled();
  });

  /** The link is cleared from the URL after the render in which the map reacts to the new filter. */
  async function render(): Promise<void> {
    fixture.detectChanges();
    await fixture.whenStable();
  }

  it('applies a filter link without login and then clears it from the URL', async () => {
    session = null;

    await createWithParams({ filter: '{"taxonIds":[26016]}', extent: '1,2,3,4' });
    await render();

    expect(filterLinkService.apply).toHaveBeenCalledWith('{"taxonIds":[26016]}', '1,2,3,4');
    expect(authService.login).not.toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalledWith(
      [],
      expect.objectContaining({ queryParams: { filter: null, extent: null, observationId: null }, queryParamsHandling: 'merge', replaceUrl: true }),
    );
  });

  it("opens the link's observation after the filter is applied, since the map closes the details on a filter change", async () => {
    await createWithParams({ filter: '{"taxonIds":[26016]}', observationId: '7' });
    await render();

    expect(router.navigate).toHaveBeenCalledWith([], expect.objectContaining({ queryParams: { filter: null, extent: null, observationId: '7' } }));
  });

  it('opens an old ?filter=<id> link as a saved filter, not as JSON', async () => {
    await createWithParams({ filter: '0193f4d2-8c1a-7c3e-9b2a-4f5e6d7c8b9a' });

    expect(filterLinkService.apply).not.toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalledWith(
      [],
      expect.objectContaining({ queryParams: { filter: null, savedFilter: '0193f4d2-8c1a-7c3e-9b2a-4f5e6d7c8b9a' }, replaceUrl: true }),
    );
  });
});
