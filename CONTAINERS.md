# Artskart3 i containere

Skrevet for drift, som forberedelse til flytting fra Azure App Services til Azure
Container Apps. Dokumentet beskriver hva imagene er, hvordan de henger sammen, hvilke
miljøvariabler de trenger, og hva som må være åpnet for at de skal starte.

For å kjøre oppsettet lokalt, se seksjonen «Kjøre Artskart 3 i containere lokalt» i
[README.md](README.md).

## Tre images

| Image | Dockerfile | Innhold |
| --- | --- | --- |
| `web` | `Artskart3.WebApp/Dockerfile` | nginx som serverer Angular-bygget og videresender API-stiene. Eneste offentlige inngang |
| `api` | `Artskart3.Api/Dockerfile` | ASP.NET 10. REST-API, innlogging (Duende BFF), serverside-rendring av kartbilder |
| `workers` | `Artskart3.Workers/Dockerfile` | Hangfire-server. CSV- og Excel-eksport, periodiske jobber |

**Byggkonteksten er reporoten for alle tre**, ikke mappa Dockerfila ligger i:

```bash
docker build -f Artskart3.WebApp/Dockerfile -t artskart3-web .
docker build -f Artskart3.Api/Dockerfile    -t artskart3-api .
docker build -f Artskart3.Workers/Dockerfile -t artskart3-workers .
```

Imagene inneholder ingen hemmeligheter. All konfigurasjon kommer fra miljøvariabler og
Key Vault ved oppstart.

Ett image kan promoteres gjennom miljøene. Frontend-bygget er miljøuavhengig —
`apiUrl` er tom og alle kall går til samme opphav — så samme image kan gå fra test til
staging til produksjon uten å bygges om.

## Topologi

```
Gateway / brannmur
  └─> web  (ekstern ingress, port 8080)
        ├─ /                        statiske filer, SPA-fallback
        └─ /api, /bff, /signin-oidc, /signout-callback-oidc,
           /signout-oidc, /swagger, /hc, /robots.txt
                                    proxy til api  (intern ingress, port 8080)

      workers  (ingen ingress nødvendig, port 8080 for helsesjekk)
```

**Frontend og API må ligge på samme offentlige vertsnavn.** Dette er et krav, ikke en
preferanse. Innloggingen bruker en sesjonscookie som er `SameSite=Lax`; ligger frontend
på et annet vertsnavn enn API-et, sendes cookien ikke med, og ingen får logget inn. Det
er derfor web-containeren proxyer API-stiene i stedet for at gatewayen ruter til to
ingresser — stilista blir vårt ansvar i stedet for deres, og en glemt sti gir
innloggingsfeil som er vanskelige å spore.

Stilista ligger i `Artskart3.WebApp/nginx/default.conf.template`. Legges det til et nytt
toppnivå-endepunkt i API-et, må det inn der.

## Miljøvariabler

### web

| Variabel | Verdi | Hemmelig |
| --- | --- | --- |
| `API_ORIGIN` | API-ets interne adresse. Bare opphav, ingen sti: `http://<api-intern>:8080` | Nei |
| `TLS_ENABLED` | `0` i Azure. Gateway/ingress terminerer TLS; containeren skal ikke gjøre det igjen | Nei |

Web-containeren trenger ingen hemmeligheter, ingen databasetilgang og ingen identitet.

### api

| Variabel | Verdi | Hemmelig |
| --- | --- | --- |
| `ASPNETCORE_ENVIRONMENT` | Miljønavnet. Alt annet enn `Development` gjør at appen henter konfigurasjon fra Key Vault | Nei |
| `KeyVault__Name` | Navnet på hvelvet | Nei |
| `AZURE_CLIENT_ID` | Bare ved user-assigned managed identity. Uten den finner `DefaultAzureCredential` ingen identitet | Nei |
| `ConnectionStrings__ArtskartIndex` | Tilkobling til databasen. Kan ligge i Key Vault | Ja |
| `Auth__Authority`, `Auth__ClientId`, `Auth__ClientSecret` | Identitetsleverandøren. Ligger i Key Vault | Ja |
| `ApplicationInsights__ConnectionString` | Telemetri. Tom verdi slår det av | Ja |
| `DataProtection__BlobUri` | Full URI til blob-en nøkkelringen lagres i, inkludert container og filnavn | Nei |
| `DataProtection__KeyVaultKeyId` | Key Vault-*nøkkel* som krypterer nøkkelringen i ro | Nei |
| `Database__AutoMigrate` | `false`. Se migrasjoner under | Nei |
| `ForwardedHeaders__OriginalHostHeaderName` | Standard `X-Original-Host`. Settes bare hvis leddet foran sender et annet navn | Nei |
| `Cors__AllowedOrigins__0` | Bare nødvendig hvis noe kaller API-et fra et annet opphav | Nei |

