# Artskart3.Tests.Integration

xUnit-baserte integrasjonstester for API-laget og repository-laget. Testene verifiserer at hele stabelen — kontroller, tjenester, repositories og database — fungerer korrekt sammen.

Integrasjonstestene er delt i to kategorier:

| Kategori | Factory | Trenger Docker? | Eksempel |
|---|---|---|---|
| Databaseavhengige | `CustomWebApplicationFactory` + `DatabaseFixture` | **Ja** | `SearchEndpointTests` |
| Filbaserte / ingen DB | `NotificationsWebApplicationFactory` | **Nei** | `NotificationsEndpointTests` |

Dersom en kontroller-handling ikke bruker databasen, bruk `NotificationsWebApplicationFactory` for å holde testen enkel.

---

## Forutsetninger

| Krav | Gjelder | Detaljer |
|---|---|---|
| [Docker Desktop](https://www.docker.com/products/docker-desktop/) | Databaseavhengige tester | Testcontainers bruker Docker til å starte en SQL Server-container automatisk. |
| .NET 10 SDK | Alle tester | Samme versjon som resten av løsningen. |

### Starte Docker Desktop (kun nødvendig for databaseavhengige tester)

1. Åpne **Docker Desktop** fra Start-menyen
2. Vent til ikonet i systemstatusfeltet viser at Docker kjører (grønt ikon / «Docker Desktop is running»)
3. Kjør deretter testene som normalt — Testcontainers håndterer resten automatisk

> **Feilmelding uten Docker:**
> ```
> System.ArgumentException : Docker is either not running or misconfigured.
> ```
> Denne feilen betyr at Docker Desktop ikke kjører. Kun tester som bruker `CustomWebApplicationFactory` er berørt. Enhetstester (`Artskart3.Tests.Unit`) og filbaserte integrasjonstester (`NotificationsEndpointTests`) kan kjøres uten Docker.

---

## Hvordan integrasjonstestene fungerer

Integrasjonstestene bruker to nøkkelkomponenter:

### Testcontainers

[Testcontainers](https://dotnet.testcontainers.org/) starter en ekte **SQL Server 2022**-container i Docker for hver testkjøring. Containeren:
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
