param([string]$GameDir = 'D:\Steam\steamapps\common\Probably Stolen')
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$managed = Join-Path $GameDir 'Probably Stolen_Data/Managed'
$dotnet = (Get-Command dotnet).Source
$sdk = & $dotnet --version
$csc = Join-Path (Split-Path $dotnet) "sdk/$sdk/Roslyn/bincore/csc.dll"
$package = Join-Path $PSScriptRoot 'dist/package'
$out = Join-Path $package 'Mods/MobileCasher'
$web = Join-Path $package 'MobileCasherPhone'
New-Item -Force -ItemType Directory $out,"$web/assets" | Out-Null
$qrLibrary = Join-Path $PSScriptRoot 'lib/QrCodeGenerator.dll'
if (-not (Test-Path -LiteralPath $qrLibrary)) {
    $qrPackage = Join-Path $PSScriptRoot 'lib/QrCodeGenerator.2.0.6.nupkg'
    Invoke-WebRequest -UseBasicParsing -Uri 'https://api.nuget.org/v3-flatcontainer/net.codecrete.qrcodegenerator/2.0.6/net.codecrete.qrcodegenerator.2.0.6.nupkg' -OutFile $qrPackage
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($qrPackage)
    try {
        $entry = $archive.GetEntry('lib/netstandard2.0/QrCodeGenerator.dll')
        [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $qrLibrary, $true)
    } finally { $archive.Dispose() }
}
$refs = 'mscorlib','System','System.Core','netstandard','Assembly-CSharp','0Harmony','UnityEngine.CoreModule','UnityEngine.UI','UnityEngine.TextRenderingModule','UnityEngine.IMGUIModule','UnityEngine.ImageConversionModule','UnityEngine.JSONSerializeModule'
$refArgs = $refs | ForEach-Object { '/r:' + (Join-Path $managed "$_.dll") }
$refArgs += '/r:' + $qrLibrary
$sources = (Get-ChildItem "$PSScriptRoot/mod/*.cs").FullName
& $dotnet $csc /nologo /noconfig /nostdlib+ /target:library /optimize+ "/out:$out/MobileCasher.dll" $refArgs $sources
if ($LASTEXITCODE -ne 0) { throw "编译失败" }
Copy-Item mod/manifest.xml $out
Copy-Item lib/QrCodeGenerator.dll,lib/QrCodeGenerator-LICENSE.txt $out
Copy-Item preview/receipt.css,preview/receipt.js,mod/live.js,mod/live.css $web
Copy-Item preview/assets/transfer-rounded.ttf,preview/assets/Chiron-GoRound-LICENSE.txt,mod/unknown.svg "$web/assets"
$html = Get-Content -Raw -Encoding utf8 preview/index.html
$html = $html.Replace('<html lang="zh-CN">','<html lang="zh-CN" data-live>')
$html = $html.Replace('<main>','<link rel="stylesheet" href="live.css"><main><section id="connection-panel"><h1 id="waiting">手机收款</h1><p id="browser-guide" hidden></p><p id="link-label">连接链接</p><a id="connection-link"></a><button id="copy-link" type="button">复制连接链接</button><p id="copy-status" role="status"></p><button id="use-embedded" type="button" hidden>继续使用当前客户端</button><button id="connect">开始收款</button></section><p id="connection-status" role="status"></p>')
$html = $html.Replace('<div class="stage">','<div class="stage" hidden>')
$html = $html.Replace('<div class="money-slip">','<div class="money-slip"><span id="live-sender"></span>')
$html = $html.Replace('</html>','<script src="live.js"></script></html>')
[IO.File]::WriteAllText("$web/index.html",$html)
Copy-Item mod/MobileCasher-使用说明.txt $package
Compress-Archive -Path "$package/*" -DestinationPath 'dist/MobileCasher.zip' -Force
Write-Output "dist/MobileCasher.zip"
