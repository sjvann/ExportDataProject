#Requires -Version 5.1
<#
  發布析庫並做出 Windows、Linux、macOS 安裝檔。
  用法：powershell -File installer/build-installers.ps1
#>
param(
  [string]$Version = "0.1.0",
  [ValidateSet("all", "win", "linux", "osx")]
  [string]$Target = "all"
)

$ErrorActionPreference = "Stop"
$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
Set-Location $Root
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_NOLOGO = "1"

$Iscc = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
$Assets = Join-Path $Root "artifacts\installer-assets"
$PublishRoot = Join-Path $Root "artifacts\publish"
$StageRoot = Join-Path $Root "artifacts\stage"
$OutDir = Join-Path $Root "artifacts\installers"
$Tmp = Join-Path $Root "artifacts\tmp"
New-Item -ItemType Directory -Force -Path $Assets, $OutDir, $Tmp | Out-Null

function Invoke-Native {
  param([Parameter(Mandatory = $true)][string]$File, [string[]]$ArgumentList)
  & $File @ArgumentList
  if ($LASTEXITCODE -ne 0) {
    throw "$File 失敗，結束代碼 $LASTEXITCODE"
  }
}

function Convert-ToWslPath {
  param([string]$Path)
  $full = [IO.Path]::GetFullPath($Path) -replace '\\', '/'
  if ($full -match '^([A-Za-z]):(.*)$') {
    return "/mnt/$($Matches[1].ToLower())$($Matches[2])"
  }
  return $full
}

function Copy-UnixFile {
  param([string]$Source, [string]$Destination)
  $text = [IO.File]::ReadAllText($Source)
  $text = $text -replace "`r`n", "`n" -replace "`r", "`n"
  if (-not $text.EndsWith("`n")) { $text += "`n" }
  $utf8 = New-Object System.Text.UTF8Encoding $false
  $dir = Split-Path $Destination
  if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
  [IO.File]::WriteAllText($Destination, $text, $utf8)
}

function Write-UsageText {
  param([string]$Path, [string]$Platform)
  $text = @"
析庫 $Version

這是裝在自己電腦上的舊系統資料庫工具。
開啟「析庫」後，瀏覽器會打開 http://127.0.0.1:5107 。

停止方式：
- Windows：關掉標題為「析庫」的主控台視窗。
- Linux：執行 exportdata-workbench stop
- macOS：在活動監視器結束 ExportDataWeb。

命令列匯出：
- Windows 開始功能表「析庫命令列」
- Linux 與 macOS 終端機執行 exportdata --interactive

此安裝包：$Platform
Apple 晶片（M 系列）請先安裝 Rosetta：
  softwareupdate --install-rosetta --agree-to-license
"@
  $utf8 = New-Object System.Text.UTF8Encoding $false
  $text = $text -replace "`r`n", "`n"
  [IO.File]::WriteAllText($Path, $text.Trim() + "`n", $utf8)
}

