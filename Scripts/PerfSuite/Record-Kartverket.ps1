#Requires -Version 7.0
<#
.SYNOPSIS
    Tar opp Kartverkets svar for matrikkelenhetene i Fixtures.ps1, til stubben i
    Kartverket.ps1.

.DESCRIPTION
    Kjoeres naar en eiendom legges til eller byttes ut i $PerfCadastralUnits. Roerer
    verken API-et eller databasen, men spoer Kartverkets aapne Eiendom API én gang
    per eiendom.

    Spoerringen er den samme som CadastralUnitService sender, saa opptaket er det
    API-et ville faatt fra den ekte tjenesten.

    Et nytt opptak kan gi en annen eiendom enn den gamle, hvis Matrikkelen er
    oppdatert i mellomtiden. Da er ikke matrikkelcasene lenger sammenlignbare med
    eldre kjoeringer.

.EXAMPLE
    ./Scripts/PerfSuite/Record-Kartverket.ps1
#>

[CmdletBinding()]
param(
    [string] $BaseUrl = 'https://api.kartverket.no/eiendom/v1/'
)

$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'PerfLib.psm1') -Force
. (Join-Path $PSScriptRoot 'Fixtures.ps1')
. (Join-Path $PSScriptRoot 'Kartverket.ps1')

$ider = @($script:PerfCadastralUnits.Values | ForEach-Object { $_.Ids } | Sort-Object -Unique)
$null = New-Item -ItemType Directory -Path $script:PerfKartverketDir -Force

Write-Host "Tar opp $($ider.Count) matrikkelenheter fra $BaseUrl" -ForegroundColor Cyan
foreach ($id in $ider) {
    $url = '{0}geokoding?matrikkelnummer={1}&omrade=true&utkoordsys=25833' -f $BaseUrl, [uri]::EscapeDataString($id)
    $fil = Get-PerfKartverketFile -Id $id

    Invoke-WebRequest -Uri $url -OutFile $fil

    $teiger = @((Get-Content $fil -Raw | ConvertFrom-Json).features).Count
    Write-Host ("  {0,-16} {1,3} teiger  {2,8:N0} byte" -f $id, $teiger, (Get-Item $fil).Length)
}