### workers

| Variabel | Verdi | Hemmelig |
| --- | --- | --- |
| `ASPNETCORE_ENVIRONMENT` | Som over | Nei |
| `KeyVault__Name` | Navnet på hvelvet | Nei |
| `AZURE_CLIENT_ID` | Som over | Nei |
| `ConnectionStrings__ArtskartIndex` | Databasen | Ja |
| `ConnectionStrings__ArtskartIndex_Worker` | Valgfri. Egen tilkobling for bakgrunnsjobber, slik at de er lette å skille i databasens egne logger. Tom verdi gjør at den bruker den over | Ja |
| `CsvExport__BlobStorage__ConnectionString` | Blob storage for eksportfiler | Ja |

> Workers sender **ingen telemetri** til Application Insights i dag. Pakken er referert
> i prosjektfila, men `AddApplicationInsightsTelemetry` kalles aldri i
> `Artskart3.Workers/Program.cs`, så en connection string her har ingen effekt. Verdt å
> vite hvis dere lurer på hvorfor eksportjobber ikke vises i Application Insights.
> Loggene går til stdout og fanges av plattformen som normalt.

## Data Protection — nøkkelringen

Nøkkelringen krypterer innloggingscookiene. Lagres den ikke utenfor containeren,
forsvinner den ved hver utrulling, og alle brukere blir logget ut. Med mer enn én
replica kan replicaene heller ikke lese hverandres cookies.

Det som må være på plass:

1. En blob-container appens identitet kan nå. **Appen oppretter den ikke** — den må
   finnes. URI-en til blob-en, inkludert filnavn, settes som `DataProtection__BlobUri`.
2. Rollen `Storage Blob Data Contributor` på den containeren for identiteten appen
   kjører som. Den må både lese og skrive den ene blob-en.
3. Anbefalt: en nøkkel i Key Vault — en *key*, ikke en secret — med wrap- og
   unwrap-tilgang for samme identitet, satt som `DataProtection__KeyVaultKeyId`. Uten
   den lagres nøkkelringen ukryptert, og den som kan lese blob-en kan lage gyldige
   innloggingscookies. Appen logger en advarsel når den mangler.

**Ett sett per miljø.** Test, staging og produksjon må ikke dele samme blob — da ville
en innloggingscookie fra test vært gyldig i produksjon.

Settes ingenting, starter appen likevel og logger hvorfor det er feil i et deployet
miljø.

## Helsesjekker

| Tjeneste | Liveness | Readiness |
| --- | --- | --- |
| web | `/healthz` | — |
| api | `/hc/live` | `/hc/ready` |
| workers | `/health` | — |

De to på API-et er bevisst forskjellige. `/hc/live` sjekker ingen avhengigheter og sier
bare at prosessen lever — er databasen nede, hjelper det ingen å restarte containeren i
løkke. `/hc/ready` sjekker databasen og sier om containeren kan betjene trafikk.
`/hc` finnes fortsatt og kjører alle sjekker, for eksisterende overvåking.

**Viktig om oppstart:** API-et laster områdehierarkiet fra databasen før det begynner å
svare. Er databasen utilgjengelig, avbryter appen oppstarten og containeren går i
crashloop — den kommer aldri i en tilstand der readiness kan melde «ikke klar».
Brannmur og nettverk mot SQL må altså være på plass før første utrulling, ikke etterpå.

## Skalering

**Workers må ha minst én replica til enhver tid.** Den kjører periodiske jobber —
eksportkøen pollers hvert sekund — og kan ikke skalere til null. Flere replicaer er
teknisk greit, Hangfire har distribuerte låser, men gir ingen gevinst i dag.

**API-et kan skaleres ut** forutsatt at Data Protection-nøkkelringen er satt opp som
over. Uten den kan ikke replicaene lese hverandres innloggingscookies.

**web** kan skaleres fritt. Den har ingen tilstand.

## Utgående trafikk

Appene må nå disse. Uten dem starter de ikke, eller mister funksjonalitet uten å feile
tydelig.

### api

| Mål | Port | Hva som ryker uten |
| --- | --- | --- |
| SQL Server-instansen | 1433 | Alt. Containeren starter ikke |
| `<keyvault>.vault.azure.net` | 443 | Starter ikke — all konfigurasjon ligger der |
| `<storage>.blob.core.windows.net` | 443 | Innlogging ryker ved hver utrulling |
| Identitetsleverandøren (`Auth:Authority`) | 443 | Innlogging feiler |
| `nortaxa.artsdatabanken.no` | 443 | Artssøk feiler |
| `wms.geonorge.no` | 443 | Kartbildet på observasjoner blir tomt |
| `geodata.npolar.no` | 443 | Samme, for Svalbard og oversiktskart |
| App Insights-ingestion i norwayeast | 443 | Ingen telemetri. Appen virker ellers |

