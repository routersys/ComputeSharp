#Requires -Version 7

<#
.SYNOPSIS
    Verifies that ComputeWeave.Dxc copies the native libraries the requested platform asks for.

.DESCRIPTION
    An explicit 'Platform' decides which architecture is copied to the output; the environment
    variable CI_RUNNER_DOTNET_TEST_PLATFORM is only the default when no platform was given.

.PARAMETER ProjectPath
    The project to evaluate. Defaults to src/ComputeWeave.Dxc/ComputeWeave.Dxc.csproj.

.EXAMPLE
    pwsh build/verify-dxc-native-copy.ps1
#>

[CmdletBinding()]
param(
    [string] $ProjectPath
)

$ErrorActionPreference = 'Stop'

if (-not $ProjectPath)
{
    $ProjectPath = Join-Path (Split-Path $PSScriptRoot -Parent) 'src/ComputeWeave.Dxc/ComputeWeave.Dxc.csproj'
}

# What each combination must copy, held here rather than derived from the project being checked.
# An entry is the architecture of the source library and the path it is copied to.
$expectedCopies = @(
    @{ Platform = ''; Variable = ''; Expected = @(
        'arm64 -> runtimes\win-arm64\native\dxcompiler.dll'
        'arm64 -> runtimes\win-arm64\native\dxil.dll'
        'x64 -> runtimes\win-x64\native\dxcompiler.dll'
        'x64 -> runtimes\win-x64\native\dxil.dll') }
    @{ Platform = ''; Variable = 'x64'; Expected = @(
        'x64 -> dxcompiler.dll'
        'x64 -> dxil.dll') }
    @{ Platform = ''; Variable = 'ARM64'; Expected = @(
        'arm64 -> dxcompiler.dll'
        'arm64 -> dxil.dll') }
    @{ Platform = 'x64'; Variable = ''; Expected = @(
        'x64 -> dxcompiler.dll'
        'x64 -> dxil.dll') }
    @{ Platform = 'x64'; Variable = 'ARM64'; Expected = @(
        'x64 -> dxcompiler.dll'
        'x64 -> dxil.dll') }
    @{ Platform = 'ARM64'; Variable = ''; Expected = @(
        'arm64 -> dxcompiler.dll'
        'arm64 -> dxil.dll') }
    @{ Platform = 'ARM64'; Variable = 'x64'; Expected = @(
        'arm64 -> dxcompiler.dll'
        'arm64 -> dxil.dll') }
)

# Reads the None items after evaluation and returns the copies the project asks for.
function Get-NativeCopy
{
    param([string] $Project, [string] $Platform)

    $arguments = @($Project, '-getItem:None', '-nologo')

    if ($Platform)
    {
        $arguments += "-p:Platform=$Platform"
    }

    $output = & dotnet msbuild @arguments 2>&1

    if ($LASTEXITCODE -ne 0)
    {
        throw "Evaluating $Project failed:`n$($output -join [Environment]::NewLine)"
    }

    $items = ($output | Out-String | ConvertFrom-Json).Items.None
    $copies = [System.Collections.Generic.List[string]]::new()

    foreach ($item in $items)
    {
        if (-not $item.CopyToOutputDirectory)
        {
            continue
        }

        if ($item.Identity -match 'libs\\(x64|arm64)\\dx')
        {
            $copies.Add("$($Matches[1]) -> $($item.Link)")
        }
    }

    return @($copies | Sort-Object)
}

# The runner image and the CI job both put a value in the environment, so every case sets its own.
$originalPlatform = $env:Platform
$originalVariable = $env:CI_RUNNER_DOTNET_TEST_PLATFORM
$failures = [System.Collections.Generic.List[string]]::new()

try
{
    foreach ($case in $expectedCopies)
    {
        $env:Platform = $null
        $env:CI_RUNNER_DOTNET_TEST_PLATFORM = $case.Variable

        $actual = Get-NativeCopy -Project $ProjectPath -Platform $case.Platform
        $expected = [string[]] $case.Expected
        $label = "Platform=$(if ($case.Platform) { $case.Platform } else { '(none)' }), variable=$(if ($case.Variable) { $case.Variable } else { '(none)' })"

        if (Compare-Object $expected $actual -SyncWindow 0)
        {
            $failures.Add("$label copies $($actual -join ', ') instead of $($expected -join ', ').")
        }

        Write-Host ("{0,-46} {1}" -f $label, ($actual -join ', '))
    }
}
finally
{
    $env:Platform = $originalPlatform
    $env:CI_RUNNER_DOTNET_TEST_PLATFORM = $originalVariable
}

Write-Host ''

if ($failures.Count -eq 0)
{
    Write-Host "All $($expectedCopies.Count) combinations copy the native libraries they are expected to copy."

    exit 0
}

Write-Host "$($failures.Count) problem(s) found:"
Write-Host ''

foreach ($failure in $failures)
{
    Write-Host "  $failure"
}

Write-Host ''
Write-Host 'If the change to which libraries are copied was intended, update the expected copies in'
Write-Host 'build/verify-dxc-native-copy.ps1 in the same commit that changes it.'

exit 1
