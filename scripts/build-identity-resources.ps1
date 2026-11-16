param([Parameter(Mandatory=$true)][string]$PackageDirectory)
$ErrorActionPreference='Stop'
$PackageDirectory=[IO.Path]::GetFullPath($PackageDirectory)
& "$PSScriptRoot/make-store-assets.ps1" -Destination (Join-Path $PackageDirectory 'Assets/Store')
$sdk=Get-ChildItem 'C:/Program Files (x86)/Windows Kits/10/bin' -Directory | Where-Object {$_.Name -match '^10\.' -and (Test-Path -LiteralPath "$($_.FullName)/x64/makepri.exe")} | Sort-Object {[version]$_.Name} -Descending | Select-Object -First 1
if(!$sdk){throw 'Windows SDK MakePri not found.'}
$config=Join-Path (Split-Path $PackageDirectory -Parent) ('pri-config-'+[guid]::NewGuid().ToString('N')+'.xml')
try{
 & "$($sdk.FullName)/x64/makepri.exe" createconfig /cf $config /dq zh-CN /o | Out-Null
 if($LASTEXITCODE){throw 'Identity PRI configuration failed.'}
 & "$($sdk.FullName)/x64/makepri.exe" new /pr $PackageDirectory /cf $config /of (Join-Path $PackageDirectory 'resources.pri') /o | Out-Null
 if($LASTEXITCODE){throw 'Identity PRI generation failed.'}
}finally{if(Test-Path -LiteralPath $config){Remove-Item -LiteralPath $config -Force}}
