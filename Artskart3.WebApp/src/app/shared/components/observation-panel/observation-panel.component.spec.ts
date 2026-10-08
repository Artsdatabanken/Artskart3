import { TestBed } from '@angular/core/testing';
import { Location } from '@angular/common';
import { provideLocationMocks } from '@angular/common/testing';
import { provideRouter, Router, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { TranslateModule } from '@ngx-translate/core';
import { of, Subject, throwError } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';
import { ObservationService } from '@shared/services/observation/observation.service';
import { ObservationDetailDto } from '@shared/types/api.types';
import { ObservationPanelComponent } from './observation-panel.component';

describe('ObservationPanel', () => {
  let harness: RouterTestingHarness;
  let panel: ObservationPanelComponent;
  const getDetail = vi.fn((id: number) => of<ObservationDetailDto>({ id, scientificName: `Species ${id}` }));

  beforeEach(async () => {
    getDetail.mockReset().mockImplementation((id: number) => of({ id, scientificName: `Species ${id}` }));
    await TestBed.configureTestingModule({
      imports: [TranslateModule.forRoot()],
      providers: [
        provideRouter(
          [
            {
              path: '',
              component: ObservationPanelComponent,
              data: {
                selection: { key: 1, kind: 'point', locationIds: [5], geometryLabel: 'UTM33 1, 2' },
                requestState: 'ready',
                observations: [
                  { id: 1, taxonId: 1, displayName: 'Species', dateTimeCollected: '2024-01-01', locationId: 5 },
                  { id: 2, taxonId: 1, displayName: 'Species', dateTimeCollected: '2025-01-01', locationId: 5 },
                ],
              },
            },
          ],
          withComponentInputBinding(),
        ),
        provideLocationMocks(),
        { provide: ObservationService, useValue: { getDetail } },
      ],
    }).compileComponents();
    harness = await RouterTestingHarness.create();
    TestBed.inject(Router).setUpLocationChangeListener();
    panel = await harness.navigateByUrl('/', ObservationPanelComponent);
    const ref = harness.routeDebugElement!.componentInstance;
    expect(ref).toBe(panel);
    harness.fixture.detectChanges();
  });

  it('loads a direct URL as one observation and handles invalid IDs without a request', async () => {
    await harness.navigateByUrl('/?observationId=42', ObservationPanelComponent);
    expect(panel.detail()?.id).toBe(42);
    expect(panel.count()).toBe(1);
    panel.move(1);
    expect(getDetail).toHaveBeenCalledTimes(1);
    await harness.navigateByUrl('/?observationId=invalid', ObservationPanelComponent);
    expect(panel.state()).toBe('notFound');
    expect(getDetail).toHaveBeenCalledTimes(1);
  });

  it('retains its component across URL changes and surfaces missing records', async () => {
    getDetail.mockReturnValueOnce(throwError(() => new HttpErrorResponse({ status: 404 })));
    const same = await harness.navigateByUrl('/?observationId=42', ObservationPanelComponent);
    expect(same).toBe(panel);
    expect(panel.state()).toBe('notFound');
    await harness.navigateByUrl('/?observationId=43', ObservationPanelComponent);
    expect(panel.detail()?.id).toBe(43);
  });

  it('cancels superseded details and does not display stale responses', async () => {
    const pending = new Subject<ObservationDetailDto>();
    getDetail.mockReturnValueOnce(pending);
    await harness.navigateByUrl('/?observationId=42', ObservationPanelComponent);
    expect(panel.state()).toBe('loading');
    await harness.navigateByUrl('/?observationId=43', ObservationPanelComponent);
    expect(pending.observed).toBe(false);
    pending.next({ id: 42 });
    expect(panel.detail()?.id).toBe(43);
  });

  it('closes a standalone observation by removing only its query parameter', async () => {
    await harness.navigateByUrl('/?observationId=42&other=kept', ObservationPanelComponent);
    panel.dismiss();
    await harness.fixture.whenStable();
    expect(TestBed.inject(Router).url).toBe('/?other=kept');
    expect(panel.rawId()).toBeNull();
  });

  it('forwards closing the list', () => {
    const listClosed = vi.fn();
    panel.listClosed.subscribe(listClosed);
    panel.list()!.dismiss.emit();
    expect(listClosed).toHaveBeenCalledOnce();
  });

  it('preserves the list, follows tree order, wraps, and records browser history', async () => {
    const list = panel.list()!;
    list.currentFilter.set('location');
    await harness.fixture.whenStable();
    expect(list.orderedObservationIds()).toEqual([2, 1]);
    panel.open(2);
    await harness.fixture.whenStable();
    expect(panel.list()).toBe(list);
    expect(panel.count()).toBe(2);
    expect(panel.position()).toBe(0);
    expect(harness.routeNativeElement!.querySelector('app-observation-list')!.hasAttribute('hidden')).toBe(true);
    panel.move(-1);
    await harness.fixture.whenStable();
    expect(panel.selectedId()).toBe(1);
    TestBed.inject(Location).back();
    await vi.waitFor(() => expect(panel.selectedId()).toBe(2));
    panel.dismiss();
    await harness.fixture.whenStable();
    expect(panel.list()).toBe(list);
    expect(list.currentFilter()).toBe('location');
    expect(harness.routeNativeElement!.querySelector('app-observation-list')!.hasAttribute('hidden')).toBe(false);
  });
});
