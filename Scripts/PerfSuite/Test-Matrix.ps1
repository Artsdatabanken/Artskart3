#Requires -Version 7.0
<#
.SYNOPSIS
    Selvtest av matrisegenereringen. Trygg aa kjoere - roerer verken API eller database.

.DESCRIPTION
    Suiten er verdiloes hvis matrisen er feil. Denne testen bygger syntetiske
    fixtures som ligner det Lookup-endepunktene ville gitt, og kontrollerer at
    casene blir riktige.

    Den finnes fordi to reelle feil slapp gjennom under utviklingen, og begge
    ville gitt maalinger som saa fine ut:

      1. '-is [PSCustomObject]' er sant for tilnaermet alt i PowerShell. Tallet 5
         ble til en tom hashtable og strengen '0301' til @{ Length = 4 }.
         Filterkroppen saa riktig ut i PowerShell, men ble serialisert til
         {"categoryIds":{}} - og API-et filtrerte paa ingenting. Vi ville maalt
         ufiltrerte spoerringer og trodd kombinasjonene var raske.

      2. En funksjon som returnerer @(11) pakker ut lista til skalaren 11.
         "taxonIds": 11 binder ikke til int[] i System.Text.Json.

    Kjoer denne etter endringer i Fixtures.ps1 eller Cases.ps1.

.EXAMPLE
    ./Scripts/PerfSuite/Test-Matrix.ps1
#>

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'PerfLib.psm1') -Force
. (Join-Path $PSScriptRoot 'Fixtures.ps1')
. (Join-Path $PSScriptRoot 'Cases.ps1')

$feilTotalt = 0

function Assert-Perf {
    param([string] $Navn, [bool] $Betingelse, [string] $Detalj)

    if ($Betingelse) {
        Write-Host ("  {0,-28} OK" -f $Navn) -ForegroundColor DarkGray
    }
    else {
        Write-Host ("  {0,-28} FEIL {1}" -f $Navn, $Detalj) -ForegroundColor Red
        $script:feilTotalt++
    }
}

# ---------------------------------------------------------------------------
# Syntetiske fixtures
# ---------------------------------------------------------------------------

function New-FakeDimension {
    param([string] $Name, [string] $Field, [switch] $AsString)

    $mk = {
        param($v)
        if ($AsString) { @{ $Field = @("$v") } } else { @{ $Field = @([int]$v) } }
    }.GetNewClosure()

    New-PerfDimension -Name $Name -Tiers @(
        New-PerfTier -Key "$Name`:tung"  -Role 'tung'  -Label "$Name tung"  -Filter (& $mk 1)
        New-PerfTier -Key "$Name`:lett"  -Role 'lett'  -Label "$Name lett"  -Filter (& $mk 2)
        New-PerfTier -Key "$Name`:annen" -Role 'annen' -Label "$Name annen" -Filter (& $mk 3)
    )
}

