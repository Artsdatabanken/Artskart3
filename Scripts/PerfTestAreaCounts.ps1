<#
.SYNOPSIS
    Ytelsestest av områdetellinger (AreaMarkers/AreaCounts) på tvers av filterkombinasjoner.

.DESCRIPTION
    Kjører en matrise av filterkombinasjoner mot søke-API-et og måler responstid per case.
    Brukes til å:
      - finne trege filterkombinasjoner
      - verifisere hvilke indekser på ObservationEntityIndex som faktisk brukes
        (kjør Scripts/IndexUsageDiff.sql før og etter)

    Testdata hentes automatisk fra Lookup-endepunktene. Taksoner på hvert rangnivå
    finnes ved å gå nedover i artstreet og alltid velge noden med flest observasjoner,
    slik at vi tester tyngste realistiske tilfelle.

.PARAMETER BaseUrl
    Rot-URL til API-et. Standard: https://localhost:5088

.PARAMETER Endpoint
    AreaMarkers (standard) eller AreaCounts.

    MERK: AreaCounts har 5 minutters minnecache per filter (SearchService.cs).
    Gjentatte kjøringer innen 5 minutter måler cache-treff, ikke databasen.
    AreaMarkers cacher kun det ufiltrerte tilfellet, så filtrerte kall er alltid ferske.

.PARAMETER ZoomLevels
    Hvilke zoomnivåer som testes. Standard: 1 og 2.

.EXAMPLE
    ./Scripts/PerfTestAreaCounts.ps1

.EXAMPLE
    ./Scripts/PerfTestAreaCounts.ps1 -BaseUrl https://localhost:5088 -ZoomLevels 1
#>

[CmdletBinding()]
param(
    [string]   $BaseUrl = 'https://localhost:5088',
    [ValidateSet('AreaMarkers', 'AreaCounts')]
    [string]   $Endpoint = 'AreaMarkers',
    [int[]]    $ZoomLevels = @(1, 2),
    [int]      $SlowThresholdMs = 1000,

    # Antall kall mot artstreet under leting etter tyngste takson per rangnivå.
    # Høyere verdi gir større sjanse for å finne det virkelig tyngste taksonet.
    [int]      $TaxonSearchBudget = 60
)

$ErrorActionPreference = 'Stop'
$BaseUrl = $BaseUrl.TrimEnd('/')

# Rangnivåer vi vil ha testdata for. Speiler kolonnene på ObservationEntityIndex
# pluss nivåene over som løses opp i minnet av TaxonHierarchyService.
#
# MERK: vanlig hashtable, ikke [ordered]. En ordered dictionary tolker heltalls-
# indeksering som posisjon, ikke nøkkel, så $RankNames[11] ville gitt feil verdi.
$RankNames = @{
    1  = 'Kingdom'
    3  = 'Phylum'
    6  = 'Class'
    11 = 'Order'
    15 = 'Family'
    19 = 'Genus'
    22 = 'Species'
}
$RankOrder = @(1, 3, 6, 11, 15, 19, 22)

function Invoke-Api {
    param(
        [string] $Path,
        [string] $Method = 'GET',
        [object] $Body
    )

    # X-CSRF kreves av Duende BFF (Program.cs: .AsBffApiEndpoint()).
    # BFF-sjekken kjører før [AllowAnonymous], så uten headeren får vi 401.
    $params = @{
        Uri                  = "$BaseUrl$Path"
        Method               = $Method
        Headers              = @{ 'X-CSRF' = '1' }
        SkipCertificateCheck = $true
        TimeoutSec           = 300
    }

    if ($null -ne $Body) {
        $params.Body        = ($Body | ConvertTo-Json -Depth 10 -Compress)
        $params.ContentType = 'application/json'
    }

    Invoke-RestMethod @params
}

function Remove-Markup {
    # Artstreet returnerer navn med HTML (f.eks. "<i>Larus</i>")
    param([string] $Text)
    if (-not $Text) { return $Text }
    return ($Text -replace '<[^>]+>', '')
}

