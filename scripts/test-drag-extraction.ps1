param([ValidateSet('x64','arm64')][string]$Architecture='x64')
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
. "$PSScriptRoot/native-environment.ps1"
$buildEnvironment=Get-NativeBuildEnvironment $Architecture
$vcvars=$buildEnvironment.Script;$vcArgument=$buildEnvironment.Argument
$output=Join-Path $projectRoot "artifacts/tests/drag-extraction-$Architecture"
New-Item -ItemType Directory -Path $output -Force | Out-Null
Push-Location $projectRoot
try {
  & cmd /c "`"$vcvars`" $vcArgument >nul && cl /nologo /std:c++17 /EHsc /O2 /MT /utf-8 /DUNICODE /D_UNICODE tests\native\DragExtraction.cpp /Fo`"$output\DragExtraction.obj`" /Fe:`"$output\DragExtraction.exe`" /link ole32.lib oleaut32.lib shell32.lib user32.lib urlmon.lib runtimeobject.lib"
  if($LASTEXITCODE){throw 'Drag extraction test build failed.'}
  & "$output/DragExtraction.exe" "$projectRoot/artifacts/native/$Architecture/7z.dll"
  if($LASTEXITCODE){throw 'Drag extraction tests failed.'}
}finally{Pop-Location}
