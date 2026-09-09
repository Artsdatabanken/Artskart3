<#
.SYNOPSIS
    Ytelsestest av listevisningen (/api/Search/Observation) på tvers av filterkombinasjoner.

.DESCRIPTION
    Søsterskript til PerfTestAreaCounts.ps1, men mot observasjonssøket i stedet for
    områdetellingene. Kjører en matrise av filterkombinasjoner mot API-et og måler
    responstid per case.

    Listevisningen har en annen spørringsprofil enn områdetellingene:
    ApplyCommonFilters bygger predikatene, resultatet sorteres på Observation.Id og
    kuttes med TOP (ResultsPerPage x LookaheadMultiplier). Det gjør ytelsen avhengig
    av hvor langt SQL Server må lese nedover klyngeindeksen før TOP-en er fylt — og
    dermed av hvor SELEKTIVT filteret er.

    Derfor tester matrisen samme filter med ulik selektivitet, ikke bare med én
    vilkårlig verdi. Institusjonsfilteret kjøres mot tyngste, median og letteste
    institusjon; kommunefilteret mot tyngste og letteste kommune. Et filter som er
    raskt for den tyngste verdien kan være svært tregt for den letteste.

.PARAMETER BaseUrl
    Rot-URL til API-et. Standard: https://localhost:5088

.PARAMETER ResultsPerPage
    Sidestørrelse. Standard 10, som er det listevisningen faktisk sender
    (pageSizeOptions[0] i list-view.component.ts). Repositoriet ganger opp med
    LookaheadMultiplier (4), så SQL-en henter TOP 40.

.PARAMETER Runs
    Antall kjøringer per case. Første kjøring rapporteres for seg (kald: plan
    kompileres, sider leses fra disk), resten som median. Stort sprik mellom de to
    betyr at spørringen leser mye data.

.PARAMETER Only
    Kjør bare case der gruppe eller navn inneholder denne teksten.
    F.eks. -Only Institusjon

.NOTES
    Observasjonssøket har ingen cache (SearchService.GetObservationsAsync kaller rett
    på repositoriet), så gjentatte kjøringer måler databasen og ikke et cache-treff.
    Det er forskjellen fra AreaCounts, som har 5 minutters minnecache per filter.

    API-et må peke på en database med produksjonslignende datamengder — en tom
    database gir misvisende tall. Artskart3IndexProdLikeTestMigrations med
    Windows-autentisering er den vi bruker:

      "ConnectionStrings:DefaultConnection": "data source=localhost;initial catalog=Artskart3IndexProdLikeTestMigrations;Integrated Security=true;MultipleActiveResultSets=True;TrustServerCertificate=True"

.EXAMPLE
    ./Scripts/PerfTestListView.ps1

.EXAMPLE
    ./Scripts/PerfTestListView.ps1 -Only Institusjon -Runs 5

.EXAMPLE
    ./Scripts/PerfTestListView.ps1 -BaseUrl https://localhost:5088 -ResultsPerPage 25
#>

[CmdletBinding()]
param(
    [string]   $BaseUrl = 'https://localhost:5088',
    [int]      $ResultsPerPage = 10,
    [int]      $Runs = 2,
    [int]      $SlowThresholdMs = 1000,
    [string]   $Only,

    # Antall kall mot artstreet under leting etter tyngste takson per rangnivå.
    [int]      $TaxonSearchBudget = 60
)

$ErrorActionPreference = 'Stop'
$BaseUrl = $BaseUrl.TrimEnd('/')

# Rangnivåer vi vil ha testdata for. Speiler kolonnene på ObservationTaxonHierarchy
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

