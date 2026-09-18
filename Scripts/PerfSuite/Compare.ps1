#Requires -Version 7.0
<#
.SYNOPSIS
    Sammenligner to resultatfiler fra ytelsessuiten og viser regresjoner.

.DESCRIPTION
    Gjoer ingen kall mot API-et og laster ikke databasen - den leser bare to
    CSV-filer. Trygg aa kjoere naar som helst.

    Terskelen er BAADE relativ og absolutt. Bare relativ ville flommet over av
    stoey fra korte case (40 ms -> 90 ms er +125 %, men uinteressant); bare
    absolutt ville skjult at et 300 ms-kall ble tredoblet.

.EXAMPLE
    ./Scripts/PerfSuite/Compare.ps1 -Baseline results/20260901-101500-abc1234-Full.csv `
                                    -Current  results/20260909-084500-bff02c7-Full.csv

.EXAMPLE
    # Uten argumenter: de to nyeste filene i results/
    ./Scripts/PerfSuite/Compare.ps1
#>

[CmdletBinding()]
param(
    [string] $Baseline,
    [string] $Current,

    # Begge terskler maa passeres foer noe rapporteres som regresjon.
    [int]    $MinPercent = 25,
    [int]    $MinMs      = 200
)

$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'PerfLib.psm1') -Force

$resultsDir = Join-Path $PSScriptRoot 'results'

if (-not $Baseline -or -not $Current) {
    $filer = @(Get-ChildItem $resultsDir -Filter '*.csv' -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending)

    if ($filer.Count -lt 2) {
        Write-Host 'Trenger minst to resultatfiler i results/ for aa sammenligne automatisk.' -ForegroundColor Red
        Write-Host 'Angi -Baseline og -Current eksplisitt, eller kjoer suiten en gang til.' -ForegroundColor Red
        exit 1
    }

    if (-not $Current)  { $Current  = $filer[0].FullName }
    if (-not $Baseline) { $Baseline = $filer[1].FullName }

    Write-Host "Baseline: $(Split-Path -Leaf $Baseline)" -ForegroundColor DarkGray
    Write-Host "Naa:      $(Split-Path -Leaf $Current)"  -ForegroundColor DarkGray
}

Compare-PerfRuns -BaselinePath $Baseline -CurrentPath $Current `
    -MinPercent $MinPercent -MinMs $MinMs
