#Requires -Version 7

<#
.SYNOPSIS
    Verifies that the analyzer release files record each rule's history once and in order.

.DESCRIPTION
    The release tracking analyzer refuses two entries for one rule inside a single release, and it
    refuses a descriptor whose severity moved without an entry. It says nothing about a rule listed
    as new in two different releases, which is what a release does when it moves a changed rule the
    way it moves an added one: the build stays green and the file then claims the rule was added
    twice. It also says nothing about a changed rule with no release that added it.

    Every line is accounted for rather than searched for a known shape, so a rule this reads past
    fails here instead of leaving the counts quietly short.

    A rule removed in one release and added again in another is reported all the same, no release
    here having removed one and no identifier being reused. Reading the removals instead would let
    a rule listed as added twice through whenever a removal sits anywhere in its history.

.PARAMETER Path
    The repository root. Defaults to the parent of this script.

.EXAMPLE
    pwsh build/verify-analyzer-releases.ps1
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

# Reads a release file as one record per rule and section, and as the lines it could not account for.
function Read-File
{
    param([string] $File)

    $release = if ((Split-Path -Leaf $File) -eq 'AnalyzerReleases.Unshipped.md') { 'unshipped' } else { $null }
    $section = $null
    $entries = [System.Collections.Generic.List[object]]::new()
    $unread = [System.Collections.Generic.List[object]]::new()
    $number = 0

    foreach ($line in (Get-Content -LiteralPath $File))
    {
        $number++
        $trimmed = $line.Trim()

        # A comment, a blank line, and the header and delimiter of a table carry no rule
        if ($trimmed -eq '' -or $trimmed.StartsWith(';') -or $trimmed -match '^Rule ID\s*\|' -or $trimmed -match '^-+\|')
        {
            continue
        }

        if ($trimmed -match '^##\s+Release\s+(.+)$')
        {
            $release = $Matches[1].Trim()
            $section = $null

            continue
        }

        if ($trimmed -match '^###\s+(New|Changed|Removed)\s+Rules$')
        {
            $section = $Matches[1]

            continue
        }

        # A rule is an identifier of letters and digits in the first cell of a row, and the prefixes here
        # carry a digit in the middle, so what is read is a letter followed by letters and digits
        if ($trimmed -match '^([A-Za-z][A-Za-z0-9]*)\s*\|')
        {
            $entries.Add([pscustomobject]@{
                Rule    = $Matches[1]
                Release = $release
                Section = $section
                Number  = $number
            })

            continue
        }

        $unread.Add([pscustomobject]@{ Number = $number; Text = $trimmed })
    }

    return [pscustomobject]@{ Entries = $entries; Unread = $unread }
}

$files = @(Get-ChildItem -LiteralPath (Join-Path $root 'src') -Recurse -Filter 'AnalyzerReleases.*.md' |
    Sort-Object FullName)

if ($files.Count -eq 0)
{
    throw "No analyzer release file was found under $root/src."
}

$failures = [System.Collections.Generic.List[string]]::new()
$entryCount = 0

# Each project keeps its own history, so the files are grouped by the directory that holds them
foreach ($group in ($files | Group-Object { Split-Path -Parent $_.FullName }))
{
    $project = (Resolve-Path -LiteralPath $group.Name -Relative).Replace('\', '/')
    $entries = [System.Collections.Generic.List[object]]::new()

    foreach ($file in $group.Group)
    {
        $report = Read-File -File $file.FullName
        $relativePath = (Resolve-Path -LiteralPath $file.FullName -Relative).Replace('\', '/')

        foreach ($entry in $report.Entries)
        {
            $entries.Add($entry)
        }

        foreach ($line in $report.Unread)
        {
            $excerpt = $line.Text.Substring(0, [Math]::Min(60, $line.Text.Length))

            $failures.Add("${relativePath}:$($line.Number) is neither a comment, a heading, nor a rule: $excerpt")
        }
    }

    $entryCount += $entries.Count

    $added = @($entries | Where-Object { $_.Section -eq 'New' })
    $changed = @($entries | Where-Object { $_.Section -eq 'Changed' })
    $removed = @($entries | Where-Object { $_.Section -eq 'Removed' })

    foreach ($entry in ($entries | Where-Object { $null -eq $_.Section }))
    {
        $failures.Add("$project carries $($entry.Rule) under no section of its own")
    }

    foreach ($rule in ($added | Group-Object Rule | Where-Object { @($_.Group.Release | Sort-Object -Unique).Count -gt 1 }))
    {
        $releases = ($rule.Group.Release | Sort-Object -Unique) -join ', '

        $failures.Add("$project lists $($rule.Name) as a new rule in more than one release: $releases")
    }

    foreach ($entry in $changed)
    {
        if ($entry.Rule -notin $added.Rule)
        {
            $failures.Add("$project records a change to $($entry.Rule) in release $($entry.Release) with no release that added it")
        }
    }

    # The three add up to the entries read, so a section this does not read shows as a gap rather than as nothing
    Write-Host ("{0,-46} {1,3} entries, {2} added, {3} changed, {4} removed" -f $project, $entries.Count, $added.Count, $changed.Count, $removed.Count)
}

Write-Host ''

if ($failures.Count -eq 0)
{
    Write-Host "All $entryCount entries across $($files.Count) release files record one history each."

    exit 0
}

Write-Host "$($failures.Count) finding(s):"
Write-Host ''

foreach ($failure in $failures)
{
    Write-Host "  $failure"
}

Write-Host ''
Write-Host 'A rule whose severity or category moved belongs under a "Changed Rules" heading of the release'
Write-Host 'that moved it, not under "New Rules" again: the release that added it is already recorded.'

exit 1