function New-AppIcons {
  Add-Type -AssemblyName System.Drawing
  $navy = [System.Drawing.Color]::FromArgb(255, 30, 58, 95)
  $ink = [System.Drawing.Color]::FromArgb(255, 248, 250, 252)
  $green = [System.Drawing.Color]::FromArgb(255, 5, 150, 105)

  function Add-RoundRect([System.Drawing.Drawing2D.GraphicsPath]$Path, [single]$X, [single]$Y, [single]$W, [single]$H, [single]$R) {
    $d = [Math]::Min($R * 2, [Math]::Min($W, $H))
    $Path.AddArc($X, $Y, $d, $d, 180, 90) | Out-Null
    $Path.AddArc(($X + $W - $d), $Y, $d, $d, 270, 90) | Out-Null
    $Path.AddArc(($X + $W - $d), ($Y + $H - $d), $d, $d, 0, 90) | Out-Null
    $Path.AddArc($X, ($Y + $H - $d), $d, $d, 90, 90) | Out-Null
    $Path.CloseFigure()
  }

  function New-Mark([int]$Size) {
    $bmp = New-Object System.Drawing.Bitmap $Size, $Size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)
    $scale = $Size / 32.0
    $tile = New-Object System.Drawing.Drawing2D.GraphicsPath
    Add-RoundRect $tile 0 0 ($Size - 1) ($Size - 1) (8 * $scale)
    $brush = New-Object System.Drawing.SolidBrush $navy
    $g.FillPath($brush, $tile)
    $pen = New-Object System.Drawing.Pen $ink, ([single](1.75 * $scale))
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Square
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Square
    $g.DrawLine($pen, (8 * $scale), (12 * $scale), (24 * $scale), (12 * $scale))
    $g.DrawLine($pen, (8 * $scale), (20 * $scale), (24 * $scale), (20 * $scale))
    $g.DrawLine($pen, (16 * $scale), (8 * $scale), (16 * $scale), (20 * $scale))
    $cell = New-Object System.Drawing.Drawing2D.GraphicsPath
    Add-RoundRect $cell (17.15 * $scale) (21.15 * $scale) (6.7 * $scale) (6.7 * $scale) (1.4 * $scale)
    $gbrush = New-Object System.Drawing.SolidBrush $green
    $g.FillPath($gbrush, $cell)
    $g.Dispose(); $brush.Dispose(); $pen.Dispose(); $gbrush.Dispose(); $tile.Dispose(); $cell.Dispose()
    return $bmp
  }

  function Get-PngBytes([System.Drawing.Bitmap]$Bitmap) {
    $ms = New-Object IO.MemoryStream
    $Bitmap.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bytes = $ms.ToArray()
    $ms.Dispose()
    return ,$bytes
  }

  $sizes = @(16, 32, 48, 128, 256, 512)
  $bitmaps = @{}
  foreach ($size in $sizes) { $bitmaps[$size] = New-Mark $size }
  $bitmaps[256].Save((Join-Path $Assets "icon-256.png"), [System.Drawing.Imaging.ImageFormat]::Png)
  $bitmaps[128].Save((Join-Path $Assets "icon-128.png"), [System.Drawing.Imaging.ImageFormat]::Png)
  $bitmaps[512].Save((Join-Path $Assets "icon-512.png"), [System.Drawing.Imaging.ImageFormat]::Png)

  $ico = Join-Path $Assets "icon.ico"
  $pngs = @(
    (Get-PngBytes $bitmaps[16]),
    (Get-PngBytes $bitmaps[32]),
    (Get-PngBytes $bitmaps[48]),
    (Get-PngBytes $bitmaps[256])
  )
  $fs = [IO.File]::Create($ico)
  $bw = New-Object IO.BinaryWriter $fs
  $bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$pngs.Count)
  $offset = 6 + (16 * $pngs.Count)
  $icoSizes = @(16, 32, 48, 256)
  for ($i = 0; $i -lt $pngs.Count; $i++) {
    $side = $icoSizes[$i]
    $bw.Write([byte]($(if ($side -ge 256) { 0 } else { $side })))
    $bw.Write([byte]($(if ($side -ge 256) { 0 } else { $side })))
    $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32)
    $bw.Write([uint32]$pngs[$i].Length)
    $bw.Write([uint32]$offset)
    $offset += $pngs[$i].Length
  }
  foreach ($png in $pngs) { $bw.Write($png) }
  $bw.Dispose(); $fs.Dispose()

  $icns = Join-Path $Assets "AppIcon.icns"
  $chunks = New-Object System.Collections.Generic.List[byte]
  function Add-IcnsChunk([string]$Type, [byte[]]$Data) {
    $chunks.AddRange([Text.Encoding]::ASCII.GetBytes($Type))
    $len = [uint32](8 + $Data.Length)
    $lenBytes = [BitConverter]::GetBytes($len)
    if ([BitConverter]::IsLittleEndian) { [Array]::Reverse($lenBytes) }
    $chunks.AddRange($lenBytes)
    $chunks.AddRange($Data)
  }
  Add-IcnsChunk "ic07" (Get-PngBytes $bitmaps[128])
  Add-IcnsChunk "ic08" (Get-PngBytes $bitmaps[256])
  Add-IcnsChunk "ic09" (Get-PngBytes $bitmaps[512])
  $file = New-Object System.Collections.Generic.List[byte]
  $file.AddRange([Text.Encoding]::ASCII.GetBytes("icns"))
  $total = [uint32](8 + $chunks.Count)
  $totalBytes = [BitConverter]::GetBytes($total)
  if ([BitConverter]::IsLittleEndian) { [Array]::Reverse($totalBytes) }
  $file.AddRange($totalBytes)
  $file.AddRange($chunks)
  [IO.File]::WriteAllBytes($icns, $file.ToArray())
  foreach ($bmp in $bitmaps.Values) { $bmp.Dispose() }
}

