#Requires -Version 7.0
<#
.SYNOPSIS
    LANGTKJOERENDE ytelsessuite. Kjoeres kun manuelt, paa eksplisitt beskjed.

.DESCRIPTION
    ==========================================================================
     ADVARSEL TIL AGENTER OG AUTOMATIKK
     Dette skriptet skal IKKE kjoeres uten at brukeren eksplisitt har bedt om
     det. En full kjoering tar fra titalls minutter til flere timer, sender
     tusenvis av spoerringer og legger merkbar last paa databasen.

     Trenger du bare aa se hva suiten dekker, bruk -ListOnly. Den gjoer ingen
     maalinger og laster ikke databasen.

     For raske, avgrensede maalinger finnes Scripts/PerfTestListView.ps1 og
     Scripts/PerfTestAreaCounts.ps1.
    ==========================================================================

    Suiten dekker alle 17 filterdimensjonene i IObservationFilter, hver paa
    flere selektivitetsnivaaer, og kombinerer dem systematisk i stedet for
    haandplukket. Formaalet er todelt:

      1. Finne hvilke FILTERKOMBINASJONER som er trege — ikke bare hvilke
         enkeltfiltre vi allerede mistenker.
      2. Oppdage REGRESJONER. Resultatene lagres, og -Compare stiller denne
         kjoeringen mot en tidligere.

    Testdataene fryses i fixtures.json og gjenbrukes mellom kjoeringer. Uten det
    kan "tyngste institusjon" bytte identitet naar data vokser, og
    sammenligningen ville maalt noe annet enn den tror. Se Fixtures.ps1.

.PARAMETER Level
    Quick     enkeltfiltre og oppslag. Minutter.
    Standard  + alle dimensjonspar, 40 tripler, brukerreiser. Titalls minutter.
    Full      + par ogsaa paa lette nivaaer, 150 tripler, flere endepunkter. Timer.

.PARAMETER IncludeAreaCounts
    Tar med AreaCounts-endepunktet. AV som standard: det har fem minutters
    minnecache per filter, saa i en lang kjoering maaler gjentatte treff cachen
    og ikke databasen. Restart API-et foerst hvis du bruker flagget.

.PARAMETER Compare
    Sti til en tidligere resultatfil. Etter kjoeringen skrives en regresjonstabell.

.PARAMETER ListOnly
    Skriver ut matrisen og avslutter. Ingen maalinger, ingen last paa databasen.

.EXAMPLE
    ./Scripts/PerfSuite/Run.ps1 -ListOnly

.EXAMPLE
    ./Scripts/PerfSuite/Run.ps1 -Level Standard

.EXAMPLE
    ./Scripts/PerfSuite/Run.ps1 -Level Full -Compare Scripts/PerfSuite/results/20260901-101500-bff02c7-Full.csv

.NOTES
    API-et maa peke paa en database med produksjonslignende datamengder — en tom
    database gir misvisende tall. Artskart3IndexProdLikeTestMigrations med
    Windows-autentisering er den vi bruker:

      "ConnectionStrings:DefaultConnection": "data source=localhost;initial catalog=Artskart3IndexProdLikeTestMigrations;Integrated Security=true;MultipleActiveResultSets=True;TrustServerCertificate=True"

    Kjoer Scripts/IndexUsageDiff.sql foer og etter for aa se hvilke indekser som
    faktisk ble brukt.
#>

