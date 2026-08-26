#Requires -Version 7

<#
.SYNOPSIS
    Verifies that the repository's Markdown tables render with every row and every cell intact.

.DESCRIPTION
    A blank line ends a table, so rows after one render as literal pipe text. An unescaped pipe
    inside a row splits an extra cell, and the cells past the header's width are dropped.

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

# Splits a row on its unescaped pipes, dropping the ones that only delimit the row itself.
function Split-Row
{
    param([string] $Line)

    $cells = @($Line.Trim() -split '(?<!\\)\|')

    if ($cells.Count -gt 0 -and $cells[0] -eq '')
    {
        $cells = @($cells[1..($cells.Count - 1)])
    }

    if ($cells.Count -gt 0 -and $cells[-1] -eq '')
    {
        $cells = @($cells[0..($cells.Count - 2)])
    }

    return $cells
}

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
    $ragged = [System.Collections.Generic.List[object]]::new()

    foreach ($run in $groups)
    {
        if ($run.Count -ge 2 -and (Test-DelimiterRow $run[1].Text))
        {
            # The delimiter row is dropped; the header and data rows are what a renderer emits.
            $rows += $run.Count - 1

            $width = (Split-Row $run[0].Text).Count

            foreach ($row in $run[2..($run.Count - 1)])
            {
                $count = (Split-Row $row.Text).Count

                if ($count -ne $width)
                {
                    $ragged.Add([pscustomobject]@{ Number = $row.Number; Text = $row.Text; Width = $width; Count = $count })
                }
            }
        }
        else
        {
            $orphans.AddRange($run)
        }
    }

    return [pscustomobject]@{
        Rows    = $rows
        Orphans = $orphans.ToArray()
        Ragged  = $ragged.ToArray()
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

    foreach ($row in $report.Ragged)
    {
        $excerpt = $row.Text.Substring(0, [Math]::Min(70, $row.Text.Length))

        $failures.Add("${relativePath}:$($row.Number) splits into $($row.Count) cells under a header of $($row.Width): $excerpt")
    }

    if ($report.Rows -gt 0 -or $report.Orphans.Count -gt 0 -or $report.Ragged.Count -gt 0)
    {
        Write-Host ("{0,-52} {1,4} table rows, {2} orphaned, {3} ragged" -f $relativePath, $report.Rows, $report.Orphans.Count, $report.Ragged.Count)
    }
}

Write-Host ''

if ($failures.Count -eq 0)
{
    Write-Host "All $rowCount table rows across $($documents.Count) documents render whole."

    exit 0
}

Write-Host "$($failures.Count) row(s) do not render whole:"
Write-Host ''

foreach ($failure in $failures)
{
    Write-Host "  $failure"
}

Write-Host ''
Write-Host 'A blank line ends a table: remove it, or give the rows below it their own header.'
Write-Host 'A pipe inside a row splits a cell even within a code span: write it as \| instead.'

exit 1
