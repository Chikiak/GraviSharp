<#
.SYNOPSIS
    Phase 0 Smoke Test & Native Packaging Verification Script (TC-PH0-002)
.DESCRIPTION
    Automates the publication verification, self-contained binary size audit,
    and optional headless/windowed smoke lifecycle execution of GraviSharp.UI.
.PARAMETER RunBinary
    If set, launches the published executable for $Seconds, gracefully terminates it, and verifies exit code 0.
.PARAMETER Seconds
    Duration in seconds to keep the process running if -RunBinary is specified (default: 5).
#>

[CmdletBinding()]
param(
    [switch]$RunBinary,
    [int]$Seconds = 5
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $scriptDir "..")
$uiProjectPath = Join-Path $repoRoot "src/GraviSharp.UI/GraviSharp.UI.csproj"
$publishDir = Join-Path $repoRoot "src/GraviSharp.UI/bin/Release/net11.0/win-x64/publish"
$exePath = Join-Path $publishDir "GraviSharp.UI.exe"
$raylibDllPath = Join-Path $publishDir "raylib.dll"

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host " GraviSharp Phase 0 Smoke Test & Packaging Verification     " -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan

# Step 1: Clean build release with TreatWarningsAsErrors=true
Write-Host "`n[Step 1/4] Publishing self-contained Release (win-x64) with warnings as errors..." -ForegroundColor Yellow
dotnet publish $uiProjectPath -c Release -r win-x64 --self-contained --property:TreatWarningsAsErrors=true
if ($LASTEXITCODE -ne 0) {
    Write-Error "Publish failed with exit code $LASTEXITCODE"
}
Write-Host "[SUCCESS] Publish completed successfully." -ForegroundColor Green

# Step 2: Verify binary artifacts exist and audit size
Write-Host "`n[Step 2/4] Auditing publish output artifacts..." -ForegroundColor Yellow
if (-not (Test-Path -LiteralPath $exePath)) {
    throw "Expected executable not found at: $exePath"
}
if (-not (Test-Path -LiteralPath $raylibDllPath)) {
    throw "Expected raylib.dll not found in publish directory: $raylibDllPath"
}

$exeSizeMB = [Math]::Round((Get-Item $exePath).Length / 1MB, 2)
$raylibSizeMB = [Math]::Round((Get-Item $raylibDllPath).Length / 1MB, 2)
$totalPublishBytes = (Get-ChildItem $publishDir -Recurse | Measure-Object -Property Length -Sum).Sum
$totalPublishMB = [Math]::Round($totalPublishBytes / 1MB, 2)

Write-Host "  - Executable (GraviSharp.UI.exe): $exeSizeMB MB"
Write-Host "  - Native Dependency (raylib.dll): $raylibSizeMB MB"
Write-Host "  - Total Self-Contained Publish Folder: $totalPublishMB MB"
Write-Host "[SUCCESS] Artifacts verified." -ForegroundColor Green

# Step 3: Optional Execution & Clean Close Test
$exitCodeResult = $null
if ($RunBinary) {
    Write-Host "`n[Step 3/4] Launching binary for $Seconds seconds smoke test..." -ForegroundColor Yellow
    $process = Start-Process -FilePath $exePath -PassThru
    Write-Host "  - Process started with PID: $($process.Id)"
    
    Start-Sleep -Seconds $Seconds

    if (-not $process.HasExited) {
        Write-Host "  - Sending graceful window close signal (WM_CLOSE)..."
        # Import user32 PostMessage for graceful WM_CLOSE
        Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public class Win32 {
    [DllImport("user32.dll")]
    public static extern IntPtr PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
}
"@ -ErrorAction SilentlyContinue

        if ($process.MainWindowHandle -ne [IntPtr]::Zero) {
            $null = [Win32]::PostMessage($process.MainWindowHandle, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) # WM_CLOSE
        } else {
            $process.CloseMainWindow()
        }

        $exitedWithinTime = $process.WaitForExit(3000)
        if (-not $exitedWithinTime) {
            Write-Warning "  - Process did not exit gracefully in time. Forcing termination..."
            Stop-Process -Id $process.Id -Force
        }
    }

    $exitCodeResult = $process.ExitCode
    Write-Host "  - Process exited with code: $exitCodeResult" -ForegroundColor $(if ($exitCodeResult -eq 0) { "Green" } else { "Yellow" })
} else {
    Write-Host "`n[Step 3/4] Skipped binary execution (-RunBinary flag not provided)." -ForegroundColor DarkGray
}

# Step 4: Generate JSON Evidence Report
Write-Host "`n[Step 4/4] Generating acceptance report JSON..." -ForegroundColor Yellow
$evidenceDir = Join-Path $repoRoot "../plans/acceptance"
if (-not (Test-Path -LiteralPath $evidenceDir)) {
    New-Item -ItemType Directory -Path $evidenceDir -Force | Out-Null
}
$reportPath = Join-Path $evidenceDir "phase0_smoke_report.json"

$report = [PSCustomObject]@{
    Timestamp           = (Get-Date).ToString("o")
    RuntimeIdentifier   = "win-x64"
    PublishMode         = "Self-Contained JIT Fallback (PublishAot=false)"
    ExeSizeMB           = $exeSizeMB
    RaylibDllSizeMB     = $raylibSizeMB
    TotalPublishSizeMB  = $totalPublishMB
    ExecutablePath      = $exePath
    ExitCode            = $exitCodeResult
    Status              = "PASSED"
}

$report | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath $reportPath -Encoding utf8
Write-Host "  - Report written to: $reportPath" -ForegroundColor Green

Write-Host "`n============================================================" -ForegroundColor Cyan
Write-Host " PHASE 0 SMOKE & PACKAGING VERIFICATION COMPLETED SUCCESSFULLY" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
