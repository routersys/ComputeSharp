; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/master/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
CMPW0122 | ComputeWeave.Shaders | Error | [Documentation](https://github.com/routersys/ComputeWeave)
CMPW0123 | ComputeWeave.Shaders | Error | [Documentation](https://github.com/routersys/ComputeWeave)

; The rule below changed severity rather than being added, so it moves into the shipped file under a
; "Changed Rules" heading of its own. build/verify-analyzer-releases.ps1 refuses it under "New Rules".
### Changed Rules

Rule ID | New Category | New Severity | Old Category | Old Severity | Notes
--------|--------------|--------------|--------------|--------------|-------
CMPW0121 | ComputeWeave.Shaders | Error | ComputeWeave.Shaders | Info | [Documentation](https://github.com/routersys/ComputeWeave)
