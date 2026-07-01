import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { provideTranslateService } from '@ngx-translate/core';

import { NbicMapComponent } from '@artsdatabanken/nbic-map-component';
import { MapToolbarComponent } from './map-toolbar.component';
import { ToolbarAction } from './map-toolbar.constants';

describe('MapToolbarComponent', () => {
  let component: MapToolbarComponent;
  let fixture: ComponentFixture<MapToolbarComponent>;

  const createMockPermissionStatus = (state: PermissionState) => ({
    state,
    onchange: null as (() => void) | null,
    addEventListener: vi.fn(),
    removeEventListener: vi.fn(),
    dispatchEvent: vi.fn(),
  });

  beforeEach(async () => {
    Object.defineProperty(navigator, 'permissions', {
      value: { query: vi.fn() },
      writable: true,
      configurable: true,
    });

    await TestBed.configureTestingModule({
      imports: [MapToolbarComponent],
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
      providers: [provideTranslateService()],
    }).compileComponents();
  });

  it('should create', async () => {
    vi.mocked(navigator.permissions.query).mockResolvedValue(
      createMockPermissionStatus('prompt') as unknown as PermissionStatus,
    );
    fixture = TestBed.createComponent(MapToolbarComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
    expect(component).toBeTruthy();
  });

  describe('geolocation denied', () => {
    it('should disable the geolocation button when permission is denied', async () => {
      const mockPermissionStatus = createMockPermissionStatus('denied');

      vi.mocked(navigator.permissions.query).mockResolvedValue(
        mockPermissionStatus as unknown as PermissionStatus,
      );

      fixture = TestBed.createComponent(MapToolbarComponent);
      component = fixture.componentInstance;
      fixture.detectChanges();
      await fixture.whenStable();
      fixture.detectChanges();

      const button = fixture.nativeElement.querySelector(
        `[data-action="${ToolbarAction.GEOLOCATION}"]`,
      );

      expect(button).toBeTruthy();
      expect(button.disabled).toBe(true);

      expect(button.getAttribute('aria-describedby')).toBe('geolocation-denied-tooltip');
      const tooltip = fixture.nativeElement.querySelector('#geolocation-denied-tooltip');
      expect(tooltip).toBeTruthy();
      expect(tooltip.getAttribute('role')).toBe('tooltip');
      expect(tooltip.textContent.trim()).toBe('mapToolbar.geolocationDeniedTooltip');
    });

    it('should not disable the geolocation button when permission is granted', async () => {
      const mockPermissionStatus = createMockPermissionStatus('granted');

      vi.mocked(navigator.permissions.query).mockResolvedValue(
        mockPermissionStatus as unknown as PermissionStatus,
      );

      fixture = TestBed.createComponent(MapToolbarComponent);
      component = fixture.componentInstance;
      fixture.detectChanges();
      await fixture.whenStable();

      const button = fixture.nativeElement.querySelector(
        `[data-action="${ToolbarAction.GEOLOCATION}"]`,
      );

      expect(button).toBeTruthy();
      expect(button.disabled).toBe(false);
    });

    it('should disable button when geolocation permission is denied on click', async () => {
      const mockPermissionStatus = createMockPermissionStatus('prompt');

      vi.mocked(navigator.permissions.query).mockResolvedValue(
        mockPermissionStatus as unknown as PermissionStatus,
      );

      Object.defineProperty(navigator, 'geolocation', {
        value: {
          getCurrentPosition: (_success: PositionCallback, error: PositionErrorCallback) => {
            error({
              code: 1, // PERMISSION_DENIED
              message: 'User denied',
            } as GeolocationPositionError);
          },
        },
        writable: true,
        configurable: true,
      });

      const mockMap = {
        zoomToGeolocation: vi.fn().mockResolvedValue(true),
      } as unknown as NbicMapComponent;

      fixture = TestBed.createComponent(MapToolbarComponent);
      component = fixture.componentInstance;
      component.map = mockMap;
      fixture.detectChanges();
      await fixture.whenStable();

      component.onButtonClick(ToolbarAction.GEOLOCATION);
      await fixture.whenStable();
      fixture.detectChanges();

      const button = fixture.nativeElement.querySelector(
        `[data-action="${ToolbarAction.GEOLOCATION}"]`,
      );

      expect(button.disabled).toBe(true);
    });

    it('should update disabled state when permission changes', async () => {
      const mockPermissionStatus = createMockPermissionStatus('prompt');

      vi.mocked(navigator.permissions.query).mockResolvedValue(
        mockPermissionStatus as unknown as PermissionStatus,
      );

      fixture = TestBed.createComponent(MapToolbarComponent);
      component = fixture.componentInstance;
      fixture.detectChanges();
      await fixture.whenStable();

      const button = fixture.nativeElement.querySelector(
        `[data-action="${ToolbarAction.GEOLOCATION}"]`,
      );
      expect(button.disabled).toBe(false);

      mockPermissionStatus.state = 'denied';
      mockPermissionStatus.onchange!();
      fixture.detectChanges();

      expect(button.disabled).toBe(true);
    });
  });
});
