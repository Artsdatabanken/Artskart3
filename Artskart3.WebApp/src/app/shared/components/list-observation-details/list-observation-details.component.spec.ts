import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { By } from '@angular/platform-browser';
import { TranslateModule } from '@ngx-translate/core';
import { Observable, of, throwError } from 'rxjs';
import { ObservationService } from '@shared/services/observation/observation.service';
import { ObservationDetailDto } from '@shared/types/api.types';
import { ObservationDetailsComponent } from '../observation-details/observation-details.component';
import { ListObservationDetailsComponent } from './list-observation-details.component';

describe('ListObservationDetails', () => {
  const getDetail = vi.fn<(id: number) => Observable<ObservationDetailDto>>();
  let fixture: ComponentFixture<ListObservationDetailsComponent>;

  beforeEach(async () => {
    getDetail.mockReset().mockImplementation((id) => of({ id, scientificName: `Species ${id}` }));
    await TestBed.configureTestingModule({
      imports: [ListObservationDetailsComponent, TranslateModule.forRoot()],
      providers: [{ provide: ObservationService, useValue: { getDetail, getAnnotations: () => of([]) } }],
    }).compileComponents();
    fixture = TestBed.createComponent(ListObservationDetailsComponent);
    fixture.componentRef.setInput('observationId', 2);
    fixture.componentRef.setInput('observationIds', [1, 2, 3]);
    await fixture.whenStable();
  });

  function details(): ObservationDetailsComponent {
    return fixture.debugElement.query(By.directive(ObservationDetailsComponent)).componentInstance;
  }

  it('shows the loaded observation at its place in the list, filling the panel', () => {
    expect(getDetail).toHaveBeenCalledWith(2);
    expect(details().state()).toBe('ready');
    expect(details().detail()).toEqual({ id: 2, scientificName: 'Species 2' });
    expect(details().position()).toBe(2);
    expect(details().count()).toBe(3);
    expect(details().fillHeight()).toBe(true);
  });

  it('steps through the list in both directions and wraps around', async () => {
    const changes: number[] = [];
    fixture.componentInstance.observationId.subscribe((id) => changes.push(id));

    details().next.emit();
    details().next.emit();
    details().previous.emit();
    await fixture.whenStable();

    expect(changes).toEqual([3, 1, 3]);
    expect(getDetail).toHaveBeenLastCalledWith(3);
    expect(details().position()).toBe(3);
  });

  it('shows an observation that is no longer in the list on its own', async () => {
    fixture.componentRef.setInput('observationIds', [7, 8]);
    await fixture.whenStable();

    details().next.emit();

    expect(details().position()).toBe(1);
    expect(details().count()).toBe(1);
    expect(fixture.componentInstance.observationId()).toBe(2);
  });

  it('reports a missing observation as not found', async () => {
    getDetail.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 404 })));
    fixture.componentRef.setInput('observationId', 9);
    await fixture.whenStable();

    expect(details().state()).toBe('notFound');
    expect(details().detail()).toBeNull();
  });

  it('loads again when retrying after a failure', async () => {
    getDetail.mockReturnValueOnce(throwError(() => new HttpErrorResponse({ status: 500 })));
    fixture.componentRef.setInput('observationId', 9);
    await fixture.whenStable();
    expect(details().state()).toBe('error');

    details().retry.emit();
    await fixture.whenStable();

    expect(details().state()).toBe('ready');
    expect(details().detail()).toEqual({ id: 9, scientificName: 'Species 9' });
  });

  it('collapses expanded details when the list tab is hidden, so the modal cannot block the page unseen', async () => {
    details().toggleExpanded();
    await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('dialog').classList.contains('expanded')).toBe(true);

    fixture.componentRef.setInput('active', false);
    await fixture.whenStable();

    expect(details().expanded()).toBe(false);
    expect(fixture.nativeElement.querySelector('dialog').classList.contains('expanded')).toBe(false);
  });

  it('closes when the details are dismissed', () => {
    const closed = vi.fn();
    fixture.componentInstance.closed.subscribe(closed);

    details().dismiss.emit();

    expect(closed).toHaveBeenCalledOnce();
  });
});
