#Requires -Version 7

<#
.SYNOPSIS
    Verifies that every shipped NuGet package carries the files it is meant to carry.

.DESCRIPTION
    The release workflow already checks that each package is the right package: it reads the
    package id, the version and the repository commit out of the .nuspec. That is identity,
    not payload. A package whose id, version and commit are all correct can still carry none
    of the files that make it useful, and nothing downstream notices:

      - Dropping the native libraries from ComputeWeave.Dxc leaves a package that restores
        cleanly. The resolver in that package falls back to loading DXC by name, so any other
        copy on the machine answers instead and the consumer keeps working.
      - Dropping the generator from an analyzer-bearing package leaves a package that restores
        cleanly and then fails the consumer's build, because the shader types never receive
        their generated descriptor.

    Both are silent at pack time. This script closes that gap by reading the archive itself.

    The expected entries below are held here rather than derived from the .csproj files on
    purpose. A check that derives its expectation from the thing it checks agrees with it when
    both are wrong, which is the one case that matters. Changing what a package ships is
    therefore a deliberate edit to this list.

    Only the functional payload is compared. NuGet plumbing, the readme, the icon and the
    third-party notices are left alone, and package identity stays with the workflow step that
    already covers it.

.PARAMETER PackageDirectory
    The directory holding the packed .nupkg files. Defaults to the repository's artifacts folder.

.EXAMPLE
    pwsh build/verify-package-payload.ps1

.EXAMPLE
    pwsh build/verify-package-payload.ps1 -PackageDirectory artifacts
#>

[CmdletBinding()]
param(
    [string] $PackageDirectory
)

$ErrorActionPreference = 'Stop'

# The payload every shipped package must carry, exactly. Measured from the packed output, so a
# package that gains or loses an entry fails here until this list is updated to match.
$expectedPayload = [ordered]@{
    'ComputeWeave.Core' = @(
        'analyzers/dotnet/cs/ComputeWeave.Core.SourceGenerators.dll'
        'build/ComputeWeave.Core.targets'
        'buildTransitive/ComputeWeave.Core.targets'
        'lib/net10.0/ComputeWeave.Core.dll'
        'lib/net10.0/ComputeWeave.Core.xml'
    )
    'ComputeWeave' = @(
        'analyzers/dotnet/cs/ComputeWeave.CodeFixers.dll'
        'analyzers/dotnet/cs/ComputeWeave.SourceGenerators.dll'
        'build/ComputeWeave.targets'
        'buildTransitive/ComputeWeave.targets'
        'lib/net10.0/ComputeWeave.dll'
        'lib/net10.0/ComputeWeave.xml'
    )
    'ComputeWeave.Dxc' = @(
        'lib/net10.0/ComputeWeave.Dxc.dll'
        'lib/net10.0/ComputeWeave.Dxc.xml'
        'runtimes/win-arm64/native/dxcompiler.dll'
        'runtimes/win-arm64/native/dxil.dll'
        'runtimes/win-x64/native/dxcompiler.dll'
        'runtimes/win-x64/native/dxil.dll'
    )
    'ComputeWeave.D3D12MemoryAllocator' = @(
        'lib/net10.0/ComputeWeave.D3D12MemoryAllocator.dll'
        'lib/net10.0/ComputeWeave.D3D12MemoryAllocator.xml'
    )
    'ComputeWeave.D2D1' = @(
        'analyzers/dotnet/cs/ComputeWeave.D2D1.CodeFixers.dll'
        'analyzers/dotnet/cs/ComputeWeave.D2D1.SourceGenerators.dll'
        'build/ComputeWeave.D2D1.targets'
        'buildTransitive/ComputeWeave.D2D1.targets'
        'lib/net10.0/ComputeWeave.D2D1.dll'
        'lib/net10.0/ComputeWeave.D2D1.xml'
    )
}

# The archive folders that hold payload. Everything outside them is NuGet plumbing or metadata.
$payloadRoots = @('lib', 'analyzers', 'build', 'buildTransitive', 'runtimes')

if (-not $PackageDirectory)
{
    $PackageDirectory = Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts'
}

if (-not (Test-Path $PackageDirectory))
{
    throw "No package directory at $PackageDirectory. Pack the projects before running this."
}

Add-Type -AssemblyName System.IO.Compression.FileSystem

# Index the packed archives by the id in their .nuspec. The file name cannot be split back into
# an id and a version without guessing where one ends, so the manifest inside decides.
$packages = @{}

foreach ($file in Get-ChildItem -LiteralPath $PackageDirectory -File -Filter '*.nupkg')
{
    $archive = [System.IO.Compression.ZipFile]::OpenRead($file.FullName)

    try
    {
        $manifest = @($archive.Entries | Where-Object { $_.FullName -like '*.nuspec' -and $_.FullName -notlike '*/*' })

        if ($manifest.Count -ne 1)
        {
            throw "$($file.Name) does not carry exactly one manifest at its root."
        }

        $reader = [System.IO.StreamReader]::new($manifest[0].Open())

        try
        {
            [xml] $document = $reader.ReadToEnd()
        }
        finally
        {
            $reader.Dispose()
        }

        $namespaces = [System.Xml.XmlNamespaceManager]::new($document.NameTable)
        $namespaces.AddNamespace('n', $document.DocumentElement.NamespaceURI)

        $id = $document.SelectSingleNode('/n:package/n:metadata/n:id', $namespaces).InnerText

        $payload = @(
            $archive.Entries |
                Select-Object -ExpandProperty FullName |
                Where-Object { $payloadRoots -contains $_.Split('/')[0] } |
                Sort-Object
        )

        $packages[$id] = [pscustomobject]@{
            Name    = $file.Name
            Payload = $payload
        }
    }
    finally
    {
        $archive.Dispose()
    }
}

$failures = [System.Collections.Generic.List[string]]::new()

foreach ($id in $expectedPayload.Keys)
{
    if (-not $packages.ContainsKey($id))
    {
        $failures.Add("$id was not packed into $PackageDirectory.")

        continue
    }

    $package = $packages[$id]
    $expected = [string[]] $expectedPayload[$id]
    $actual = [string[]] $package.Payload

    foreach ($entry in $expected)
    {
        if ($actual -notcontains $entry)
        {
            $failures.Add("$($package.Name) does not carry '$entry'.")
        }
    }

    foreach ($entry in $actual)
    {
        if ($expected -notcontains $entry)
        {
            $failures.Add("$($package.Name) carries '$entry', which this script does not expect.")
        }
    }

    Write-Host ("{0,-34} {1,2} payload entries" -f $id, $actual.Count)
}

Write-Host ''

if ($failures.Count -eq 0)
{
    Write-Host "All $($expectedPayload.Count) packages carry the payload they are expected to carry."

    exit 0
}

Write-Host "$($failures.Count) problem(s) found:"
Write-Host ''

foreach ($failure in $failures)
{
    Write-Host "  $failure"
}

Write-Host ''
Write-Host 'If the change to what a package ships was intended, update the expected payload in'
Write-Host 'build/verify-package-payload.ps1 in the same commit that changes it.'

exit 1
