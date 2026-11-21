# HP MCP Revit - bo cai dat tu dong cho Windows
# Cong dung : 1) Tim cac ban Revit da cai (2017 tro len)
#             2) Chep add-in + file .addin vao %APPDATA%\Autodesk\Revit\Addins\<nam> cua tung ban
#             3) Cai MCP server (Python) vao %LOCALAPPDATA%\HP-MCP-Revit
#             4) Dang ky MCP ten "hp-revit" voi Claude Code va Claude Desktop
# Cach dung : bam dup CaiDat.bat, hoac:
#             powershell -ExecutionPolicy Bypass -File installer\install.ps1 [-ChiAddin] [-BoQuaClaude] [-GoCai] [-Phienban 2025]
# Ngay viet : 2026-11-21
param(
    [switch]$ChiAddin,     # chi chep add-in, khong cai server / khong dang ky Claude
    [switch]$BoQuaClaude,  # cai add-in + server nhung khong dang ky voi Claude
    [switch]$GoCai,        # go add-in khoi cac ban Revit va bo dang ky MCP
    [string[]]$Phienban    # them nam phien ban tay neu khong tu tim thay, vd: -Phienban 2024,2025
)
$ErrorActionPreference = 'Continue'   # loi nghiem trong dung 'throw' ben duoi
$Root    = Split-Path -Parent $PSScriptRoot
$Dest    = Join-Path $env:LOCALAPPDATA 'HP-MCP-Revit'
$Stamp   = Get-Date -Format 'yyMMdd-HHmmss'
$McpName = 'hp-revit'
$AddinId = 'b6f1c3a2-7d44-4e0f-9a61-5c2e8d0f1a77'

function Say($m, $c = 'Gray') { Write-Host $m -ForegroundColor $c }

# ---------- 1. Tim cac ban Revit (chi nhan ban co Revit.exe that) ----------
function Find-RevitYears {
    $years = @{}
    foreach ($root in @("$env:ProgramFiles\Autodesk", "${env:ProgramFiles(x86)}\Autodesk")) {
        if ($root -and (Test-Path $root)) {
            Get-ChildItem $root -Directory | ForEach-Object {
                if ($_.Name -match '^(Autodesk )?Revit (20\d\d)$' -and (Test-Path (Join-Path $_.FullName 'Revit.exe'))) { $years[$Matches[2]] = $_.FullName }
            }
        }
    }
    foreach ($y in $Phienban) { $years["$y"] = 'chi dinh tay' }
    $years.Keys | Where-Object { [int]$_ -ge 2017 } | Sort-Object
}

$years = @(Find-RevitYears)
if ($years.Count -eq 0) { Say 'Khong tim thay Revit 2017 tro len. Cai Revit truoc, hoac dung -Phienban 2024.' Yellow }
else { Say ('Tim thay Revit: ' + ($years -join ', ')) Cyan }

foreach ($y in $years) {
    $addDir = Join-Path $env:APPDATA "Autodesk\Revit\Addins\$y"
    $dllSrc = Join-Path $Root "dist\addin\$y\HPRevitMcp.dll"
    $dllDir = Join-Path $addDir 'HPRevitMcp'
    $manifest = Join-Path $addDir 'HPRevitMcp.addin'
    if ($GoCai) {
        if (Test-Path $manifest) { Remove-Item $manifest -Force }
        if (Test-Path $dllDir)   { Remove-Item $dllDir -Recurse -Force }
        Say "  Revit $y : da go add-in" Green
        continue
    }
    if (-not (Test-Path $dllSrc)) { Say "  Revit $y : thieu $dllSrc (bien dich bang: dotnet build addin -c Release -p:RevitYear=$y)" Yellow; continue }
    New-Item -ItemType Directory -Force $addDir | Out-Null
    if ((Test-Path $manifest) -or (Test-Path $dllDir)) {
        $bk = Join-Path $Dest "_backup\$Stamp\Revit $y"
        New-Item -ItemType Directory -Force $bk | Out-Null
        if (Test-Path $manifest) { Copy-Item $manifest $bk -Force }
        if (Test-Path $dllDir)   { Copy-Item $dllDir $bk -Recurse -Force }
        Say "  Revit $y : da sao luu ban cu vao $bk"
    }
    New-Item -ItemType Directory -Force $dllDir | Out-Null
    Copy-Item $dllSrc $dllDir -Force
    Unblock-File (Join-Path $dllDir 'HPRevitMcp.dll') -ErrorAction SilentlyContinue
    $xml = @"
<?xml version="1.0" encoding="utf-8"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>HP Revit MCP</Name>
    <Assembly>HPRevitMcp\HPRevitMcp.dll</Assembly>
    <AddInId>$AddinId</AddInId>
    <FullClassName>HPRevitMcp.App</FullClassName>
    <VendorId>HPNP</VendorId>
    <VendorDescription>Nguyen Huu Phuc - HP MCP Revit</VendorDescription>
  </AddIn>
</RevitAddIns>
"@
    [IO.File]::WriteAllText($manifest, $xml, (New-Object Text.UTF8Encoding $false))
    Say "  Revit $y : da chep add-in vao $addDir" Green
}

