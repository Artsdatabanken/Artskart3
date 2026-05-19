# Artskart3.Tests.Performance

BenchmarkDotNet-baserte ytelsestester for `SearchService`- og repository-laget. Testene kjøres mot en ekte SQL Server-database med produksjonslignende datamengder for å gi meningsfulle resultater.

Resultater fra nattlige CI-kjøringer publiseres til [GitHub Pages](#se-resultater-på-github-pages).

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

## Sammenligne kjøringer over tid (lokalt)

Bruk Microsofts [ResultsComparer](https://github.com/dotnet/performance/tree/main/src/tools/ResultsComparer)-verktøy til å sammenligne to sett med resultater.

### Installer

```bash
dotnet tool install -g ResultsComparer
```

### Sammenlign

Lagre resultatmapper med beskrivende navn før og etter en endring:

```
BenchmarkDotNet.Artifacts/
  results-før-optimalisering/
  results-etter-optimalisering/
```

Sammenlign deretter:

```bash
ResultsComparer --base results-før-optimalisering/ --diff results-etter-optimalisering/ --threshold 5%
```

Resultatet viser **regresjoner**, **forbedringer** og **ingen endring** per benchmark, basert på en statistisk test fremfor rene tall.

---

## GitHub Actions — nattlig CI-kjøring

Benchmark-pipelinen kjøres nattlig kl. **02:00 UTC** fra `develop`-grenen og publiserer resultater til GitHub Pages.

### Workflow-fil

`.github/workflows/benchmarks.yml` — kjører tester og pusher resultater til `gh-pages`-grenen ved hjelp av [github-action-benchmark](https://github.com/benchmark-action/github-action-benchmark).

### Påkrevd GitHub-secret

Workflow-en trenger en tilkoblingsstreng til en produksjonslignende database. Legg den til som repository-secret i GitHub:

1. Gå til **Settings → Secrets and variables → Actions**
2. Klikk **New repository secret**
3. Navn: `BENCHMARK_CONNECTION_STRING`
4. Verdi: den fullstendige SQL Server-tilkoblingsstrengen

### Aktivere GitHub Pages

1. Gå til **Settings → Pages**
2. Sett **Source** til `Deploy from a branch`
3. Sett **Branch** til `gh-pages` / `(root)`
4. Lagre

Etter den første vellykkede workflow-kjøringen vil resultater være tilgjengelig på:

```
https://<org>.github.io/Artskart3/dev/bench/
```

---

## Se resultater på GitHub Pages

GitHub Pages-dashbordet viser et tidsseriediagram per benchmark-metode, oppdatert etter hver nattlig kjøring. Hvert datapunkt lenker tilbake til workflow-kjøringen som produserte det.

Regresjoner over 120 % av grunnlinjen flagges som advarsler på workflow-kjøringen, men stopper ikke bygget.

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
