# Artskart3.Workers

Bakgrunnstjeneste for Artskart3 som kjører planlagte jobber via **Hangfire**. Deployed som en egen Azure App Service.

## Jobber

| Jobb | Status | Beskrivelse |
|------|--------|-------------|
| CSV-eksport | Implementert | Poller databasen for ventende eksportjobber, streamer CSV til Azure Blob Storage |
| Harvest | Placeholder | Henter data fra eksterne datakilder (GBIF, Artsobservasjoner m.fl.) |
| Import | Placeholder | Prosesserer data fra cache til index-databasen |
| Vedlikehold | Placeholder | Taksonomi-sync, områdeoppdateringer |

## Oppsett for lokal utvikling

### 1. Azurite (blob storage-emulator)

Kreves for CSV-eksport. Kjør én gang for å opprette containeren:

```bash
podman run -d --name azurite --restart always \
  -p 10000:10000 -p 10001:10001 -p 10002:10002 \
  mcr.microsoft.com/azure-storage/azurite
```

Containeren starter automatisk ved oppstart etter dette. Manuell kontroll:

```bash
podman start azurite
podman stop azurite
```

### 2. User secrets

Workers-prosjektet trenger connection string til databasen. Bruk samme som API-prosjektet:

```bash
dotnet user-secrets init --project Artskart3.Workers
dotnet user-secrets set "ConnectionStrings:ArtskartIndex" "data source=localhost;initial catalog=Artskart3Index;Integrated Security=true;MultipleActiveResultSets=True;App=EntityFramework;TrustServerCertificate=True" --project Artskart3.Workers
```

### 3. Kjør prosjektet

```bash
dotnet run --project Artskart3.Workers
```

Hangfire-dashboardet er tilgjengelig på `http://localhost:<port>/hangfire` (porten vises i konsollen).

## Kjøre API og Worker samtidig

For å teste hele flyten (opprett eksportjobb via API → worker plukker den opp) må begge prosjektene kjøre samtidig.

### Visual Studio

1. Høyreklikk på solution i Solution Explorer
2. Velg **Configure Startup Projects...**
3. Velg **Multiple startup projects**
4. Sett både `Artskart3.Api` og `Artskart3.Workers` til **Start**
5. Klikk OK og kjør med F5

### JetBrains Rider

1. Klikk på run-konfigurasjonen øverst i toolbar
2. Velg **Edit Configurations...**
3. Klikk **+** → **Compound**
4. Gi den et navn, f.eks. `API + Workers`
5. Legg til både `Artskart3.Api` og `Artskart3.Workers`
6. Kjør compound-konfigurasjonen

### Terminal

Åpne to separate terminaler:

```bash
# Terminal 1
dotnet run --project Artskart3.Api

# Terminal 2
dotnet run --project Artskart3.Workers
```

## Konfigurasjon

Viktige innstillinger i `appsettings.json`:

| Innstilling | Standardverdi | Beskrivelse |
|-------------|---------------|-------------|
| `CsvExport:Limits:SoftRowLimit` | 50 000 | Advarsel til bruker (eksport tillatt) |
| `CsvExport:Limits:HardRowLimit` | 100 000 | Blokkerer eksport |
| `CsvExport:Limits:MaxConcurrentPerUser` | 3 | Maks samtidige jobber per bruker |
| `CsvExport:Worker:BatchSize` | 5 000 | Rader per database-batch |
| `CsvExport:Worker:InterBatchDelayMs` | 100 | Pause mellom batcher (ms) |
| `CsvExport:BlobStorage:ConnectionString` | `UseDevelopmentStorage=true` | Azurite lokalt, ekte connection string i prod |

## Hangfire

- **Storage:** `Hangfire`-schema i eksisterende index-database (opprettes automatisk)
- **Dashboard:** `/hangfire` (ingen autentisering foreløpig)
- **CSV-eksport poll:** Kjører hvert 30. sekund
