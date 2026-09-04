; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/master/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
CMPWD2D0095 | ComputeWeave.D2D1.Shaders | Error | [Documentation](https://github.com/routersys/ComputeWeave)
CMPWD2D0096 | ComputeWeave.D2D1.Shaders | Error | [Documentation](https://github.com/routersys/ComputeWeave)
CMPWD2D0097 | ComputeWeave.D2D1.Shaders | Error | [Documentation](https://github.com/routersys/ComputeWeave)

; The rule below changed severity rather than being added, so it moves into the shipped file under a
; "Changed Rules" heading of its own. build/verify-analyzer-releases.ps1 refuses it under "New Rules".
### Changed Rules

Rule ID | New Category | New Severity | Old Category | Old Severity | Notes
--------|--------------|--------------|--------------|--------------|-------
CMPWD2D0094 | ComputeWeave.D2D1.Shaders | Error | ComputeWeave.D2D1.Shaders | Info | [Documentation](https://github.com/routersys/ComputeWeave)
