import { inject } from '@angular/core';
import { CanActivateFn, Params, RedirectFunction, Route, Router, UrlTree } from '@angular/router';
import { Artskart2LinkService } from '../services/artskart2-link/artskart2-link.service';
import { FilterLinkService } from '../services/filter-link/filter-link.service';
import { EXTENT_LINK_PARAM, FILTER_LINK_PARAM } from '../utils/filter-link';
import { translateArtskart2View } from './artskart2-filter';

/** Observation details: featureInfo and annotation carry what we store as ProxyId, observationInfo carries `Observation.Id`. */
const OBSERVATION_LINK = /^(featureInfo|annotation|observationInfo)\/(.+)$/;

/** The dataset page, whose "show in map" filtered on the import source. */
const DATASET_PAGE = /^dataSet\/[^/]*\/(\d+)$/;

/** A filter Artskart 2 saved for its "create link" button. */
const BOOKMARK = /^bookmark\/([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})$/i;

/** `#<view>/<center>/<zoom>(/background/<bg>)(/id/<observationId>)(/filter/<json>)(/session/<session>)`. */
const VIEW_LINK =
  /^(?:map|list|gallery|statistics)\/([^/]*)\/([^/]*)(?:\/background\/[^/]*)?(?:\/id\/([^/]*))?(?:\/filter\/(.*?))?(?:\/session\/[^/]*)?$/;

/** Turns Artskart 2 links into Artskart 3 ones, so links in articles and on artsdatabanken.no keep working. */
export const artskart2LinkGuard: CanActivateFn = (route) => {
  const router = inject(Router);
  // Some Artskart 2 pages wrote #/dataSet/..., which its router read like #dataSet/...
  const fragment = (route.fragment ?? '').replace(/^\/+/, '');
  const home = (queryParams: Params = {}) => router.createUrlTree(['/'], { queryParams });

  const observation = OBSERVATION_LINK.exec(fragment);
  if (observation) {
    const [, view, id] = observation;
    if (view !== 'observationInfo') return home({ occurrenceId: id });
    return home(isObservationId(id) ? { observationId: id } : {});
  }

  const datasetPage = DATASET_PAGE.exec(fragment);
  const bookmark = BOOKMARK.exec(fragment);
  const viewLink = VIEW_LINK.exec(fragment);
  if (!datasetPage && !bookmark && !viewLink) return true;

  const links = inject(Artskart2LinkService);
  const filterLinks = inject(FilterLinkService);
  filterLinks.startLink();

  /** Redirects to the translated filter, which the home page applies and names what was left out of. */
  const toFilterLink = async (source: unknown, extraParams: Params = {}): Promise<UrlTree> => {
    const translation = await links.translate(source);
    if (!translation) {
      filterLinks.warnUnreadable();
      return home(extraParams);
    }
    filterLinks.carry(translation.dropped);
    return home({ [FILTER_LINK_PARAM]: translation.filter, ...extraParams });
  };

  let redirect: Promise<UrlTree>;
  if (datasetPage) {
    // What "show in map" left set: the import source, the default Found/NotRecovered, and no Blocked.
    redirect = toFilterLink({ NodeIds: [Number(datasetPage[1])], Found: [2], NotRecovered: [2] });
  } else if (bookmark) {
    redirect = links.loadBookmark(bookmark[1]).then((stored) => {
      if (stored !== null) return toFilterLink(stored);
      filterLinks.warn('filterLink.bookmarkNotFound');
      return home();
    });
  } else {
    const [, center, zoom, observationId, json] = viewLink!;
    // A link without a filter opened Artskart 2's default view.
    const source = json === undefined ? { Found: [2], NotRecovered: [2], Blocked: [2] } : parseJson(json);
    const extent = translateArtskart2View(center, zoom);
    redirect = toFilterLink(source, {
      ...(extent ? { [EXTENT_LINK_PARAM]: extent.join(',') } : {}),
      ...(isObservationId(observationId) ? { observationId } : {}),
    });
  }

  // A table or bookmark that can't be loaded, typically in a tab left open across a deploy.
  return redirect.catch(() => {
    filterLinks.warn('filterLink.failed');
    return home();
  });
};

/** Guards normally skip fragment-only changes, such as a link pasted into a tab where the app is already open. */
export const artskart2LinkRouteGuards: Pick<Route, 'canActivate' | 'runGuardsAndResolvers'> = {
  canActivate: [artskart2LinkGuard],
  runGuardsAndResolvers: (from, to) => from.fragment !== to.fragment,
};

/** Artskart 2 ran under /app/, so its links arrive there. */
export const artskart2AppRedirect: RedirectFunction = ({ queryParams, fragment }) =>
  inject(Router).createUrlTree(['/'], { queryParams, fragment: fragment ?? undefined });

function isObservationId(value: string | undefined): value is string {
  return !!value && /^\d+$/.test(value);
}

function parseJson(json: string): unknown {
  try {
    return JSON.parse(json);
  } catch {
    return null;
  }
}
