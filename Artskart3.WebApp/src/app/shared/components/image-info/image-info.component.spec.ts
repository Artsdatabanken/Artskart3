import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TranslateModule } from '@ngx-translate/core';
import { ImageInfoComponent } from './image-info.component';

describe('ImageInfoComponent', () => {
  let fixture: ComponentFixture<ImageInfoComponent>;
  let element: HTMLElement;

  function text(selector: string): string | undefined {
    return element.querySelector(selector)?.textContent?.replace(/\s+/g, ' ').trim();
  }

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [ImageInfoComponent, TranslateModule.forRoot()] }).compileComponents();
    fixture = TestBed.createComponent(ImageInfoComponent);
    element = fixture.nativeElement;
  });

  it('shows the species names with the author', async () => {
    fixture.componentRef.setInput('names', { popularName: 'brunbjørn', scientificName: 'Ursus arctos', author: '(Linnaeus, 1758)' });
    await fixture.whenStable();

    expect(text('.taxon')).toBe('observationDetails.speciesName: brunbjørn Ursus arctos (Linnaeus, 1758)');
    expect(element.querySelector('.taxon i')?.textContent).toBe('Ursus arctos');
  });

  it('shows the scientific name alone when that is all there is', async () => {
    fixture.componentRef.setInput('names', { scientificName: 'Ursus arctos' });
    await fixture.whenStable();

    expect(text('.taxon')).toBe('observationDetails.speciesName: Ursus arctos');
  });

  it('shows the photographer and links the license', async () => {
    fixture.componentRef.setInput('media', { rightsHolder: 'Fotograf', license: 'https://creativecommons.org/licenses/by/4.0/' });
    await fixture.whenStable();

    expect(text('.rights-holder')).toBe('common.by: Fotograf');
    expect(element.querySelector('.license a')?.getAttribute('href')).toBe('https://creativecommons.org/licenses/by/4.0/');
    expect(text('.license')).toBe('observationDetails.license: https://creativecommons.org/licenses/by/4.0/');
  });

  it('leaves out the photographer where it is shown elsewhere', async () => {
    fixture.componentRef.setInput('media', { rightsHolder: 'Fotograf', license: 'CC BY 4.0' });
    fixture.componentRef.setInput('withRightsHolder', false);
    await fixture.whenStable();

    expect(element.querySelector('.rights-holder')).toBeNull();
    expect(text('.license')).toBe('observationDetails.license: CC BY 4.0');
  });
});
