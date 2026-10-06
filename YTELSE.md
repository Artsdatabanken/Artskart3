# Ytelsesarbeid — hva som er prøvd, og hva som ble beholdt

Dette dokumentet finnes for at ingen skal prøve det samme to ganger. Det er
organisert per API-endepunkt, ikke kronologisk: står du med et tregt kall, slå
opp endepunktet og se hva som allerede er forsøkt.

Hver oppføring sier hva som ble gjort, om det ble **beholdt** eller
**forkastet**, og hvorfor — med tall. Et forkastet forsøk er like verdifullt
som et beholdt, og ofte mer: det forteller hvorfor den opplagte løsningen ikke
virker.

## Hvordan måle

`Scripts/PerfSuite/Run.ps1` er fasiten. **Ikke start den uten at noen har bedt
om det** — se `CLAUDE.md`. Nivåene er Quick (minutter), Standard (titalls
minutter) og Full (rundt 20 minutter veggklokke).

Enkeltmålinger lyver. Det er dokumentert minst tre ganger nedenfor at en
konklusjon trukket fra ett tilfelle ble motbevist av en full kjøring. Regelen
er: **et planhint eller en indeks skal ikke inn uten tall fra hele
Full-nivået.**

---

## Locations — `POST /api/search/Locations`

Returnerer inntil 100 000 lokasjoner med antall observasjoner, filtrert på
kartutsnitt. Det dyreste endepunktet i suiten.

### Slik tiden fordeler seg

Målt for et ufiltrert Oslo-kall med 100 000 lokasjoner, totalt ~410 ms:

| Steg | Tid | Andel |
|---|---|---|
| Serverside spørring | ~270 ms | 66 % |
| Overføring + EF-materialisering | ~50 ms | 12 % |
| JSON-skriving (3,04 MB) | ~50 ms | 12 % |
| Rammeverk, socket | ~40 ms | 10 % |

Innenfor de 270 serverside-millisekundene: skanning 65 ms, sortering 68 ms,
**koordinatjoin mot Location 84 ms**, resten plan og resultatsett.

### Beholdt

**Områdeluking mot kartutsnittet** (`AreaBoundsPruner`). Et valgt område hvis
omsluttende boks ikke når utsnittet kan ikke bidra, og fjernes fra spørringen.
Når ingen når utsnittet returneres tomt uten å spørre databasen. Standard
282 → 196 s, og alle fire Locations-timeoutene forsvant. Boksene lastes ved
oppstart og hver time; manglende overlapp er et bevis, overlapp bare en
mulighet, så lukingen feiler i trygg retning.

**Denormalisert `MonthCollected`.** `DATEPART(month, …)` var ikke sargbar og
tvang en Compute Scalar over alle rader. `periode:annen` 4212 → 860 ms.

**Områdefilteret forankret på områderaden** i stedet for et `EXISTS`-oppslag
mot søsterrad. Locations med områdefilter 25 → 12 s. Forutsetter
`COUNT(DISTINCT ObservationId)` i kalleren — den forankrede formen gir én rad
per (observasjon, område), så `COUNT(*)` ville dobbelttalt. Voktet av
`AreaFilterAnchoringIntegrationTests`.

**Punktbuffer** (`LocationCountCacheLevel1`). Ferdig talte observasjoner per
(dimensjon, bøtte, lokasjon), 75 097 617 rader på 2178 MB. Standard Locations
147,7 → 66,9 s. Bare ett nivå: bufferen lagrer marginaler, og to filtre krever
samfordelingen.

**Aggregering fra `ObservationEntityIndex`, ikke fra `Observation`.**
Columnstore og batch mode slår dedupliseringskostnaden for de brede filtrene,
og de er flertallet.

**`ThenBy(LocationId)` som tiebreaker.** For Oslo-utsnittet ligger grensen ved
TOP 100 000 på to observasjoner, og 42 308 lokasjoner har nøyaktig to. Uten
tiebreakeren returnerer to kjøringer ulike lokasjoner.

**Koordinatjoinen etter `Take`.** Med lat/lon i GROUP BY-nøkkelen må joinen skje
før aggregeringen, for alle treff: 2332 ms mot 36 ms.

### Forkastet

**Telle fra `Observation` i stedet for indekstabellen.** Standard: indekstabell
196 s, Observation med tvungen loop join 322 s, Observation med RECOMPILE
488 s. Den tvungne planen var 10× raskere på brede filtre og 260× tregere på
smale.

**Tvunget planvalg.** `katalognr:tung` gikk fra 9 til 13 007 ms.

**Antallssortert indeks** `(DimensionId, BucketId, ObservationCount DESC, …)`.
Fjerner sorteringen: skanning+sortering 133 → 65 ms. Men med koordinatjoinen
med forsvinner forskjellen (217 → 221 ms), den koster ~1,2 GB, og optimizeren
valgte den feil for mellomstore utsnitt — telemark50k 26 → 103 ms, ostfold10k
12 → 30 ms.

**Lat/lon i bufferen** for å fjerne koordinatjoinen. 1,2 GB for 84 ms. Feil
retning: se «East/North i svaret» under åpne spørsmål.

**Grid for raskere filtrering.** Klyngenøkkelen er
`(DimensionId, BucketId, East, North, LocationId)`, så bare `East` kan søkes
på og `North` blir restledd. Sløsingen er reell — Oslo leser 2 751 132 rader
for å finne 966 708, små utsnitt opptil 99 % — men koster bare 58 → 14 ms,
altså 44 ms av 410. Et rektangel dekker dessuten mange celler (Oslo: 209 ved
10 km), så spørringen trenger `IN`-lister eller oppstykkede Z-order-intervaller.
For mye kompleksitet for 11 %.

**Nivå 2 i bufferen** (parbøtter for to filtre). Målt til ~660 mill. rader og
~16 GB for alle 66 par. To-filter-kallene er allerede 301 ms i snitt, og en
stor del av det er svarstørrelse som nivå 2 ikke rører.

