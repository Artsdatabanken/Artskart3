import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TranslateModule } from '@ngx-translate/core';
import { ImageViewerComponent } from './image-viewer.component';

describe('ImageViewerComponent', () => {
  let fixture: ComponentFixture<ImageViewerComponent>;
  let element: HTMLElement;
  let previous: () => void;
  let next: () => void;

  async function show(hasPrevious: boolean, hasNext: boolean): Promise<void> {
    fixture.componentRef.setInput('image', { src: 'https://example.org/photo.jpg', alt: 'Brunbjørn' });
    fixture.componentRef.setInput('hasPrevious', hasPrevious);
    fixture.componentRef.setInput('hasNext', hasNext);
    await fixture.whenStable();
  }

  function press(key: string): void {
    document.dispatchEvent(new KeyboardEvent('keydown', { key }));
  }

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [ImageViewerComponent, TranslateModule.forRoot()] }).compileComponents();
    fixture = TestBed.createComponent(ImageViewerComponent);
    element = fixture.nativeElement;
    previous = vi.fn<() => void>();
    next = vi.fn<() => void>();
    fixture.componentInstance.previous.subscribe(previous);
    fixture.componentInstance.next.subscribe(next);
  });

  it('shows the names, photographer and license of the image', async () => {
    fixture.componentRef.setInput('image', {
      src: 'https://example.org/photo.jpg', alt: 'Brunbjørn',
      popularName: 'brunbjørn', scientificName: 'Ursus arctos', author: 'Linnaeus, 1758',
      rightsHolder: 'Fotograf', license: 'CC BY 4.0',
    });
    await fixture.whenStable();

    const text = element.querySelector('app-image-info')!.textContent!.replace(/\s+/g, ' ');
    expect(text).toContain('brunbjørn Ursus arctos Linnaeus, 1758');
    expect(text).toContain('common.by: Fotograf');
    expect(text).toContain('CC BY 4.0');
  });

  it('links to the original in a new tab', async () => {
    await show(false, false);

    const link = element.querySelector<HTMLAnchorElement>('a.original')!;
    expect(link.href).toBe('https://example.org/photo.jpg');
    expect(link.target).toBe('_blank');
    expect(link.rel).toBe('noopener noreferrer');
  });

  it('has no step buttons when there is nothing to step to', async () => {
    await show(false, false);

    expect(element.querySelector('.step')).toBeNull();
  });

  it('disables the step button at the end of the list', async () => {
    await show(false, true);

    expect(element.querySelector<HTMLElement & { disabled: boolean }>('.previous')!.disabled).toBe(true);
    expect(element.querySelector<HTMLElement & { disabled: boolean }>('.next')!.disabled).toBe(false);
  });

  it('steps with the buttons and the arrow keys', async () => {
    await show(true, true);

    element.querySelector<HTMLElement>('.next')!.click();
    press('ArrowLeft');

    expect(next).toHaveBeenCalledTimes(1);
    expect(previous).toHaveBeenCalledTimes(1);
  });

  it('ignores steps past the end of the list', async () => {
    await show(false, true);

    element.querySelector<HTMLElement>('.previous')!.click();
    press('ArrowLeft');

    expect(previous).not.toHaveBeenCalled();
  });
});
