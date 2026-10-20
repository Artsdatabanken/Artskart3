# Artskart3
Ny versjon av Artskart, som per idag (april 2026) ligger på artskart.artsdatabanken.no (videre omtalt som Artskart 2).
Første innsats i Artskart 3 har som mål å erstatte Artskart 2, så snart det er mulig. Videre er det ambisjoner om å utvide Artskart med mer funksjonalitet enn det som finnes i Artskart 2.

Noen viktige mål
- Få bukt med ytelsesproblemer
- Ny og foredret backend som er mer modulær og oversiktlig
- Få en er moderne frontend
- Få en portal som er i henhold til Artsdatabankens designsystem og visuelle identitet

## Requirements
* Node.js 24 LTS (minst 24.15.0, som Angular 22 krever) for Angular frontend
* Angular CLI 22.1
* Visual Studio 2026 er nødvendig for .NET 10
* [Docker Desktop](https://www.docker.com/products/docker-desktop/) — kreves for å kjøre integrasjonstester (Testcontainers starter en SQL Server-container automatisk)

* installasjon av lokale nbic-map-komponent
Pakken er tilgjengelig på npm: https://www.npmjs.com/package/@artsdatabanken/nbic-map-component
Install the package from `artskart3.webapp`:

    ```bash
    npm install @artsdatabanken/nbic-map-component
    ```

[Connecting to GitHub with SSH](https://docs.github.com/en/authentication/connecting-to-github-with-ssh) og spesifikt seksjonen Generate new SSH Key.
## Kjøre Artskart 3 lokalt
* Kjør `gh repo clone Artsdatabanken/Artskart3` eller bruk Github Desktop for å laste ned repositoriet
### Frontend
* Åpne Artskart 3 i terminalen og kjør `cd .\Artskart3.WebApp\; npm i` for å installere pakkene i frontend
* Kjør kommandoen `ng serve` fra Artskart3/Artskart3.Webapp/ i terminalen for å starte frontend 

### Backend (API)
* Åpne løsningsfilen `Artskart3.slnx` i Visual Studio/Rider og kjør prosjektet med profilen `Artskart3.Api`
* API-et starter på `https://localhost:5088`
* Swagger UI er tilgjengelig på `https://localhost:5088/swagger`
* Helsesjekk for databasetilkobling og andre avhengigheter: `https://localhost:5088/hc`

> **NB:** Første gang du kjører API-et på HTTPS må du sette opp trust mot det lokale utviklersertifikatet:
> ```powershell
> dotnet dev-certs https --trust
> ```

#### Database
Du vil trenge en sql server database for å kjøre apiet. En .bacpac fil ligger på Teams (Artskart->General->Shared->NyeArtskart->DatabaseDumps->Artskart2Index-2026-3-12-14-27.bacpak) hvis du ønsker å starte med en database med data. Ønsker du å starte med en tom database, se Databasemigrasjoner lenger ned.

#### Sett opp User Secrets
##### Visual studio/rider
VS: Høyreklikk prosjektet og velg "Manage User Secrets"
Rider: Høyreklikk prosjektet og velg Tools->.Net user Secrets

Filen skal se noe slik ut, endre connectionstring ved behov.
```
{
  "ConnectionStrings:ArtskartIndex": "data source=localhost;initial catalog=Artskart3Index;Integrated Security=true;MultipleActiveResultSets=True;App=EntityFramework;TrustServerCertificate=True"
}
```

##### Command line
```powershell
cd Artskart3.Api
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:ArtskartIndex" "data source=localhost;initial catalog=Artskart3Index;Integrated Security=true;MultipleActiveResultSets=True;App=EntityFramework;TrustServerCertificate=True"
```

Valgfritt — legg til Application Insights ConnectionString hvis du skal teste det lokalt

### Databasemigrasjoner (EF Core Code-First)
Prosjektet bruker EF Core code-first migrasjoner. Alle migrasjoner kjøres fra `Artskart3.Infrastructure`-mappen.

**Forutsetninger** — installer EF Core-verktøyet globalt én gang:
```powershell
dotnet tool install --global dotnet-ef
```

**Legge til en ny migrasjon** etter å ha endret en domeneklasse:
```powershell
cd Artskart3.Infrastructure
dotnet ef migrations add <NavnPåMigrasjon> --startup-project ..\Artskart3.Api
```

**Kjøre migrasjoner mot lokal database:**
```powershell
cd Artskart3.Infrastructure
dotnet ef database update --context ArtskartDbContext --startup-project ..\Artskart3.Api
```

**Angre siste migrasjon** (kun hvis den ikke er kjørt mot databasen ennå):
```powershell
cd Artskart3.Infrastructure
dotnet ef migrations remove --startup-project ..\Artskart3.Api
```

> Migrasjonsfilene ligger i `Artskart3.Infrastructure/Migrations/`. Ikke rediger disse manuelt etter at de er kjørt mot en delt database.

## Kjøre Artskart 3 i containere lokalt

Det vanlige utviklingsoppsettet over er fortsatt det du bruker til daglig. Dette er for
å kjøre løsningen slik den kjører i Azure Container Apps — tre containere, med
frontend bak nginx. Se [CONTAINERS.md](CONTAINERS.md) for topologi, miljøvariabler og
det drift trenger.

**Forutsetninger:** podman eller docker, og en SQL Server du kan nå over TCP med
SQL-pålogging. LocalDB virker ikke — den lytter bare på en lokal named pipe. Se
kommentarene i `.env.example` for hva som må være på plass på instansen.

```bash
cp .env.example .env        # fyll inn DB_CONNECTION_STRING
podman compose up -d --build
```

| | |
| --- | --- |
| Frontend | https://localhost:49219 |
| Swagger | https://localhost:49219/swagger |
| Readiness (database) | https://localhost:49219/hc/ready |
| Liveness | https://localhost:49219/hc/live |
| nginx liveness | https://localhost:49219/healthz |
| Workers | http://localhost:8082/health |
| Hangfire | http://localhost:8082/hangfire |
| API direkte (feilsøking) | http://localhost:8081 |

Web-containeren svarer på tre porter: 49219 og 8443 over TLS, 8080 over http. Bruk
**49219** — det er opphavet Angular dev-serveren bruker, og den returadressen er
allerede registrert hos identitetsleverandøren, så pålogging virker. Alt skal gå
gjennom web-containeren; API-et er bare publisert direkte for feilsøking, og har ingen
ekstern ingress i Azure.

**Unngå sertifikatadvarsel** ved å bruke ASP.NET sitt utviklersertifikat, det samme
Visual Studio-oppsettet bruker:

```bash
podman compose -f docker-compose.yml -f docker-compose.devcert.yml up -d --build web
```

Uten den overstyringen lager containeren et selvsignert sertifikat, og nettleseren
klager. Sertifikatfilene legges på plass av `prestart`-scriptet i
`Artskart3.WebApp/package.json`; har du kjørt `npm start` en gang, finnes de.

**Nyttige kommandoer:**

```bash
podman logs -f artskart3-web-1       # nginx: hvilke stier treffer hva
podman logs -f artskart3-api-1       # API: forespørsler og trege spørringer
podman compose restart api           # etter en endring i .env
podman compose down                  # stopp alt
```

> Migrasjoner kjøres ikke fra compose, og `Database__AutoMigrate` er `false`. Basen din
> har skjemaet allerede, og oppsettet skal ikke skrive til den. Trenger du migrasjoner,
> kjør dem som beskrevet over.

## Testdekning (code coverage)

Prosjektet bruker `coverage.runsettings` for å ekskludere generert kode (EF Core-migrasjoner, OpenAPI-generatorer, kompilatorgenererte typer) fra dekningsanalysen.

### Kjøre tester med dekningsrapport

**Kjør alle tester og samle dekningsdata:**
```powershell
dotnet test --settings coverage.runsettings --collect "XPlat Code Coverage"
```

**Kjør kun enhetstester:**
```powershell
dotnet test Artskart3.Tests.Unit --settings coverage.runsettings --collect "XPlat Code Coverage"
```

**Kjør kun integrasjonstester:**

> **Forutsetning:** Docker Desktop må kjøre. Testcontainers starter og stopper en SQL Server-container automatisk for hver testkjøring.
> Se [`Artskart3.Tests.Integration/README.md`](Artskart3.Tests.Integration/README.md) for detaljer.

```powershell
dotnet test Artskart3.Tests.Integration --settings coverage.runsettings --collect "XPlat Code Coverage"
```

Dekningsrapporten lagres som en `coverage.cobertura.xml`-fil under `TestResults/`-mappen i hvert testprosjekt.

### Generere HTML-rapport

Installer rapportverktøyet én gang globalt:
```powershell
dotnet tool install --global dotnet-reportgenerator-globaltool
```

Generer HTML-rapport fra alle innsamlede filer:
```powershell
reportgenerator -reports:"**/TestResults/**/coverage.cobertura.xml" -targetdir:"TestResults/CoverageReport" -reporttypes:Html
```

Åpne rapporten i nettleseren:
```powershell
Start-Process "TestResults/CoverageReport/index.html"
```

## Navngiving av branches
Standariserer navngiving av branches er `feature/navn-på-branch` som for eksempel: `feature/authentication` for features og `bugfix/fix-ip-blocking` hvis det er en bugfix. 

## Merging av endringer
For å gjøre endringer i Artskart krever det at det lages en pull request som må godkjennes av en annen utvikler.

## Varsler (notifications)
Driftsmeldinger og andre varsler som vises i portalen styres via en JSON-fil som redigeres manuelt av en superbruker. Se [`Artskart3.Infrastructure/Data/JsonDb/README.md`](Artskart3.Infrastructure/Data/JsonDb/README.md) for feltbeskrivelse og fremgangsmåte for å legge til nye varsler.

## Detaljer om funn

Et funn i kartets funnliste åpner et detaljpanel. Listen beholdes i DOM-en med
gruppering, åpne grener og rulleposisjon. Forrige/neste følger treets rekkefølge,
også gjennom lukkede grener, og går rundt ved første/siste funn. Detaljene kan
utvides med knappen i smal visning. Tilbake-knappen vises bare i fullskjerm og
går tilbake til smal detaljvisning; lukk viser listen igjen fra begge visninger.
Det valgte funnet markeres i hovedkartet
uten panorering eller endring av zoom, også ved forrige/neste og direkte lenker.

Lenker på formen `/?observationId=123` åpner et enkelt funn. Hvert skifte av
funn legger til en oppføring i nettleserhistorikken. En lenke uten gjeldende
listekontekst viser ett funn uten forrige/neste; filtre og listeutvalg lagres
ikke i URL-en. Endrede filtre eller et nytt kartutvalg avslutter den gamle
detaljøkten.

API:

- `GET /api/observations/{id}` returnerer offentlige opplysninger og
  bildemetadata, aldri bildeblob eller base64. Vanlig BFF-header `X-CSRF: 1`
  kreves som for andre JSON-endepunkter.
- `GET /api/observations/{id}/media/{mediaId}/image` leverer lagret bilde
  ved behov. Nettleseren prøver først `MediaFile.Origin`, deretter blob-endepunktet
  én gang. Kun støttede rasterbildetyper vises; tomme bildegallerier skjules.
- `GET /api/observations/{id}/maps/local` og `/maps/overview` returnerer
  henholdsvis 800x800 og 500x800 PNG. Begge bildeendepunktene er offentlige,
  skrivebeskyttede og unntatt BFF-headerkravet slik at de virker i `<img>`.
  Slettede funn/bilder eller bilder som tilhører et annet funn, utleveres ikke.

Kartene genereres med SkiaSharp fra faste HTTPS WMS-kilder hos Kartverket
(Norges grunnkart, terrengmodell og GEBCO) og Norsk Polarinstitutt (Svalbard).
Kildelenker vises under kartene og i bildeviseren. Ingen vilkårlige WMS-adresser,
lag, bildestørrelser eller koordinater kan angis av API-klienten.
Offentlige koordinater brukes gjennomgående; `SensitiveObservationData`
brukes ikke. Lokalkartet er omtrent 82 km bredt, og oversiktskartet utvides
ved behov for å inkludere funnet.

Rendereren har fire samtidige jobber, en egen 64 MiB minnebuffer med 15 minutters
levetid, 25 sekunders samlet tidsgrense og maksimalt 12 MiB per WMS-svar.
Kartendepunktet tillater fire samtidige forespørsler og 16 i kø; over dette
returneres 429. Feil fra kartkilder gir 502/504 og kan prøves på nytt i panelet.
Bilder støtter ETag og fem minutters HTTP-mellomlagring. Deployment må inkludere
SkiaSharps native Linux-bibliotek (pakken `SkiaSharp.NativeAssets.Linux.NoDependencies`);
en bygging på macOS alene verifiserer ikke Linux-runtime.

Vurderingslenker velges fra aktive `TaxonProperty`-rader for riktig kategori og
område, med siste årstall i prefikset. Tvetydige/udaterte lenker utelates og
logges. For eldre importerte taxa brukes den eksisterende offentlige takson-ID-en
når `ExternalTaxonId` ikke er fylt ut. Dataleverandørlenke utelates inntil modellen
har en pålitelig URL-kilde.


## Begrenset tilgjengelighet til miljøene
Per juli 2026 er alle miljøene kun tilgjengelig dersom man er tilkoblet Artsdatabankens nettverk direkte eller via VPN.

* Testing i teamet: artskart3.test.artsdatabanken.no (20.251.135.164)
* Testing før release til produksjon: artskart3-staging.test.artsdatabanken.no (20.251.135.164)
* Produksjon/Beta: artskart3.artsdatabanken.no (51.120.48.232)

# Produksjonssetting
* Opprett en PR som slår sammen develop-branchen inn i staging-branchen
* Når en reviewer har godkjent PR-en, velg merge. Staging-branchen blir oppdatert og lukkes.
* Opprett en PR som slår sammen staging-branchen inn i main-branchen
* Når en reviewer har godkjent PR-en, velg merge. Main-branchen blir oppdatert og lukkes. 
* Flytt issues fra "Accepted" til "Done" i projsktet-boardet. FERDIG.

## Ytelsesoptimalisering

### Database: Indekser

| Dato | Beskrivelse |
|------|-------------|
| Apr 2026 | Lagt til komposittindekser på søkefilterkolonner i Observation-tabellen (`LocationId`, `TaxonGroupId`, `CategoryId`, `YearCollected`, `MonthCollected` m.m.) |
| Jun 2026 | Lagt til dekkende indeks på `Area(AreaTypeId, Fid, IsCurrent)`, `LocationAreas(AreaId, LocationId)` og `OrganizationRelation(OrganizationId, ObservationId)` for raskere joins |
| Jun 2026 | Erstattet to separate indekser på `ObservationEntityIndex` med en dekkende komposittindeks `(EntityTypeId, EntityId, ObservationId)`, pluss indeks på `Observation.DateTimeCollected` og `Area(ZoomLevel, IsCurrent)` |

### Database: Denormalisering

| Dato | Beskrivelse |
|------|-------------|
| Jun 2026 | Opprettet `ObservationEntityIndex`-tabell som flat projeksjon av observasjon-til-omrade-relasjoner — eliminerer dyre multi-tabell-joins for filtrering pa kommune, fylke, verneomrade, havomrade og institusjon |
| Jun 2026 | Byttet fra `AreaFid` (string) til `EntityId` (int) i indekstabellen for raskere heltallssammenlikning |

### Database: Sporringsoptimalisering

| Dato | Beskrivelse |
|------|-------------|
| Mai 2026 | Erstattet Include-basert eager loading med `.Select()`-projeksjon slik at databasen kun returnerer kolonner som faktisk brukes i DTO-en |
| Jun 2026 | Omskrev kommune-/fylkesfiltre fra korrelerte subsporreringer (`Location.Areas.Any()`) til to-stegs oppslag via `ObservationEntityIndex` |
| Aug 2026 | Slo sammen to databasesporreringer til en for lokasjonsendepunktet — GROUP BY med JOIN i stedet for aggregering + separat koordinatoppslag |
| Aug 2026 | Flyttet envelope-filter fra `Observation.East/North` (ingen indeks) til `Location.East/North` (bruker `IX_EastNorth`) |

### API: Payload-reduksjon

| Dato | Beskrivelse |
|------|-------------|
| Aug 2026 | Fjernet duplisert CRS per feature i GeoJSON (~5.5 MB spart ved 100k features) |
| Aug 2026 | Erstattet GeoJSON med kompakt array-format `[id, lon, lat, count]` (~7x reduksjon) |
| Aug 2026 | Fjernet `Locality` fra bulk-lokasjonsdata |
| Aug 2026 | Forenklet `LocationModel` fra 15+ felt til 4, fjernet ubrukte felt fra DB-projeksjon |

### API: Stromming og minnebruk

| Dato | Beskrivelse |
|------|-------------|
| Aug 2026 | Strommer JSON direkte til `HttpResponse.Body` via `Utf8JsonWriter` i stedet for a mellomlagre som streng |
| Aug 2026 | Fjernet unodvendig `IAsyncEnumerable`-wrapping rundt ferdig materialisert liste |

### Frontend

| Dato | Beskrivelse |
|------|-------------|
| Jun 2026 | Erstattet Angular pipe (`AreaNamePipe`) per rad i listevisning med forhandsberegnet `Map`-oppslag via computed signal — unngår lineart sok per rad per change detection |
| Aug 2026 | Forenklet lokasjonsparsing fra fire metoder (regex-basert GeoJSON-parsing) til en enkel array-parser |

### Infrastruktur

| Dato | Beskrivelse |
|------|-------------|
| Jun 2026 | Lagt til `SlowQueryLog`-tabell og action filter som logger sporreringer over konfigurerbar terskel — muliggjor identifisering av trege endepunkter |