**Romlig sortering av utdataene for bedre komprimering.** Svaret sorteres på
observasjonsantall, så koordinatene kommer i tilfeldig romlig rekkefølge.
Tanken var at naboverdier ville komprimere bedre. Gir **ingen gevinst**:
0,91 MB mot 0,92 MB gzippet. Gzip jobber på bytemønstre i et 32 kB-vindu, ikke
på numeriske differanser, så koordinater som deler sifre hjelper ikke uten
eksplisitt delta-koding.

**Binært format for mindre nyttelast.** 4 × int32 per lokasjon er 1,53 MB mot
JSON-ens 2,72 MB — men **identisk etter gzip**, begge 0,91 MB. Begge treffer
samme entropigrense. Binært har fortsatt verdi for CPU, ikke byte: det fjerner
tallformateringen på serveren og `JSON.parse` av 3 MB i nettleseren, som kan
leses rett inn i typed arrays. Klientsiden er ikke målt.

---

## LocationPolygons — `POST /api/search/LocationPolygons`

Returnerer geometrien til lokasjoner som ikke er punkt. Navnet er for snevert —
linjer er også med, se `LocationPolygonDto`. Egen task på omdøping.

### Beholdt

**`PolygonLocationStore` — hele datasettet i minnet.** 75 990 lokasjoner og
3 946 488 observasjoner i kolonnære arrayer, ~670 MB, bygget på 34 sekunder ved
oppstart i en bakgrunnstjeneste. Full: LocationPolygons 360 → 41 s.

Det avgjørende er ikke hastigheten, men at **grensen på ett filter forsvinner**.
En buffer lagrer marginaler; en skanning evaluerer predikatene direkte, så fire
filtre koster det samme som ett:

| Filtre | Med buffer | I minnet |
|---|---|---|
| 1 | 124 s | 18 s |
| 2 | 158 s | 17 s |
| 3 | 51 s | 5 s |
| 4 | 23 s | 2 s |

Observasjonene er sortert på lokasjon med en offset-array, så et kartutsnitt
hopper over hele lokasjoner uten å røre observasjonene deres. Takson leses fra
`ObservationTaxonHierarchy` og ikke fra takson-treet, fordi det er nøyaktig det
`GetObservationIdsByTaxonHierarchy` filtrerer mot.

Verifisert mot databasestien på 38 filterformer — hvert taksonrangnivå,
geografi, prosjekt, alle kolonnefiltrene og to-, tre- og firefilterkombinasjoner
— med eksakt likt resultat rad for rad. Nødbryter:
`ARTSKART_DISABLE_POLYGON_MEMORY=1`.

Databasestien brukes bare til datasettet er bygget. Den er derfor holdt enkel
med vilje: ingen buffer, ingen sonde, intet planhint.

**`GeometryTypeId`** — persistert beregnet kolonne med indeks
`(GeometryTypeId, East, North)`. `STGeometryType()` måtte kalles per rad,
967 649 ganger for Oslo-utsnittet, og kostet 776 av 951 ms. Polygonspørringen
1010 → 92 ms. Brukes nå også til å finne kandidatsettet når minnelageret bygges.

**Typefilteret før `TOP`.** Ble tidligere brukt etterpå, så vi hentet 5000
lokasjoner og satt igjen med 159 polygoner. 5808 → 1004 ms.

**Linjer med i filteret.** 2101 linjelokasjoner med 62 764 observasjoner falt
stille ut fordi filteret bare slapp gjennom Polygon og MultiPolygon.

### Forkastet

**Tvungen loop join fra Location** (`INNER LOOP JOIN` + `FORCE ORDER`). Stor
gevinst på brede filtre — regstatus:tung 10 799 → 1 135 ms — men katastrofal når
to filtre til sammen ikke treffer noe: `atferd:tung + prosjekt:tung` 12 ms uten
hint mot **68 708 ms** med. Tolv av 1432 polygonmålinger brukte over ti sekunder
og sto for 336 av 746 s. Merk at `INNER LOOP JOIN` binder joinrekkefølgen i seg
selv; `FORCE ORDER` tilførte ingenting.

**RECOMPILE på polygontaggen.** Så ut som løsningen: drapstilfellet 68 708 →
648 ms, og ingen kostnad på brede filtre. En full kjøring viste at den bare
flyttet problemet — `bilder:lett + prosjekt:lett` gikk fra 612 til 43 801 ms, og
halen ble verre: 12 → 16 tilfeller over ti sekunder, 336 → 392 s. **Dette er
eksempelet på hvorfor enkeltmålinger ikke holder.**

**Selektivitetssonde.** Telte opp til 50 000 treff for takson- og
prosjektfilteret og droppet hintet når ett av dem var under. Den virket —
55 790 → 135 ms — og terskelen var målt, ikke gjettet. Fjernet fordi
minnelageret gjorde både den og hintet overflødig.

**Polygonbuffer** (`LocationPolygonCountCacheLevel1`, 1,2 mill. rader / 50 MB).
Erstattet av minnelageret. Hadde dessuten en korrekthetsfeil verdt å huske:
bufferens taksondimensjon er bygget på ordensnivå, mens polygonstien filtrerer
på taksonets eget rangnivå. 266 774 observasjoner er bestemt til klasse uten
orden, så `takson:Class` ga 4920 lokasjoner mot tellingens 4921 — og 32
lokasjoner hadde for lave tall. Perf-suitens radtelling fanget bare den ene som
forsvant helt.

---

## Observation (listevisning) — `POST /api/search/Observation`

Paginert liste sortert på `DateTimeCollected`.

### To mekanismer gjør den treg — de er forskjellige

**Selektivitet.** Sorteringen kombinert med `OFFSET/FETCH` gjør at arbeidet er
antall rader som trengs delt på selektiviteten. Med 125 treff i 61 millioner
skanner selv side 1 nesten hele datoindeksen. Et resultat med mange rader
pagineres derimot billig helt ut — `regstatus:lett` alene er 21/89/113 ms på
side 1/10/50. Løst med filtrert indeks, se under.

