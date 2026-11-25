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

  async function create(filterId: string | null): Promise<void> {
    queryParamMap = new BehaviorSubject(convertToParamMap(filterId ? { filter: filterId } : {}));

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
    expect(router.navigate).toHaveBeenCalledWith([], expect.objectContaining({ queryParams: { filter: null }, replaceUrl: true }));
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

  it('holder filterfanen ved bunnen av panelet og bruker panelhøyden ved draing', async () => {
    const originalWidth = Object.getOwnPropertyDescriptor(window, 'innerWidth');
    Object.defineProperty(window, 'innerWidth', { configurable: true, value: 393 });

    try {
      await create(null);
      const component = fixture.componentInstance;
      const sheet: HTMLElement = fixture.nativeElement.querySelector('.sidebar');
      const handle: HTMLElement = fixture.nativeElement.querySelector('.sheet-handle');
      Object.defineProperty(sheet, 'offsetHeight', { configurable: true, value: 616 });
      handle.setPointerCapture = vi.fn();
      handle.releasePointerCapture = vi.fn();

      expect(component.sheetTransform()).toBe('translateY(calc(100% - var(--home-handle-height, 56px)))');

      handle.dispatchEvent(new MouseEvent('pointerdown', { bubbles: true, clientY: 600 }));
      expect(component.sheetTransform()).toBe('translateY(560px)');
      handle.dispatchEvent(new MouseEvent('pointerup', { bubbles: true, clientY: 600 }));
      expect(component.sheetTransform()).toBe('translateY(24px)');

      component.isFilterOpen.set(false);
      Object.defineProperty(sheet, 'offsetHeight', { configurable: true, value: 516 });
      component.onWindowResize();
      expect(component.sheetTransform()).toBe('translateY(calc(100% - var(--home-handle-height, 56px)))');

      handle.dispatchEvent(new MouseEvent('pointerdown', { bubbles: true, clientY: 500 }));
      handle.dispatchEvent(new MouseEvent('pointermove', { bubbles: true, clientY: 450 }));
      expect(component.sheetTransform()).toBe('translateY(410px)');
      handle.dispatchEvent(new MouseEvent('pointerup', { bubbles: true, clientY: 450 }));
      expect(component.isFilterOpen()).toBe(false);
    } finally {
      if (originalWidth) Object.defineProperty(window, 'innerWidth', originalWidth);
    }
  });

  it('gjør ingenting uten parameter', async () => {
    await create(null);

    expect(savedFilterService.findById).not.toHaveBeenCalled();
    expect(router.navigate).not.toHaveBeenCalled();
  });
});