# ---------- Claude ----------
function Find-ClaudeCli {
    $c = Get-Command claude -ErrorAction SilentlyContinue
    if ($c) { return $c.Source }
    $cands = @()
    foreach ($base in @("$env:APPDATA\Claude\claude-code", "$env:LOCALAPPDATA\Packages\Claude_pzs8sxrjxfjjc\LocalCache\Roaming\Claude\claude-code", "$env:USERPROFILE\.local\bin")) {
        if (Test-Path $base) { $cands += Get-ChildItem $base -Recurse -Filter 'claude.exe' -ErrorAction SilentlyContinue }
    }
    if ($cands.Count) { return ($cands | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName }
    return $null
}

function Set-DesktopConfig($cfgPath, $py, $srv, [switch]$Remove) {
    $dir = Split-Path $cfgPath
    if (-not (Test-Path $dir)) { return $false }
    if (Test-Path $cfgPath) {
        Copy-Item $cfgPath "$cfgPath.bak-$Stamp" -Force
        $raw = [IO.File]::ReadAllText($cfgPath)
        $cfg = if ($raw.Trim()) { $raw | ConvertFrom-Json } else { New-Object psobject }
    } else { $cfg = New-Object psobject }
    if (-not $cfg.PSObject.Properties['mcpServers']) { $cfg | Add-Member mcpServers (New-Object psobject) }
    if ($cfg.mcpServers.PSObject.Properties[$McpName]) { $cfg.mcpServers.PSObject.Properties.Remove($McpName) }
    if (-not $Remove) { $cfg.mcpServers | Add-Member $McpName ([pscustomobject]@{ command = $py; args = @($srv) }) }
    [IO.File]::WriteAllText($cfgPath, ($cfg | ConvertTo-Json -Depth 20), (New-Object Text.UTF8Encoding $false))
    return $true
}

$desktopCfgs = @("$env:APPDATA\Claude\claude_desktop_config.json",
                 "$env:LOCALAPPDATA\Packages\Claude_pzs8sxrjxfjjc\LocalCache\Roaming\Claude\claude_desktop_config.json")

if ($GoCai) {
    $cli = Find-ClaudeCli
    if ($cli) { & $cli mcp remove --scope user $McpName 2>$null | Out-Null; Say 'Da bo dang ky MCP trong Claude Code' Green }
    foreach ($p in $desktopCfgs) { if (Test-Path $p) { Set-DesktopConfig $p '' '' -Remove | Out-Null; Say "Da bo muc $McpName trong $p" Green } }
    Say "Thu muc server $Dest khong bi xoa; anh tu xoa neu muon." Yellow
    return
}
if ($ChiAddin) { Say 'Xong (chi chep add-in).' Green; return }

# ---------- 3. Cai MCP server ----------
New-Item -ItemType Directory -Force $Dest | Out-Null
Copy-Item (Join-Path $Root 'server\server.py') $Dest -Force
Copy-Item (Join-Path $Root 'server\requirements.txt') $Dest -Force
$venv = Join-Path $Dest '.venv'
$py   = Join-Path $venv 'Scripts\python.exe'
$srv  = Join-Path $Dest 'server.py'
if (-not (Test-Path $py)) {
    $uv = Get-Command uv -ErrorAction SilentlyContinue
    if ($uv) { Say 'Tao moi truong Python bang uv...'; & uv venv --python 3.12 $venv | Out-Null }
    else {
        $sys = Get-Command python -ErrorAction SilentlyContinue
        if (-not $sys) { throw 'Chua co Python 3.10+ hoac uv. Cai Python tu python.org (tich "Add python.exe to PATH") roi chay lai.' }
        Say 'Tao moi truong Python bang python -m venv...'
        & $sys.Source -m venv $venv
    }
}
if (-not (Test-Path $py)) { throw "Khong tao duoc $py" }
Say 'Cai thu vien mcp...'
$uv = Get-Command uv -ErrorAction SilentlyContinue
if ($uv) { & uv pip install --python $py -r (Join-Path $Dest 'requirements.txt') | Out-Null }
else     { & $py -m pip install --disable-pip-version-check -q -r (Join-Path $Dest 'requirements.txt') }
& $py -c "import sys; assert sys.version_info >= (3,10), 'Can Python 3.10+'; import mcp.server.fastmcp; print('  Python', sys.version.split()[0], '- thu vien mcp OK')"
if ($LASTEXITCODE -ne 0) { throw 'Kiem tra thu vien mcp that bai.' }
Say "  Server: $srv" Green

if ($BoQuaClaude) { Say 'Xong (khong dang ky Claude).' Green; return }

# ---------- 4. Dang ky voi Claude ----------
$cli = Find-ClaudeCli
if ($cli) {
    & $cli mcp remove --scope user $McpName 2>$null | Out-Null
    & $cli mcp add --scope user $McpName -- $py $srv
    Say "Claude Code: da dang ky MCP '$McpName' ($cli)" Green
} else { Say 'Khong thay Claude Code (lenh claude). Bo qua buoc nay.' Yellow }
$didDesktop = $false
foreach ($p in $desktopCfgs) { if (Set-DesktopConfig $p $py $srv) { Say "Claude Desktop: da ghi muc '$McpName' vao $p" Green; $didDesktop = $true } }
if (-not $didDesktop) { Say 'Khong thay Claude Desktop. Bo qua.' Yellow }

Say ''
Say 'HOAN TAT. Viec tiep theo:' Cyan
Say '  1. Mo Revit. Neu hien hop thoai bao add-in khong co chu ky: chon "Always Load".'
Say '  2. Khoi dong lai Claude (Desktop/Code), hoi: "kiem tra ket noi Revit".'