### workers

| Mål | Port | Hva som ryker uten |
| --- | --- | --- |
| SQL Server-instansen | 1433 | Alt |
| `<keyvault>.vault.azure.net` | 443 | Starter ikke |
| `<storage>.blob.core.windows.net` | 443 | Eksport feiler |
| App Insights-ingestion i norwayeast | 443 | Ingen telemetri |

### web

Ingen utgående trafikk utover API-containeren internt.

### Merknader

- **Azure SQL med Redirect-tilkoblingspolicy trenger også portene 11000–11999**, ikke
  bare 1433. Redirect er standard for tilkoblinger fra innsiden av Azure. Symptomet ved
  feil er en tilkobling som henger uten forklaring.
- **Kartbildene hentes server-side.** API-et rendrer et statisk kartbilde per
  observasjon ved å hente WMS-fliser selv. Derfor står geonorge og npolar på lista over
  utgående trafikk fra containeren, ikke bare som noe nettleseren laster.
- Kartlagene brukeren kan slå på i kartet hentes derimot av nettleseren
  (`kart.artsdatabanken.no`, `wms.miljodirektoratet.no`). De må være tilgjengelige fra
  klientnettet, men krever ingen åpning fra containeren.
- **Plattformen må nå registeret** for å kunne pulle imagene.

## Migrasjoner

Migrasjonene kjøres som i dag: et eget steg i release-pipelinen, før appen deployeres.
Ingen endring, og ikke noe eget image.

`Database__AutoMigrate` skal være `false` i alle deployede miljøer. Grunnen er at
migrasjonen som fyller datasettet tar timer, og appen svarer ikke mens den pågår — da
blir containeren drept av helsesjekken om og om igjen. Den tunge migrasjonen utløses
bare av en tom database, og siden dagens databaser gjenbrukes, er migreringen i det nye
miljøet unnagjort på minutter.

## Dimensjonering

Målt 2026-10-20 med podman på en utviklingsmaskin, mot en database med
produksjonslikt skjema og datasett. Alle spørringer kjørt med tomt filter, altså uten
noen avgrensning — det er verste fall, og det riktige grunnlaget for dimensjonering.

### Minne

Målt med minnegrense satt på containeren, fordi .NET sin GC leser cgroup-grensen og
tilpasser seg den. Uten grense samler den søppel sent — det er billigere — og
forbruket vokser til nesten 1 GB uten at appen trenger det. Tall målt uten grense er
derfor ikke brukbare til dimensjonering.

| Grense på api | Topp under last | OOM | Alle svar 200 |
| --- | --- | --- | --- |
| ingen (32 GB synlig) | 981 MB | nei | ja |
| 2 GiB | 535 MB | nei | ja |
| 1 GiB | 540 MB | nei | ja |
| 1 GiB, 12 samtidige klienter mot 21 MB-svaret | 462 MB | nei | ja, 0,6–1,1 s |
| 512 MB | 462 MB | nei | ja |

Arbeidssettet ligger altså rundt 460–540 MB uansett hvilken grense som settes, og
svartidene var like på 512 MB og 2 GiB — minnet er ikke det som begrenser ytelsen.

| Container | I ro rett etter oppstart | Arbeidssett med varme cacher |
| --- | --- | --- |
| api | 146 MB | 460–540 MB |
| workers | 149 MB | 294 MB |
| web | 16 MB | 21 MB |

Tallet i ro er misvisende for API-et: områdetellinger og taksonhierarki ligger i minne
**per replica** og fylles under bruk.

### Anbefaling

| Container | Minne | vCPU (følger av minnet) |
| --- | --- | --- |
| api | **2 GiB** | 1,0 |
| workers | **1 GiB** | 0,5 |
| web | **0,5 GiB** | 0,25 |

Container Apps kobler CPU og minne i faste par, så valget av minne låser CPU-en.
Det er grunnen til at api anbefales med 2 GiB og ikke 1: **minnet alene tilsier
1 GiB** — arbeidssettet er 460–540 MB, og containeren overlevde til og med en grense
på 512 MB — men 1 GiB betyr 0,5 vCPU, og **CPU er ikke målt**. API-et komprimerer svar
på opptil 21 MB og rendrer kartbilder serverside, så 0,5 vCPU kan bli knapt. Det er
CPU-siden som driver anbefalingen opp, ikke minnet. Viser det seg at CPU-en ligger lavt
i drift, er 1 GiB / 0,5 vCPU et forsvarlig sted å spare.