**Tapt sortering når ELLER-et står over flere indeksintervaller.** Dette er noe
annet, og rammer bare flerverdifiltre.

> **RETTELSE.** Denne seksjonen forklarte tidligere
> `institusjon:x2-median-lett` med korrelasjon mellom filteret og
> sorteringskolonnen: at nyeste treff er fra 2015-10-30 og at det finnes
> 31 350 917 observasjoner nyere enn det. Begge tallene stemmer, men de
> forklarer ikke tiden. Samme data med **én** verdi er 18 ms. Datokorrelasjonen
> er altså en egenskap ved dataene som ikke koster noe. Mekanismen er en annen,
> og den står under.

`IX_Observation_InstitutionOrgId` er nøklet `(InstitutionOrgId,
DateTimeCollected)`. Indeksen opprettes med rå SQL i migrasjon
`20260904114820` og står **ikke** i `ArtskartDbContext` — et sted å huske å se.

Med én verdi blir det ett sammenhengende intervall. SQL Server leser det
baklengs og får `DateTimeCollected DESC` gratis, så `TOP 10` stopper på de
første radene. Med to verdier blir det to intervaller, og rekkefølgen kan ikke
bevares på tvers av dem — planen må hente alle treffene og sortere før TOP-en
kan tas. Den eneste forskjellen i SQL-en er `= @p` mot `IN (@p1, @p2)`.

Taksongruppe har samme form via `IX_TaxonGroupId`.

Målt på side 1, listevisningen:

| Filter | ms |
|---|---|
| Molltax (22 872 obs) alene | 18 |
| Veterinærinstituttet (32 obs) alene | 18 |
| Birdlife Norge (29,2 M) + Veterinærinstituttet | 17 |
| fem institusjoner, inkl. Birdlife | 20 |
| **Molltax + Veterinærinstituttet** | **2520** |
| Hjuldyr (44 519) + Mesozoa (1) | 344 |

Regelen er: unionen er treg **hvis og bare hvis alle verdiene er selektive**.
Er en vanlig verdi med, forlater optimizeren indeksen, skanner datoindeksen i
stedet og treffer nok rader med en gang.

Spørringen kjører allerede med `OPTION (RECOMPILE)` via `listview-search`.
Optimizeren ser altså de faktiske verdiene og velger sorteringsplanen likevel.
Dette er derfor **ikke** et estimatproblem, og RECOMPILE kan ikke løse det.

### Beholdt

**RECOMPILE** via `listview-search`-taggen. ~20 ms per kall, sparer over et
sekund på de trege filtrene.

**Denormaliserte filterkolonner** — `RegistrationStatusId`, `HasMediaFiles`,
`BehaviorId`, `InstitutionOrgId`, `DatasetOrgId`. Erstattet semi-joins som
tvang fram oppslag per kandidatrad. Registreringsstatus 2: 1548 → 89 ms.

**Gren per verdi på institusjon og taksongruppe** —
`GetBranchedObservationIdsAsync`. Én spørring per verdi med `TOP(skip+take)`,
flettet i minnet. Samme mønster som områdefilteret. Tak på åtte grener; over
det brukes IN-formen som før.

| Sak | Før | Etter |
|---|---|---|
| institusjon:x2-median-lett, side 1 | 2520 ms | **24 ms** |
| institusjon:x2-median-lett, side1-stor | 1999 ms | **71 ms** |
| institusjon:x2-median-lett, side 50 | 698 ms | **26 ms** |
| taksongruppe:x2-median-lett, side 50 | 580 ms | **26 ms** |
| taksongruppe:x2-median-lett, side1-stor | 484 ms | **70 ms** |
| taksongruppe:x2-median-lett, side 1 | 316 ms | **25 ms** |

Prisen er de settene som allerede var raske. Summert over alle 24 grenede
tilfeller i Full: side 1 fra 2940 til 198 ms, side 50 fra 1386 til 197 ms,
side1-stor fra 2606 til 661 ms. Verste enkeltregresjon er 76 ms, og alle de
store ligger på `side1-stor` — der er `take` 400 per gren fordi
`LookaheadMultiplier` er 4, så fem grener henter 2000 rader for å levere 400.
På side 1, som er det brukeren møter, er verste regresjon 15 ms.

Byttet er bevisst: grening har **kjent** kost — hver gren er 16–19 ms uansett
om verdien har 1 eller 29 millioner observasjoner — mens IN er et lotteri
mellom 20 og 2520 ms.

Korrektheten hviler på at det globale topp-K alltid ligger i unionen av
gren-vise topp-K, og derfor på at hver gren henter `skip+take`, ikke `take`.
`BranchedFilterIntegrationTests.Dyp_side_henter_rader_utover_forste_gren`
vokter nettopp det: den er verifisert ved å endre `Take(trengs)` til
`Take(take)`, og da er den den eneste av de seks som feiler.

**Filtrert indeks `IX_Observation_RegistrationStatusRare`** på de to sjeldne
statusverdiene. **2,1 MB, tre sekunder å bygge.**

| Sak | Før | Etter |
|---|---|---|
| bilder:lett + regstatus:lett, side 50 | 3859 ms | 27 ms |
| atferd:lett + regstatus:lett, side 1 | 2981 ms | 28 ms |
| bilder:lett + regstatus:lett, side1-stor | 1725 ms | 30 ms |

Fordelingen gjør den billig: verdi 1 er 99,67 %, verdi 2 er 0,32 %, verdi 3 er
0,01 %. Verdi 1 er rask uten og kan ikke filtreres nyttig.

### Forkastet

**Vanlig indeks `(RegistrationStatusId, DateTimeCollected, Id)`.** 562 MB, 56
sekunder å bygge — og optimizeren valgte den **aldri** for kombinasjonene.
Tvunget fram var den 19× raskere. Årsaken er kardinalitetsestimatet: 49 485
anslåtte rader der det er 125, fra exponential backoff som antar delvis
korrelasjon mens den her er sterkt negativ (observasjoner merket «ikke påvist»
har nesten aldri bilder).

