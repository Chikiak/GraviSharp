$ErrorActionPreference = 'Stop'
$root = Resolve-Path "$PSScriptRoot\.."
Set-Location $root

$publishDir = Join-Path $root 'publish_qg2'
if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }

dotnet clean src\GraviSharp.UI\GraviSharp.UI.csproj -c Release /clp:ErrorsOnly | Out-Null
if ($LASTEXITCODE -ne 0) { Write-Error "clean failed"; exit 1 }

dotnet publish src\GraviSharp.UI\GraviSharp.UI.csproj -c Release `
    -o $publishDir `
    /t:Rebuild `
    /clp:ErrorsOnly
if ($LASTEXITCODE -ne 0) { Write-Error "publish failed"; exit 1 }

$outFile = Join-Path $root "phase1_qg2_b1_stdout.txt"
if (Test-Path $outFile) { Remove-Item $outFile -Force }

$exe = Join-Path $publishDir 'GraviSharp.UI.exe'
$proc = Start-Process -FilePath $exe `
    -ArgumentList "--verify-heap" `
    -PassThru -NoNewWindow `
    -RedirectStandardOutput $outFile

Start-Sleep -Seconds 30

if (-not $proc.HasExited) {
    [void]$proc.CloseMainWindow()
    if (-not $proc.WaitForExit(5000)) {
        Stop-Process -Id $proc.Id -Force
        Start-Sleep -Seconds 2
    }
}

if (-not $proc.HasExited) { [void]$proc.WaitForExit() }

$stdout = Get-Content $outFile -Raw
Write-Output $stdout

$exitCode = -1
if ($proc.HasExited) { try { $exitCode = $proc.ExitCode } catch { $exitCode = -1 } }
Write-Output ("[info] process exit code: " + $exitCode)

if ($stdout -notmatch 'MaxHeapDeltaPerFrame=0') {
    Write-Error "FAIL: MaxHeapDeltaPerFrame=0 not found"; exit 1
}
Write-Output "QG-PH1-002 B1 PASS"
exit 0
