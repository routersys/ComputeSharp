#Requires -Version 7

<#
.SYNOPSIS
    Verifies that the diagnostic counts the README states match the descriptors that ship.

.DESCRIPTION
    Several documents state how many diagnostics the analyzers report, including the two that
    ship inside the package. Nothing recomputed those numbers when a pull request added a
    descriptor, so they fell behind the sources they describe. This reads the identifiers out
    of the descriptor files and compares them against the numbers the documents state.

.PARAMETER Path
    The repository root. Defaults to the parent of this script.

.EXAMPLE
    pwsh build/verify-readme-counts.ps1
#>

[CmdletBinding()]
param(
    [string] $Path
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrEmpty($Path))
{
    $Path = Split-Path -Parent $PSScriptRoot
}

$root = (Resolve-Path -LiteralPath $Path).Path

# Counts the distinct diagnostic identifiers a descriptor file declares as string literals.
function Measure-DiagnosticIds
{
    param([string] $File, [string] $Pattern)

    $full = Join-Path $root $File

    if (-not (Test-Path -LiteralPath $full))
    {
        throw "The descriptor file $File is missing."
    }

    $text = Get-Content -LiteralPath $full -Raw

    $ids = [regex]::Matches($text, $Pattern) | ForEach-Object { $_.Groups[1].Value }

    return @($ids | Sort-Object -Unique).Count
}

$claims = @(
    @{
        Descriptors = 'src/ComputeWeave.SourceGenerators/Diagnostics/DiagnosticDescriptors.cs'
        Pattern     = '"(CMPW[0-9]{4})"'
        Documents   = @(
            @{ File = 'README.md';                           Pattern = 'report (\d+) diagnostics with the `CMPW` prefix' }
            @{ File = 'README.ja.md';                        Pattern = '接頭辞 `CMPW` の診断(\d+)種類として報告します' }
            @{ File = 'src/ComputeWeave/README.md';          Pattern = 'report (\d+) diagnostics with the `CMPW` prefix' }
            @{ File = 'src/ComputeWeave/README.ja.md';       Pattern = '接頭辞 `CMPW` の診断(\d+)種類として報告します' }
        )
    }
    @{
        Descriptors = 'src/ComputeWeave.D2D1.SourceGenerators/Diagnostics/DiagnosticDescriptors.cs'
        Pattern     = '"(CMPWD2D[0-9]{4})"'
        Documents   = @(
            @{ File = 'README.md';    Pattern = 'report (\d+) diagnostics with the `CMPWD2D` prefix' }
            @{ File = 'README.ja.md'; Pattern = '`CMPWD2D` を接頭辞とする(\d+)件の診断を報告する' }
        )
    }
)

$failures = [System.Collections.Generic.List[string]]::new()

foreach ($claim in $claims)
{
    $declared = Measure-DiagnosticIds -File $claim.Descriptors -Pattern $claim.Pattern

    foreach ($document in $claim.Documents)
    {
        $full = Join-Path $root $document.File

        if (-not (Test-Path -LiteralPath $full))
        {
            $failures.Add("$($document.File) is missing.")

            continue
        }

        $text = Get-Content -LiteralPath $full -Raw

        $matched = [regex]::Matches($text, $document.Pattern)

        if ($matched.Count -ne 1)
        {
            $failures.Add("$($document.File) states the count $($matched.Count) times; the check expects it once. Update the pattern in this script when the sentence is reworded.")

            continue
        }

        $stated = [int] $matched[0].Groups[1].Value

        if ($stated -ne $declared)
        {
            $failures.Add("$($document.File) states $stated where $($claim.Descriptors) declares $declared.")

            continue
        }

        Write-Host ("{0,-52} {1,4} declared, {2} stated" -f $document.File, $declared, $stated)
    }
}

Write-Host ''

if ($failures.Count -eq 0)
{
    Write-Host 'Every diagnostic count the READMEs state matches the descriptors that ship.'

    exit 0
}

Write-Host "$($failures.Count) count(s) do not match:"
Write-Host ''

foreach ($failure in $failures)
{
    Write-Host "  $failure"
}

Write-Host ''
Write-Host 'A pull request that adds or removes a descriptor has to carry the new number into every document that states it.'

exit 1