**Flerkolonnestatistikk** på `(RegistrationStatusId, HasMediaFiles)` med
`FULLSCAN`. Gjorde anslaget **verre** — 193 510 i stedet for 49 485 — og
kostnaden for feil plan falt fra 0,046 til 0,014.

**`USE HINT('DISABLE_OPTIMIZER_ROWGOAL')`.** Ingen effekt på tid eller planvalg.

**Dekkende `INCLUDE` på datoindeksen.** `(DateTimeCollected DESC, Id DESC)` med
elleve `INCLUDE`-kolonner, altså alle filterkolonnene — tanken var å gjøre den
VALGTE planen billig i stedet for å kjempe mot planvalget, slik at
datoskanningen kunne filtrere uten oppslag. **1221 MB, 173 sekunder å bygge.**

Den virket som tenkt: planen bruker den og gjør ingen oppslag. Men de
tilfellene den hjelper var allerede raske (atferd:lett 22 → 2 ms,
taksongruppe:tung 16 → 1 ms), og tilfellet den skulle fikse ble ikke bedre —
`institusjon:x2-median-lett` 2534 → 2224 ms. Vi vet nå hvorfor: kostnaden der
er sorteringen av alle treffene, ikke oppslagene, og en bredere datoindeks
rører ikke den. Se rettelsen lenger opp.

Halen på endepunktet domineres dessuten av `fylke:*`, som filtrerer via
semi-join mot indekstabellen og ikke berøres av en indeks på `Observation` i
det hele tatt: 40 målinger over ett sekund står for 53 av 217 sekunder, og de
fleste er fylke eller institusjon.

---

## AreaCounts og AreaMarkers

### Beholdt

**Geo-semijoinen utelates når den ikke kan fjerne noen rad** —
`AreaCountGeoJoinRules.CanSkipGeoJoin`.

`ComputeFilteredAreaCounts` bygger spørringen i to deler: et ankerledd som
velger radene som blir til utdataceller, og et `EXISTS` som sjekker at
observasjonen ligger i et av de valgte områdene. Ved zoomnivå 1 med fylkesfilter
er ankerraden ALLEREDE fylkesraden, og `EXISTS`-leddet spør om nettopp den.
Det fjerner ingenting, men koster en selvjoin over hele ankersettet.

Symptomet var AreaCounts-kall som brukte over ett sekund og returnerte **én
rad**. Raden var riktig — med fylkesfilter kan bare ett fylke ha treff — men
tallet i den kostet en unødvendig selvjoin over 7,98 millioner indeksrader.

Målt isolert mot prodlik base, identisk svar i alle fire:

| Sak | Med | Uten |
|---|---|---|
| z1 fylke + institusjon + koordpresisjon + regstatus | 1634 ms | **169 ms** |
| z1 fylke + havområde + institusjon + taksongruppe | 1172 ms | **186 ms** |
| z1 funntype + fylke + taksongruppe | 445 ms | **18 ms** |
| z2 fylke + institusjon + koordpresisjon + regstatus | 951 ms | **253 ms** |

To regler, holdt adskilt i koden fordi den ene er et bevis og den andre en
forutsetning:

1. **Delmengderegelen.** Er utdataområdet selv et valgt område, tilfredsstiller
   ankerraden `EXISTS`-leddet ved seg selv. Følger av spørringens form.
2. **Etterkommerregelen.** Er utdataområdet en kommune i et valgt fylke, er det
   en ANNEN rad som tilfredsstiller leddet. At den alltid finnes håndheves av
   invariantsjekken i `BackfillAll.sql`, som avbryter ved brudd. Målt til 0
   brudd over 357 kommuner.

Sjekken står bevisst UTENFOR `@Kontroller` i backfillen: de kontrollene betyr
«rader som gjenstår å behandle» og skriptet kjøres om igjen til summen er null.
Områderadene utledes fra `LocationAreas`, så et brudd kan ikke fikses ved å
kjøre på nytt — lagt i `@Kontroller` ville migrasjonen aldri blitt registrert
som anvendt.

**Opprulling fra kommune til fylke** — `AreaCountRollupRules` +
`ComputeRolledUpAreaCounts`.

Der semijoinen IKKE er overflødig — zoomnivå 1 med kommunefilter, der
utdatacellen er fylket og valget er en kommune — kan den unngås ved å snu
spørringen. Ankrer vi på kommuneraden, er kommunen både det vi filtrerer på og
det vi teller, og da gjelder delmengderegelen av seg selv. Fylkestallet settes
sammen etterpå via `Area.ParentFid`.

Forutsetningen er bare `Area.ParentFid`, som `FilterAreasBySelection` uansett
bruker for å velge hvilke celler som vises. Ingen ny invariant.

**Distinkt telling er ikke valgfritt.** 213 302 observasjoner (0,38 %) har
kommunerad i mer enn én kommune, fordi lokasjonen ligger på en kommunegrense.
Summeres kommunetallene, telles de to ganger i fylkescellen. Det er også
grunnen til at `AggregateMunicipalityCountsByCounty` ikke kan gjenbrukes — den
summerer `Area.ObservationCount` og arver problemet.

**Taket på to fylker er målt, ikke antatt.** Opprullingen kjører én spørring
per fylke, så kostnaden ganges opp mens semijoinen er én spørring uansett:

| Fylker | Opprulling | Semijoin |
|---|---|---|
| 1 | **439 ms** | 728 ms |
| 2 | **547 ms** | 1946 ms |
| 3 | 812 ms | **658 ms** |
| 5 | 1315 ms | **877 ms** |
| 15 | 2354 ms | **878 ms** |

Uten taket innførte optimaliseringen en regresjon: `reise:kartlegging` 525 →
1702 ms og `reise:alt-paa-en-gang` 1207 → 4350 ms, begge med `kommune:mange`
(15 kommuner i 9 fylker). Med taket: 530 og 1276 ms.

Skal taket heves, må opprullingen først bli ÉN spørring — gruppert på et
CASE-uttrykk som mapper kommune til fylke, med `COUNT(DISTINCT ObservationId)`.
Da forsvinner multiplikatoren. Ikke forsøkt.

