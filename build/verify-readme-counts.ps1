#Requires -Version 7

<#
.SYNOPSIS
    Verifies that the diagnostic counts the README states match the descriptors that ship.

.DESCRIPTION
    Several documents state how many diagnostics the analyzers report, including the two that
    ship inside the package. Nothing recomputed those numbers when a pull request added a
    descriptor, so they fell behind the sources they describe. This reads the identifiers out
    of the descriptor files and compares them against the numbers the documents state.

    Which files hold the descriptors, and which documents state a count, are read from the tree
    rather than taken from the list below, so a descriptor declared somewhere else or a document
    the list does not name is reported instead of leaving the number unverified. A document is
    read a paragraph at a time, a sentence being wrapped in some of them and a file holding
    unrelated mentions in others. The sentence patterns are the wordings the documents use for a
    count, so a wording none of them reach is a claim this cannot read and belongs there first.
    Both sets are read as text rather than parsed, so a comment writing out a declaration or a
    count is read as one.

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

# A count of diagnostics, in the wordings the documents use for one.
$countSentence = '\d+\s*(?:diagnostics|件の診断)|診断\s*\d+\s*種類'

# A descriptor declaration, with the type named on either side of the assignment.
$declaration = 'DiagnosticDescriptor\s+\w+\s*=\s*new\(|new\s+DiagnosticDescriptor\s*\('

$claims = @(
    @{
        Prefix      = 'CMPW'
        Mention     = 'CMPW(?!D2D)'
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
        Prefix      = 'CMPWD2D'
        Mention     = 'CMPWD2D'
        Descriptors = 'src/ComputeWeave.D2D1.SourceGenerators/Diagnostics/DiagnosticDescriptors.cs'
        Pattern     = '"(CMPWD2D[0-9]{4})"'
        Documents   = @(
            @{ File = 'README.md';    Pattern = 'report (\d+) diagnostics with the `CMPWD2D` prefix' }
            @{ File = 'README.ja.md'; Pattern = '`CMPWD2D` を接頭辞とする(\d+)件の診断を報告する' }
        )
    }
)

# The sets come from the index, so build output and untracked files stay out.
function Get-TrackedFiles
{
    param([string] $Pattern)

    $files = @(git -C $root ls-files $Pattern)

    if ($LASTEXITCODE -ne 0)
    {
        throw "Could not list tracked files under $root. This script needs a git checkout."
    }

    return $files
}

$failures = [System.Collections.Generic.List[string]]::new()

$counted = @($claims | ForEach-Object { $_.Descriptors })
$sources = Get-TrackedFiles -Pattern 'src/*.cs'
$documents = Get-TrackedFiles -Pattern '*.md'

# A scan that reads nothing agrees with everything, so an empty set is the failure it looks like.
if ($sources.Count -eq 0 -or $documents.Count -eq 0)
{
    $failures.Add("The scans read $($sources.Count) source files and $($documents.Count) documents, so they compared nothing.")
}

# A declaration outside the counted files leaves the documents stating a smaller number.
foreach ($file in $sources)
{
    if ($counted -contains $file)
    {
        continue
    }

    if ([System.IO.File]::ReadAllText((Join-Path $root $file)) -match $declaration)
    {
        $failures.Add("$file declares a descriptor and no claim counts it.")
    }
}

# A count in a document no claim reads is a number nothing compares against.
foreach ($file in $documents)
{
    # Paragraphs, so a wrapped sentence stays whole and unrelated mentions stay apart.
    $blocks = [System.IO.File]::ReadAllText((Join-Path $root $file)) -split '\r?\n\s*\r?\n'

    foreach ($claim in $claims)
    {
        if ($claim.Documents.File -contains $file)
        {
            continue
        }

        foreach ($block in $blocks)
        {
            if (($block -match $countSentence) -and ($block -match $claim.Mention))
            {
                $failures.Add("$file states a count for the ``$($claim.Prefix)`` prefix and no claim reads it.")

                break
            }
        }
    }
}

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