function Get-HeaviestLookupItem {
    <#
        Henter elementet med flest observasjoner fra et Lookup-endepunkt.
        De fleste Lookup-DTO-ene eksponerer observationCount, så vi kan velge
        verste tilfelle i stedet for et vilkårlig element.

        NestedProperty brukes for Categories, der svaret er kategorityper
        som hver inneholder en categories-liste.
    #>
    param([string] $Path, [string] $NestedProperty)

    try { $response = Invoke-Api -Path $Path }
    catch {
        Write-Warning "Kunne ikke hente $Path : $($_.Exception.Message)"
        return $null
    }

    $items = if ($NestedProperty) { @($response) | ForEach-Object { $_.$NestedProperty } }
             else                 { @($response) }

    $items = @($items | Where-Object { $null -ne $_ -and $null -ne $_.id })
    if ($items.Count -eq 0) {
        Write-Warning "Ingen elementer med id funnet i $Path"
        return $null
    }

    $heaviest = $items |
        Sort-Object -Property @{ Expression = { [int64]($_.observationCount ?? 0) } } -Descending |
        Select-Object -First 1

    return [PSCustomObject]@{
        Id    = [int]$heaviest.id
        Name  = Remove-Markup $heaviest.name
        Count = [int64]($heaviest.observationCount ?? 0)
    }
}

function Get-TaxonSamplesByRank {
    <#
        Finner det tyngste taksonet per rangnivå via best-først-søk i artstreet.

        Et enkelt grådig dypdykk (alltid tyngste barn) ville bare gitt tyngste
        takson langs én gren. Den tyngste arten ligger ikke nødvendigvis under
        den tyngste ordenen — den kan sitte i en helt annen del av treet.

        Derfor ekspanderer vi i stedet alltid den tyngste ikke-besøkte noden på
        tvers av hele fronten, og beholder maksimum per rangnivå underveis.
        Søket er begrenset av et request-budsjett.
    #>
    param([int] $MaxRequests = 60, [int] $FrontierCap = 250)

    Write-Host "Finner tyngste takson per rangnivaa (maks $MaxRequests kall)..." -ForegroundColor Cyan

    $samples  = @{}
    $frontier = [System.Collections.Generic.List[object]]::new()
    $requests = 0

    function Get-Weight($node) { [int64]($node.cumulativeObservationCount ?? 0) }

    function Register-Node($node) {
        # Eksplisitt [int]-cast: JSON-tall kan deserialiseres som Int64,
        # som ikke matcher Int32-nøklene i $RankNames.
        $rank = [int]$node.taxonRankId
        if (-not $RankNames.ContainsKey($rank)) { return }

        $weight = Get-Weight $node
        if ($samples.ContainsKey($rank) -and $samples[$rank].Count -ge $weight) { return }

        $samples[$rank] = [PSCustomObject]@{
            Id    = $node.id
            Name  = Remove-Markup ($node.validScientificName ?? $node.preferredPopularName)
            Count = $weight
        }
    }

    # Seed med rotnodene
    try { $roots = Invoke-Api -Path '/api/Lookup/TaxonTree' }
    catch {
        Write-Warning "Kunne ikke hente artstreet: $($_.Exception.Message)"
        return $samples
    }

    foreach ($root in $roots) {
        Register-Node $root
        if ($root.hasChildren) { $frontier.Add($root) }
    }

    while ($frontier.Count -gt 0 -and $requests -lt $MaxRequests) {
        # Ekspander den tyngste noden på fronten
        $sorted  = $frontier | Sort-Object -Property @{ Expression = { Get-Weight $_ } } -Descending
        $current = $sorted[0]
        $frontier.Remove($current) | Out-Null

        try { $children = Invoke-Api -Path "/api/Lookup/TaxonTree?parentTaxonId=$($current.id)" }
        catch { continue }
        finally { $requests++ }

        if (-not $children) { continue }

        foreach ($child in $children) {
            Register-Node $child
            if ($child.hasChildren) { $frontier.Add($child) }
        }

        # Hold fronten liten — vi bryr oss uansett bare om de tyngste grenene
        if ($frontier.Count -gt $FrontierCap) {
            $trimmed = $frontier |
                Sort-Object -Property @{ Expression = { Get-Weight $_ } } -Descending |
                Select-Object -First $FrontierCap
            $frontier.Clear()
            foreach ($n in $trimmed) { $frontier.Add($n) }
        }
    }

    Write-Host "  ($requests kall, $($frontier.Count) noder igjen paa fronten)" -ForegroundColor DarkGray
    foreach ($rankId in $RankOrder) {
        if (-not $samples.ContainsKey($rankId)) { continue }
        $s = $samples[$rankId]
        Write-Host ("  {0,-8} {1,-30} id={2,-8} obs={3}" -f $RankNames[$rankId], $s.Name, $s.Id, $s.Count)
    }

    return $samples
}

