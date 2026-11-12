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

  it('opens the viewer from the enlarge button only, not from the image', async () => {
    await TestBed.configureTestingModule({ imports: [ObservationGalleryComponent, TranslateModule.forRoot()] }).compileComponents();
    const fixture = TestBed.createComponent(ObservationGalleryComponent);
    const image = { id: 7, origin: 'https://example.org/photo.jpg' };
    fixture.componentRef.setInput('observationId', 42);
    fixture.componentRef.setInput('images', [image]);
    await fixture.whenStable();
    const element: HTMLElement = fixture.nativeElement;
    const viewer = element.querySelector<HTMLDialogElement>('app-image-viewer dialog')!;

    const photo = element.querySelector<HTMLElement>('.photo img')!;
    photo.dispatchEvent(new Event('load'));
    await fixture.whenStable();
    photo.click();
    await fixture.whenStable();
    expect(viewer.open).toBe(false);

    element.querySelector<HTMLElement>('adb-icon-button.enlarge')!.click();
    await fixture.whenStable();
    expect(viewer.open).toBe(true);
    expect(viewer.querySelector('img')?.getAttribute('src')).toBe(image.origin);
  });

  it('offers to enlarge only a loaded original, never the stored thumbnail', async () => {
    await TestBed.configureTestingModule({ imports: [ObservationGalleryComponent, TranslateModule.forRoot()] }).compileComponents();
    const fixture = TestBed.createComponent(ObservationGalleryComponent);
    const original = { id: 7, origin: 'https://example.org/photo.jpg', hasStoredImage: true };
    const thumbnailOnly = { id: 8, origin: 'not a url', hasStoredImage: true };
    fixture.componentRef.setInput('observationId', 42);
    fixture.componentRef.setInput('images', [original, thumbnailOnly]);
    await fixture.whenStable();
    const element: HTMLElement = fixture.nativeElement;
    const enlargeButtons = () => element.querySelectorAll('.enlarge').length;
    expect(enlargeButtons()).toBe(0);

    element.querySelectorAll('.photo img').forEach((img) => img.dispatchEvent(new Event('load')));
    await fixture.whenStable();
    expect(enlargeButtons()).toBe(1);

    fixture.componentInstance.fail(original, original.origin);
    await fixture.whenStable();
    element.querySelectorAll('.photo img').forEach((img) => img.dispatchEvent(new Event('load')));
    await fixture.whenStable();
    expect(enlargeButtons()).toBe(0);
  });

  it('steps between the observation\'s originals in the viewer', async () => {
    await TestBed.configureTestingModule({ imports: [ObservationGalleryComponent, TranslateModule.forRoot()] }).compileComponents();
    const fixture = TestBed.createComponent(ObservationGalleryComponent);
    const images = [
      { id: 7, origin: 'https://example.org/7.jpg' },
      { id: 8, origin: 'not a url', hasStoredImage: true },
      { id: 9, origin: 'https://example.org/9.jpg' },
    ];
    fixture.componentRef.setInput('observationId', 42);
    fixture.componentRef.setInput('images', images);
    await fixture.whenStable();
    const component = fixture.componentInstance;
    component.selected.set(9);

    component.step(-1);

    expect(component.viewer()?.src).toBe('https://example.org/7.jpg');
    expect(component.previousImage()).toBeNull();
    expect(component.nextImage()?.id).toBe(9);
  });

  it('gives the viewer the observation\'s species names', async () => {
    await TestBed.configureTestingModule({ imports: [ObservationGalleryComponent, TranslateModule.forRoot()] }).compileComponents();
    const fixture = TestBed.createComponent(ObservationGalleryComponent);
    fixture.componentRef.setInput('observationId', 42);
    fixture.componentRef.setInput('images', [{ id: 7, origin: 'https://example.org/7.jpg' }]);
    fixture.componentRef.setInput('names', { popularName: 'brunbjørn', scientificName: 'Ursus arctos', author: 'Linnaeus, 1758' });
    await fixture.whenStable();

    fixture.componentInstance.selected.set(7);

    expect(fixture.componentInstance.viewer()).toEqual(
      expect.objectContaining({ popularName: 'brunbjørn', scientificName: 'Ursus arctos', author: 'Linnaeus, 1758' }),
    );
  });

  it('rejects unsafe origins rather than binding them to images', () => {
    expect(webUrl('javascript:alert(1)')).toBeNull();
    expect(webUrl('file:///image')).toBeNull();
    expect(webUrl('https://user:password@example.org/photo')).toBeNull();
    expect(webUrl('https://example.org/photo')).toBe('https://example.org/photo');
  });
});