### AreaCounts over fire Full-kjøringer

| Gruppe | Kall | ingen fiks | +geojoin | +opprulling | +tak |
|---|---|---|---|---|---|
| z1 + kommune (opprulling) | 78 | 12,5 s | 15,2 s | 4,1 s | **3,8 s** |
| semijoin hoppet over | 251 | 32,7 s | 15,7 s | 14,7 s | 12,5 s |
| semijoin beholdt | 495 | 60,2 s | 46,4 s | 45,2 s | 35,3 s |
| ingen områdefilter (uberørt) | 730 | 40,8 s | 45,8 s | 47,6 s | 38,3 s |
| **AreaCounts totalt** | 1554 | **146 s** | 123 s | 112 s | **90 s** |

Siste kjøring hadde medvind på 16–24 % på de uberørte gruppene, så
opprullingens −75 % er reelt nærmere −68 %. Retningen er uansett entydig.

Full-kjøring, AreaCounts-tilfellene med områdefilter over 824 kall:

| | uten fiks | med fiks #1 | med fiks #2 |
|---|---|---|---|
| MED områdefilter (berørt) | 105,5 s | **76,4 s** | **77,2 s** |
| UTEN områdefilter (uberørt) | 40,8 s | 43,7 s | 45,8 s |
| | | −28 % / +7 % | −27 % / +12 % |

0 av 6758 endrede radantall i begge. Største enkeltutslag 1586 → 265 ms.

Begge kjøringene hadde motvind på alt uberørt (se «Fallgruver»), så den reelle
effekten er større enn −28 %. At den berørte gruppen ligger stabilt på 76–77 s
mens den uberørte driver oppover, er det som gjør funnet troverdig.

**Områdebuffer** (`AreaCountCacheLevel1` / `-Level2`). Ferdig beregnede antall
per (dimensjon, bøtte, utdatacelle): 1 448 814 rader på ettnivå, 36 320 085 på
toernivå. Dimensjonsregisteret i `AreaCountCacheDimensions` deles med
punktbufferen.

**Geografi som ÉN dimensjon.** Kommune, fylke, Svalbard, verneområde og
havområde ORes sammen i semi-joinen, så et utvalg over flere er en union —
og en union lar seg ikke sette sammen av separate dimensjoner uten et
korreksjonsledd. Splittet ga dessuten *flere* rader på toernivå: 4 316 084 mot
3 630 515.

**Ferdig serialisert og gzippet svar** for ufiltrerte AreaMarkers. Det er
identisk hver gang og det eneste frontend ber om der — to kall per sidelast.

**`includeGeometry = false`** for tellinger. Geometriene er 15 MB på zoomnivå 2
og brukes ikke når svaret bare er tall.

---

## Eksport — `POST /api/export/csv/*`

Endepunktene krever ekte OIDC-innlogging, så perf-suiten dekker dem ikke.
Tallene under er målt ved å kjøre spørringene direkte mot
`Artskart3IndexProdLikeTestMigrations`, med samme form som koden bruker.

Stien har to helt ulike spørringer, og bare den ene er brukeren sin å vente på.

### `summary` — COUNT før eksporten startes

Synkron, og brukeren ser den som ventetid etter å ha trykket. Grensene er
**300 000 rader mykt og 500 000 hardt** (`CsvExport:Limits` i
`Artskart3.Api/appsettings.json`). Merk at `ExportService` har 50 000/100 000
som kodefallback — de tallene gjelder ikke, og en måling som bruker dem
undervurderer taket med 5×.

| Filter | Rader | Varm |
|---|---|---|
| ufiltrert | 61 052 216 | 438 ms |
| institusjon Birdlife | 29 218 886 | 190 ms |
| **fylke Trøndelag** | 7 976 997 | **3837 ms** |
| **fylke + taksongruppe Fugler** | 4 924 690 | **2297 ms** |
| **kommune Farsund** | 2 169 747 | **4095 ms** |
| taksongruppe Hjuldyr | 44 519 | 2 ms |
| taksongruppe, to verdier | 44 520 | 2 ms |
| institusjon Molltax | 22 872 | 1 ms |
| institusjon, to verdier | 22 904 | 1 ms |

Rekkefølgen er ikke etter radantall: å telle 61 millioner ufiltrert tar 438 ms,
mens å telle 2,2 millioner i én kommune tar 4095 ms. Kostnaden er
`EXISTS`-semi-joinen mot `ObservationEntityIndex`, ikke radene.
**Områdefilteret er hele problemet her.**

### Arbeiderens batchløkke — keyset-paginering

`WHERE <filter> AND Id > @lastId ORDER BY Id` i batcher på 5000, med en
statussjekk og 100 ms pause mellom hver (`CsvExportOptions.Worker`). Tidene er
ren SQL; pausene kommer i tillegg med 100 ms per batch.

Målt ved reelt tak (500 000 rader, 100 batcher):

| Filter | Rader | SQL | + pauser |
|---|---|---|---|
| institusjon Havforskning | 480 402 | 15,1–26,2 s | ~25–36 s |
| taksongruppe Veps | 484 658 | 3,0 s | ~13 s |
| **kommune 4613** | **480 174** | **17,0 s** | **~26,7 s** |
| **kommune 5021** | **370 460** | **11,7 s** | **~19,2 s** |
| kommune Farsund — ULOVLIG, kappet | 500 000 | 112,0 s | ~122 s |

Farsund-raden er med for sammenligningens skyld, men **den eksporten kan ikke
skje**: 2 169 747 observasjoner er over hardgrensen, så `summary` avviser den.
Løkken ble kappet ved 500 000 rader for å se formen, og tallet er dermed
hverken en reell eksport eller representativt for en.

Jeg antok først at en lovlig eksport måtte være *verre*, fordi den må
traversere hele Id-området mens Farsund-kjøringen stoppet etter ~23 %. Det var
feil, og målingen viser det motsatte: 17,0 s mot 112,0 s. Kostnaden følger
altså ikke hvor mye av klyngeindeksen som traverseres. Mest sannsynlig velger
optimalisereren plan etter hvor tett filteret treffer — Farsund er 3,6 % av
tabellen, kommune 4613 er 0,8 % — men det er en hypotese, ikke verifisert mot
planen.