# ---------------------------------------------------------------------------
# Oppdagelsesfase — bygg testdata fra API-et
# ---------------------------------------------------------------------------

Write-Host "`nYtelsestest av $Endpoint mot $BaseUrl" -ForegroundColor Green
Write-Host ('=' * 78)

if ($Endpoint -eq 'AreaCounts') {
    Write-Warning 'AreaCounts har 5 min cache per filter. Restart API-et for rene maalinger ved gjentatte kjoeringer.'
}

# Sjekk at API-et svarer før vi gjør noe annet. Uten dette produserer skriptet
# en full kjøring der hver "måling" egentlig er en tilkoblingstimeout.
try {
    $null = Invoke-Api -Path '/api/Lookup/TaxonGroups'
}
catch {
    Write-Host ''
    Write-Error ("API-et paa $BaseUrl svarer ikke: {0}`n" -f $_.Exception.Message +
                 'Start API-et og proev igjen.')
    exit 1
}

$taxonSamples = Get-TaxonSamplesByRank -MaxRequests $TaxonSearchBudget

Write-Host 'Finner tyngste verdi per filterdimensjon...' -ForegroundColor Cyan
$category    = Get-HeaviestLookupItem -Path '/api/Lookup/Categories' -NestedProperty 'categories'
$taxonGroup  = Get-HeaviestLookupItem -Path '/api/Lookup/TaxonGroups'
$basis       = Get-HeaviestLookupItem -Path '/api/Lookup/BasisOfRecords'
$behavior    = Get-HeaviestLookupItem -Path '/api/Lookup/Behaviors'
$institution = Get-HeaviestLookupItem -Path '/api/Lookup/Institutions'