function Publish-Rid {
  param([string]$Rid)
  $webOut = Join-Path $PublishRoot "$Rid\web"
  $cliOut = Join-Path $PublishRoot "$Rid\cli"
  Write-Host "發布工作台與命令列：$Rid"
  Invoke-Native "dotnet" @(
    "publish", "ExportDataWeb/ExportDataWeb.csproj",
    "-c", "Release", "-r", $Rid, "--self-contained", "true",
    "-o", $webOut,
    "-p:DebugType=none", "-p:DebugSymbols=false",
    "-p:ErrorOnDuplicatePublishOutputFiles=false",
    "-p:SatelliteResourceLanguages=en%3Bzh-Hant"
  )
  Copy-Item (Join-Path $Root "ExportDataWeb\appsettings.json") (Join-Path $webOut "appsettings.json") -Force
  $devSettings = Join-Path $Root "ExportDataWeb\appsettings.Development.json"
  if (Test-Path (Join-Path $webOut "appsettings.Development.json")) {
    Copy-Item $devSettings (Join-Path $webOut "appsettings.Development.json") -Force
  }
  Invoke-Native "dotnet" @(
    "publish", "ExportData/ExportData.csproj",
    "-c", "Release", "-r", $Rid, "--self-contained", "true",
    "-o", $cliOut,
    "-p:DebugType=none", "-p:DebugSymbols=false",
    "-p:SatelliteResourceLanguages=en%3Bzh-Hant"
  )
  $native = Get-ChildItem $webOut -Recurse -Filter "SQLite.Interop.dll" -ErrorAction SilentlyContinue
  if (-not $native) { throw "$Rid 的發布結果沒有 SQLite.Interop.dll" }
}

function Test-WindowsPublish {
  $web = Join-Path $PublishRoot "win-x64\web\ExportDataWeb.exe"
  $cli = Join-Path $PublishRoot "win-x64\cli\ExportData.exe"
  $log = Join-Path $Tmp "win-cli.log"
  $logErr = Join-Path $Tmp "win-cli.err"
  Write-Host "測試 Windows 命令列的 SQLite"
  $cliProc = Start-Process -FilePath $cli -ArgumentList "--test" -WorkingDirectory (Split-Path $cli) -Wait -PassThru -WindowStyle Hidden -RedirectStandardOutput $log -RedirectStandardError $logErr
  $cliText = ""
  if (Test-Path $log) { $cliText += [IO.File]::ReadAllText($log) }
  if (Test-Path $logErr) { $cliText += [IO.File]::ReadAllText($logErr) }
  if ($cliProc.ExitCode -ne 0 -or $cliText -match "SQLite 測試失敗" -or $cliText -notmatch "SQLite 測試完成") {
    throw "Windows SQLite 測試失敗。`n$cliText"
  }

  $url = "http://127.0.0.1:5199"
  $outLog = Join-Path $Tmp "win-web.log"
  Write-Host "測試 Windows 工作台 $url"
  $arg = "/c set ASPNETCORE_ENVIRONMENT=Production&& set ASPNETCORE_URLS=$url&& set DOTNET_NOLOGO=1&& `"$web`" > `"$outLog`" 2>&1"
  $proc = Start-Process -FilePath "cmd.exe" -ArgumentList $arg -WorkingDirectory (Split-Path $web) -PassThru -WindowStyle Hidden
  try {
    $deadline = (Get-Date).AddSeconds(45)
    $body = $null
    while ((Get-Date) -lt $deadline) {
      try {
        $resp = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 5
        if ($resp.StatusCode -eq 200 -and $resp.Content -match "析庫") {
          $body = $resp.Content
          break
        }
      } catch { }
      if ($proc.HasExited) { break }
      Start-Sleep -Milliseconds 500
    }
    if (-not $body) {
      $tail = ""
      if (Test-Path $outLog) { $tail = [IO.File]::ReadAllText($outLog) }
      throw "Windows 工作台沒有在 45 秒內回應。`n$tail"
    }
    if ($body -notmatch 'href="([^"]*styles\.css[^"]*)"') {
      throw "首頁沒有樣式表連結"
    }
    $href = $Matches[1]
    if ($href.StartsWith("/")) { $href = $url + $href }
    $css = Invoke-WebRequest -Uri $href -UseBasicParsing -TimeoutSec 10
    if ($css.StatusCode -ne 200) { throw "樣式表回應 $($css.StatusCode)" }
    if ($body -notmatch 'href="([^"]*app-icon[^"]*)"') {
      throw "首頁沒有圖示連結"
    }
    $iconHref = $Matches[1]
    if ($iconHref.StartsWith("/")) { $iconHref = $url + $iconHref }
    $icon = Invoke-WebRequest -Uri $iconHref -UseBasicParsing -TimeoutSec 10
    if ($icon.StatusCode -ne 200) { throw "圖示回應 $($icon.StatusCode)" }
    Write-Host "Windows 工作台回應正常"
  } finally {
    if (-not $proc.HasExited) {
      & taskkill.exe /PID $proc.Id /T /F | Out-Null
    }
  }
}