Praktisk konsekvens: **lovlige områdeeksporter ligger på 12–17 sekunder SQL**,
på linje med de andre filtrene. Hardgrensen beskytter, men ikke fordi den
begrenser skannearbeidet — den utelukker de tette filtrene, og det er de tette
som får den dårlige planen.

Mindre eksporter:

| Filter | Rader | SQL |
|---|---|---|
| institusjon Molltax | 22 872 | 0,25–0,67 s |
| institusjon, to verdier | 22 904 | 0,16–0,24 s |
| taksongruppe Hjuldyr | 44 519 | 5,4 s |
| taksongruppe, to verdier | 44 520 | 5,7–6,4 s |

Tallene varierer 1,7× mellom kjøringer på samme filter (Havforskning 15,1 og
26,2 s i to kjøringer), så les dem som størrelsesorden.

Farsund viser at per-batch-tiden **vokser utover i løpet**: 277, 249, 210, 208,
211, 551, … 1471, 1434, 1428, …, 1551, 1622 ms. Omtrent 6× fra første til
siste. Det er keyset-paginering der filteret ikke dekkes av en indeks på `Id`:
planen går framover i klyngeindeksen fra `lastId` og må lese stadig flere rader
per 5000 treff. Hardgrensen holder det i sjakk; heves den, vokser dette videre.

### Datosortering — målt og FORKASTET

> **Konklusjonen her ble snudd av en måling.** Første runde dekket bare
> institusjon og taksongruppe, og der er datosortering like god eller bedre.
> På **områdefilter er den 204× verre**, og områdefilter er det dyreste vi har.
> Tabellene under står i den rekkefølgen funnene kom, med vendepunktet til
> slutt.

Spørsmålet var om eksporten også burde sortere på dato, og om det ville
forenkle noe. Målt med keyset på `(DateTimeCollected DESC, Id DESC)` mot
dagens keyset på `Id`:

| Filter | Rader | keyset Id | keyset dato |
|---|---|---|---|
| institusjon Havforskning | 480 k | 15,1–26,2 s | **3,6–4,6 s** |
| taksongruppe Veps | 485 k | 3,0 s | 4,0 s |
| kommune Farsund (til taket) | 500 k | 112,0 s | **89,7 s** |
| institusjon Havforskning + BIOREHAB | 486 k | 28,9 s | 30,3 s |
| institusjon Havforskning + Molltax | 500 k | 30,8 s | 31,8 s |

Ved taket er datosortering aldri vesentlig verre og ofte mye bedre. Grunnen er
at `IX_Observation_InstitutionOrgId` er nøklet `(InstitutionOrgId,
DateTimeCollected)` — datosortert keyset går rett langs indeksen, mens
Id-keyset må sortere. Per-batch-tiden blir også flat (49–52 ms helt ut) der
Id-keyset degraderer.

**Men IN-problemet fra listevisningen kommer tilbake**, fordi det er
sorteringen som utløser det. På mindre eksporter:

| Filter | Rader | keyset Id | keyset dato |
|---|---|---|---|
| institusjon Molltax alene | 22 872 | 0,67 s | **0,18 s** |
| institusjon, to små | 22 904 | 0,24 s | 1,02 s |
| institusjon, tre små | 28 398 | 0,17 s | 2,07 s |
| taksongruppe Hjuldyr alene | 44 519 | 5,39 s | **0,24 s** |
| taksongruppe, to små | 44 520 | 5,67 s | 12,06 s |

Enkeltverdi blir 3–22× raskere, flerverdi 4–12× tregere. Utslaget forsvinner
ved taket (der er flerverdi 30,3 mot 28,9 s), så det rammer bare små eksporter
— og i absolutte tall er verste tilfelle 12 s på en bakgrunnsjobb.

**Korrekthetsfellen:** 958 912 observasjoner (1,57 %) har `DateTimeCollected
= NULL`. I SQL Server sorterer NULL sist ved DESC, og keyset-predikatet
`DateTimeCollected < @siste` utelukker dem — de ville aldri blitt nådd. Målt:
Havforskning 480 402 → 479 736 rader, Veps 484 658 → 476 332, Hjuldyr
44 519 → 42 959 (3,5 % borte). En ekte implementasjon må ta dem i en egen
runde til slutt. Det er kompleksitet som kommer i tillegg, ikke som går bort.

#### Vendepunktet: områdefilter

Tabellene over dekker institusjon og taksongruppe. Med områdefilter kollapser
datosortering fullstendig. Lovlige eksporter, kjørt helt gjennom:

| Filter | Rader | keyset Id | keyset dato | Faktor |
|---|---|---|---|---|
| kommune 4613 | 480 174 | **17,0 s** | **3473 s (58 min)** | 204× |
| kommune 5021 | 370 460 | **11,7 s** | **1890 s (32 min)** | 161× |

Første batch tok 57,8 og 57,7 sekunder — begge datokjøringene betaler nesten et
helt minutt bare på å finne de 5000 nyeste radene.

Årsaken er at institusjon har `IX_Observation_InstitutionOrgId
(InstitutionOrgId, DateTimeCollected)` — datosortert keyset går rett langs den.
Områdefilteret har ingen tilsvarende indeks på `Observation`; det er et `EXISTS`
mot `ObservationEntityIndex`. For å få datosortert rekkefølge må planen gå langs
datoindeksen og probe `EXISTS` per rad, og med 480 000 treff i 61 millioner
rader betyr det omtrent 61 millioner prober fordelt på 96 batcher.

Datokjøringene mistet også rader uten dato, som ventet: 4613 leverte 478 219 av
480 174 (0,41 % borte), 5021 363 428 av 370 460 (1,9 %).

**Datosortering skal ikke innføres i eksporten.** Ikke som generell endring.

