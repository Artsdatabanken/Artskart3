import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { GalleryViewComponent } from './gallery-view.component';
import { FilterStateService } from '../../services/filter-state/filter-state.service';
import { MapViewService } from '../../services/map-view/map-view.service';
import { ObservationImageDto, PagedObservationImageResponse } from '../../types/api.types';

const IMAGES_URL = '/api/Search/ObservationImages';

function image(observationId: number, mediaId: number, origin: string | null = `https://example.org/${mediaId}.jpg`): ObservationImageDto {
  return {
    observationId,
    popularName: 'Brunbjørn',
    scientificName: 'Ursus arctos',
    author: 'Linnaeus, 1758',
    point: { latitude: 59.9, longitude: 10.7, east: 262000, north: 6650000 },
    coordinatePrecision: 8000,
    image: { id: mediaId, origin, mimeType: 'image/jpeg', hasStoredImage: true, rightsHolder: 'Fotograf', license: 'CC BY 4.0' },
  };
}

function page(items: ObservationImageDto[], lookaheadCount = 0, hasMorePages = false): PagedObservationImageResponse {
  return { items, pageNumber: 1, resultsPerPage: 25, lookaheadCount, hasMorePages };
}

describe('GalleryViewComponent', () => {
  let component: GalleryViewComponent;
  let fixture: ComponentFixture<GalleryViewComponent>;
  let httpTesting: HttpTestingController;

  async function respond(response: PagedObservationImageResponse): Promise<void> {
    fixture.detectChanges();
    httpTesting.expectOne((r) => r.url === IMAGES_URL).flush(response);
    await fixture.whenStable();
  }

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [GalleryViewComponent, TranslateModule.forRoot()],
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();

    httpTesting = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(GalleryViewComponent);
    component = fixture.componentInstance;
  });

  it('requests the first page with the default page size', () => {
    fixture.detectChanges();
    const request = httpTesting.expectOne((r) => r.url === IMAGES_URL);
    expect(request.request.body).toEqual(expect.objectContaining({ pageNumber: 1, resultsPerPage: 25 }));
  });

  it('renders one card per image with species and photographer', async () => {
    await respond(page([image(1, 10), image(1, 11), image(2, 20)]));

    const cards = fixture.nativeElement.querySelectorAll('.card');
    expect(cards.length).toBe(3);
    expect(cards[0].querySelector('img').getAttribute('src')).toBe('https://example.org/10.jpg');
    expect(cards[0].querySelector('.species').textContent).toBe('Brunbjørn Ursus arctos');
    expect(cards[0].textContent).toContain('Fotograf');
  });

  it('expands the species name to the image info, without repeating the photographer', async () => {
    await respond(page([image(1, 10)]));
    const toggle = fixture.nativeElement.querySelector('.species-toggle');
    expect(toggle.getAttribute('aria-expanded')).toBe('false');
    expect(fixture.nativeElement.querySelector('.card app-image-info')).toBeNull();

    toggle.click();
    await fixture.whenStable();

    expect(toggle.getAttribute('aria-expanded')).toBe('true');
    const info = fixture.nativeElement.querySelector('.card app-image-info');
    expect(info.id).toBe(toggle.getAttribute('aria-controls'));
    const text = info.textContent.replace(/\s+/g, ' ');
    expect(text).toContain('Brunbjørn Ursus arctos Linnaeus, 1758');
    expect(text).toContain('CC BY 4.0');
    expect(info.querySelector('.rights-holder')).toBeNull();
  });

  it('shows the empty message when the filter has no images', async () => {
    await respond(page([]));

    expect(fixture.nativeElement.querySelector('.card')).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('galleryView.noResults');
  });

  it('falls back to the stored image when the origin fails', async () => {
    const item = image(1, 10);
    await respond(page([item]));

    component.fail(item, 'https://example.org/10.jpg');

    expect(component.source(item)).toBe('/api/observations/1/media/10/image');
  });

  async function loadImage(card = 0): Promise<void> {
    fixture.nativeElement.querySelectorAll('.card img')[card].dispatchEvent(new Event('load'));
    await fixture.whenStable();
  }

  it('offers to enlarge the original only once it has loaded', async () => {
    await respond(page([image(1, 10)]));
    expect(fixture.nativeElement.querySelector('.enlarge')).toBeNull();

    await loadImage();

    expect(fixture.nativeElement.querySelector('.enlarge')).not.toBeNull();
  });

  it('never offers to enlarge the stored thumbnail', async () => {
    const withoutOrigin = image(1, 10, null);
    const failedOrigin = image(2, 20);
    await respond(page([withoutOrigin, failedOrigin]));
    component.fail(failedOrigin, 'https://example.org/20.jpg');
    await fixture.whenStable();

    await loadImage(0);
    await loadImage(1);

    expect(component.source(withoutOrigin)).toBe('/api/observations/1/media/10/image');
    expect(component.source(failedOrigin)).toBe('/api/observations/2/media/20/image');
    expect(fixture.nativeElement.querySelector('.enlarge')).toBeNull();
  });

  it('opens the viewer from the enlarge button', async () => {
    await respond(page([image(1, 10)]));
    await loadImage();

    fixture.nativeElement.querySelector('.enlarge').click();
    await fixture.whenStable();

    expect(component.viewer()).toEqual(
      expect.objectContaining({
        src: 'https://example.org/10.jpg',
        rightsHolder: 'Fotograf',
        popularName: 'Brunbjørn',
        scientificName: 'Ursus arctos',
        author: 'Linnaeus, 1758',
      }),
    );
    expect(fixture.nativeElement.querySelector('dialog').open).toBe(true);
  });

  it('opens the observation details through the observationId query parameter', async () => {
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const shown = vi.fn();
    component.observationShown.subscribe(shown);
    await respond(page([image(42, 10)]));

    fixture.nativeElement.querySelector('.show-observation').click();

    expect(navigate).toHaveBeenCalledWith([], { queryParams: { observationId: 42, occurrenceId: null }, queryParamsHandling: 'merge' });
    expect(shown).toHaveBeenCalled();
  });

  it('steps between originals in the viewer, skipping images without one', async () => {
    await respond(page([image(1, 10), image(2, 20, null), image(3, 30)]));
    await loadImage();
    fixture.nativeElement.querySelector('.enlarge').click();
    await fixture.whenStable();
    expect(component.previousItem()).toBeNull();

    fixture.nativeElement.querySelector('app-image-viewer .next').click();
    await fixture.whenStable();

    expect(component.viewer()?.src).toBe('https://example.org/30.jpg');
    expect(component.nextItem()).toBeNull();
    expect(component.previousItem()?.image?.id).toBe(10);
  });

  it('moves on when an original fails in the viewer', async () => {
    const failing = image(2, 20);
    await respond(page([image(1, 10), failing, image(3, 30)]));
    await loadImage();
    fixture.nativeElement.querySelector('.enlarge').click();
    component.step(1);

    component.viewerFailed();

    expect(component.viewer()?.src).toBe('https://example.org/30.jpg');
    expect(component.source(failing)).toBe('/api/observations/2/media/20/image');
  });

  it('asks the map to show the observation with its coordinate precision', async () => {
    vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    await respond(page([image(42, 10)]));

    fixture.nativeElement.querySelector('.show-observation').click();

    expect(TestBed.inject(MapViewService).requestedExtent()).toEqual([254000, 6642000, 270000, 6658000]);
  });

  it('leaves the map as it is when the observation has no usable coordinate', async () => {
    vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    await respond(page([{ ...image(42, 10), point: undefined }]));

    fixture.nativeElement.querySelector('.show-observation').click();

    expect(TestBed.inject(MapViewService).requestedExtent()).toBeNull();
  });

  it('sends no searches while hidden, and catches up when shown again', async () => {
    await respond(page([image(1, 10)]));

    fixture.componentRef.setInput('active', false);
    TestBed.inject(FilterStateService).toggleCategory(1);
    fixture.detectChanges();
    await fixture.whenStable();
    httpTesting.expectNone((r) => r.url === IMAGES_URL);

    fixture.componentRef.setInput('active', true);
    fixture.detectChanges();
    httpTesting.expectOne((r) => r.url === IMAGES_URL);
  });

  it('keeps its result when shown again with the same filter', async () => {
    await respond(page([image(1, 10)]));

    fixture.componentRef.setInput('active', false);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.componentRef.setInput('active', true);
    fixture.detectChanges();
    await fixture.whenStable();

    httpTesting.expectNone((r) => r.url === IMAGES_URL);
    expect(fixture.nativeElement.querySelectorAll('.card').length).toBe(1);
  });

  it('closes the viewer when hidden', async () => {
    await respond(page([image(1, 10)]));
    await loadImage();
    fixture.nativeElement.querySelector('.enlarge').click();
    await fixture.whenStable();
    expect(component.viewer()).not.toBeNull();

    fixture.componentRef.setInput('active', false);
    await fixture.whenStable();

    expect(component.viewer()).toBeNull();
    expect(fixture.nativeElement.querySelector('dialog').open).toBe(false);
  });

  it('uses lookahead for the visible pages', async () => {
    await respond(page([image(1, 10)], 3, true));

    expect(component.totalVisiblePages()).toBe(4);
    expect(component.hasMorePages()).toBe(true);
  });

  it('resets to page 1 when the filter changes', async () => {
    component.onPageChange(4);
    expect(component.pageNumber()).toBe(4);

    TestBed.inject(FilterStateService).toggleCategory(1);

    expect(component.pageNumber()).toBe(1);
  });

  it('resets to page 1 when the page size changes', () => {
    component.onPageChange(4);

    component.onPageSizeChange(50);

    expect(component.resultsPerPage()).toBe(50);
    expect(component.pageNumber()).toBe(1);
  });
});
