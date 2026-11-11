import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { artskart2LinkRouteGuards } from './artskart2-link.guard';

@Component({ template: '' })
class HomeStubComponent {}

describe('artskart2LinkGuard', () => {
  let harness: RouterTestingHarness;
  let router: Router;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [provideRouter([{ path: '', component: HomeStubComponent, ...artskart2LinkRouteGuards }])],
    });
    harness = await RouterTestingHarness.create();
    router = TestBed.inject(Router);
  });

  it('turns an Artskart 2 featureInfo link into an occurrenceId link', async () => {
    await harness.navigateByUrl('/#featureInfo/nina%2Fskandobs%2F0008ef28-6c98-4791-b3f6-914b3908e4a3');
    const url = router.parseUrl(router.url);
    expect(url.queryParams).toEqual({ occurrenceId: 'nina/skandobs/0008ef28-6c98-4791-b3f6-914b3908e4a3' });
    expect(url.fragment).toBeNull();
  });

  it('also redirects when only the fragment changes on an open page', async () => {
    await harness.navigateByUrl('/?observationId=42');
    await harness.navigateByUrl('/#featureInfo/urn%3Acatalog%3AO%3AL%3A37');
    expect(router.parseUrl(router.url).queryParams).toEqual({ occurrenceId: 'urn:catalog:O:L:37' });
  });

  it('leaves other URLs alone', async () => {
    await harness.navigateByUrl('/?observationId=42#other');
    expect(router.url).toBe('/?observationId=42#other');
    await harness.navigateByUrl('/#featureInfo/');
    expect(router.parseUrl(router.url).fragment).toBe('featureInfo/');
  });
});