Vil man ha den, må områdefiltrerte eksporter drives fra
`ObservationEntityIndex` med `IX_OEI_AreaListView` i stedet for fra
`Observation` med `EXISTS` — altså samme strategi som listevisningen bruker.
Det er en større omskriving enn å bytte sorteringsnøkkel, og den må måles for
seg.

### Forkastet

**`OFFSET`/`FETCH` med datosortering.** Vokser lineært med offset:
Havforskning 51,2 s, Veps 72,6 s, og Farsund nådde bare 275 000 av 500 000
rader på fire minutter. Keyset finnes nettopp for å unngå dette.

### Undersøkt og forkastet

**Å porte områdefiksen fra listevisningen hit.** Eksportspørringen har verken
`ORDER BY` eller `TOP`, så den tomme Svalbard-grenen koster ingenting — det var
tapet av den sorterte indekslesningen som gjorde den dyr i listevisningen.
Målt på fylke Trøndelag: 4217 ms med begge grenene, 4476 ms med bare fylke.
Ingen forskjell.

**Grening per verdi på flerverdifiltre.** Samme årsak: uten sortering er det
ingen rekkefølge å miste. Målt i batchløkken, der det tross alt er en
`ORDER BY Id`: taksongruppe Hjuldyr alene 5972 ms mot begge verdiene 6411 ms,
institusjon Molltax alene 250 ms mot begge 155 ms. Mekanismen fra
listevisningen finnes ikke her.

---

## Fallgruver i selve målingen

Disse har kostet reell tid og feilkonklusjoner. Les dem før du måler.

**`QUOTED_IDENTIFIER`.** `ObservationTaxonHierarchy` har filtrerte indekser, og
filtrerte indekser krever innstillingen PÅ for å kunne brukes. .NET SqlClient
har den på; **sqlcmd har den av**. En måling fra sqlcmd uten `-I` viste 210 035
sidelesninger der API-et bruker 3 — og ga en helt feil diagnose.

**Lokale variabler er ikke parametere.** `DECLARE @x INT = 5` gir
tetthetsestimat, ikke parametersniffing, og dermed en annen plan enn EF får. En
måling med lokale variabler viste 11 ms på en spørring som i virkeligheten tok
105 047 ms. Bruk `sp_executesql` med ekte parametere.

**`SYSUTCDATETIME()` evalueres én gang per spørring.** Tidtaking lagt inn i
SELECT-lista på en aggregering gir meningsløse tall.

**Kald mot varm.** Suiten måler hvert tilfelle to ganger og rapporterer bare
den varme. Full bruker rundt 24 minutter reelt arbeid men rapporteres som ~12.
Kald og varm er i praksis like (720 mot 718 s), så kaldmålingen fanger
ingenting lenger — den dobler bare kjøretiden.

**Radantall fanger ikke alt.** Suitens korrekthetssjekk sammenligner antall
rader per måling. Polygonbufferens taksonfeil ga 32 lokasjoner med for lave
tall, men bare én forsvant helt — så sjekken så 1 avvik der det var 32. Vil du
bevise at to stier er like, sammenlign innholdet.

**Ett utsnitt er ikke alle utsnitt.** Perf-suitens `oslo` er 170566–355820 ×
6599473–6702091. Frontendens maks-utsnitt på zoomnivå 11 ser likt ut i form men
ligger på Trondheim-breddegrad. En måling på feil utsnitt ser helt troverdig ut.

**Støyen mellom kjøringer er stor nok til å lese som en endring.** Tre Full-
kjøringer der de to første hadde **identisk kode** ga AreaCounts 154, 126 og
146 sekunder — ±20 % uten at noe var endret. Fordelingen flyttet seg bredt
(median 10 → 17 → 11 ms), så den så ut som en ekte regresjon og ikke som
enkeltutslag. Enkelttilfeller svinger tilsvarende:
`atferd:lett+koordpresisjon:lett` på side 50 målte 433, 261 og 713 ms.

Praktisk regel: en endring under ~25 s på et endepunkt, eller under ~2× på et
enkeltkall, kan ikke avgjøres med én kjøring. Kjør om igjen før du tror på den
— i begge retninger.

**Hele maskinen kan være treg, ikke bare endringen din.** To kjøringer
2026-10-05 lå 7–15 % over kjøringen 2026-10-01 på ALT som ikke var rørt, og
avstanden økte mellom de to:

| Uberørt | 10-01 | 10-05 #1 | 10-05 #2 |
|---|---|---|---|
| LocationPolygons | 40,4 s | 43,5 s | 45,5 s |
| Observation | 70,1 s | 79,5 s | 83,9 s |
| Locations | 332,9 s | 382,4 s | 401,1 s |
| AreaCounts uten områdefilter | 40,8 s | 43,7 s | 45,8 s |

LocationPolygons serveres fra minnelageret og rører aldri databasen, så det kan
hverken forklares med en spørringsendring eller med databasen.
`PolygonLocationStore` brukte dessuten 45,3 s og 43,6 s på å bygge mot 31,6 s
den 10-01 — samme arbeid, +40 %.

**Prøvd og avkreftet: at API-prosessen degraderer.** Nærliggende hypotese, siden
prosessen hadde servert tre Full-kjøringer. Målt med Quick før og etter
omstart: 16 946 mot 16 397 ms, og LocationPolygons ble marginalt *tregere* på
den ferske prosessen (1204 → 1323 ms). Prosessalder er ikke årsaken. Jeg vet
ikke hva som er det.

Lærdommen står uansett: sammenlign alltid mot en gruppe endringen IKKE kan ha
rørt, i samme kjøring. Totaler mellom kjøringer kan ikke skilles fra
maskinstøy, men «berørt mot uberørt innenfor samme kjøring» kan det.

**Suitens totaltid er ikke målet.** Den er et representativt utvalg som skal
avdekke problemområder. At en fiks «bare» tar ned totalen med 5 av 568
sekunder er ikke et argument mot den, hvis kallet den fikser går fra 2,5
sekunder til 24 ms for brukeren. Suiten vekter etter antall testtilfeller, ikke
etter hvor ofte noen faktisk gjør det. Bruk totalen til å oppdage at noe annet
ble verre, og vurder funnet på hva enkeltkallet gjør med brukeropplevelsen.