$dims = @(
    (New-PerfDimension -Name 'takson' -Tiers @(
        New-PerfTier -Key 'takson:Order'    -Role 'tung'  -Label 'Passeriformes' -Filter @{ taxonIds = @(11) }
        New-PerfTier -Key 'takson:Species'  -Role 'annen' -Label 'Larus'         -Filter @{ taxonIds = @(22) }
        New-PerfTier -Key 'takson:art-lett' -Role 'lett'  -Label 'Sjelden art'   -Filter @{ taxonIds = @(99) }
    )),
    (New-FakeDimension 'taksongruppe' 'taxonGroupIds'),
    (New-FakeDimension 'kategori'     'categoryIds'),
    (New-FakeDimension 'institusjon'  'organizationIds'),
    (New-FakeDimension 'funntype'     'basisOfRecordIds'),
    (New-FakeDimension 'atferd'       'behaviorIds'),
    (New-FakeDimension 'fylke'        'countyIds'         -AsString),
    (New-FakeDimension 'verneomraade' 'restrictedAreaIds' -AsString),
    (New-FakeDimension 'havomraade'   'oceanAreaIds'      -AsString),
    (New-FakeDimension 'datasett'     'datasetOrgId'),
    (New-FakeDimension 'prosjekt'     'projectOrgId'),
    (New-PerfDimension -Name 'kommune' -Tiers @(
        New-PerfTier -Key 'kommune:tung'  -Role 'tung'  -Label 'Oslo'        -Filter @{ municipalityIds = @('0301') }
        New-PerfTier -Key 'kommune:lett'  -Role 'lett'  -Label 'Utsira'      -Filter @{ municipalityIds = @('1151') }
        New-PerfTier -Key 'kommune:mange' -Role 'annen' -Label '15 tyngste'  -Filter @{ municipalityIds = @('0301', '1103', '5001') }
    )),
    (New-PerfDimension -Name 'katalognr' -Tiers @(
        New-PerfTier -Key 'katalognr:tung' -Role 'tung' -Label '12345' -Filter @{ observationIds = @(1, 2, 3) }
    )),
    (New-PerfDimension -Name 'regstatus' -Tiers @(
        New-PerfTier -Key 'regstatus:tung' -Role 'tung' -Label 'paavist'      -Filter @{ registrationStatusId = 1 }
        New-PerfTier -Key 'regstatus:lett' -Role 'lett' -Label 'ikke paavist' -Filter @{ registrationStatusId = 2 }
    )),
    (New-PerfDimension -Name 'bilder' -Tiers @(
        New-PerfTier -Key 'bilder:lett' -Role 'lett' -Label 'med bilder'  -Filter @{ withImages = $true }
        New-PerfTier -Key 'bilder:tung' -Role 'tung' -Label 'uten bilder' -Filter @{ withImages = $false }
    )),
    (New-PerfDimension -Name 'periode' -Tiers @(
        New-PerfTier -Key 'periode:tung' -Role 'tung' -Label '1970-2026' -Filter @{ period = @{ from = 1970; to = 2026 } }
        New-PerfTier -Key 'periode:lett' -Role 'lett' -Label '2023-2024' -Filter @{ period = @{ from = 2023; to = 2024 } }
    )),
    (New-PerfDimension -Name 'koordpresisjon' -Tiers @(
        New-PerfTier -Key 'koordpresisjon:tung' -Role 'tung' -Label 'vid'    -Filter @{ coordinatePrecision = @{ from = 0; to = 10000 } }
        New-PerfTier -Key 'koordpresisjon:lett' -Role 'lett' -Label 'presis' -Filter @{ coordinatePrecision = @{ from = 0; to = 10 } }
    ))
)

# Rundtur via JSON — det er slik fixtures faktisk leses inn i en ekte kjoering,
# og det er der de to feilene over oppsto.
$fixtures = [PSCustomObject]@{
    createdUtc = 'test'; baseUrl = 'https://localhost:5088'
    dimensions = $dims;  envelopes = $script:PerfEnvelopes
} | ConvertTo-Json -Depth 12 | ConvertFrom-Json

# ---------------------------------------------------------------------------
# Stoerrelse
# ---------------------------------------------------------------------------

Write-Host "`nMatrisestoerrelse ($($fixtures.dimensions.Count) dimensjoner):" -ForegroundColor Cyan

# Maalingene kan ikke lenger regnes ut som case x endepunkt: hvert endepunkt kan
# snevre inn hvilke case det gjelder for. Vi teller det samme veien Run.ps1 gjoer.
function Measure-PerfMatrixSize {
    param($Cases, $Endpoints)
    $filterCases = @($Cases | Where-Object Layer -ne 'oppslag')
    $n = @($Cases | Where-Object Layer -eq 'oppslag').Count
    foreach ($ep in $Endpoints) {
        $n += @($filterCases | Where-Object { Test-PerfEndpointRunsCase -Endpoint $ep -Case $_ }).Count
    }
    return $n
}