[CmdletBinding()]
param(
    [string] $BaseUrl = 'https://localhost:5088',

    [ValidateSet('Quick', 'Standard', 'Full')]
    [string] $Level = 'Standard',

    # Antall kjoeringer per maaling. Foerste holdes utenfor medianen (kald tid).
    [int]    $Runs = 2,

    [int]    $ResultsPerPage = 10,

    # Tak per kall. Case som bruker lengre tid registreres som TIMEOUT og
    # kjoeringen gaar videre — uten dette stopper én daarlig kombinasjon alt.
    [int]    $CaseTimeoutSec = 60,

    [int]    $SlowThresholdMs = 1000,

    # Stopper pent naar budsjettet er brukt opp. 0 = ingen grense.
    [int]    $TimeBudgetMinutes = 0,

    # Kjoer bare case der noekkel, lag eller dimensjon inneholder denne teksten.
    [string] $Only,

    [ValidateSet('enkelt', 'par', 'trippel', 'reise', 'oppslag')]
    [string[]] $Layer,

    [switch] $IncludeAreaCounts,

    # Bygger testdataene paa nytt. Gjoer gamle baselinjer usammenlignbare.
    [switch] $RefreshFixtures,

    [int]    $TaxonSearchBudget = 80,

    # Indekssporing. Er begge satt, leser suiten sys.dm_db_index_usage_stats foer
    # og etter hver maaling og skriver differansen til CSV-en. Det er forskjellen
    # paa aa vite AT noe ble tregere og HVORFOR: byttet planen indeks, eller er
    # det bare mer data? Lesningen skjer etter at stoppeklokken er stoppet.
    #
    # Krever leserettigheter paa serveren, og at det er SAMME database API-et
    # peker paa - ellers maaler du en annen instans enn du tror.
    [string] $SqlServer,
    [string] $SqlDatabase,

    [string] $Compare,

    # Styrer utvalget av tripler. Endres den, endres matrisen.
    [int]    $Seed = 20260909,

    [switch] $ListOnly
)

$ErrorActionPreference = 'Stop'

$suiteDir   = $PSScriptRoot
$resultsDir = Join-Path $suiteDir 'results'

Import-Module (Join-Path $suiteDir 'PerfLib.psm1') -Force
. (Join-Path $suiteDir 'Fixtures.ps1')
. (Join-Path $suiteDir 'Cases.ps1')

Initialize-PerfApi -BaseUrl $BaseUrl -TimeoutSec $CaseTimeoutSec

Write-Host ''
Write-Host ('=' * 78)
Write-Host " Artskart3 ytelsessuite - nivaa $Level" -ForegroundColor Green
Write-Host ('=' * 78)

# ---------------------------------------------------------------------------
# Testdata og matrise
# ---------------------------------------------------------------------------

$fixturePath = Join-Path $suiteDir 'fixtures.json'

if ($ListOnly -and -not (Test-Path $fixturePath)) {
    Write-Host 'Ingen fixtures.json finnes enna. Foerste kjoering maa hente testdata fra API-et,' -ForegroundColor Yellow
    Write-Host 'saa -ListOnly kan ikke brukes for det er gjort en gang.' -ForegroundColor Yellow
    exit 1
}

if (-not $ListOnly) {
    if (-not (Test-PerfApi)) {
        Write-Host 'Start API-et og proev igjen.' -ForegroundColor Red
        exit 1
    }
}

$fixtures = Get-PerfFixtureSet -Path $fixturePath -Refresh:$RefreshFixtures -TaxonSearchBudget $TaxonSearchBudget

$cases = @(New-PerfCaseMatrix -Fixtures $fixtures -Level $Level -Seed $Seed)

$endpoints = @(New-PerfEndpointMatrix -Level $Level -Envelopes $fixtures.envelopes `
    -IncludeAreaCounts:$IncludeAreaCounts -ResultsPerPage $ResultsPerPage)

if ($Layer)  { $cases = @($cases | Where-Object { $Layer -contains $_.Layer }) }
if ($Only)   { $cases = @($cases | Where-Object { $_.CaseKey -like "*$Only*" -or $_.Layer -like "*$Only*" -or $_.Dimensions -like "*$Only*" }) }

if ($cases.Count -eq 0) {
    Write-Host 'Ingen case matchet filtrene.' -ForegroundColor Red
    exit 1
}

# Oppslagscasene kjoerer ett kall hver; resten kjoerer ett per endepunkt.
$oppslagCount = @($cases | Where-Object Layer -eq 'oppslag').Count
$filterCount  = $cases.Count - $oppslagCount
$totalRuns    = ($filterCount * $endpoints.Count) + $oppslagCount

Write-Host ''
Write-Host 'Matrise:' -ForegroundColor Cyan
$cases | Group-Object Layer | Sort-Object Name | ForEach-Object {
    Write-Host ("  {0,-10} {1,4} case" -f $_.Name, $_.Count)
}
Write-Host ("  {0,-10} {1,4} varianter: {2}" -f 'endepunkt', $endpoints.Count,
    (@($endpoints | ForEach-Object { "$($_.Endpoint)/$($_.Variant)" }) -join ', '))
Write-Host ("  {0,-10} {1,4} maalinger x {2} kjoeringer = {3} kall" -f `
    'totalt', $totalRuns, $Runs, ($totalRuns * $Runs)) -ForegroundColor Yellow

