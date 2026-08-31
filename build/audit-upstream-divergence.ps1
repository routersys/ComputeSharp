#Requires -Version 7

<#
.SYNOPSIS
    Reports commits that touch upstream-inherited source but are not accounted for by the
    divergence ledger in CONTRIBUTING.md.

.DESCRIPTION
    The ledger records every place where this fork deliberately behaves differently from
    upstream ComputeSharp. A divergence that is not written down is one the next merge
    silently undoes, and the ledger has fallen behind before, so this script replaces
    memory with a derivation:

      1. Every file present at the fork point is upstream code.
      2. Every commit after the watermark that modifies such a file is a candidate.
      3. Candidates already cited by a ledger row are accounted for.
      4. Whatever remains needs a human decision: add a row, or judge it not a divergence
         and move the watermark.

    The script cannot tell a defect fix from a feature built on inherited code, and does
    not try to. It produces a queue; the judgement stays with the reviewer.

.PARAMETER Watermark
    Audit from this commit instead of the one recorded in CONTRIBUTING.md.

.EXAMPLE
    pwsh build/audit-upstream-divergence.ps1

.EXAMPLE
    pwsh build/audit-upstream-divergence.ps1 -Watermark v2.1.0
#>

[CmdletBinding()]
param(
    [string] $Watermark
)

$ErrorActionPreference = 'Stop'

# The last commit inherited from upstream ComputeSharp. Every path in that tree is upstream code.
$forkPoint = 'c3e9ad4f'

$repository = Split-Path $PSScriptRoot -Parent
$guidePath = Join-Path $repository 'CONTRIBUTING.md'

if (-not (Test-Path $guidePath))
{
    throw "CONTRIBUTING.md was not found at $guidePath."
}

$guide = Get-Content $guidePath -Raw

if (-not $Watermark)
{
    if ($guide -notmatch 'ledger is audited through commit `([0-9a-f]{7,40})')
    {
        throw 'CONTRIBUTING.md carries no "ledger is audited through commit" line; pass -Watermark explicitly.'
    }

    $Watermark = $Matches[1]
}

# The identifiers a ledger row can be citing. Every row writes an abbreviation, and how many
# characters git prints for one depends on the size of the object database at the moment it
# asks, so the width in the document and the width of a fresh listing need not agree. These
# are resolved to full hashes below and compared at that width.
$citations = [System.Collections.Generic.List[string]]::new()

foreach ($match in [regex]::Matches($guide, '`([0-9a-f]{8,40})`'))
{
    $citations.Add($match.Groups[1].Value)
}

Push-Location $repository

try
{
    foreach ($commit in @($forkPoint, $Watermark))
    {
        if ((git cat-file -t $commit 2>$null) -ne 'commit')
        {
            throw "$commit is not a commit in this repository."
        }
    }

    # A citation is an identifier that names a commit here. A hexadecimal string in the
    # document that names nothing, a byte count for instance, resolves to "missing" and is
    # dropped, so reading every one of them costs nothing.
    $cited = [System.Collections.Generic.HashSet[string]]::new()

    foreach ($line in ($citations | git cat-file --batch-check="%(objectname) %(objecttype)"))
    {
        $parts = $line.Split(' ')

        if ($parts.Count -ge 2 -and $parts[1] -eq 'commit')
        {
            [void]$cited.Add($parts[0])
        }
    }

    # Every path that existed at the fork point, plus the name each one carries today: the
    # fork renamed ComputeSharp to ComputeWeave across the tree, so both spellings must match.
    $upstream = [System.Collections.Generic.HashSet[string]]::new()

    foreach ($path in (git ls-tree -r $forkPoint --name-only))
    {
        [void]$upstream.Add($path)
        [void]$upstream.Add($path.Replace('ComputeSharp', 'ComputeWeave'))
    }

    $queue = [System.Collections.Generic.List[object]]::new()
    $current = $null

    foreach ($line in (git log --no-merges --name-status --format='C|%H|%ad|%s' --date=short "$Watermark..HEAD"))
    {
        if ($line.StartsWith('C|'))
        {
            $fields = $line.Split('|', 4)
            $current = [pscustomobject]@{
                Hash    = $fields[1]
                Date    = $fields[2]
                Subject = $fields[3]
                Files   = [System.Collections.Generic.List[string]]::new()
            }

            continue
        }

        if (-not $current -or -not $line.StartsWith('M'))
        {
            continue
        }

        $path = $line.Split("`t")[-1].Trim()

        # Only source is considered. Project files and documents move for reasons of their own.
        if ($path.StartsWith('src/') -and $path.EndsWith('.cs') -and $upstream.Contains($path))
        {
            $current.Files.Add($path)

            if ($current.Files.Count -eq 1 -and -not $cited.Contains($current.Hash))
            {
                $queue.Add($current)
            }
        }
    }

    Write-Host "Fork point       $forkPoint"
    Write-Host "Audited through  $Watermark"
    Write-Host "Ledger rows cite $($cited.Count) commits"
    Write-Host ''

    if ($queue.Count -eq 0)
    {
        Write-Host 'No commit after the watermark modifies upstream-inherited source. The ledger is current.'
        exit 0
    }

    Write-Host "$($queue.Count) commit(s) modify upstream-inherited source and are not cited by the ledger:"
    Write-Host ''

    foreach ($commit in $queue)
    {
        Write-Host "  $($commit.Hash.Substring(0, 8))  $($commit.Date)  $($commit.Subject)"

        foreach ($file in $commit.Files)
        {
            Write-Host "      $file"
        }
    }

    Write-Host ''
    Write-Host 'For each one, either add a row to the ledger in CONTRIBUTING.md, or decide that it is'
    Write-Host 'not a divergence. When the queue is empty, move the line that records how far the ledger is audited.'

    exit 1
}
finally
{
    Pop-Location
}
