param([string]$AppDirectory,[int]$Rounds=5,[string]$ReportPath)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(!$AppDirectory){$AppDirectory=Join-Path $root 'artifacts/app'}
if(!$ReportPath){$ReportPath=Join-Path $root 'artifacts/startup-after.json'}
$rows=@(for($round=1;$round -le $Rounds;$round++){
 $probe=Join-Path $root ('artifacts/startup-probe-'+[guid]::NewGuid().ToString('N')+'.json')
 $timer=[Diagnostics.Stopwatch]::StartNew()
 $process=Start-Process -FilePath (Join-Path $AppDirectory 'New7z.App.exe') -ArgumentList @('--measure-startup',('"'+$probe+'"')) -WorkingDirectory $AppDirectory -WindowStyle Hidden -PassThru
 try{
  $windowMs=$null
  while($timer.Elapsed.TotalSeconds -lt 30){
   $process.Refresh();if($process.HasExited){throw "Startup exited with code $($process.ExitCode)."}
   if(!$windowMs -and $process.MainWindowHandle -ne 0){$windowMs=[math]::Round($timer.Elapsed.TotalMilliseconds,1)}
   if(Test-Path -LiteralPath $probe){break}
   Start-Sleep -Milliseconds 20
  }
  if(!(Test-Path -LiteralPath $probe)){throw 'First rendering probe timed out.'}
  $sample=Get-Content -LiteralPath $probe -Raw | ConvertFrom-Json
  [pscustomobject]@{Round=$round;WindowMilliseconds=$windowMs;LoadedMilliseconds=[double]::Parse($sample.LoadedMilliseconds,[Globalization.CultureInfo]::InvariantCulture);FirstRenderingMilliseconds=[double]::Parse($sample.FirstRenderingMilliseconds,[Globalization.CultureInfo]::InvariantCulture)}
 }finally{
  if(!$process.HasExited){$process.CloseMainWindow() | Out-Null;if(!$process.WaitForExit(5000)){$process.Kill()}}
  if(Test-Path -LiteralPath $probe){Remove-Item -LiteralPath $probe -Force}
 }
})
$rows | ConvertTo-Json | Set-Content -LiteralPath $ReportPath -Encoding utf8
$rows | Format-Table -AutoSize
