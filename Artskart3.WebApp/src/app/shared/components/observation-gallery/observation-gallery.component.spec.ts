import { TestBed } from '@angular/core/testing';
import { TranslateModule } from '@ngx-translate/core';
import { ObservationGalleryComponent } from './observation-gallery.component';
import { webUrl } from '../image-viewer/image-source';

describe('ObservationGallery', () => {
  it('uses origin first, falls back exactly once, then exposes unavailable state', async () => {
    await TestBed.configureTestingModule({ imports: [ObservationGalleryComponent, TranslateModule.forRoot()] }).compileComponents();
    const fixture = TestBed.createComponent(ObservationGalleryComponent);
    const image = { id: 7, origin: 'https://example.org/photo.jpg', hasStoredImage: true };
    fixture.componentRef.setInput('observationId', 42);
    fixture.componentRef.setInput('images', [image]);
    await fixture.whenStable();
    const component = fixture.componentInstance;
    expect(component.source(image)).toBe(image.origin);
    component.fail(image, image.origin);
    expect(component.source(image)).toBe('/api/observations/42/media/7/image');
    component.fail(image, '/api/observations/42/media/7/image');
    expect(component.source(image)).toBeNull();
    await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('.photo')).toBeNull();
    expect(fixture.nativeElement.querySelector('[role="status"]')).not.toBeNull();
    fixture.componentRef.setInput('images', [{ ...image }]);
    await fixture.whenStable();
    expect(component.source(image)).toBe(image.origin);
  });

  it('rejects unsafe origins rather than binding them to images', () => {
    expect(webUrl('javascript:alert(1)')).toBeNull();
    expect(webUrl('file:///image')).toBeNull();
    expect(webUrl('https://user:password@example.org/photo')).toBeNull();
    expect(webUrl('https://example.org/photo')).toBe('https://example.org/photo');
  });
});
