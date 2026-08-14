# Artskart3Client

This project was generated using [Angular CLI](https://github.com/angular/angular-cli) version 21.1.4.

## Development server

To start a local development server, run:

```bash
ng serve
```

Once the server is running, open your browser and navigate to `http://localhost:4200/`. The application will automatically reload whenever you modify any of the source files.

## Code scaffolding

Angular CLI includes powerful code scaffolding tools. To generate a new component, run:

```bash
ng generate component component-name
```

For a complete list of available schematics (such as `components`, `directives`, or `pipes`), run:

```bash
ng generate --help
```

## Building

To build the project run:

```bash
ng build
```

This will compile your project and store the build artifacts in the `dist/` directory. By default, the production build optimizes your application for performance and speed.

## Running unit tests

To execute unit tests with the [Vitest](https://vitest.dev/) test runner, use the following command:

```bash
ng test
```

## Running end-to-end tests

For end-to-end (e2e) testing, run:

```bash
ng e2e
```

Angular CLI does not come with an end-to-end testing framework by default. You can choose one that suits your needs.

## Caching av kartdata

Kartkomponenten bruker en flernivå caching-strategi for å minimere nettverkstrafikk og gi umiddelbar respons ved filter- og zoomendringer.

### Geometri-cache (`geometryCacheByApiZoom`)

- Lagrer `AreaMarkerDto[]` (polygoner, centroider, navn) per API-zoomnivå (1 = fylker, 2 = kommuner).
- Fylles ved oppstart via `prefetchAreaGeometries()` som henter fylker først, deretter kommuner i bakgrunnen.
- **Tømmes aldri** under sesjonen — geometrier endres svært sjelden.
- Zoomnivå 1 inkluderer også havområder og Svalbard/Bjørnøya/Jan Mayen-geometrier.

### Antall-cache (`countsCacheByApiZoom`)

- Lagrer `{ counts: Map<fid, antall>, etag: string | null, unrestricted: boolean }` per zoomnivå.
- `unrestricted`-flagget indikerer om antallene dekker alle områder (`true`) eller kun et utvalg (`false`).
- **Tømmes når attributtfiltre endres** (taksongruppe, kategori, periode, etc.) fordi disse påvirker antall per område.
- **Beholdes ved områdevalg-endringer** — bruker `unrestricted`-flagget for å avgjøre om cachede verdier dekker de nødvendige områdene.

### ETag-støtte

- Backend-endepunktet `POST /api/Search/AreaCounts` returnerer en ETag-header basert på MD5-hash av responsen.
- Frontend sender `If-None-Match` ved etterfølgende forespørsler. Ved treff returnerer backend `304 Not Modified` uten data.
- Backend cacher resultater (antall + ETag) i `IMemoryCache` med en nøkkel basert på SHA256-hash av filteret. TTL er 5 minutter. Dette betyr at gjentatte forespørsler med samme filter hopper over databasespørringen helt.

### Enhetlig oppdatering (`rebuildAllLayers`)

All oppdatering av kartlag skjer gjennom én funksjon: `rebuildAllLayers()`. Den kalles ved:
- Filterendringer (via `_onFilterChange`-effekten)
- Kamerabevegelser (debounced med 150ms via `cameraChanged$`)
- Fullført prefetch av geometrier

Funksjonen oppdaterer alltid **begge** områdelag (fylker og kommuner) samt overlay-laget, og sikrer at ingen lag viser foreldede data uavhengig av zoom- eller filterrekkefølge.

### Overlay-lag

Et eget ikke-klikkbart kartlag (`area-overlay-selected`) viser omrissene av valgte områder uavhengig av zoomnivå. Det inkluderer:
- Valgte fylker og havområder
- Foreldrefylker til valgte kommuner (for kontekst)
- Valgte kommuner

## Kjente forbedringspunkter

- **Listevisningen henter data når den ikke er aktiv**: `list-view.component` trigger `api/Search/Observation`-kall ved filterendringer selv når listfanen ikke er synlig. Bør undersøkes for å unngå unødvendige backend-kall.
- **Service Worker for geometri-caching**: Geometridata (WKT-polygoner for fylker og kommuner) er store og endres sjelden. En Service Worker med Cache API kan lagre disse på tvers av sidelastinger, slik at prefetch-kallene ved oppstart unngås. Dette vil redusere initial lastetid med ca. 1-5 MB i nettverkstrafikk.

## Additional Resources

For more information on using the Angular CLI, including detailed command references, visit the [Angular CLI Overview and Command Reference](https://angular.dev/tools/cli) page.