if ($ListOnly) {
    Write-Host ''
    $cases | Select-Object Layer, CaseKey, Dimensions, Label | Format-Table -AutoSize
    Write-Host 'ListOnly: ingen maalinger utfoert.' -ForegroundColor DarkGray
    exit 0
}

# ---------------------------------------------------------------------------
# Kjoering
# ---------------------------------------------------------------------------

if (-not (Test-Path $resultsDir)) { New-Item -ItemType Directory -Path $resultsDir | Out-Null }

$gitSha = try { (git rev-parse --short HEAD 2>$null) } catch { 'ukjent' }
if (-not $gitSha) { $gitSha = 'ukjent' }

$runId      = (Get-Date).ToString('yyyyMMdd-HHmmss')
$timestamp  = (Get-Date).ToUniversalTime().ToString('o')
$outputPath = Join-Path $resultsDir "$runId-$gitSha-$Level.csv"

Write-Host ''
Write-Host "Skriver til $outputPath" -ForegroundColor DarkGray
Write-Host "Terskel for gul markering: $SlowThresholdMs ms. Tak per kall: $CaseTimeoutSec s."
if ($TimeBudgetMinutes -gt 0) { Write-Host "Tidsbudsjett: $TimeBudgetMinutes minutter." }
Write-Host ''

$sporIndekser = $false
if ($SqlServer -and $SqlDatabase) {
    $sporIndekser = Initialize-PerfSql -Server $SqlServer -Database $SqlDatabase
    if ($sporIndekser) {
        Write-Host "Indekssporing paa mot $SqlServer/$SqlDatabase." -ForegroundColor DarkGray
    }
    else {
        Write-Host 'Indekssporing AV - kjoeringen fortsetter uten.' -ForegroundColor Yellow
    }
}
elseif ($SqlServer -or $SqlDatabase) {
    Write-Host 'Bade -SqlServer og -SqlDatabase maa settes for indekssporing.' -ForegroundColor Yellow
}

$results   = [System.Collections.Generic.List[object]]::new()
$suiteWatch = [System.Diagnostics.Stopwatch]::StartNew()
$done      = 0
$avbrutt   = $false

