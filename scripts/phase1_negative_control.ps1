$ErrorActionPreference = 'Stop'
$root = Resolve-Path "$PSScriptRoot\.."
Set-Location $root

$publishDir = Join-Path $root 'publish_nc'
if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }

$progCs = Join-Path $root 'src\GraviSharp.UI\Program.cs'
$bak    = "$progCs.bak"
Copy-Item $progCs $bak -Force

try {
    $content = Get-Content $progCs -Raw
    $marker = 'SimulationStore.UpdateLinear(&store, WindowWidth, WindowHeight, dt);'

    $drawLine = @'
            Raylib.DrawText($"test{frameIndex}", 10, 40, 10, Color.Red);
'@

    $inject = $marker + "`n" + $drawLine

    if (-not $content.Contains($marker)) { throw "anchor line not found in Program.cs" }
    $newContent = $content.Replace($marker, $inject)
    Set-Content -Path $progCs -Value $newContent -NoNewline

    dotnet clean src\GraviSharp.UI\GraviSharp.UI.csproj -c Release /clp:ErrorsOnly | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "clean failed" }

    dotnet publish src\GraviSharp.UI\GraviSharp.UI.csproj -c Release `
        -o $publishDir `
        /t:Rebuild `
        /clp:ErrorsOnly
    if ($LASTEXITCODE -ne 0) { throw "publish failed" }

    $outFile = Join-Path $root 'phase1_nc_stdout.txt'
    if (Test-Path $outFile) { Remove-Item $outFile -Force }

    $exe = Join-Path $publishDir 'GraviSharp.UI.exe'
    $proc = Start-Process $exe `
        -ArgumentList "--verify-heap" -PassThru -NoNewWindow `
        -RedirectStandardOutput $outFile
    Start-Sleep -Seconds 5
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

    if ($stdout -notmatch 'MaxHeapDeltaPerFrame=\s*[1-9]\d*') {
        throw "FAIL: expected MaxHeapDeltaPerFrame>0, got: $stdout"
    }
    Write-Output "NEGATIVE CONTROL PASS"
    exit 0
}
finally {
    if (Test-Path $bak) {
        Copy-Item $bak $progCs -Force
        Remove-Item $bak -Force -EA SilentlyContinue
    }
}
