# Builds BlackScreen.exe with the C# compiler that ships with Windows - no SDK needed.
# -Console additionally produces a console variant so --selftest output is visible.
param([switch]$Console)
$ErrorActionPreference = "Stop"
$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
& $csc /nologo /target:winexe /optimize+ /codepage:65001 /out:BlackScreen.exe BlackScreen.cs
if ($LASTEXITCODE -ne 0) { throw "csc failed: $LASTEXITCODE" }
if ($Console) {
    & $csc /nologo /target:exe /optimize+ /codepage:65001 /out:BlackScreenCon.exe BlackScreen.cs
    if ($LASTEXITCODE -ne 0) { throw "csc failed: $LASTEXITCODE" }
}Get-Item BlackScreen*.exe | Select-Object Name, Length