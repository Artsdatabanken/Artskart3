import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { EMPTY, Observable, catchError, debounceTime, distinctUntilChanged, of, switchMap, tap } from 'rxjs';
import { SearchFilterService } from '../search-filter/search-filter.service';
import { ObservationSearchFilter } from '../../types/api.types';

/**
 * Nøklene backend kjenner. Må stemme med FacetCountCacheDimensions i Core — en nøkkel
 * som ikke finnes der hoppes over serverside, og tallet uteblir uten feilmelding.
 */
export type FacetDimensionKey = 'taxonGroup' | 'category' | 'behavior' | 'basisOfRecord' | 'institution' | 'registrationStatus';

export type FacetBoolKey = 'withImages' | 'sensitive' | 'changedByAnnotation' | 'uncertainIdentification' | 'introduced';

/**
 * Formen på svaret fra /api/Search/FacetCounts.
 *
 * Skrevet for hånd her, ikke hentet fra api.generated.ts: den regenereres med
 * `npm run generate-api-types` mot et kjørende API, og endepunktet er nytt. Byttes ut
 * med `components['schemas']['FacetCountsResponseDto']` ved neste regenerering.
 */
export interface FacetBucket {
  id: number;
  count: number;
}

export interface FacetCounts {
  key: string;
  buckets: FacetBucket[];
  source: string;
}

export interface FacetBoolCounts {
  key: string;
  whenTrue: number;
  whenFalse: number;
}

export interface FacetAreaCount {
  fid: string;
  count: number;
}

export interface FacetCountsResponse {
  dimensions: FacetCounts[];
  boolFilters: FacetBoolCounts[];
  areas: FacetAreaCount[];
}

/** Tallene slått opp per bøtte-id, klare for maler. */
export type FacetCountMap = ReadonlyMap<number, number>;

const EMPTY_COUNTS: FacetCountMap = new Map<number, number>();

/**
 * Antall observasjoner ved hvert valg i filterpanelet.
 *
 * HENTER BARE FOR ÅPNE GRUPPER
 * En gruppe melder seg på når accordionen åpnes og av når den lukkes. Det er det som
 * holder kostnaden nede: Artskart 2 hadde kryss-oppdatering av alle gruppene og skrudde
 * den av igjen (`refreshOthers` står kommentert ut i treeview.js), antakelig fordi N
 * aggregeringer per filterklikk ble for dyrt.
 *
 * ETT KALL, UANSETT HVOR MANGE GRUPPER
 * Alle åpne grupper sendes i samme forespørsel. Serveren kjører én spørring per fasett,
 * men klienten venter på én runde.
 *
 * DEBOUNCE OG KANSELLERING
 * Raske klikk i panelet skal ikke stable opp forespørsler. `switchMap` kansellerer den
 * forrige — AK3s versjon av Artskart 2s `promiseKeeper`.
 *
 * FORRIGE SVAR BLIR STÅENDE
 * Mens et kall er underveis, og om det feiler, beholdes tallene fra forrige runde.
 * `toSignal` holder på siste utsendte verdi, og et feilet kall sender ingenting
 * (`catchError` → `EMPTY`). Tallene er en hjelp, ikke noe brukeren er avhengig av —
 * de skal ikke blinke bort, og en feil skal ikke velte panelet.
 *
 * TALLENE FØLGER FILTERET, IKKE KARTUTSNITTET. Det er med vilje, og det er en
 * forutsetning for at serveren kan svare fra en forhåndsberegnet buffer.
 */
@Injectable({
  providedIn: 'root',
})
export class FacetCountService {
  private readonly http = inject(HttpClient);
  private readonly searchFilter = inject(SearchFilterService);

  private readonly endpoint = '/api/Search/FacetCounts';

  private readonly openDimensions = signal<ReadonlySet<FacetDimensionKey>>(new Set());
  private readonly openBoolGroups = signal<ReadonlySet<FacetBoolKey>>(new Set());

  /**
   * Områdelistene er en egen bryter og ikke en dimensjonsnøkkel: svaret nøkles på Fid
   * og ikke på en bøtte-id, fordi geografi er flerverdi i bufferen — én bøtte er hele
   * settet av områder en observasjon tilhører.
   */
  private readonly areasOpen = signal(false);

  private readonly loadingSignal = signal(false);

  /** True mens et kall er underveis, til å dempe tallene i panelet. */
  readonly loading = this.loadingSignal.asReadonly();

  /** Hva som skal hentes nå: filteret pluss de gruppene som står åpne. */
  private readonly request = computed(() => ({
    filter: this.searchFilter.observationFilter(),
    dimensions: [...this.openDimensions()].sort(),
    boolFilters: [...this.openBoolGroups()].sort(),
    includeAreas: this.areasOpen(),
  }));

