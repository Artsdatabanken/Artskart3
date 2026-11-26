import { TestBed } from '@angular/core/testing';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { AlertService } from '../alert/alert.service';
import { MapViewService } from '../map-view/map-view.service';
import { SearchFilterService } from '../search-filter/search-filter.service';
import { FilterLinkService } from './filter-link.service';

describe('FilterLinkService', () => {
  let service: FilterLinkService;
  let mapView: MapViewService;
  const searchFilter = { applyFilter: vi.fn().mockResolvedValue(undefined) };
  const alertService = { showWarning: vi.fn(), dismiss: vi.fn() };

  beforeEach(() => {
    vi.clearAllMocks();
    TestBed.configureTestingModule({
      imports: [TranslateModule.forRoot()],
      providers: [
        { provide: SearchFilterService, useValue: searchFilter },
        { provide: AlertService, useValue: alertService },
      ],
    });
    service = TestBed.inject(FilterLinkService);
    mapView = TestBed.inject(MapViewService);
  });

  it('applies the filter and zooms to the extent without a warning', async () => {
    await service.apply('{"taxonIds":[26016],"registrationStatusId":1}', '250000,6640000,270000,6660000');

    expect(searchFilter.applyFilter).toHaveBeenCalledWith({ taxonIds: [26016], registrationStatusId: 1 });
    expect(mapView.requestedExtent()).toEqual([250000, 6640000, 270000, 6660000]);
    expect(alertService.showWarning).not.toHaveBeenCalled();
  });

  it('applies what it can and names the rest in one warning that stays until closed', async () => {
    await service.apply('{"taxonIds":[1],"Errors":[2]}', 'not an extent');

    expect(searchFilter.applyFilter).toHaveBeenCalledWith({ taxonIds: [1] });
    expect(mapView.requestedExtent()).toBeNull();
    expect(alertService.showWarning).toHaveBeenCalledTimes(1);
    expect(alertService.showWarning).toHaveBeenCalledWith('filterLink.dropped', { autoDismissMs: 0 });
  });

  it('names dropped Artskart 2 filters as its sidebar did, once each, and keeps unknown names', () => {
    const translate = TestBed.inject(TranslateService);
    translate.setTranslation('no', {
      filterLink: { dropped: 'Ikke brukt: {{names}}', fields: { Errors: 'Kvalitetsproblemer', YearFrom: 'Funn-år', YearTo: 'Funn-år' } },
    });
    translate.use('no');

    service.warnAboutDropped(['Errors', 'YearFrom', 'YearTo', 'constructor', 'pageNumber']);

    expect(alertService.showWarning).toHaveBeenCalledWith('Ikke brukt: Kvalitetsproblemer, Funn-år, constructor, pageNumber', {
      autoDismissMs: 0,
    });
  });

  it("replaces the previous link's warnings when the next link opens", async () => {
    alertService.showWarning.mockReturnValueOnce(1);
    await service.apply('{"taxonIds":[1],"Errors":[2]}', null);

    await service.apply('{"taxonIds":[2]}', null);

    expect(alertService.dismiss).toHaveBeenCalledWith(1);
    expect(alertService.showWarning).toHaveBeenCalledTimes(1);
  });

  it('names what the Artskart 2 guard carried and what the filter link left out in one warning', async () => {
    const translate = TestBed.inject(TranslateService);
    translate.setTranslation('no', { filterLink: { dropped: 'Ikke brukt: {{names}}', fields: { Errors: 'Kvalitetsproblemer' } } });
    translate.use('no');
    alertService.showWarning.mockReturnValueOnce(1);
    service.startLink();
    service.carry(['Errors']);

    await service.apply('{"taxonIds":[1],"period":{"from":2020,"to":2010}}', null);

    expect(searchFilter.applyFilter).toHaveBeenCalledWith({ taxonIds: [1] });
    expect(alertService.showWarning).toHaveBeenCalledTimes(1);
    expect(alertService.showWarning).toHaveBeenCalledWith('Ikke brukt: Kvalitetsproblemer, period', { autoDismissMs: 0 });
  });

  it('leaves the filter panel alone when the link is not JSON', async () => {
    await service.apply('{broken', null);

    expect(searchFilter.applyFilter).not.toHaveBeenCalled();
    expect(alertService.showWarning).toHaveBeenCalledWith('filterLink.unreadable', { autoDismissMs: 0 });
  });
});