512 MB for api er ikke anbefalt: det lar bare rundt 50 MB stå igjen over
arbeidssettet, uten margin for trafikktopper.

Tallene for workers og web er målt **uten** minnegrense, så 294 MB og 21 MB er øvre
anslag — med grense ville de etter alt å dømme ligget lavere, slik api gikk fra 981 til
540 MB. De ble ikke målt med grense fordi ingen av dem er i nærheten av å være et
problem.

Alle tall er **per replica**.

### Svartider og svarstørrelser

Gjennom web-containeren, altså hele veien en bruker går.

| Endepunkt | Tid | Rått | Gzippet |
| --- | --- | --- | --- |
| `Search/Locations` | **3,5 s** | 3,23 MB | — |
| `Search/AreaMarkers` zoom 2 | 0,26–0,41 s | **21,7 MB** | 8,73 MB |
| `Search/AreaMarkers` zoom 1 | 0,23–0,25 s | 4,07 MB | 1,65 MB |
| `Search/AreaCounts` zoom 2 | 0,22 s | 14,7 kB | — |
| `Search/Observation` (listevisning) | 0,22 s | 6,7 kB | — |
| `Search/LocationCount` | 0,28 s | 33 B | — |

**Ingress-timeout:** tyngste spørring er 3,5 sekunder. Standarden på 240 sekunder i
Container Apps er altså rikelig, og trenger ingen endring. Men sett den ikke lavere enn
60 sekunder — tidene under er målt mot en lokal database på en rask maskin, og Azure SQL
vil ligge høyere. Tallene er et gulv, ikke et tak.

**Største svar** er 21,7 MB rått og 8,7 MB gzippet. Appen komprimerer selv, så det er
8,7 MB som går over nettet. Ingen begrensning i plattformen må kutte det.

### Disk

**Det er `workers` som trenger skrivbar `/tmp`, ikke `api`.** Eksportjobben bygger en
xlsx-fil på disk før den lastes opp til blob storage
(`Artskart3.Workers/Export/ExportService.cs:107`). API-et skriver ingenting til disk.

Filstørrelsen er **ikke målt** — det ble valgt bort framfor å kjøre en tung
eksportjobb mot testbasen. Sett flyktig lagring på `workers` romslig i stedet. Det som
er kjent, og som kan brukes til å anslå:

- `CsvExport:Limits:HardRowLimit` = 500 000 rader er den øvre grensen for én eksport.
- `CsvExport:Limits:MaxConcurrentPerUser` = 3, så flere filer kan være under bygging
  samtidig, også fra samme bruker.
- Filene slettes etter opplasting, men en jobb som feiler midt i kan etterlate en fil
  til neste opprydding.

## Hva som er verifisert lokalt

Kjørt med podman mot en SQL Server-tjeneste med produksjonslikt skjema, og Azurite som
blob storage.

- Alle tre imagene bygger, og kjører som ikke-rot bruker.
- Frontend serveres av web-containeren, med SPA-fallback på ukjente stier.
- API-stiene rutes gjennom web: `/hc`, `/swagger`, `/robots.txt`, `/bff`, `/api`.
- Vertsnavn og protokoll bevares gjennom proxy-kjeden, slik at innloggingen bygger
  riktige returadresser.
- Statiske filer serveres forkomprimert med riktige cache-headere.
- Workers kobler til blob storage, oppretter sitt Hangfire-skjema og registrerer de
  periodiske jobbene.
- Data Protection-nøkkelringen overlever at containeren erstattes.
- nginx følger API-containeren når den får ny adresse, uten restart.
- Innlogging går hele veien til identitetsleverandøren.

### Hva som ikke kan verifiseres lokalt

- Key Vault og managed identity. Lokalt kommer verdiene fra miljøvariabler.
- Ingress, gateway-regler og TLS-terminering i plattformen.
- Oppførsel ved skalering og ved bytte av revisjon i Container Apps.
- Ytelse på plattformens instansstørrelser.

## Oppfølging etter flyttingen

Når App Service er avviklet, kan `UseDefaultFiles`, `MapStaticAssets` og
`MapFallbackToFile` fjernes fra `Artskart3.Api/Program.cs`. De står igjen med vilje:
dagens App Service-utrulling legger Angular-bygget i `wwwroot` via pipelinen, og den må
fortsette å virke til flyttingen er fullført. Container-imaget har ingen `wwwroot`, så
kodestien er uvirksom der.
