# Artskart3.Tests.Integration

xUnit-baserte integrasjonstester for API-laget og repository-laget. Testene verifiserer at hele stabelen — kontroller, tjenester, repositories og database — fungerer korrekt sammen.

Integrasjonstestene er delt i to kategorier:

| Kategori | Factory | Trenger containermotor? | Eksempel |
|---|---|---|---|
| Databaseavhengige | `CustomWebApplicationFactory` + `DatabaseFixture` | **Ja** | `SearchEndpointTests` |
| Filbaserte / ingen DB | `NotificationsWebApplicationFactory` | **Nei** | `NotificationsEndpointTests` |

Dersom en kontroller-handling ikke bruker databasen, bruk `NotificationsWebApplicationFactory` for å holde testen enkel.

---

## Forutsetninger

| Krav | Gjelder | Detaljer |
|---|---|---|
| Containermotor — [Docker Desktop](https://www.docker.com/products/docker-desktop/) eller [Podman](https://podman.io/) | Databaseavhengige tester | Testcontainers starter en SQL Server-container automatisk. Podman fungerer like godt, se under. |
| .NET 10 SDK | Alle tester | Samme versjon som resten av løsningen. |

### Starte Docker Desktop (kun nødvendig for databaseavhengige tester)

1. Åpne **Docker Desktop** fra Start-menyen
2. Vent til ikonet i systemstatusfeltet viser at Docker kjører (grønt ikon / «Docker Desktop is running»)
3. Kjør deretter testene som normalt — Testcontainers håndterer resten automatisk

### Podman

Testcontainers trenger ikke Docker spesifikt, bare et Docker-kompatibelt API. Podman
videresender et slikt API, og gjør det på Dockers standardadresse — så `DOCKER_HOST`
skal IKKE settes:

```powershell
podman machine start
```

Utskriften er det som betyr noe:

```
API forwarding listening on: npipe:////./pipe/docker_engine

Docker API clients default to this address. You do not need to set DOCKER_HOST.
```

Står den linjen der, kjører testene som normalt.

> **EN KJØRENDE MASKIN ER IKKE NOK — VIDERESENDINGEN MÅ OGSÅ VÆRE OPPE**
> `podman machine list` kan vise «Currently running» samtidig som API-videresendingen
> er nede. Da finnes ingen `docker_engine`-pipe, og Testcontainers ser nøyaktig det
> samme som om ingen containermotor var installert — samme feilmelding, ingen antydning
> om at Podman faktisk kjører. Dette har kostet en feilsøkingsrunde før.
>
> Sjekk videresendingen, ikke maskinen. Det må gjøres ved å KOBLE TIL pipen — både
> `Test-Path '\\.\pipe\docker_engine'` og `Get-ChildItem '\\.\pipe\'` svarer «finnes
> ikke» for en pipe som fungerer, og `podman system connection list` viser de samme
> `ssh://`-URI-ene uansett om videresendingen er oppe:
>
> ```powershell
> $p = New-Object System.IO.Pipes.NamedPipeClientStream('.', 'docker_engine', 'InOut')
> try { $p.Connect(2000); 'Videresending OK'; $p.Dispose() } catch { 'Ingen videresending' }
> ```
>
> Får du «Ingen videresending», er `podman machine stop` etterfulgt av
> `podman machine start` nok — videresenderen settes opp ved oppstart. Har du ingen
> containere i gang, koster det ingenting.

> **Feilmelding uten containermotor:**
> ```
> System.ArgumentException : Docker is either not running or misconfigured.
> ```
> Meldingen nevner Docker uansett motor, og skiller ikke mellom «ikke installert» og
> «installert, men videresendingen er nede» — se boksen over. Kun tester som bruker
> `CustomWebApplicationFactory` er berørt. Enhetstester (`Artskart3.Tests.Unit`) og
> filbaserte integrasjonstester (`NotificationsEndpointTests`) kan kjøres uten.

---

## Hvordan integrasjonstestene fungerer

Integrasjonstestene bruker to nøkkelkomponenter:

### Testcontainers

[Testcontainers](https://dotnet.testcontainers.org/) starter en ekte **SQL Server 2022**-container i containermotoren for hver testkjøring. Containeren:
- Startes automatisk når testsamlingen initialiseres (`DatabaseFixture.InitializeAsync`)
- Får EF Core-skjemaet opprettet via `EnsureCreatedAsync`
- Lastes med testdata fra `SeedData/seed_data.sql` hvis filen finnes
- Stoppes og slettes automatisk når testsamlingen er ferdig

### CustomWebApplicationFactory

`CustomWebApplicationFactory` erstatter den registrerte `DbContext`-instansen med én som peker mot testcontainerens tilkoblingsstreng. Dette betyr at testene treffer den ekte API-pipeline — autentisering, mellomvare, validering og alt — men mot en isolert testdatabase.

### Delt container

All `[Collection(nameof(DatabaseCollection))]`-tester deler én enkelt container via `DatabaseFixture`. Dette er bevisst for å unngå at containeren startes og stoppes for hver testklasse, noe som ville gjort testkjøringen vesentlig tregere.

---

## Kjøre integrasjonstestene

```powershell
dotnet test Artskart3.Tests.Integration
```

Med dekningsrapport:
```powershell
dotnet test Artskart3.Tests.Integration --settings coverage.runsettings --collect "XPlat Code Coverage"
```

Fra Visual Studio: åpne **Test Explorer** og kjør testene i `Artskart3.Tests.Integration`-prosjektet.

---

## Testdata (seed data)

Se [`SeedData/README.md`](SeedData/README.md) for instruksjoner om hvordan du genererer og laster testdata. Uten `seed_data.sql` kjøres testene mot en tom database — de fleste tester er skrevet for å håndtere dette og returnerer tomme lister der det er forventet.

---

## Konvensjon: EndpointCoverageTests

`EndpointCoverageTests` feiler automatisk hvis en kontroller-handling i `Artskart3.Api` mangler minst én integrasjonstest. Konvensjonen er at testmetodenavnet **må starte med handlingens metodenavn**:

| Kontroller-handling | Gyldig testmetodenavn (eksempel) |
|---|---|
| `GetNotifications` | `GetNotifications_Returns200WithJsonArray` |
| `SearchTaxons` | `SearchTaxons_WithValidName_Returns200WithJsonArray` |
| `GetLocations` | `GetLocations_WithNoFilter_Returns200WithGeoJson` |

Når du legger til en ny kontroller-handling, legg til en tilsvarende integrasjonstest i en passende `*EndpointTests.cs`-fil.