function Invoke-WslScript {
  param([string]$Script, [string[]]$Arguments)
  $name = Split-Path $Script -Leaf
  $local = Join-Path $Tmp $name
  Copy-UnixFile $Script $local
  $wslScript = Convert-ToWslPath $local
  $wslArgs = @("-e", "bash", "--noprofile", "--norc", $wslScript) + $Arguments
  & wsl.exe @wslArgs
  if ($LASTEXITCODE -ne 0) { throw "WSL $name 失敗，結束代碼 $LASTEXITCODE" }
}

function Build-WindowsInstaller {
  if (-not (Test-Path $Iscc)) { throw "找不到 Inno Setup：$Iscc" }
  $stage = Join-Path $StageRoot "win-x64"
  New-Item -ItemType Directory -Force -Path $stage | Out-Null
  $cmd = Join-Path $stage "start-workbench.cmd"
  Copy-UnixFile (Join-Path $Root "installer\launchers\windows\start-workbench.cmd") $cmd
  $cmdText = ([IO.File]::ReadAllText($cmd) -replace "`r`n", "`n" -replace "`n", "`r`n")
  $utf8Bom = New-Object System.Text.UTF8Encoding $true
  [IO.File]::WriteAllText($cmd, $cmdText, $utf8Bom)
  Write-UsageText (Join-Path $stage "使用說明.txt") "Windows x64"

  $iss = Join-Path $Tmp "xiku.iss"
  Copy-UnixFile (Join-Path $Root "installer\windows\xiku.iss") $iss
  $issText = [IO.File]::ReadAllText($iss)
  $utf8Bom = New-Object System.Text.UTF8Encoding $true
  [IO.File]::WriteAllText($iss, $issText, $utf8Bom)

  $fwd = {
    param($p)
    ([IO.Path]::GetFullPath($p) -replace '\\', '/')
  }
  Write-Host "編譯 Windows 安裝程式"
  Invoke-Native $Iscc @(
    "/DMyAppVersion=$Version",
    "/DStageDir=$(& $fwd $stage)",
    "/DWebDir=$(& $fwd (Join-Path $PublishRoot 'win-x64\web'))",
    "/DCliDir=$(& $fwd (Join-Path $PublishRoot 'win-x64\cli'))",
    "/DOutputDir=$(& $fwd $OutDir)",
    "/DIconFile=$(& $fwd (Join-Path $Assets 'icon.ico'))",
    $iss
  )

  $setup = Join-Path $OutDir "ExportData-Setup-$Version-win-x64.exe"
  if (-not (Test-Path $setup)) { throw "沒有產生 $setup" }
  $installDir = Join-Path $env:TEMP "ExportData-install-test"
  $installed = Join-Path $installDir "web\ExportDataWeb.exe"
  if (Test-Path (Join-Path $installDir "unins000.exe")) {
    Write-Host "先移除上次的測試安裝"
    Wait-Installer (Join-Path $installDir "unins000.exe") @("/VERYSILENT", "/NORESTART") $installed -UntilMissing
  }
  if (Test-Path $installDir) { Remove-Item -Recurse -Force $installDir -ErrorAction SilentlyContinue }
  $setupLog = Join-Path $Tmp "win-setup.log"
  if (Test-Path $setupLog) { Remove-Item -Force $setupLog }
  Write-Host "靜默安裝到 $installDir 做檢查"
  # 安裝程式本體會再啟動一個程序就先結束，所以要等到檔案出現。
  Wait-Installer $setup @("/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/DIR=$installDir", "/LOG=$setupLog") $installed
  if (-not (Test-Path (Join-Path $installDir "cli\ExportData.exe"))) { throw "安裝後找不到命令列" }
  if (-not (Test-Path (Join-Path $installDir "start-workbench.cmd"))) { throw "安裝後找不到啟動腳本" }
  Wait-Installer (Join-Path $installDir "unins000.exe") @("/VERYSILENT", "/NORESTART") $installed -UntilMissing
  Write-Host "Windows 安裝程式可安裝、可移除"
}

