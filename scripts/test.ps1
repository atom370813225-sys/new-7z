param([ValidateSet('x64','arm64')][string]$Architecture='x64')
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$env:MSBuildEnableWorkloadResolver='false'
Push-Location $projectRoot
$previousArchitecture=$env:NEW7Z_TEST_ARCHITECTURE
$env:NEW7Z_TEST_ARCHITECTURE=$Architecture
$platform=if($Architecture -eq 'arm64'){'ARM64'}else{'x64'}
try {
 & dotnet run --project tests/New7z.Integration -c Release "-p:New7zArchitecture=$Architecture" "-p:Platform=$platform"
 if($LASTEXITCODE){throw 'Engine integration tests failed.'}
 & dotnet run --project tests/New7z.Icon.Tests -c Release --arch $Architecture
 if($LASTEXITCODE){throw 'Explorer icon integration tests failed.'}
 & dotnet run --project tests/New7z.Settings.Tests -c Release "-p:New7zArchitecture=$Architecture" "-p:Platform=$platform"
 if($LASTEXITCODE){throw 'Settings and update integration tests failed.'}
}finally{$env:NEW7Z_TEST_ARCHITECTURE=$previousArchitecture;Pop-Location}
