#Requires -Version 7

<#
.SYNOPSIS
    Verifies that every table row in the repository's Markdown renders as a table row.

.DESCRIPTION
    A GitHub Flavored Markdown table is a header row, a delimiter row, and the rows that
    follow without interruption. A blank line ends the table. Any row after that blank line
    is not a row any more: it renders as a paragraph of literal pipe characters.

    Nothing about that is visible in a diff, in a plain text editor, or in a commit that only
    adds rows. The divergence ledger in CONTRIBUTING.md lost three rows this way, and they
    were the rows carrying the retirement conditions for the upstream pull requests this fork
    tracks. They stayed unreadable until the document was rendered and the rendered rows were
    counted against the source rows.

    This script does that counting without a renderer. It groups contiguous lines beginning
    with a pipe and reports every group whose second line is not a delimiter row, because such
    a group never becomes a table. Fenced code blocks are skipped, so pipes inside an example
    are not mistaken for a table.

    The rule was measured against the GitHub Markdown API rather than assumed. A table may
    begin directly beneath a paragraph line, so adjacency to text is not what breaks a table;
    only the blank line is.

.PARAMETER Path
    The directory to search. Defaults to the repository root.

.EXAMPLE
    pwsh build/verify-markdown-tables.ps1

.EXAMPLE
    pwsh build/verify-markdown-tables.ps1 -Path docs
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

    # Collect the runs first, classify them after. Keeping the two apart avoids sharing
    # mutable state with a nested scope, which silently drops the count.
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
            # The delimiter row is structure rather than content, so it is the one dropped.
            # What remains, the header plus the data rows, is the number of rows a renderer
            # emits for this table, which is the number this count was validated against.
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

# The set to check is what the repository publishes, so it comes from the index rather than
# from the disk. A walk of the disk would also read build output and whatever untracked
# Markdown a working tree happens to hold, and would fail on files nobody is publishing.
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