function Wait-Installer {
  param(
    [string]$Exe,
    [string[]]$ArgumentList,
    [string]$Marker,
    [switch]$UntilMissing
  )
  $proc = Start-Process -FilePath $Exe -ArgumentList $ArgumentList -PassThru
  if ($proc) { $proc.WaitForExit() }
  $deadline = (Get-Date).AddMinutes(3)
  while ((Get-Date) -lt $deadline) {
    $exists = Test-Path $Marker
    if ($UntilMissing -and -not $exists) { return }
    if (-not $UntilMissing -and $exists) { return }
    Start-Sleep -Seconds 1
  }
  throw "安裝程式沒有在時限內完成：$Exe"
}

Write-Host "產生圖示"
New-AppIcons

$wantWin = $Target -eq "all" -or $Target -eq "win"
$wantLinux = $Target -eq "all" -or $Target -eq "linux"
$wantOsx = $Target -eq "all" -or $Target -eq "osx"

if ($wantWin) {
  Publish-Rid "win-x64"
  Test-WindowsPublish
  Build-WindowsInstaller
}
if ($wantLinux) { Publish-Rid "linux-x64" }
if ($wantOsx) { Publish-Rid "osx-x64" }

if ($wantLinux) {
  Write-Host "測試 Linux 發布結果"
  $linuxStage = Join-Path $StageRoot "linux-x64"
  New-Item -ItemType Directory -Force -Path $linuxStage | Out-Null
  Write-UsageText (Join-Path $linuxStage "使用說明.txt") "Linux x64（Debian、Ubuntu）"
  Invoke-WslScript (Join-Path $Root "installer\linux\smoke.sh") @((Convert-ToWslPath $Root))
  Write-Host "組 Debian 套件"
  Invoke-WslScript (Join-Path $Root "installer\linux\assemble-deb.sh") @((Convert-ToWslPath $Root), $Version)
}

if ($wantOsx) {
  Write-Host "確認 macOS 執行檔"
  $osxExe = Join-Path $PublishRoot "osx-x64\web\ExportDataWeb"
  Invoke-WslScript (Join-Path $Root "installer\macos\check-macho.sh") @((Convert-ToWslPath $osxExe))
  $osxStage = Join-Path $StageRoot "osx-x64"
  New-Item -ItemType Directory -Force -Path $osxStage | Out-Null
  Write-UsageText (Join-Path $osxStage "使用說明.txt") "macOS x64（Apple 晶片請先安裝 Rosetta）"
  Write-Host "組 macOS 安裝程式（第一次會先準備 mkbom）"
  Invoke-WslScript (Join-Path $Root "installer\macos\assemble-pkg.sh") @((Convert-ToWslPath $Root), $Version)
}

Write-Host "安裝檔："
Get-ChildItem $OutDir -File | ForEach-Object {
  $hash = Get-FileHash $_.FullName -Algorithm SHA256
  "{0}  {1}" -f $hash.Hash, $_.Name
}