  private readonly counts = toSignal(
    toObservable(this.request).pipe(
      distinctUntilChanged((a, b) => JSON.stringify(a) === JSON.stringify(b)),
      debounceTime(200),
      switchMap((request) => {
        // Ingen åpne grupper er ikke en forespørsel verdt å sende.
        if (request.dimensions.length === 0 && request.boolFilters.length === 0 && !request.includeAreas) {
          this.loadingSignal.set(false);
          return of(null);
        }

        this.loadingSignal.set(true);

        return this.fetch(request.filter, request.dimensions, request.boolFilters, request.includeAreas).pipe(
          tap(() => this.loadingSignal.set(false)),
          catchError(() => {
            this.loadingSignal.set(false);
            // Ingen utsending: toSignal beholder forrige svar.
            return EMPTY;
          }),
        );
      }),
    ),
    { initialValue: null as FacetCountsResponse | null },
  );

  private readonly dimensionCounts = computed(() => {
    const kart = new Map<string, FacetCountMap>();
    for (const dimensjon of this.counts()?.dimensions ?? []) {
      kart.set(dimensjon.key, new Map(dimensjon.buckets.map((b) => [b.id, b.count])));
    }
    return kart;
  });

  private readonly areaCounts = computed(
    () => new Map((this.counts()?.areas ?? []).map((a) => [a.fid, a.count])),
  );

  /** True når områdelistene faktisk er hentet, altså når et manglende område er 0. */
  private readonly hasAreaCounts = computed(() => this.counts()?.areas !== undefined);

  private readonly boolCounts = computed(() => {
    const kart = new Map<string, FacetBoolCounts>();
    for (const gruppe of this.counts()?.boolFilters ?? []) {
      kart.set(gruppe.key, gruppe);
    }
    return kart;
  });

  /**
   * Tallene for én dimensjon, eller en tom oppslagstabell om gruppen ikke er åpen eller
   * svaret ikke har kommet.
   *
   * En bøtte som mangler har NULL treff, ikke ukjent antall: serveren utelater bøtter
   * uten treff. Malen skal vise 0.
   */
  countsFor(key: FacetDimensionKey): FacetCountMap {
    return this.dimensionCounts().get(key) ?? EMPTY_COUNTS;
  }

  /** True når tallene for denne gruppen er hentet, altså når 0 betyr «ingen treff». */
  hasCountsFor(key: FacetDimensionKey): boolean {
    return this.dimensionCounts().has(key);
  }

  /** Antallet for ett bool-alternativ, eller null om det ikke er hentet ennå. */
  boolCountFor(key: FacetBoolKey, value: boolean): number | null {
    const gruppe = this.boolCounts().get(key);
    if (!gruppe) return null;
    return value ? gruppe.whenTrue : gruppe.whenFalse;
  }

  /**
   * Antallet for ett område, eller null når områdelistene ikke er hentet ennå.
   *
   * Et område som mangler i svaret har NULL treff — serveren utelater dem — så et
   * manglende tall i en HENTET liste er 0, ikke ukjent.
   */
  areaCountFor(fid: string | null | undefined): number | null {
    if (fid == null || !this.hasAreaCounts()) return null;
    return this.areaCounts().get(fid) ?? 0;
  }

  /** Meld en gruppe på eller av. Kalles fra accordionens toggle. */
  setDimensionOpen(key: FacetDimensionKey, open: boolean): void {
    this.openDimensions.update((current) => toggle(current, key, open));
  }

  setBoolGroupOpen(key: FacetBoolKey, open: boolean): void {
    this.openBoolGroups.update((current) => toggle(current, key, open));
  }

  setAreasOpen(open: boolean): void {
    this.areasOpen.set(open);
  }

  private fetch(
    filter: ObservationSearchFilter,
    dimensions: readonly string[],
    boolFilters: readonly string[],
    includeAreas: boolean,
  ): Observable<FacetCountsResponse> {
    const params: Record<string, string> = {};
    if (dimensions.length) params['dimensions'] = dimensions.join(',');
    if (boolFilters.length) params['boolFilters'] = boolFilters.join(',');
    if (includeAreas) params['includeAreas'] = 'true';

    return this.http.post<FacetCountsResponse>(this.endpoint, filter, { params });
  }
}

function toggle<T>(current: ReadonlySet<T>, key: T, open: boolean): ReadonlySet<T> {
  if (open === current.has(key)) return current;

  const neste = new Set(current);
  if (open) {
    neste.add(key);
  } else {
    neste.delete(key);
  }
  return neste;
}
