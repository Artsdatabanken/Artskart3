# Artskart3.Tests.Performance

BenchmarkDotNet-baserte ytelsestester for `SearchService`- og repository-laget. Testene kjøres mot en ekte SQL Server-database med produksjonslignende datamengder for å gi meningsfulle resultater.

---

## Hvordan benchmark-tester fungerer

En benchmark-test er en automatisert måling av hvor lang tid og hvor mye minne en kodeoperasjon bruker. I motsetning til vanlige enhetstester som kun sjekker korrekthet, måler benchmark-tester ytelse.

**BenchmarkDotNet** håndterer all målelogikk automatisk:

- **Oppvarming (warmup)** — metoden kjøres først et antall ganger uten å måle, slik at JIT-kompilatoren får kompilert og optimalisert koden. Dette sikrer at målingene reflekterer reell ytelse, ikke oppstartskostnader.
- **Iterasjoner** — etter oppvarming kjøres metoden mange ganger, og resultatene aggregeres statistisk. Dette gir stabile målinger selv om enkeltmålinger varierer.
- **Statistikk** — BenchmarkDotNet rapporterer gjennomsnitt, median, standardavvik og konfidensintervall per benchmark.
- **Minnediagnostikk** — med `[MemoryDiagnoser]`-attributtet rapporteres også antall minneallokeringer og GC-samlinger per operasjon.

### Hva resultatene betyr

| Kolonne | Betydning |
|---|---|
| `Mean` | Gjennomsnittlig kjøretid per operasjon |
| `StdDev` | Standardavvik — høy verdi indikerer ustabile målinger |
| `Allocated` | Gjennomsnittlig minneallokering per operasjon |
| `Gen0/1/2` | Antall GC-samlinger per 1000 operasjoner |

### Viktig å vite

- Benchmarks **må** kjøres i `Release`-modus for riktige resultater. `Debug`-bygg er ikke optimalisert og vil gi kunstig høye tider.
- Maskinen som kjører testene påvirker absolutte tall — sammenlign alltid mot en baseline fra **samme maskin** for meningsfulle konklusjoner.
- Nattlige CI-kjøringer gir konsistente sammenligninger over tid siden de kjører på samme type runner.

---

## Forutsetninger

- .NET 10 SDK
- Tilgang til en SQL Server-database med produksjonslignende data (se [Database](#database) nedenfor)

---

## Database

Ytelsestestene krever en SQL Server-database fylt med produksjonslignende datamengder. Å kjøre mot en tom eller minimal database vil gi misvisende resultater.

Tilkoblingsstrengen hentes i følgende prioriterte rekkefølge:

| Prioritet | Kilde | Nøkkel |
|---|---|---|
| 1 | Miljøvariabel | `ARTSKART_BENCH_CONNECTION_STRING` |
| 2 | .NET bruker-secrets | `ARTSKART_BENCH_CONNECTION_STRING` |

---

## Kjøre lokalt

### 1. Sett opp bruker-secrets

Bruker-secrets lagres utenfor repositoryet og vil aldri bli inkludert i commits.

```bash
cd Artskart3.Tests.Performance

dotnet user-secrets set "ARTSKART_BENCH_CONNECTION_STRING" "Server=<server>;Database=<db>;User Id=<bruker>;Password=<passord>;"
```

For å verifisere at secret ble lagret:

```bash
dotnet user-secrets list
```

For å fjerne den senere:

```bash
dotnet user-secrets remove "ARTSKART_BENCH_CONNECTION_STRING"
```

### 2. Kjør ytelsestestene

Testene **må** kjøres i `Release`-modus — BenchmarkDotNet vil avvise kjøring i `Debug`-modus.

Fra **rot-mappen** i repositoryet:

```bash
dotnet run -c Release --project Artskart3.Tests.Performance
```

Eller fra **`Artskart3.Tests.Performance`-mappen**:

```bash
dotnet run -c Release
```

### 3. Se resultater

Resultater skrives til `BenchmarkDotNet.Artifacts/results/` i prosjektmappen:

| Fil | Innhold |
|---|---|
| `*-report.html` | Fullstendig rapport med statistikk, åpnes i nettleser |
| `*-report-github.md` | Markdown-oppsummering |
| `*-report-full-compressed.json` | Maskinlesbare data for sammenligningsverktøy |

---

## Lokal Grafana-dashboard (tidsserie over tid)

Grafana-dashbordet viser de samme tidsserie-grafene som GitHub Pages, men kjører lokalt.
Etter hver benchmark-kjøring importerer du resultater til en lokal InfluxDB-instans.

### Forutsetninger

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) eller [Podman Desktop](https://podman-desktop.io/)

### 1. Start Grafana og InfluxDB

Fra `Artskart3.Tests.Performance/dashboard/`:

```bash
docker compose up -d
```

Grafana åpnes på **http://localhost:4242** (bruker: `admin` / passord: `admin`).
Dashbordet **Artskart3 Ytelsestester** er ferdig konfigurert og venter på data.

### 2. Kjør ytelsestester — import skjer automatisk

Kjør benchmark-testene som vanlig:

```bash
dotnet run -c Release
```

Etter at benchmark-kjøringen er ferdig importerer programmet automatisk resultater til InfluxDB.
Åpne **http://localhost:4242** og velg tidsperioden `Last 90 days` for å se trendutviklingen.

> **Merk:** Dersom InfluxDB ikke kjører vises en advarsel, men benchmark-kjøringen feiler ikke.

### Stopp containerne

```bash
docker compose down        # stopp, behold data
docker compose down -v     # stopp og slett all data
```

### Dashboard-struktur

| Panel | Innhold |
|---|---|
| SearchTaxons | Responstid (ms) for taksonnavnsøk — eksakt, starter-med, inneholder |
| GetLocations | Responstid (ms) for stedssøk med ulike filtre |
| GetAreas | Responstid (ms) for henting av kommuner og fylker |
| Minnebruk | Bytes allokert per operasjon for alle benchmarks |

---

## Legge til nye benchmarks

1. Legg til en ny metode i `SearchServiceBenchmarks.cs` (eller opprett en ny `*Benchmarks.cs`-klasse)
2. Dekorer den med `[Benchmark]`
3. Kjør lokalt for å verifisere at den fungerer før du pusher

```csharp
[Benchmark]
public async Task NyBenchmark()
{
    _ = await _searchService.EnEllerAnnenMetodeAsync(...);
}
```

Bruk `[Params(...)]` for å teste flere inngangsstørrelser:

```csharp
[Params(10, 100, 1000)]
public int MaksResultater { get; set; }

[Benchmark]
public async Task SøkMedVarierendeSidestørrelse()
{
    _ = await _searchService.GetTaxonsAsync("a", MaksResultater);
}
```