foreach ($case in $cases) {
    if ($TimeBudgetMinutes -gt 0 -and $suiteWatch.Elapsed.TotalMinutes -ge $TimeBudgetMinutes) {
        Write-Host "`nTidsbudsjettet er brukt opp. Stopper etter $done av $totalRuns maalinger." -ForegroundColor Yellow
        $avbrutt = $true
        break
    }

    # Oppslagscasene er GET uten filterkropp og kjoeres én gang, ikke per endepunkt.
    $planned = if ($case.Layer -eq 'oppslag') {
        @(@{ Endpoint = 'Lookup'; Variant = 'get'; Path = $case.LookupPath; Extra = $null })
    } else {
        $endpoints
    }

    foreach ($ep in $planned) {
        $done++

        $body = $null
        if ($null -ne $ep.Extra) {
            # Kopi, ikke referanse — ellers ville endepunktets pageNumber blitt
            # liggende igjen i casens filter og lekket til neste endepunkt.
            $body = @{}
            foreach ($k in $case.Filter.Keys) { $body[$k] = $case.Filter[$k] }
            foreach ($k in $ep.Extra.Keys)    { $body[$k] = $ep.Extra[$k] }
        }

        $method = if ($case.Layer -eq 'oppslag') { 'GET' } else { 'POST' }

        # Snapshot FOER maalingen. Selve lesningen ligger utenfor stoppeklokken,
        # som starter inne i Measure-PerfRequest.
        $idxFoer = if ($sporIndekser) { Get-PerfIndexSnapshot } else { $null }

        $m = Measure-PerfRequest -Path $ep.Path -Method $method -Body $body `
            -Runs $Runs -TimeoutSec $CaseTimeoutSec

        $idxEtter  = if ($sporIndekser) { Get-PerfIndexSnapshot } else { $null }
        $indekser  = Get-PerfIndexDelta -Before $idxFoer -After $idxEtter

        $row = [PSCustomObject]@{
            RunId      = $runId
            Timestamp  = $timestamp
            GitSha     = $gitSha
            Level      = $Level
            Layer      = $case.Layer
            CaseKey    = $case.CaseKey
            Dimensions = $case.Dimensions
            Endpoint   = $ep.Endpoint
            Variant    = $ep.Variant
            ColdMs     = $m.ColdMs
            WarmMs     = $m.WarmMs
            Rows       = $m.Rows
            Status     = $m.Status
            Indexes    = $indekser
            Label      = $case.Label
        }

        $results.Add($row)

        # Skrives fortloepende. En avbrutt kjoering skal fortsatt gi brukbare data.
        $row | Export-Csv -Path $outputPath -Append -NoTypeInformation -Encoding utf8

        $color = if ($m.Status -ne 'OK')            { 'Red' }
                 elseif ($m.WarmMs -ge $SlowThresholdMs) { 'Yellow' }
                 else                               { 'DarkGray' }

        $tid = if ($m.Status -eq 'OK') { "{0,7} ms" -f $m.WarmMs } else { $m.Status }

        Write-Host ("[{0,5}/{1}] {2,-8} {3,-18} {4}  {5,5} rader  {6}" -f `
            $done, $totalRuns, $case.Layer, "$($ep.Endpoint)/$($ep.Variant)", `
            $tid, $m.Rows, $case.CaseKey) -ForegroundColor $color
    }
}

$suiteWatch.Stop()
Close-PerfSql

# ---------------------------------------------------------------------------
# Oppsummering
# ---------------------------------------------------------------------------

Write-Host ''
Write-Host ('=' * 78)
Write-Host ("Ferdig paa {0:hh\:mm\:ss}. {1} maalinger skrevet til {2}" -f `
    $suiteWatch.Elapsed, $results.Count, (Split-Path -Leaf $outputPath)) -ForegroundColor Green
if ($avbrutt) { Write-Host 'MERK: kjoeringen ble avbrutt av tidsbudsjettet - matrisen er ikke fullstendig.' -ForegroundColor Yellow }

# Kun vellykkede kall er maalinger. En feilet case har en "tid" som egentlig er
# et tak eller en tilkoblingsfeil, og skal ikke rangeres sammen med ekte tall.
$ok = @($results | Where-Object Status -eq 'OK')

if ($ok.Count -eq 0) {
    Write-Host 'Ingen vellykkede maalinger - resultatene er ikke brukbare.' -ForegroundColor Red
    exit 1
}

Write-Host "`nTregeste case (varm tid):" -ForegroundColor Green
$ok | Sort-Object WarmMs -Descending | Select-Object -First 25 |
    Format-Table @{ N = 'VarmMs'; E = { $_.WarmMs }; A = 'right' },
                 @{ N = 'KaldMs'; E = { $_.ColdMs }; A = 'right' },
                 @{ N = 'Endepunkt'; E = { "$($_.Endpoint)/$($_.Variant)" } },
                 Layer, CaseKey, Rows -AutoSize

Write-Host 'Per endepunkt:' -ForegroundColor Green
$ok | Group-Object Endpoint, Variant | ForEach-Object {
    [PSCustomObject]@{
        Endepunkt = $_.Name
        Case      = $_.Count
        SnittMs   = [int](($_.Group | Measure-Object WarmMs -Average).Average)
        MedianMs  = (@($_.Group | Sort-Object WarmMs))[[int]($_.Count / 2)].WarmMs
        MaksMs    = ($_.Group | Measure-Object WarmMs -Maximum).Maximum
    }
} | Sort-Object SnittMs -Descending | Format-Table -AutoSize

Write-Host 'Per lag:' -ForegroundColor Green
$ok | Group-Object Layer | ForEach-Object {
    [PSCustomObject]@{
        Lag     = $_.Name
        Case    = $_.Count
        SnittMs = [int](($_.Group | Measure-Object WarmMs -Average).Average)
        MaksMs  = ($_.Group | Measure-Object WarmMs -Maximum).Maximum
    }
} | Sort-Object SnittMs -Descending | Format-Table -AutoSize

# Marginalkostnad per dimensjon — analysen enkeltmaalingene ikke kan gi.
$dimKost = @(Get-PerfDimensionCost -Results $ok)
if ($dimKost.Count -gt 0) {
    Write-Host 'Marginalkostnad per filterdimensjon:' -ForegroundColor Green
    Write-Host '  (snitt for case MED dimensjonen minus snitt for case UTEN. Korrelasjon,' -ForegroundColor DarkGray
    Write-Host '   ikke bevis - dimensjoner som ofte opptrer sammen faar liknende skaar.)' -ForegroundColor DarkGray
    $dimKost | Format-Table -AutoSize
}

# Hvilke indekser betjener de trege casene?
#
# Tabellen svarer paa noe topplista alene ikke gjoer: er det én bestemt indeks
# som gaar igjen i det som er tregt? En indeks som dominerer blant de trege og
# er fravaerende blant de raske, er der aa se etter planvipping.
if ($sporIndekser) {
    $medIndeks = @($ok | Where-Object { $_.Indexes })
    if ($medIndeks.Count -gt 0) {
        Write-Host 'Indeksbruk - trege mot raske case:' -ForegroundColor Green

        $trege  = @($medIndeks | Where-Object { $_.WarmMs -ge $SlowThresholdMs })
        $raske  = @($medIndeks | Where-Object { $_.WarmMs -lt $SlowThresholdMs })

        $navnFra = {
            param($rader)
            $rader | ForEach-Object { ($_.Indexes -split ';') } |
                ForEach-Object { ($_ -split ':')[0] } | Where-Object { $_ }
        }

        $iTrege = @(& $navnFra $trege) | Group-Object | ForEach-Object { @{ N = $_.Name; C = $_.Count } }
        $iRaske = @(& $navnFra $raske) | Group-Object

        $iTrege | ForEach-Object {
            $navn = $_.N
            $raskAntall = @($iRaske | Where-Object Name -eq $navn).Count
            [PSCustomObject]@{
                Indeks     = $navn
                ITregeCase = $_.C
                IRaskeCase = if ($raskAntall -gt 0) { (@($iRaske | Where-Object Name -eq $navn)[0]).Count } else { 0 }
            }
        } | Sort-Object ITregeCase -Descending | Select-Object -First 12 | Format-Table -AutoSize
    }
}

$feilet = @($results | Where-Object Status -ne 'OK')
if ($feilet.Count -gt 0) {
    Write-Host "$($feilet.Count) feilede eller tidsavbrutte maalinger:" -ForegroundColor Red
    $feilet | Select-Object -First 25 |
        Format-Table @{ N = 'Endepunkt'; E = { "$($_.Endpoint)/$($_.Variant)" } }, CaseKey, Status -AutoSize
}

$trege = @($ok | Where-Object { $_.WarmMs -ge $SlowThresholdMs })
if ($trege.Count -gt 0) {
    Write-Host "$($trege.Count) av $($ok.Count) maalinger over $SlowThresholdMs ms." -ForegroundColor Yellow
}
else {
    Write-Host "Alle $($ok.Count) maalinger under $SlowThresholdMs ms." -ForegroundColor Green
}

if ($Compare) {
    Compare-PerfRuns -BaselinePath $Compare -CurrentPath $outputPath
}
else {
    $forrige = @(Get-ChildItem $resultsDir -Filter '*.csv' |
        Where-Object { $_.FullName -ne $outputPath } |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1)

    if ($forrige.Count -gt 0) {
        Write-Host "`nSammenlign med forrige kjoering:" -ForegroundColor Cyan
        Write-Host "  ./Scripts/PerfSuite/Compare.ps1 -Baseline '$($forrige[0].FullName)' -Current '$outputPath'"
    }
}

Write-Host "`nKjoer naa andre halvdel av Scripts/IndexUsageDiff.sql for aa se hvilke indekser som ble brukt." -ForegroundColor Cyan