---

## Åpne spørsmål

### Grid-aggregering av Locations — ligger til avklaring med prosjektet

**Ikke** for å filtrere raskere; den varianten er målt og forkastet over. Dette
er å returnere ruteceller med antall i stedet for 100 000 enkeltpunkter.

Målt for Oslo-utsnittet, som inneholder 966 708 lokasjoner:

| Rutestørrelse | Celler | Svar | Mot dagens 3,04 MB | SQL |
|---|---|---|---|---|
| 1000 m | 16 366 | 351 kB | 11 % | 109 ms |
| 500 m | 49 121 | 1055 kB | 34 % | 126 ms |
| 250 m | 116 100 | 2494 kB | 80 % | — |
| 100 m | 266 391 | 5723 kB | **184 %** | — |
| *dagens: 100 000 punkter* | — | *3,04 MB* | *100 %* | *213 ms* |

Anslått hele kallet: 410 → ~250 ms ved 500 m, ~200 ms ved 1 km.

**Det viktigste er ikke hastigheten.** Gridet dekker alle 966 708 lokasjonene i
utsnittet, mens vi i dag returnerer de 100 000 mest observerte — altså 21 %.
Forslaget er både raskere og mer komplett.

Til perspektiv: utsnittet er 185 × 103 km vist på rundt 1500 piksler bredde, så
én piksel er ~123 meter. Ved 500 m ruter er hver celle fire piksler. Ved 100 m
snur regnestykket — da blir det flere celler enn dagens tak på 100 000, og
svaret større.

Dette er en produktbeslutning om hva kartet skal vise når punktene uansett
overlapper, ikke en teknisk optimalisering. Koordinatjoinen forsvinner også,
siden cellene har koordinater av seg selv.

#### To varianter, og de koster helt ulikt

**A: regne gridet på sparket.** En `GROUP BY East/1000, North/1000` over radene
punktbufferen allerede skanner. **Null byte lagring**, ingen byggejobb,
ingenting å holde oppdatert.

**B: forhåndsberegne gridet** i en egen tabell på
`(DimensionId, BucketId, CellX, CellY)`. 1 km-varianten er bygget og målt, de
to andre er ekstrapolert fra dens faktiske 26,1 byte per rad:

| Rutestørrelse | Rader | Størrelse |
|---|---|---|
| 1000 m | 16 651 311 | **415 MB** (målt) |
| 500 m | 24 116 616 | ~600 MB |
| 250 m | 32 481 549 | ~809 MB |
| *dagens punktbuffer* | *75 097 617* | *2178 MB* |

Byggetid 51 sekunder for 1 km, på toppen av punktbufferens 361. Må bygges om i
takt med den.

Hva det gir, samme utsnitt og filter:

| | Tid | Rader ut |
|---|---|---|
| Dagens: 100 000 punkter | 219 ms | 100 000 |
| Grid på sparket, 1 km | 107 ms | 16 366 |
| Forhåndsberegnet grid, 1 km | **14 ms** | 16 483 |

**Gridet erstatter ikke punktbufferen.** På høyt zoomnivå vil man ha faktiske
punkter, og de kallene er allerede raske (37 ms i snitt under 1000 rader).
Punktbufferen kan heller ikke trimmes — et lite utsnitt hvor som helst i landet
trenger alle lokasjoner tilgjengelig. Regnestykket for variant B er derfor
2178 + 415 = **2593 MB, altså 19 % mer**, ikke 415 i stedet for 2178.

Merk at forhåndsberegning ga 16 483 celler mot 16 366 på sparket: cellegrensene
følger ikke utsnittsgrensene, så kantceller kommer med hele og svaret dekker
litt mer areal enn det ble spurt om.

**Anbefaling: start med variant A.** Den koster ingenting, er dobbelt så rask
som i dag, og lar dere se hvordan et rutenett faktisk ser ut på kartet før noen
forplikter seg til 415 MB og en byggejobb. Variant B kan legges på etterpå uten
å endre API-et, siden svaret er det samme.

### Andre åpne spørsmål

**East/North i svaret i stedet for lat/lon.** Kartet er i EPSG:25833,
kartutsnittet sendes inn i 25833, og bufferen lagrer East/North. Men svaret
inneholder lat/lon i EPSG:4326 — og erklærer samtidig `"epsg":25833`, som er
feil. Frontend ignorerer feltet og har hardkodet `dataProjection: 'EPSG:4326'`,
så vi konverterer fra kartets eget system på serveren og tilbake igjen i
nettleseren.

Ytelsesgevinsten er liten: koordinatjoinen er målt til **18 ms** (210 mot
192 ms på ellers identiske spørringer), pluss kanskje 8 ms fra ~16 % mindre
nyttelast. Rundt 26 ms av 410. **Korrekthetsargumentet er det sterke** — dette
bør rettes fordi erklæringen er feil, ikke fordi det er tregt. Riktig løsning
er trolig å la `Epsg`-parameteren faktisk virke: 25833 gir East/North, 4326 gir
lat/lon.

**Taket på 100 000.** For Oslo-utsnittet matcher 483 272 lokasjoner og vi
returnerer 100 000. Alt skalerer lineært med antallet. Grid-aggregering over
gjør spørsmålet mindre presserende.

**`fylke:*` på listevisningen.** Fire av de fem tregeste `Observation`-kallene
er områdefiltre via semi-join mot indekstabellen. Mekanismen er ikke
undersøkt — det er ikke samme som de to dokumenterte over.

**Importjobben må ha `QUOTED_IDENTIFIER ON`.** Filtrerte indekser krever det
også ved skriving, ikke bare lesing.

**Kaldmålingen i perf-suiten.** Kald og varm er i praksis like (720 mot 718 s
på Full), så kaldmålingen fanger ingenting lenger men dobler kjøretiden. Å
fjerne den ville halvert suiten fra ~20 til ~10 minutter.
