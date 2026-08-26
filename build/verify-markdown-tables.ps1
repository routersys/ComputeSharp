#Requires -Version 7

<#
.SYNOPSIS
    Verifies that every table row in the repository's Markdown renders as a table row.

.DESCRIPTION
    A blank line ends a Markdown table, so rows placed after one render as literal pipe text.
    This groups contiguous pipe lines and reports any group whose second line is not a delimiter.

.PARAMETER Path
    The directory to search. Defaults to the repository root.

.EXAMPLE
    pwsh build/verify-markdown-tables.ps1
#>

[CmdletBinding()]
param(
    [string] $Path
)

$ErrorActionPreference = 'Stop'

# A delimiter row is one or more cells of dashes, each optionally anchored with a colon.
function Test-DelimiterRow
{
    param([string] $Line)

    $trimmed = $Line.Trim()

    if (-not $trimmed.StartsWith('|'))
    {
        return $false
    }

    $cells = @($trimmed.Trim('|') -split '\|')

    if ($cells.Count -eq 0)
    {
        return $false
    }

    foreach ($cell in $cells)
    {
        if ($cell.Trim() -notmatch '^:?-+:?$')
        {
            return $false
        }
    }

    return $true
}

# Splits a document into the runs of pipe lines it contains, and classifies each run.
function Get-TableReport
{
    param([string[]] $Lines)

    # Runs are collected first and classified after, so no count is shared with a nested scope.
    $groups = [System.Collections.Generic.List[object]]::new()
    $group = [System.Collections.Generic.List[object]]::new()
    $inFence = $false

    for ($index = 0; $index -lt $Lines.Length; $index++)
    {
        $trimmed = $Lines[$index].Trim()
        $isFence = $trimmed -match '^(```|~~~)'

        if ($isFence -or $inFence -or -not $trimmed.StartsWith('|'))
        {
            if ($group.Count -gt 0)
            {
                $groups.Add($group.ToArray())
                $group.Clear()
            }

            if ($isFence)
            {
                $inFence = -not $inFence
            }

            continue
        }

        $group.Add([pscustomobject]@{ Number = $index + 1; Text = $trimmed })
    }

    if ($group.Count -gt 0)
    {
        $groups.Add($group.ToArray())
    }

    $rows = 0
    $orphans = [System.Collections.Generic.List[object]]::new()

    foreach ($run in $groups)
    {
        if ($run.Count -ge 2 -and (Test-DelimiterRow $run[1].Text))
        {
            # The delimiter row is dropped; the header and data rows are what a renderer emits.
            $rows += $run.Count - 1
        }
        else
        {
            $orphans.AddRange($run)
        }
    }

    return [pscustomobject]@{
        Rows    = $rows
        Orphans = $orphans.ToArray()
    }
}

if (-not $Path)
{
    $Path = Split-Path $PSScriptRoot -Parent
}

if (-not (Test-Path $Path))
{
    throw "No directory at $Path."
}

$root = (Resolve-Path $Path).Path

# The set comes from the index, so build output and untracked Markdown stay out.
$tracked = @(git -C $root ls-files '*.md' '*.markdown')

if ($LASTEXITCODE -ne 0)
{
    throw "Could not list tracked files under $root. This script needs a git checkout."
}

$documents = @(
    $tracked |
        Sort-Object |
        ForEach-Object { Get-Item -LiteralPath (Join-Path $root $_) }
)

$failures = [System.Collections.Generic.List[string]]::new()
$rowCount = 0

foreach ($document in $documents)
{
    $relativePath = $document.FullName.Substring($root.Length).TrimStart('\', '/')
    $report = Get-TableReport ([System.IO.File]::ReadAllLines($document.FullName))

    $rowCount += $report.Rows

    foreach ($orphan in $report.Orphans)
    {
        $excerpt = $orphan.Text.Substring(0, [Math]::Min(70, $orphan.Text.Length))

        $failures.Add("${relativePath}:$($orphan.Number) does not render as a table row: $excerpt")
    }

    if ($report.Rows -gt 0 -or $report.Orphans.Count -gt 0)
    {
        Write-Host ("{0,-52} {1,4} table rows, {2} orphaned" -f $relativePath, $report.Rows, $report.Orphans.Count)
    }
}

Write-Host ''

if ($failures.Count -eq 0)
{
    Write-Host "All $rowCount table rows across $($documents.Count) documents render as table rows."

    exit 0
}

Write-Host "$($failures.Count) row(s) will not render as part of a table:"
Write-Host ''

foreach ($failure in $failures)
{
    Write-Host "  $failure"
}

Write-Host ''
Write-Host 'A blank line ends a Markdown table. Remove the blank line above these rows, or give'
Write-Host 'them their own header and delimiter row.'

exit 1