foreach ($level in @('Quick', 'Standard', 'Full')) {
    $c   = @(New-PerfCaseMatrix -Fixtures $fixtures -Level $level)
    $eps = @(New-PerfEndpointMatrix -Level $level -Envelopes $fixtures.envelopes)

    Write-Host ("  {0,-9} {1,4} case x {2,2} endepunkt = {3,5} maalinger  ({4})" -f `
        $level, $c.Count, $eps.Count, (Measure-PerfMatrixSize -Cases $c -Endpoints $eps),
        ((@($c | Group-Object Layer | Sort-Object Name | ForEach-Object { "$($_.Name) $($_.Count)" }) -join ', ')))
}

# Per endepunkt paa Full — det er her et feilstavet utsnittsnavn eller en
# CaseKeys-liste som ikke treffer noe ville vist seg som en tom kolonne.
$fullCases = @(New-PerfCaseMatrix -Fixtures $fixtures -Level 'Full')
$fullEps   = @(New-PerfEndpointMatrix -Level 'Full' -Envelopes $fixtures.envelopes)
$fullFilter = @($fullCases | Where-Object Layer -ne 'oppslag')
Write-Host "`nMaalinger per endepunkt (Full):" -ForegroundColor Cyan
foreach ($ep in $fullEps) {
    $n = @($fullFilter | Where-Object { Test-PerfEndpointRunsCase -Endpoint $ep -Case $_ }).Count
    Write-Host ("  {0,-28} {1,5}" -f "$($ep.Endpoint)/$($ep.Variant)", $n)
}

# ---------------------------------------------------------------------------
# Kontroller
# ---------------------------------------------------------------------------

Write-Host "`nMatrise:" -ForegroundColor Cyan

$cases = @(New-PerfCaseMatrix -Fixtures $fixtures -Level 'Full')

$dupes = @($cases | Group-Object CaseKey | Where-Object Count -gt 1)
Assert-Perf 'Unike case-noekler' ($dupes.Count -eq 0) "$($dupes.Count) duplikater"

# Determinisme er ikke kosmetikk: uten den kan ikke kjoering N sammenlignes med N-1.
$igjen = @(New-PerfCaseMatrix -Fixtures $fixtures -Level 'Full')
Assert-Perf 'Deterministisk' ((@($cases.CaseKey) -join '|') -eq (@($igjen.CaseKey) -join '|')) 'matrisen varierer'

$annen = @(New-PerfCaseMatrix -Fixtures $fixtures -Level 'Full' -Seed 12345)
$t1 = @($cases | Where-Object Layer -eq 'trippel').CaseKey -join '|'
$t2 = @($annen | Where-Object Layer -eq 'trippel').CaseKey -join '|'
Assert-Perf 'Seed paavirker tripler' ($t1 -ne $t2) 'utvalget endret seg ikke'

$dekket = @($cases | Where-Object Layer -eq 'enkelt' |
    ForEach-Object { $_.Dimensions } | Where-Object { $_ } | Sort-Object -Unique)
Assert-Perf 'Alle dimensjoner dekket' ($dekket.Count -eq $fixtures.dimensions.Count) `
    "$($dekket.Count) av $($fixtures.dimensions.Count)"

$reiser = @($cases | Where-Object Layer -eq 'reise')
Assert-Perf 'Brukerreiser bygget' ($reiser.Count -eq 8) "$($reiser.Count) av 8"

# ---------------------------------------------------------------------------
# Filterkropper
# ---------------------------------------------------------------------------

Write-Host "`nFilterkropper:" -ForegroundColor Cyan

$filterCases = @($cases | Where-Object Layer -ne 'oppslag')

$feil = @($filterCases | Where-Object {
    $ant = if ($_.Dimensions) { @($_.Dimensions -split ',').Count } else { 0 }
    $null -eq $_.Filter -or $_.Filter.Keys.Count -ne $ant
})
Assert-Perf 'Ett felt per dimensjon' ($feil.Count -eq 0) "$($feil.Count) case"

$feil = @($filterCases | Where-Object { $_.Layer -ne 'reise' } | Where-Object {
    $d = @($_.CaseKey -split '\+' | ForEach-Object { ($_ -split ':')[0] })
    @($d | Sort-Object -Unique).Count -ne $d.Count
})
Assert-Perf 'Ingen dobbel dimensjon' ($feil.Count -eq 0) "$($feil.Count) case"

# Fanget feil 1: tall og strenger som ble til objekter.
$feil = @($filterCases | Where-Object {
    $json = $_.Filter | ConvertTo-Json -Depth 8 -Compress
    $json -match '"Length"' -or $json -match ':\{\}' -or $json -match ':\[\]'
})
Assert-Perf 'JSON uten tomme objekter' ($feil.Count -eq 0) `
    "$($feil.Count) case, f.eks. $(@($feil)[0].CaseKey)"

# Fanget feil 2: ett-elements lister som ble skalarer.
$arrayFelt = @('taxonIds', 'taxonGroupIds', 'categoryIds', 'organizationIds', 'municipalityIds',
               'countyIds', 'restrictedAreaIds', 'oceanAreaIds', 'behaviorIds', 'basisOfRecordIds',
               'observationIds')
$feil = 0
foreach ($c in $filterCases) {
    foreach ($felt in $arrayFelt) {
        if (-not $c.Filter.ContainsKey($felt)) { continue }
        $json = @{ $felt = $c.Filter[$felt] } | ConvertTo-Json -Depth 8 -Compress
        if ($json -notmatch "`"$felt`":\[") { $feil++ }
    }
}
Assert-Perf 'Arrayfelt er arrayer' ($feil -eq 0) "$feil felt"

# Endepunktets pageNumber/envelope skal ikke bli liggende igjen i casens filter
# og lekke videre til neste endepunkt.
$eps  = @(New-PerfEndpointMatrix -Level 'Full' -Envelopes $fixtures.envelopes)
$case = @($cases | Where-Object Layer -eq 'par')[0]
$foer = $case.Filter | ConvertTo-Json -Compress -Depth 6
foreach ($ep in $eps) {
    $body = @{}
    foreach ($k in $case.Filter.Keys) { $body[$k] = $case.Filter[$k] }
    foreach ($k in $ep.Extra.Keys)    { $body[$k] = $ep.Extra[$k] }
}
Assert-Perf 'Ingen lekkasje i kroppen' (($case.Filter | ConvertTo-Json -Compress -Depth 6) -eq $foer) 'filteret ble mutert'

# ---------------------------------------------------------------------------
# Lokasjonsutsnittene
# ---------------------------------------------------------------------------

Write-Host "`nLokasjonsutsnitt:" -ForegroundColor Cyan

$filterCases = @($cases | Where-Object Layer -ne 'oppslag')

# Et endepunkt uten case maaler ingenting. Det skjer stille hvis et utsnittsnavn
# er feilstavet (Write-Warning i matrisen) eller CaseKeys peker paa case som ikke
# finnes — begge ville bare gitt faerre tall i rapporten, ikke en feilmelding.
$tomme = @()
foreach ($ep in $eps) {
    $n = @($filterCases | Where-Object { Test-PerfEndpointRunsCase -Endpoint $ep -Case $_ }).Count
    if ($n -eq 0) { $tomme += "$($ep.Endpoint)/$($ep.Variant)" }
}
Assert-Perf 'Alle endepunkter maaler noe' ($tomme.Count -eq 0) ($tomme -join ', ')

# Frontend henter lokasjoner foerst fra zoom 11, og det stoerste utsnittet
# endepunktet da faar er 185 x 103 km. Maaler vi over det, maaler vi en kallform
# som ikke finnes — det var nettopp feilen med de gamle norge- og trondelag-
# utsnittene. Litt slakk for at utsnittene er hentet fra kartet for haand.
$maksBredde = 190000
$maksHoyde  = 110000
$forStore = @()
foreach ($ep in @($eps | Where-Object { $_.Extra.envelope })) {
    $e = $ep.Extra.envelope
    $b = $e.maxX - $e.minX
    $h = $e.maxY - $e.minY
    if ($b -gt $maksBredde -or $h -gt $maksHoyde) {
        $forStore += "$($ep.Endpoint)/$($ep.Variant) ($([int]($b/1000)) x $([int]($h/1000)) km)"
    }
}
Assert-Perf 'Ingen utsnitt over zoom 11-taket' ($forStore.Count -eq 0) ($forStore -join ', ')

# LocationPolygons skal kjoere paa noeyaktig samme utsnitt som Locations. Ellers
# kan ikke en regresjon plasseres: ligger den i lokasjonssoeket eller i
# polygonhentingen?
$locV  = @($eps | Where-Object Endpoint -eq 'Locations'        | ForEach-Object Variant | Sort-Object)
$polyV = @($eps | Where-Object Endpoint -eq 'LocationPolygons' | ForEach-Object Variant | Sort-Object)
Assert-Perf 'LocationPolygons foelger Locations' (($locV -join ',') -eq ($polyV -join ',')) `
    "Locations: $($locV -join ','); Polygons: $($polyV -join ',')"

# Samme gating paa begge — ellers maaler de to endepunktene ulike case og kan
# ikke sammenlignes case for case.
$ulik = @()
foreach ($v in $locV) {
    $a = @($eps | Where-Object { $_.Endpoint -eq 'Locations'        -and $_.Variant -eq $v })[0]
    $b = @($eps | Where-Object { $_.Endpoint -eq 'LocationPolygons' -and $_.Variant -eq $v })[0]
    if ((($a.Layers -join ',') -ne ($b.Layers -join ',')) -or
        (($a.CaseKeys -join ',') -ne ($b.CaseKeys -join ','))) { $ulik += $v }
}
Assert-Perf 'Samme gating paa begge lokasjonsendepunkter' ($ulik.Count -eq 0) ($ulik -join ', ')

# CaseKeys-listene maa treffe ekte case. En skrivefeil her ville bare gitt
# faerre maalinger, i stillhet.
$alleKeys = @($cases.CaseKey)
$ukjente  = @()
foreach ($ep in @($eps | Where-Object { $_.CaseKeys })) {
    foreach ($k in $ep.CaseKeys) { if ($alleKeys -notcontains $k) { $ukjente += $k } }
}
$ukjente = @($ukjente | Sort-Object -Unique)
Assert-Perf 'CaseKeys peker paa ekte case' ($ukjente.Count -eq 0) ($ukjente -join ', ')

# ---------------------------------------------------------------------------
# Oppdagelseshjelperne
# ---------------------------------------------------------------------------

Write-Host "`nFixture-hjelpere:" -ForegroundColor Cyan

$institusjoner = @(
    [PSCustomObject]@{ id = 1; name = 'Stor';   observationCount = 9000000 }
    [PSCustomObject]@{ id = 2; name = 'Mellom'; observationCount = 500000  }
    [PSCustomObject]@{ id = 3; name = 'Liten';  observationCount = 12      }
    [PSCustomObject]@{ id = 4; name = 'Tom';    observationCount = 0       }
)
$spread = Get-PerfSelectivitySpread -Items $institusjoner -IdProperty 'id' -Label 'institusjon'

Assert-Perf 'Tung/median/lett' `
    ($spread.Heaviest.Name -eq 'Stor' -and $spread.Lightest.Name -eq 'Liten') `
    "fikk $($spread.Heaviest.Name)/$($spread.Lightest.Name)"

# Et element uten observasjoner maaler bare hvor lang tid det tar aa lete forgjeves.
Assert-Perf 'Tomme elementer utelatt' ($spread.All.name -notcontains 'Tom')

# Fella her: uten .GetNewClosure() bruker alle dimensjonene feltet til den siste
# i loekka, og halve matrisen ville filtrert paa feil kolonne.
$laget = foreach ($l in @(
    @{ Name = 'institusjon'; Field = 'organizationIds' },
    @{ Name = 'kategori';    Field = 'categoryIds'     })) {
    $field = $l.Field
    Get-PerfSpreadTiers -Name $l.Name -Spread $spread -FilterFactory {
        param($id) @{ $field = @([int]$id) }
    }.GetNewClosure()
}
$feltNavn = @($laget | ForEach-Object { @((ConvertTo-PerfHashtable $_.tiers[0].filter).Keys)[0] })
Assert-Perf 'Closure fanger filterfelt' ($feltNavn[0] -eq 'organizationIds' -and $feltNavn[1] -eq 'categoryIds') `
    ($feltNavn -join ' og ')

# Med faa elementer er tung, median og lett samme rad. Da skal vi ikke maale
# samme spoerring tre ganger under tre navn.
$field = 'behaviorIds'
$ett = Get-PerfSpreadTiers -Name 'atferd' `
    -Spread (Get-PerfSelectivitySpread -Items @([PSCustomObject]@{ id = 1; name = 'Eneste'; observationCount = 100 }) -IdProperty 'id' -Label 'atferd') `
    -FilterFactory { param($id) @{ $field = @([int]$id) } }.GetNewClosure()
Assert-Perf 'Ett element gir ett nivaa' ($ett.tiers.Count -eq 1) "$($ett.tiers.Count) nivaaer"

# --- Flerverdi i samme dimensjon -------------------------------------------
# Ti dimensjoner tar en array, men foer 11.09.2026 hadde bare fire av dem
# kombinasjoner - og alltid «de N tyngste». Kombinasjonen tung+lett var udekket,
# og det var den som viste seg treg i produksjon.
$mange = @(1..20 | ForEach-Object {
    [PSCustomObject]@{ id = $_; name = "Org $_"; observationCount = [int](1000000 / $_) }
})
$field = 'organizationIds'
$md = Get-PerfSpreadTiers -Name 'institusjon' `
    -Spread (Get-PerfSelectivitySpread -Items $mange -IdProperty 'id' -Label 'institusjon') `
    -FilterFactory      { param($id)   @{ $field = @([int]$id) } }.GetNewClosure() `
    -MultiFilterFactory { param($ider) @{ $field = @($ider | ForEach-Object { [int]$_ }) } }.GetNewClosure()

$komb = @($md.tiers | Where-Object { $_.key -match ':x\d' })
Assert-Perf 'Flerverdi-nivaaer bygget' ($komb.Count -eq 4) "$($komb.Count) av 4"

$tungLett = @($md.tiers | Where-Object { $_.key -eq 'institusjon:x2-tung-lett' })[0]
$verdier  = @((ConvertTo-PerfHashtable $tungLett.filter).$field)
Assert-Perf 'tung+lett har to ulike' ($verdier.Count -eq 2 -and $verdier[0] -ne $verdier[1]) `
    ($verdier -join ',')

# Uten komma-operatoren blir en ett-elements liste til en skalar, og API-et faar
# "organizationIds": 5 i stedet for [5].
$feil = 0
foreach ($t in $komb) {
    $json = @{ $field = (ConvertTo-PerfHashtable $t.filter).$field } | ConvertTo-Json -Depth 6 -Compress
    if ($json -notmatch "`"$field`":\[") { $feil++ }
}
Assert-Perf 'Flerverdi er arrayer' ($feil -eq 0) "$feil nivaaer"

# Faa elementer: tung, median og lett faller sammen, og «x2» ville blitt samme
# verdi to ganger - et enkeltverdifilter under et navn som lover noe annet.
$to = @(
    [PSCustomObject]@{ id = 1; name = 'A'; observationCount = 100 }
    [PSCustomObject]@{ id = 2; name = 'B'; observationCount = 10 })
$td = Get-PerfSpreadTiers -Name 'liten' `
    -Spread (Get-PerfSelectivitySpread -Items $to -IdProperty 'id' -Label 'liten') `
    -FilterFactory      { param($id)   @{ $field = @([int]$id) } }.GetNewClosure() `
    -MultiFilterFactory { param($ider) @{ $field = @($ider | ForEach-Object { [int]$_ }) } }.GetNewClosure()
$duplikat = @($td.tiers | Where-Object { $_.key -match ':x\d' } | Where-Object {
    $v = @((ConvertTo-PerfHashtable $_.filter).$field)
    @($v | Select-Object -Unique).Count -lt $v.Count })
Assert-Perf 'Ingen duplikat i kombinasjon' ($duplikat.Count -eq 0) "$($duplikat.Count) nivaaer"

# Omraader identifiseres med 'fid', ikke 'id' - og rad-objektene fra Lookup har
# BEGGE (Area.Id og Area.Fid). Leser flerverdi-koden $_.Id paa raa-objektet, faar
# den primaernoekkelen i stedet for Fid, og filteret peker paa en verdi som ikke
# finnes. Det skjedde: {"municipalityIds":["807"]} der det skulle staa ["4206"].
# Enkeltverdi-nivaaene var riktige, saa bare kombinasjonene maalte ingenting.
$omr = @(
    [PSCustomObject]@{ id = 807;  fid = '4206'; name = 'Farsund';  observationCount = 2169747 }
    [PSCustomObject]@{ id = 113;  fid = '5029'; name = 'Skaun';    observationCount = 82842 }
    [PSCustomObject]@{ id = 4180; fid = '5526'; name = 'Soerreisa'; observationCount = 6782 })
$ofeltet = 'municipalityIds'
$od = Get-PerfSpreadTiers -Name 'kommune' `
    -Spread (Get-PerfSelectivitySpread -Items $omr -IdProperty 'fid' -Label 'kommune') `
    -FilterFactory      { param($id)   @{ $ofeltet = @("$id") } }.GetNewClosure() `
    -MultiFilterFactory { param($ider) @{ $ofeltet = @($ider | ForEach-Object { "$_" }) } }.GetNewClosure()

$gyldigeFid = @($omr.fid)
$feilId = @($od.tiers | Where-Object { $_.key -match ':x\d' } | Where-Object {
    $v = @((ConvertTo-PerfHashtable $_.filter).$ofeltet)
    @($v | Where-Object { $_ -notin $gyldigeFid }).Count -gt 0 })
Assert-Perf 'Omraader bruker Fid, ikke Id' ($feilId.Count -eq 0) `
    $(if ($feilId.Count) { (ConvertTo-PerfHashtable $feilId[0].filter).$ofeltet -join ',' } else { '' })

# Etiketten skal ha radantallet med. Tom parentes betyr at koden leste en
# egenskap som ikke finnes paa objektet.
$utenAntall = @($od.tiers | Where-Object { $_.key -match ':x\d' -and $_.label -match '\(\)' })
Assert-Perf 'Etiketter har radantall' ($utenAntall.Count -eq 0) `
    $(if ($utenAntall.Count) { $utenAntall[0].label } else { '' })

# Verneomraader kan mangle observationCount. Da faller vi tilbake til posisjon,
# men nivaaene maa fortsatt vaere de samme mellom kjoeringer.
$verne = @(
    [PSCustomObject]@{ fid = 'VA3'; name = 'Verne C' }
    [PSCustomObject]@{ fid = 'VA1'; name = 'Verne A' }
    [PSCustomObject]@{ fid = 'VA2'; name = 'Verne B' }
)
# 3>$null: advarselen er forventet her. $WarningPreference i skriptet hjelper
# ikke — Write-Warning inne i en modul leser modulens egen preference, ikke
# kallerens, saa stroemmen maa omdirigeres paa kallstedet.
$v1 = Get-PerfSelectivitySpread -Items $verne -IdProperty 'fid' -Label 'verneomraade' 3>$null
$v2 = Get-PerfSelectivitySpread -Items ($verne | Sort-Object name -Descending) -IdProperty 'fid' -Label 'verneomraade' 3>$null
Assert-Perf 'Stabilt uten telling' ($v1.Heaviest.Id -eq $v2.Heaviest.Id) 'rekkefoelgen varierer'

# ---------------------------------------------------------------------------
# Sammenligning
# ---------------------------------------------------------------------------

Write-Host "`nSammenligning:" -ForegroundColor Cyan

$tmpB = Join-Path ([System.IO.Path]::GetTempPath()) 'perf-test-baseline.csv'
$tmpC = Join-Path ([System.IO.Path]::GetTempPath()) 'perf-test-current.csv'

$skriv = {
    param($sti, $a, $b, $c)
    @(
        [PSCustomObject]@{ RunId='r'; Timestamp='t'; GitSha='s'; Level='Full'; Layer='par'; CaseKey='a+b'; Dimensions='a,b'; Endpoint='Observation'; Variant='side1'; ColdMs=$a; WarmMs=$a; Rows=10; Status='OK'; Label='' }
        [PSCustomObject]@{ RunId='r'; Timestamp='t'; GitSha='s'; Level='Full'; Layer='par'; CaseKey='c+d'; Dimensions='c,d'; Endpoint='Observation'; Variant='side1'; ColdMs=$b; WarmMs=$b; Rows=10; Status='OK'; Label='' }
        [PSCustomObject]@{ RunId='r'; Timestamp='t'; GitSha='s'; Level='Full'; Layer='par'; CaseKey='e+f'; Dimensions='e,f'; Endpoint='Observation'; Variant='side1'; ColdMs=$c; WarmMs=$c; Rows=10; Status='OK'; Label='' }
    ) | Export-Csv -Path $sti -NoTypeInformation
}

# a: +50 ms, under absoluttterskelen. b: +2000 ms, ekte regresjon. c: uendret.
& $skriv $tmpB 100 1000 500
& $skriv $tmpC 150 3000 500

# 6>&1 fanger Write-Host-stroemmen, som er der rapporten faktisk skrives.
$linjer = (Compare-PerfRuns -BaselinePath $tmpB -CurrentPath $tmpC 6>&1 | Out-String)

Assert-Perf 'Fanger regresjon' ($linjer -match '1 REGRESJONER') 'fant den ikke'
Assert-Perf 'Rapporterer riktig case' ($linjer -match [regex]::Escape('c+d')) 'feil case i tabellen'
# Pluss er en kvantifikator i regex, saa noekkelen maa escapes for aa bli lest bokstavelig.
Assert-Perf 'Ignorerer smaa utslag' ($linjer -notmatch [regex]::Escape('a+b')) 'rapporterte 100->150 ms'

Remove-Item $tmpB, $tmpC -Force -ErrorAction SilentlyContinue

# ---------------------------------------------------------------------------

Write-Host ''
if ($feilTotalt -eq 0) {
    Write-Host 'Alle kontroller passerte.' -ForegroundColor Green
    exit 0
}
else {
    Write-Host "$feilTotalt kontroller feilet." -ForegroundColor Red
    exit 1
}
