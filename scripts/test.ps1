$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$env:MSBuildEnableWorkloadResolver='false'
Push-Location $projectRoot
try {
 & dotnet run --project tests/New7z.Integration -c Release
 if($LASTEXITCODE){throw 'Engine integration tests failed.'}
 & dotnet run --project tests/New7z.Icon.Tests -c Release
 if($LASTEXITCODE){throw 'Explorer icon integration tests failed.'}
}finally{Pop-Location}