function Get-SelectivitySpread {
    <#
        Plukker tyngste, median og letteste element fra en liste med
        observationCount — de tre punktene på selektivitetsskalaen.

        Dette er kjernen i testen. Et filter mot tyngste institusjon fylles av de
        første radene TOP-en leser; et filter mot den letteste kan kreve at
        SQL Server leser store deler av klyngeindeksen før den finner nok rader.
        Samme kode, samme plan, helt ulik kostnad.

        Elementer uten observasjoner tas ut — de gir et tomt resultat og måler
        ikke annet enn hvor lang tid det tar å lese hele tabellen forgjeves.
    #>
    param([object[]] $Items, [string] $IdProperty = 'id', [string] $Label)

    # Invoke-RestMethod skriver en JSON-liste som ETT pipeline-objekt, ikke som 54.
    # @(Invoke-Api ...) gir derfor en ett-elements liste som inneholder listen, og
    # $_.observationCount blir en liste med 54 tall i stedet for ett tall.
    # Ett nivå utpakking her gjør funksjonen upåvirket av hvordan kalleren henter
    # dataene. Get-HeaviestLookupItem slipper unna ved å mellomlagre i en variabel
    # først — se $response der.
    $Items = @($Items | ForEach-Object { $_ })

    $withCount = @($Items |
        Where-Object { $null -ne $_ -and [int64]($_.observationCount ?? 0) -gt 0 } |
        Sort-Object -Property @{ Expression = { [int64]($_.observationCount ?? 0) } } -Descending)

    if ($withCount.Count -eq 0) {
        Write-Warning "Ingen $Label med observationCount > 0"
        return $null
    }

    function New-Sample($item, $tag) {
        [PSCustomObject]@{
            Id    = $item.$IdProperty
            Name  = Remove-Markup $item.name
            Count = [int64]($item.observationCount ?? 0)
            Tag   = $tag
        }
    }

    $result = [PSCustomObject]@{
        Heaviest = New-Sample $withCount[0] 'tyngst'
        Median   = New-Sample $withCount[[int]($withCount.Count / 2)] 'median'
        Lightest = New-Sample $withCount[-1] 'lettest'
        All      = $withCount
    }

    Write-Host "  $Label ($($withCount.Count) med observasjoner):"
    foreach ($s in @($result.Heaviest, $result.Median, $result.Lightest)) {
        Write-Host ("    {0,-8} {1,-34} id={2,-8} obs={3}" -f $s.Tag, $s.Name, $s.Id, $s.Count)
    }

    return $result
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

Write-Host "`nYtelsestest av listevisningen mot $BaseUrl" -ForegroundColor Green
Write-Host ("Sidestoerrelse $ResultsPerPage (TOP $($ResultsPerPage * 4) med lookahead), $Runs kjoeringer per case")
Write-Host ('=' * 78)

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

Write-Host "`nFinner tyngste verdi per filterdimensjon..." -ForegroundColor Cyan
$category    = Get-HeaviestLookupItem -Path '/api/Lookup/Categories' -NestedProperty 'categories'
$taxonGroup  = Get-HeaviestLookupItem -Path '/api/Lookup/TaxonGroups'
$basis       = Get-HeaviestLookupItem -Path '/api/Lookup/BasisOfRecords'
$behavior    = Get-HeaviestLookupItem -Path '/api/Lookup/Behaviors'

foreach ($pair in @(
    @{ Label = 'Kategori';     Item = $category },
    @{ Label = 'Taksongruppe'; Item = $taxonGroup },
    @{ Label = 'Funntype';     Item = $basis },
    @{ Label = 'Atferd';       Item = $behavior })) {

    if ($pair.Item) {
        Write-Host ("  {0,-13} {1,-30} id={2,-8} obs={3}" -f `
            $pair.Label, $pair.Item.Name, $pair.Item.Id, $pair.Item.Count)
    }
}

# ---------------------------------------------------------------------------
# Selektivitetsspenn — institusjon og område
#
# Det rapporterte problemet er institusjonsfilteret. InstitutionOrgId har bevisst
# ingen rowstore-indeks (se ArtskartDbContext): begrunnelsen var at institusjon
# har få distinkte verdier med mange rader hver, så et klyngeindeks-scan som
# stopper ved TOP N slår et seek etterfulgt av sortering.
#
# Den begrunnelsen holder for en STOR institusjon. For en liten må scanet lese
# langt nedover Observation.Id før det finner nok rader. Vi måler derfor hele
# spennet i stedet for én verdi, ellers ser filteret raskt ut nettopp i det
# tilfellet det er raskt.
# ---------------------------------------------------------------------------
Write-Host "`nFinner selektivitetsspenn..." -ForegroundColor Cyan

$institutions = $null
try {
    $institutionList = Invoke-Api -Path '/api/Lookup/Institutions'
    $institutions = Get-SelectivitySpread -Items @($institutionList) -Label 'Institusjon'
}
catch { Write-Warning "Kunne ikke hente institusjoner: $($_.Exception.Message)" }

$counties = $null; $municipalities = $null
try {
    $areas = Invoke-Api -Path '/api/Lookup/Areas'
    $municipalities = Get-SelectivitySpread -Items @($areas.municipalities.areas) -IdProperty 'fid' -Label 'Kommune'
    $counties       = Get-SelectivitySpread -Items @($areas.counties.areas)       -IdProperty 'fid' -Label 'Fylke'
}
catch { Write-Warning "Kunne ikke hente omraader: $($_.Exception.Message)" }

# CompleteFilter — samling, prosjekt og katalognummer tar IDer fra typeahead,
# ikke strenger. Testen gjør det samme: slår opp en verdi først, måler så filteret.
$datasetOrg = $null; $projectOrg = $null; $catalogMatch = $null
try {
    $datasetOrg   = Invoke-Api -Path '/api/Lookup/Datasets?search=a&maxCount=1'       | Select-Object -First 1
    $projectOrg   = Invoke-Api -Path '/api/Lookup/Projects?search=a&maxCount=1'       | Select-Object -First 1
    $catalogMatch = Invoke-Api -Path '/api/Lookup/CatalogNumbers?search=12&maxCount=1' | Select-Object -First 1
}
catch { Write-Warning "Kunne ikke hente CompleteFilter-oppslag: $($_.Exception.Message)" }

if ($datasetOrg)   { Write-Host ("  Datasett     {0,-30} id={1}" -f $datasetOrg.name, $datasetOrg.id) }
if ($projectOrg)   { Write-Host ("  Prosjekt     {0,-30} id={1}" -f $projectOrg.name, $projectOrg.id) }
if ($catalogMatch) { Write-Host ("  Katalognr    {0,-30} obs={1}" -f $catalogMatch.catalogNumber, $catalogMatch.observationIds.Count) }

# ---------------------------------------------------------------------------
# Testmatrise
# ---------------------------------------------------------------------------

$cases = [System.Collections.Generic.List[object]]::new()

function Add-Case {
    param(
        [string] $Name,
        [hashtable] $Filter,
        [string] $Group,
        [hashtable] $PageOverride
    )

    # Alle case pagineres som listevisningen gjør, med mindre casen tester
    # paginering selv.
    $body = @{ pageNumber = 1; resultsPerPage = $ResultsPerPage }
    foreach ($k in $Filter.Keys)       { $body[$k] = $Filter[$k] }
    if ($PageOverride) {
        foreach ($k in $PageOverride.Keys) { $body[$k] = $PageOverride[$k] }
    }

    $cases.Add([PSCustomObject]@{ Name = $Name; Filter = $body; Group = $Group })
}

# --- Referanse -------------------------------------------------------------
Add-Case 'Uten filter' @{} 'Referanse'

# --- Institusjon: hele selektivitetsspennet --------------------------------
if ($institutions) {
    foreach ($s in @($institutions.Heaviest, $institutions.Median, $institutions.Lightest)) {
        Add-Case "Institusjon $($s.Tag) ($($s.Name), $($s.Count) obs)" `
            @{ organizationIds = @($s.Id) } 'Institusjon'
    }

    $topThree = @($institutions.All | Select-Object -First 3 | ForEach-Object { $_.id })
    Add-Case "Institusjon x$($topThree.Count) (tyngste)" @{ organizationIds = $topThree } 'Institusjon'

    # Kombinasjoner med den letteste institusjonen. Hvis et ekstra, mer selektivt
    # filter gjør spørringen rask, peker det på at problemet er hvor langt TOP-en
    # må lese — ikke på institusjonspredikatet i seg selv.
    $light = $institutions.Lightest
    if ($municipalities) {
        Add-Case "Institusjon lettest + kommune tyngst" `
            @{ organizationIds = @($light.Id); municipalityIds = @($municipalities.Heaviest.Id) } 'Institusjon'
    }
    Add-Case 'Institusjon lettest + periode' `
        @{ organizationIds = @($light.Id); period = @{ from = 2020; to = 2024 } } 'Institusjon'
    if ($taxonGroup) {
        Add-Case 'Institusjon lettest + taksongruppe' `
            @{ organizationIds = @($light.Id); taxonGroupIds = @($taxonGroup.Id) } 'Institusjon'
    }
    Add-Case 'Institusjon lettest, side 10' `
        @{ organizationIds = @($light.Id) } 'Institusjon' -PageOverride @{ pageNumber = 10 }
}

# --- Geografi --------------------------------------------------------------
if ($municipalities) {
    Add-Case "Kommune tyngst ($($municipalities.Heaviest.Name))"  @{ municipalityIds = @($municipalities.Heaviest.Id) } 'Geografi'
    Add-Case "Kommune lettest ($($municipalities.Lightest.Name))" @{ municipalityIds = @($municipalities.Lightest.Id) } 'Geografi'

    $muni3  = @($municipalities.All | Select-Object -First 3  | ForEach-Object { $_.fid })
    $muni15 = @($municipalities.All | Select-Object -First 15 | ForEach-Object { $_.fid })
    Add-Case "Kommune x$($muni3.Count)"  @{ municipalityIds = $muni3 }  'Geografi'
    Add-Case "Kommune x$($muni15.Count)" @{ municipalityIds = $muni15 } 'Geografi'
}

if ($counties) {
    Add-Case "Fylke tyngst ($($counties.Heaviest.Name))" @{ countyIds = @($counties.Heaviest.Id) } 'Geografi'
    $cty3 = @($counties.All | Select-Object -First 3 | ForEach-Object { $_.fid })
    Add-Case "Fylke x$($cty3.Count)" @{ countyIds = $cty3 } 'Geografi'
}

if ($municipalities -and $counties) {
    # OR-kombinering i EXISTS mot ObservationEntityIndex
    Add-Case 'Kommune + fylke' `
        @{ municipalityIds = @($municipalities.Heaviest.Id); countyIds = @($counties.Heaviest.Id) } 'Geografi'
}

# --- Takson: ett case per rangnivå ----------------------------------------
foreach ($rankId in $RankOrder) {
    if (-not $taxonSamples.ContainsKey($rankId)) {
        Write-Warning "Fant ikke takson for rang $rankId ($($RankNames[$rankId])) - hopper over"
        continue
    }
    $sample = $taxonSamples[$rankId]
    Add-Case "Takson: $($RankNames[$rankId]) ($($sample.Name))" @{ taxonIds = @($sample.Id) } 'Takson'
}

if ($taxonSamples.ContainsKey(22) -and $taxonSamples.ContainsKey(19)) {
    # Flere taksoner blir en UNION av delspørringer — én per takson
    Add-Case 'Takson x2 (union)' `
        @{ taxonIds = @($taxonSamples[22].Id, $taxonSamples[19].Id) } 'Takson'
}

# MERK: her lå det tidligere en Tekstsoek-gruppe som målte preferredPopularName,
# scientificName og author. Feltene fantes i ObservationSearchFilterDto, men
# ingenting sendte dem — artssøket er en typeahead som sender taxonIds. Målingene
# var altså av død API-flate, ikke av noe brukerne treffer. Feltene er fjernet fra
# API-et; ikke legg dem tilbake her uten at det finnes en klient som sender dem.

# --- Enkeltfiltre ----------------------------------------------------------
if ($category)   { Add-Case "Kategori ($($category.Name))"       @{ categoryIds      = @($category.Id) }   'Enkeltfilter' }
if ($taxonGroup) { Add-Case "Taksongruppe ($($taxonGroup.Name))" @{ taxonGroupIds    = @($taxonGroup.Id) } 'Enkeltfilter' }
if ($basis)      { Add-Case "Funntype ($($basis.Name))"          @{ basisOfRecordIds = @($basis.Id) }      'Enkeltfilter' }
if ($behavior)   { Add-Case "Atferd ($($behavior.Name))"         @{ behaviorIds      = @($behavior.Id) }   'Enkeltfilter' }

Add-Case 'Registreringsstatus: paavist'          @{ registrationStatusId = 1 }                      'Enkeltfilter'
Add-Case 'Registreringsstatus: ikke paavist'     @{ registrationStatusId = 2 }                      'Enkeltfilter'
Add-Case 'Registreringsstatus: ikke gjenfunnet'  @{ registrationStatusId = 3 }                      'Enkeltfilter'
Add-Case 'Med bilder'                            @{ withImages = $true }                            'Enkeltfilter'
Add-Case 'Uten bilder'                           @{ withImages = $false }                           'Enkeltfilter'
Add-Case 'Periode (aarsspenn)'                   @{ period = @{ from = 2020; to = 2024 } }          'Enkeltfilter'
Add-Case 'Periode (maaneder)'                    @{ period = @{ months = @(6, 7, 8) } }             'Enkeltfilter'
Add-Case 'Koordinatpresisjon'                    @{ coordinatePrecision = @{ from = 1; to = 100 } } 'Enkeltfilter'

if ($datasetOrg)   { Add-Case "Datasett ($($datasetOrg.name))" @{ datasetOrgId  = $datasetOrg.id } 'Enkeltfilter' }
if ($projectOrg)   { Add-Case "Prosjekt ($($projectOrg.name))" @{ projectOrgId  = $projectOrg.id } 'Enkeltfilter' }
if ($catalogMatch) { Add-Case "Katalognummer ($($catalogMatch.catalogNumber))" @{ observationIds = @($catalogMatch.observationIds) } 'Enkeltfilter' }

# --- Paginering ------------------------------------------------------------
# Skip skjer i SQL. OFFSET vokser med sidetallet, og siden sorteringen er på
# Observation.Id må raden telles forbi én for én.
if ($municipalities) {
    $muniHeavy = @{ municipalityIds = @($municipalities.Heaviest.Id) }
    Add-Case 'Kommune, side 1'   $muniHeavy 'Paginering'
    Add-Case 'Kommune, side 10'  $muniHeavy 'Paginering' -PageOverride @{ pageNumber = 10 }
    Add-Case 'Kommune, side 100' $muniHeavy 'Paginering' -PageOverride @{ pageNumber = 100 }
    Add-Case 'Kommune, 100 per side' $muniHeavy 'Paginering' -PageOverride @{ resultsPerPage = 100 }
}

# --- Kombinasjoner ---------------------------------------------------------
if ($municipalities -and $category) {
    Add-Case 'Kommune + kategori + periode' @{
        municipalityIds = @($municipalities.All | Select-Object -First 3 | ForEach-Object { $_.fid })
        categoryIds     = @($category.Id)
        period          = @{ from = 2015; to = 2025 }
    } 'Kombinasjon'
}

if ($institutions -and $municipalities) {
    Add-Case 'Institusjon tyngst + kommune + periode' @{
        organizationIds = @($institutions.Heaviest.Id)
        municipalityIds = @($municipalities.All | Select-Object -First 3 | ForEach-Object { $_.fid })
        period          = @{ from = 2015; to = 2025 }
    } 'Kombinasjon'
}

if ($institutions -and $municipalities -and $counties -and $category -and $taxonGroup) {
    Add-Case 'Alt paa en gang' @{
        organizationIds     = @($institutions.All | Select-Object -First 3 | ForEach-Object { $_.id })
        municipalityIds     = @($municipalities.All | Select-Object -First 3 | ForEach-Object { $_.fid })
        countyIds           = @($counties.All | Select-Object -First 3 | ForEach-Object { $_.fid })
        categoryIds         = @($category.Id)
        taxonGroupIds       = @($taxonGroup.Id)
        period              = @{ from = 2015; to = 2025 }
        coordinatePrecision = @{ from = 1; to = 1000 }
    } 'Kombinasjon'
}

# ---------------------------------------------------------------------------
# Kjør matrisen
# ---------------------------------------------------------------------------

if ($Only) {
    $cases = [System.Collections.Generic.List[object]](@($cases |
        Where-Object { $_.Group -like "*$Only*" -or $_.Name -like "*$Only*" }))

    if ($cases.Count -eq 0) {
        Write-Host "`nIngen case matchet -Only '$Only'." -ForegroundColor Red
        exit 1
    }
}

function Measure-Case {
    <#
        Kjører én case $Runs ganger og returnerer måleresultatet.

        Første kjøring holdes utenfor medianen. Den betaler for plankompilering i
        SQL Server og for lesing fra disk, mens de neste treffer plancache og
        bufferpool. Begge tallene er interessante: brukeren møter den kalde tiden
        når filteret er nytt, og den varme når hun blar videre.
    #>
    param([object] $Case)

    $times  = [System.Collections.Generic.List[int]]::new()
    $status = 'OK'
    $rows   = 0

    for ($i = 0; $i -lt $Runs; $i++) {
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        try {
            $response = Invoke-Api -Path '/api/Search/Observation' -Method POST -Body $Case.Filter
            $rows = if ($null -eq $response)   { 0 }
                    elseif ($response.items)   { @($response.items).Count }
                    else                       { @($response).Count }
        }
        catch {
            $status = "FEIL: $($_.Exception.Message)"
            $sw.Stop()
            break
        }
        $sw.Stop()
        $times.Add([int]$sw.Elapsed.TotalMilliseconds)
    }

    $first  = if ($times.Count -gt 0) { $times[0] } else { 0 }
    $warm   = @($times | Select-Object -Skip 1)
    $median = if ($warm.Count -gt 0) {
                  $sorted = @($warm | Sort-Object)
                  $sorted[[int]($sorted.Count / 2)]
              } else { $first }

    $color = if ($status -ne 'OK') { 'Red' } elseif ($median -ge $SlowThresholdMs) { 'Yellow' } else { 'Gray' }
    Write-Host ("  {0,7} ms kald  {1,7} ms varm  {2,4} rader  {3}" -f `
        $first, $median, $rows, $Case.Name) -ForegroundColor $color

    return [PSCustomObject]@{
        Gruppe  = $Case.Group
        Case    = $Case.Name
        KaldMs  = $first
        VarmMs  = $median
        Rader   = $rows
        Status  = $status
    }
}

Write-Host "`nKjoerer $($cases.Count) case x $Runs kjoeringer...`n" -ForegroundColor Cyan

$results = [System.Collections.Generic.List[object]]::new()
foreach ($case in $cases) {
    $results.Add((Measure-Case -Case $case))
}

# ---------------------------------------------------------------------------
# Oppsummering
# ---------------------------------------------------------------------------

Write-Host "`n$('=' * 78)"

# Kun vellykkede kall er målinger. En feilet case har en "tid" som egentlig er
# tilkoblingstimeout, og skal ikke rangeres sammen med ekte resultater.
$ok = @($results | Where-Object Status -eq 'OK')

Write-Host 'Tregeste case (varm tid):' -ForegroundColor Green
$ok | Sort-Object VarmMs -Descending | Select-Object -First 15 |
    Format-Table @{ N = 'VarmMs'; E = { $_.VarmMs }; A = 'right' },
                 @{ N = 'KaldMs'; E = { $_.KaldMs }; A = 'right' },
                 Gruppe, Case, Rader -AutoSize

Write-Host 'Gjennomsnitt per gruppe:' -ForegroundColor Green
$ok | Group-Object Gruppe | ForEach-Object {
    [PSCustomObject]@{
        Gruppe  = $_.Name
        Antall  = $_.Count
        SnittMs = [int](($_.Group | Measure-Object VarmMs -Average).Average)
        MaksMs  = ($_.Group | Measure-Object VarmMs -Maximum).Maximum
    }
} | Sort-Object SnittMs -Descending | Format-Table -AutoSize

$failed = $results | Where-Object Status -ne 'OK'
if ($failed) {
    Write-Host 'Feilede case:' -ForegroundColor Red
    $failed | Format-Table Gruppe, Case, Status -AutoSize
}

if ($ok.Count -eq 0) {
    Write-Host 'Ingen vellykkede maalinger - resultatene over er ikke brukbare.' -ForegroundColor Red
}
else {
    $slow = @($ok | Where-Object { $_.VarmMs -ge $SlowThresholdMs })
    if ($slow.Count -gt 0) {
        Write-Host "$($slow.Count) av $($ok.Count) case over $SlowThresholdMs ms." -ForegroundColor Yellow
    }
    else {
        Write-Host "Alle $($ok.Count) case under $SlowThresholdMs ms." -ForegroundColor Green
    }
}

Write-Host "`nKjoer naa andre halvdel av Scripts/IndexUsageDiff.sql for aa se hvilke indekser som ble brukt." -ForegroundColor Cyan
