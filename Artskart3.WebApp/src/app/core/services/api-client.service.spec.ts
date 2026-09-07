import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApiClientService } from './api-client.service';

describe('ApiClientService', () => {
  let service: ApiClientService;
  let httpMock: HttpTestingController;
  const endpoint = '/api/Search/AreaCounts?zoomLevel=1';

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [ApiClientService, provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(ApiClientService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  describe('postJsonWithETag', () => {
    it('should send If-None-Match when an etag is provided', () => {
      service.postJsonWithETag(endpoint, {}, '"abc123"').subscribe();

      const req = httpMock.expectOne(endpoint);
      expect(req.request.headers.get('If-None-Match')).toBe('"abc123"');
      req.flush([]);
    });

    it('should not send If-None-Match when no etag is provided', () => {
      service.postJsonWithETag(endpoint, {}).subscribe();

      const req = httpMock.expectOne(endpoint);
      expect(req.request.headers.has('If-None-Match')).toBe(false);
      req.flush([]);
    });

    it('should return body and etag for a 200 response', () => {
      let result: { body: unknown; etag: string | null; notModified: boolean } | undefined;
      service.postJsonWithETag(endpoint, {}).subscribe(r => (result = r));

      httpMock.expectOne(endpoint).flush([{ fid: '03', observationCount: 5 }], {
        headers: { ETag: '"abc123"' },
      });

      expect(result).toEqual({
        body: [{ fid: '03', observationCount: 5 }],
        etag: '"abc123"',
        notModified: false,
      });
    });

    it('should resolve a 304 without retrying', () => {
      let result: { body: unknown; etag: string | null; notModified: boolean } | undefined;
      service.postJsonWithETag(endpoint, {}, '"abc123"').subscribe(r => (result = r));

      httpMock
        .expectOne(endpoint)
        .flush(null, { status: 304, statusText: 'Not Modified', headers: { ETag: '"abc123"' } });

      expect(result).toEqual({ body: null, etag: '"abc123"', notModified: true });
      // Ingen ytterligere forespørsler — 304 skal ikke gå gjennom retry
      httpMock.expectNone(endpoint);
    });

    it('should fall back to the request etag when the 304 has no ETag header', () => {
      let result: { body: unknown; etag: string | null; notModified: boolean } | undefined;
      service.postJsonWithETag(endpoint, {}, '"abc123"').subscribe(r => (result = r));

      httpMock.expectOne(endpoint).flush(null, { status: 304, statusText: 'Not Modified' });

      expect(result).toEqual({ body: null, etag: '"abc123"', notModified: true });
    });

    it('should still retry server errors', async () => {
      vi.useFakeTimers();
      let result: { body: unknown; etag: string | null; notModified: boolean } | undefined;
      service.postJsonWithETag(endpoint, {}).subscribe(r => (result = r));

      httpMock
        .expectOne(endpoint)
        .flush(null, { status: 503, statusText: 'Service Unavailable' });
      await vi.advanceTimersByTimeAsync(1000);

      httpMock.expectOne(endpoint).flush([], { headers: { ETag: '"abc123"' } });

      expect(result).toEqual({ body: [], etag: '"abc123"', notModified: false });
      vi.useRealTimers();
    });
  });
});