foreach ($pair in @(
    @{ Label = 'Kategori';     Item = $category },
    @{ Label = 'Taksongruppe'; Item = $taxonGroup },
    @{ Label = 'Funntype';     Item = $basis },
    @{ Label = 'Atferd';       Item = $behavior },
    @{ Label = 'Institusjon';  Item = $institution })) {

    if ($pair.Item) {
        Write-Host ("  {0,-13} {1,-30} id={2,-8} obs={3}" -f `
            $pair.Label, $pair.Item.Name, $pair.Item.Id, $pair.Item.Count)
    }
}

# Områder — AreaResponseDto pakker hver type i en AreaTypeDto med en areas-liste
$countyFids = @(); $municipalityFids = @()
try {
    $areas = Invoke-Api -Path '/api/Lookup/Areas'
    $countyFids       = @($areas.counties.areas       | Select-Object -First 2 | ForEach-Object { $_.fid })
    $municipalityFids = @($areas.municipalities.areas | Select-Object -First 2 | ForEach-Object { $_.fid })
}
catch { Write-Warning "Kunne ikke hente omraader: $($_.Exception.Message)" }

$countyFids       = @($countyFids       | Where-Object { $_ })
$municipalityFids = @($municipalityFids | Where-Object { $_ })

Write-Host "  Fylker=$($countyFids -join ',') Kommuner=$($municipalityFids -join ',')"

# ---------------------------------------------------------------------------
# Testmatrise
# ---------------------------------------------------------------------------

$cases = [System.Collections.Generic.List[object]]::new()

function Add-Case {
    <#
        Target styrer hvilket endepunkt casen kjøres mot:
          Area        - $Endpoint (AreaMarkers/AreaCounts), kjøres per zoomnivå
          Observation - /api/Search/Observation, kjøres én gang (ingen zoom)
          Locations   - /api/Search/Locations, kjøres én gang (ingen zoom)
    #>
    param(
        [string] $Name,
        [hashtable] $Filter,
        [string] $Group,
        [ValidateSet('Area', 'Observation', 'Locations')]
        [string] $Target = 'Area'
    )
    $cases.Add([PSCustomObject]@{ Name = $Name; Filter = $Filter; Group = $Group; Target = $Target })
}

# Ett case per rangnivå — treffer hver denormaliserte kolonne og oppløsningen over
foreach ($rankId in $RankOrder) {
    if (-not $taxonSamples.ContainsKey($rankId)) {
        Write-Warning "Fant ikke takson for rang $rankId ($($RankNames[$rankId])) - hopper over"
        continue
    }
    $sample = $taxonSamples[$rankId]
    Add-Case "Takson: $($RankNames[$rankId]) ($($sample.Name))" @{ taxonIds = @($sample.Id) } 'Takson'
}

# Enkeltfiltre — de lavselektive som columnstore skal dekke
if ($category)   { Add-Case "Kategori ($($category.Name))"       @{ categoryIds      = @($category.Id) }   'Enkeltfilter' }
if ($taxonGroup) { Add-Case "Taksongruppe ($($taxonGroup.Name))" @{ taxonGroupIds    = @($taxonGroup.Id) } 'Enkeltfilter' }
if ($basis)      { Add-Case "Funntype ($($basis.Name))"          @{ basisOfRecordIds = @($basis.Id) }      'Enkeltfilter' }
Add-Case 'Registreringsstatus alene' @{ registrationStatusId = 1 }                  'Enkeltfilter'
Add-Case 'Med bilder'                @{ withImages = $true }                        'Enkeltfilter'
Add-Case 'Uten bilder'               @{ withImages = $false }                       'Enkeltfilter'
Add-Case 'Periode (aarsspenn)'       @{ period = @{ from = 2000; to = 2010 } }      'Enkeltfilter'
Add-Case 'Periode (maaneder)'        @{ period = @{ months = @(6, 7, 8) } }         'Enkeltfilter'
Add-Case 'Koordinatpresisjon'        @{ coordinatePrecision = @{ from = 0; to = 100 } } 'Enkeltfilter'

# ---------------------------------------------------------------------------
# MIDLERTIDIG DEAKTIVERT — subquery-filtrene
#
# Disse tar 3-24 sekunder hver og dominerer hele kjøretiden, uten å gi ny
# informasjon: vi vet allerede hvorfor de er trege (needsObservationSubquery
# tvinger join mot Observation-tabellen) og at CompleteFilter-planen fikser dem.
#
# Målt utgangspunkt før planen (192M rader, zoom 1 / zoom 2):
#   Prosjektnavn   21 878 / 23 983 ms
#   Katalognummer  18 509 / 19 363 ms
#   Atferd          5 446 /  5 605 ms
#   Institusjon     3 657 /  4 208 ms
#
# LEGG DISSE TILBAKE når CompleteFilter startes — de er før/etter-målingen for
# hele planen, og ferdigkriteriet er at gruppen havner under ~1,5 s.
#
# if ($behavior)    { Add-Case "Atferd ($($behavior.Name))"         @{ behaviorIds     = @($behavior.Id) }    'Subquery' }
# if ($institution) { Add-Case "Institusjon ($($institution.Name))" @{ organizationIds = @($institution.Id) } 'Subquery' }
# Add-Case 'Prosjektnavn: 1 tegn'      @{ projectName   = 'a' }        'Subquery'
# Add-Case 'Prosjektnavn: lengre'      @{ projectName   = 'univers' }  'Subquery'
# Add-Case 'Katalognummer: 1 tegn'     @{ catalogNumber = '1' }        'Subquery'
# Add-Case 'Katalognummer: lengre'     @{ catalogNumber = '123456' }   'Subquery'
# ---------------------------------------------------------------------------

# Områdevalg
if ($countyFids)       { Add-Case 'Fylkesvalg'   @{ countyIds       = $countyFids }       'Omraade' }
if ($municipalityFids) { Add-Case 'Kommunevalg'  @{ municipalityIds = $municipalityFids } 'Omraade' }

# Kombinasjoner
if ($category -and $taxonGroup) {
    Add-Case 'Kategori + taksongruppe' @{ categoryIds = @($category.Id); taxonGroupIds = @($taxonGroup.Id) } 'Kombinasjon'
}
if ($taxonSamples.ContainsKey(22) -and $category) {
    Add-Case 'Art + kategori' @{ taxonIds = @($taxonSamples[22].Id); categoryIds = @($category.Id) } 'Kombinasjon'
}
if ($taxonSamples.ContainsKey(11)) {
    Add-Case 'Orden + periode' @{ taxonIds = @($taxonSamples[11].Id); period = @{ from = 2000; to = 2010 } } 'Kombinasjon'
}
if ($taxonSamples.ContainsKey(22) -and $countyFids) {
    Add-Case 'Art + fylke' @{ taxonIds = @($taxonSamples[22].Id); countyIds = $countyFids } 'Kombinasjon'
}

# ---------------------------------------------------------------------------
# Observasjonssøk (/api/Search/Observation)
#
# Går ikke via ComputeFilteredAreaCounts. Bruker ApplyCommonFilters mot
# Observation-tabellen pluss skalare subselects mot ObservationEntityIndex
# (Institution og MunicipalityId) som slår opp på ObservationId — altså PK-en.
# Dette er stien som forklarer hvorfor PK_ObservationEntityIndex viste null seeks
# da matrisen bare dekket AreaMarkers.
# ---------------------------------------------------------------------------

Add-Case 'Uten filter'  @{}                                        'Observasjon' -Target Observation
Add-Case 'Paginert'     @{ pageNumber = 1; resultsPerPage = 25 }   'Observasjon' -Target Observation

if ($taxonSamples.ContainsKey(22)) {
    Add-Case "Art ($($taxonSamples[22].Name))" @{ taxonIds = @($taxonSamples[22].Id) } 'Observasjon' -Target Observation
}
if ($taxonSamples.ContainsKey(11)) {
    Add-Case "Orden ($($taxonSamples[11].Name))" @{ taxonIds = @($taxonSamples[11].Id) } 'Observasjon' -Target Observation
}
if ($category) {
    Add-Case "Kategori ($($category.Name))" @{ categoryIds = @($category.Id) } 'Observasjon' -Target Observation
}
if ($countyFids) {
    Add-Case 'Fylke' @{ countyIds = $countyFids } 'Observasjon' -Target Observation
}

# ---------------------------------------------------------------------------
# Lokasjonssøk (/api/Search/Locations)
#
# Grupperer observasjoner per lokasjon innenfor et kartutsnitt. Envelope er i
# EPSG:25833 (UTM 33N). To utsnitt: hele Norge (verste tilfelle) og et
# bynært utsnitt (typisk bruk).
# ---------------------------------------------------------------------------

$envNorge = @{ minX = -80000; maxX = 1120000; minY = 6440000; maxY = 7950000 }
$envOslo  = @{ minX = 240000; maxX =  280000; minY = 6630000; maxY = 6670000 }

Add-Case 'Hele Norge, uten filter' @{ envelope = $envNorge } 'Lokasjon' -Target Locations
Add-Case 'Oslo, uten filter'       @{ envelope = $envOslo }  'Lokasjon' -Target Locations

if ($taxonSamples.ContainsKey(22)) {
    Add-Case "Hele Norge + art" @{ envelope = $envNorge; taxonIds = @($taxonSamples[22].Id) } 'Lokasjon' -Target Locations
}
if ($taxonSamples.ContainsKey(11)) {
    Add-Case "Hele Norge + orden" @{ envelope = $envNorge; taxonIds = @($taxonSamples[11].Id) } 'Lokasjon' -Target Locations
}
if ($category) {
    Add-Case "Hele Norge + kategori" @{ envelope = $envNorge; categoryIds = @($category.Id) } 'Lokasjon' -Target Locations
}

# ---------------------------------------------------------------------------
# Kjør matrisen
# ---------------------------------------------------------------------------

function Measure-Case {
    <#
        Kjører én case mot ett endepunkt og returnerer måleresultatet.
        Radtellingen er tilnærmet — responsformene varierer mellom endepunktene.
    #>
    param([object] $Case, [string] $Path, [string] $Label)

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $status = 'OK'
    $rows = 0

    try {
        $response = Invoke-Api -Path $Path -Method POST -Body $Case.Filter

        $rows = if ($null -eq $response)       { 0 }
                elseif ($response.locations)   { @($response.locations).Count }  # { epsg, locations[] }
                elseif ($response.items)       { @($response.items).Count }      # paginert observasjonssvar
                else                           { @($response).Count }
    }
    catch {
        $status = "FEIL: $($_.Exception.Message)"
    }

    $sw.Stop()
    $ms = [int]$sw.Elapsed.TotalMilliseconds

    $color = if ($status -ne 'OK') { 'Red' } elseif ($ms -ge $SlowThresholdMs) { 'Yellow' } else { 'Gray' }
    Write-Host ("  {0,-4} {1,6} ms  {2}" -f $Label, $ms, $Case.Name) -ForegroundColor $color

    return [PSCustomObject]@{
        Nivaa  = $Label
        Gruppe = $Case.Group
        Case   = $Case.Name
        Ms     = $ms
        Rader  = $rows
        Status = $status
    }
}

$areaCases  = @($cases | Where-Object Target -eq 'Area')
$otherCases = @($cases | Where-Object Target -ne 'Area')
$totalRuns  = ($areaCases.Count * $ZoomLevels.Count) + $otherCases.Count

Write-Host "`nKjoerer $totalRuns spoerringer ($($areaCases.Count) omraade-case x $($ZoomLevels.Count) zoomnivaa + $($otherCases.Count) andre)...`n" -ForegroundColor Cyan

$results = [System.Collections.Generic.List[object]]::new()

# Områdetellinger — én kjøring per zoomnivå
foreach ($zoom in $ZoomLevels) {
    foreach ($case in $areaCases) {
        $results.Add((Measure-Case -Case $case -Label "z$zoom" `
            -Path "/api/Search/$Endpoint`?zoomLevel=$zoom"))
    }
}

