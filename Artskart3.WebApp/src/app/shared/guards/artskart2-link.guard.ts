import { inject } from '@angular/core';
import { CanActivateFn, Route, Router } from '@angular/router';

/** Artskart 2 linked to one observation as #featureInfo/<id>, where id is usually what we store as ProxyId. */
export const artskart2LinkGuard: CanActivateFn = (route) => {
  const id = /^featureInfo\/(.+)$/.exec(route.fragment ?? '')?.[1];
  return id ? inject(Router).createUrlTree(['/'], { queryParams: { occurrenceId: id } }) : true;
};

/** Guards normally skip fragment-only changes, such as a link pasted into a tab where the app is already open. */
export const artskart2LinkRouteGuards: Pick<Route, 'canActivate' | 'runGuardsAndResolvers'> = {
  canActivate: [artskart2LinkGuard],
  runGuardsAndResolvers: (from, to) => from.fragment !== to.fragment,
};
