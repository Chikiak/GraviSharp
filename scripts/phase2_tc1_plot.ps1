[CmdletBinding()]
param(
    [string]$CsvPath,
    [string]$SvgPath
)

if ([string]::IsNullOrEmpty($CsvPath)) {
    $CsvPath = Join-Path $PSScriptRoot '..\..\plans\acceptance\phase2_tc1_energy_momentum.csv'
}
if ([string]::IsNullOrEmpty($SvgPath)) {
    $SvgPath = Join-Path $PSScriptRoot '..\..\plans\acceptance\phase2_tc1_energy_momentum.svg'
}

if (!(Test-Path -LiteralPath $CsvPath)) {
    Write-Error "CSV file not found at $CsvPath. Run the test TwoBody_DumpsEnergyMomentumCsv_ArtifactForPlot first."
    exit 1
}

$rows = Import-Csv -Path $CsvPath
if ($rows.Count -eq 0) {
    Write-Error "CSV file is empty."
    exit 1
}

$width = 800
$height = 400
$padding = 60

$ticks = $rows | ForEach-Object { [int]$_.tick }
$energies = $rows | ForEach-Object { [double]$_.E_total }
$momentums = $rows | ForEach-Object { [double]$_.Px }

$minTick = $ticks | Measure-Object -Minimum | Select-Object -ExpandProperty Minimum
$maxTick = $ticks | Measure-Object -Maximum | Select-Object -ExpandProperty Maximum
$minE = $energies | Measure-Object -Minimum | Select-Object -ExpandProperty Minimum
$maxE = $energies | Measure-Object -Maximum | Select-Object -ExpandProperty Maximum

if ($minE -eq $maxE) { $minE -= 1; $maxE += 1 }
$eRange = $maxE - $minE

$svg = [System.Text.StringBuilder]::new()
[void]$svg.AppendLine("<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 $width $height' width='$width' height='$height'>")
[void]$svg.AppendLine("<style>")
[void]$svg.AppendLine("  .bg { fill: #1e1e1e; }")
[void]$svg.AppendLine("  .axis { stroke: #cccccc; stroke-width: 1.5; }")
[void]$svg.AppendLine("  .grid { stroke: #333333; stroke-width: 1; stroke-dasharray: 4; }")
[void]$svg.AppendLine("  .line-e { fill: none; stroke: #4ec9b0; stroke-width: 2.5; }")
[void]$svg.AppendLine("  .text { fill: #cccccc; font-family: sans-serif; font-size: 12px; }")
[void]$svg.AppendLine("  .title { fill: #ffffff; font-family: sans-serif; font-size: 16px; font-weight: bold; }")
[void]$svg.AppendLine("</style>")

[void]$svg.AppendLine("<rect class='bg' width='$width' height='$height' rx='8'/>")
[void]$svg.AppendLine("<text x='$width' y='30' text-anchor='middle' class='title'>TC-PH2-001: Total Energy Conservation (1000 Ticks)</text>")

# Plot area
$plotWidth = $width - (2 * $padding)
$plotHeight = $height - (2 * $padding)
$originX = $padding
$originY = $height - $padding

# Grid & Axis
[void]$svg.AppendLine("<line x1='$originX' y1='$originY' x2='$($width - $padding)' y2='$originY' class='axis'/>")
[void]$svg.AppendLine("<line x1='$originX' y1='$originY' x2='$originX' y2='$padding' class='axis'/>")

# Y-axis labels & grid
$steps = 5
for ($i = 0; $i -le $steps; $i++) {
    $val = $minE + ($eRange * ($i / $steps))
    $y = $originY - ($plotHeight * ($i / $steps))
    [void]$svg.AppendLine("<line x1='$originX' y1='$y' x2='$($width - $padding)' y2='$y' class='grid'/>")
    $formatted = "{0:N2}" -f $val
    [void]$svg.AppendLine("<text x='$($originX - 10)' y='$($y + 4)' text-anchor='end' class='text'>$formatted</text>")
}

# X-axis labels
for ($i = 0; $i -le 4; $i++) {
    $t = [int]($minTick + (($maxTick - $minTick) * ($i / 4)))
    $x = $originX + ($plotWidth * ($i / 4))
    [void]$svg.AppendLine("<line x1='$x' y1='$originY' x2='$x' y2='$($originY + 5)' class='axis'/>")
    [void]$svg.AppendLine("<text x='$x' y='$($originY + 20)' text-anchor='middle' class='text'>$t</text>")
}

# Polyline points
$points = [System.Text.StringBuilder]::new()
for ($i = 0; $i -lt $rows.Count; $i++) {
    $t = [int]$rows[$i].tick
    $e = [double]$rows[$i].E_total
    
    $x = $originX + $plotWidth * (($t - $minTick) / ($maxTick - $minTick))
    $y = $originY - $plotHeight * (($e - $minE) / $eRange)
    [void]$points.Append("$x,$y ")
}

[void]$svg.AppendLine("<polyline points='$points' class='line-e'/>")
[void]$svg.AppendLine("</svg>")

$svg.ToString() | Set-Content -Path $SvgPath -Encoding UTF8
Write-Host "SVG plot generated successfully at: $SvgPath"
