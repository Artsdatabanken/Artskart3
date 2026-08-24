# Artskart3
Ny versjon av Artskart, som per idag (april 2026) ligger på artskart.artsdatabanken.no (videre omtalt som Artskart 2).
Første innsats i Artskart 3 har som mål å erstatte Artskart 2, så snart det er mulig. Videre er det ambisjoner om å utvide Artskart med mer funksjonalitet enn det som finnes i Artskart 2.

Noen viktige mål
- Få bukt med ytelsesproblemer
- Ny og foredret backend som er mer modulær og oversiktlig
- Få en er moderne frontend
- Få en portal som er i henhold til Artsdatabankens designsystem og visuelle identitet

## Requirements
* Node versjon 22.14 for Angular frontend
* Angular CLI 21.1
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
  "ConnectionStrings:ArtskartIndex": "data source=localhost;initial catalog=Artskart3Index;Integrated Security=true;MultipleActiveResultSets=True;App=EntityFramework;TrustServerCertificate=True",
  "ClientSafeList": "127.0.0.1;::1"
}
```

##### Command line
```powershell
cd Artskart3.Api
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:ArtskartIndex" "data source=localhost;initial catalog=Artskart3Index;Integrated Security=true;MultipleActiveResultSets=True;App=EntityFramework;TrustServerCertificate=True"
dotnet user-secrets set "ClientSafeList" "127.0.0.1;::1"
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