# Observasjons- og lokasjonssøk — ingen zoomnivå
foreach ($case in $otherCases) {
    $results.Add((Measure-Case -Case $case -Label '-' `
        -Path "/api/Search/$($case.Target)"))
}

# ---------------------------------------------------------------------------
# Oppsummering
# ---------------------------------------------------------------------------

Write-Host "`n$('=' * 78)"

# Kun vellykkede kall er målinger. En feilet case har en "tid" som egentlig er
# tilkoblingstimeout, og skal ikke rangeres sammen med ekte resultater.
$ok = @($results | Where-Object Status -eq 'OK')

Write-Host 'Tregeste case:' -ForegroundColor Green
$ok | Sort-Object Ms -Descending | Select-Object -First 15 |
    Format-Table Nivaa, @{ N = 'Ms'; E = { $_.Ms }; A = 'right' }, Gruppe, Case, Rader -AutoSize

Write-Host 'Gjennomsnitt per gruppe:' -ForegroundColor Green
$ok | Group-Object Gruppe | ForEach-Object {
    [PSCustomObject]@{
        Gruppe  = $_.Name
        Antall  = $_.Count
        SnittMs = [int](($_.Group | Measure-Object Ms -Average).Average)
        MaksMs  = ($_.Group | Measure-Object Ms -Maximum).Maximum
    }
} | Sort-Object SnittMs -Descending | Format-Table -AutoSize

$failed = $results | Where-Object Status -ne 'OK'
if ($failed) {
    Write-Host 'Feilede case:' -ForegroundColor Red
    $failed | Format-Table Nivaa, Case, Status -AutoSize
}

if ($ok.Count -eq 0) {
    Write-Host 'Ingen vellykkede maalinger - resultatene over er ikke brukbare.' -ForegroundColor Red
}
else {
    $slow = @($ok | Where-Object { $_.Ms -ge $SlowThresholdMs })
    if ($slow.Count -gt 0) {
        Write-Host "$($slow.Count) av $($ok.Count) case over $SlowThresholdMs ms." -ForegroundColor Yellow
    }
    else {
        Write-Host "Alle $($ok.Count) case under $SlowThresholdMs ms." -ForegroundColor Green
    }
}

Write-Host "`nKjoer naa andre halvdel av Scripts/IndexUsageDiff.sql for aa se hvilke indekser som ble brukt." -ForegroundColor Cyan
