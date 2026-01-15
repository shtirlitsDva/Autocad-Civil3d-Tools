<#
EncodingStats2.ps1
- Recursively scans root
- Excludes: .dwg, .bak and common folders
- Reports BOM types, UTF-8 validity, likely UTF-16(no BOM), binary heuristics
- Flags single-byte legacy as "Legacy 8-bit (ambiguous)" (cannot reliably name cp1252)

Usage:
  pwsh .\EncodingStats2.ps1 -Root . -OutCsv .\encoding_stats.csv
#>

param(
  [string]$Root = ".",
  [string]$OutCsv = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$excludeExt = @(".dwg", ".bak")  # per your requirement
$excludeDirRegex = "\\bin\\|\\obj\\|\\\.git\\|\\\.vs\\|\\node_modules\\|\\packages\\|\\TestResults\\"

function Classify-Encoding([byte[]]$bytes) {
  if ($bytes.Length -eq 0) { return @{Kind="Empty"; IsText=$true; Detail=""} }

  # BOMs
  if ($bytes.Length -ge 3 -and $bytes[0]-eq 0xEF -and $bytes[1]-eq 0xBB -and $bytes[2]-eq 0xBF) {
    return @{Kind="UTF-8 (BOM)"; IsText=$true; Detail="EF BB BF"}
  }
  if ($bytes.Length -ge 4 -and $bytes[0]-eq 0xFF -and $bytes[1]-eq 0xFE -and $bytes[2]-eq 0x00 -and $bytes[3]-eq 0x00) {
    return @{Kind="UTF-32 LE (BOM)"; IsText=$true; Detail="FF FE 00 00"}
  }
  if ($bytes.Length -ge 4 -and $bytes[0]-eq 0x00 -and $bytes[1]-eq 0x00 -and $bytes[2]-eq 0xFE -and $bytes[3]-eq 0xFF) {
    return @{Kind="UTF-32 BE (BOM)"; IsText=$true; Detail="00 00 FE FF"}
  }
  if ($bytes.Length -ge 2 -and $bytes[0]-eq 0xFF -and $bytes[1]-eq 0xFE) {
    return @{Kind="UTF-16 LE (BOM)"; IsText=$true; Detail="FF FE"}
  }
  if ($bytes.Length -ge 2 -and $bytes[0]-eq 0xFE -and $bytes[1]-eq 0xFF) {
    return @{Kind="UTF-16 BE (BOM)"; IsText=$true; Detail="FE FF"}
  }

  # Binary / UTF-16(no BOM) heuristics
  $sampleLen = [Math]::Min($bytes.Length, 4096)
  $nul = 0
  $ctrl = 0
  for ($i=0; $i -lt $sampleLen; $i++) {
    $b = $bytes[$i]
    if ($b -eq 0) { $nul++ }
    if ($b -lt 0x20 -and $b -ne 9 -and $b -ne 10 -and $b -ne 13) { $ctrl++ }
  }

  if ($nul -gt 0) {
    # likely UTF-16 w/o BOM if NULs cluster on one parity
    $evenNul = 0; $oddNul = 0
    for ($i=0; $i -lt $sampleLen; $i++) {
      if (($i % 2) -eq 0 -and $bytes[$i] -eq 0) { $evenNul++ }
      if (($i % 2) -eq 1 -and $bytes[$i] -eq 0) { $oddNul++ }
    }
    $ratioEven = $evenNul / [double]$sampleLen
    $ratioOdd  = $oddNul  / [double]$sampleLen
    if ($ratioEven -gt 0.20 -or $ratioOdd -gt 0.20) {
      return @{Kind="Likely UTF-16 (no BOM)"; IsText=$true; Detail="evenNulRatio=$ratioEven oddNulRatio=$ratioOdd"}
    }
    return @{Kind="Binary/Unknown (NUL bytes)"; IsText=$false; Detail="nulCount=$nul in first $sampleLen bytes"}
  }

  if (($ctrl / [double]$sampleLen) -gt 0.10) {
    return @{Kind="Binary/Unknown (control chars)"; IsText=$false; Detail="ctrlCount=$ctrl in first $sampleLen bytes"}
  }

  # Strict UTF-8 validation
  try {
    $utf8Strict = New-Object System.Text.UTF8Encoding($false, $true)
    [void]$utf8Strict.GetString($bytes)
    return @{Kind="UTF-8 (no BOM)"; IsText=$true; Detail="valid UTF-8"}
  } catch {
    # Single-byte legacy is ambiguous: cannot safely assert "Windows-1252"
    return @{Kind="Legacy 8-bit (ambiguous)"; IsText=$true; Detail="invalid UTF-8; could be cp1252/others"}
  }
}

$rootFull = (Resolve-Path $Root).Path

$files = Get-ChildItem -LiteralPath $rootFull -Recurse -File -Force |
  Where-Object {
    $_.FullName -notmatch $excludeDirRegex -and
    ($excludeExt -notcontains $_.Extension.ToLowerInvariant())
  }

$results = foreach ($f in $files) {
  $bytes = $null
  try { $bytes = [System.IO.File]::ReadAllBytes($f.FullName) }
  catch {
    [pscustomobject]@{ Path=$f.FullName; Extension=$f.Extension.ToLowerInvariant(); SizeBytes=$f.Length; Kind="Unreadable"; Detail=$_.Exception.Message; IsText=$false }
    continue
  }

  $c = Classify-Encoding $bytes
  [pscustomobject]@{
    Path = $f.FullName
    Extension = $f.Extension.ToLowerInvariant()
    SizeBytes = $f.Length
    Kind = $c.Kind
    Detail = $c.Detail
    IsText = $c.IsText
  }
}

$totalFiles = $results.Count
$totalBytes = ($results | Measure-Object SizeBytes -Sum).Sum

Write-Host ""
Write-Host "Root: $rootFull"
Write-Host ("Files scanned (excluding {0}): {1:n0} | Total size: {2:n0} bytes" -f ($excludeExt -join ","), $totalFiles, $totalBytes)
Write-Host ""

$byKind = $results | Group-Object Kind | ForEach-Object {
  $sumBytes = ($_.Group | Measure-Object SizeBytes -Sum).Sum
  [pscustomobject]@{
    Kind = $_.Name
    Files = $_.Count
    Bytes = $sumBytes
    PctFiles = [Math]::Round(($_.Count / [double]$totalFiles) * 100, 2)
    PctBytes = if ($totalBytes -gt 0) { [Math]::Round(($sumBytes / [double]$totalBytes) * 100, 2) } else { 0 }
  }
} | Sort-Object Files -Descending

$byKind | Format-Table -AutoSize

Write-Host ""
Write-Host "Files that are NOT UTF-8 (no BOM):"
$results |
  Where-Object { $_.Kind -ne "UTF-8 (no BOM)" } |
  Select-Object Kind, SizeBytes, Path |
  Sort-Object Kind, SizeBytes -Descending |
  Format-Table -AutoSize

if ($OutCsv -and $OutCsv.Trim().Length -gt 0) {
  $dir = Split-Path -Parent $OutCsv
  if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }
  $results | Export-Csv -NoTypeInformation -Encoding UTF8 -Path $OutCsv
  Write-Host ""
  Write-Host "Wrote per-file results to: $OutCsv"
}