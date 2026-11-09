param(
    [string]$GameDir = 'D:\Steam\steamapps\common\Probably Stolen',
    [string]$DependencyDir = ''
)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$managed = Join-Path $GameDir 'Probably Stolen_Data/Managed'
$dotnet = (Get-Command dotnet).Source
$sdk = & $dotnet --version
$csc = Join-Path (Split-Path $dotnet) "sdk/$sdk/Roslyn/bincore/csc.dll"
$version = ([xml](Get-Content mod/manifest.xml -Raw)).Manifest.ModVersion
$out = Join-Path $PSScriptRoot 'dist/workshop/MobileCasher'
$web = Join-Path $PSScriptRoot 'dist/build/phone'
$release = Join-Path $PSScriptRoot "dist/release/$version"
$install = Join-Path $release 'Mods/MobileCasher'
New-Item -Force -ItemType Directory $out,"$web/assets",$install | Out-Null
if (!$DependencyDir) {
    $steamapps = Split-Path (Split-Path $GameDir)
    $DependencyDir = Join-Path $steamapps 'workshop/content/4348910/3831162516'
}
$dependencyDll = Join-Path $DependencyDir 'AnnounceTheTransaction.dll'
# Only names are embedded. Audio remains in the installed prerequisite's folder.
$tokens = Get-ChildItem -LiteralPath (Join-Path $DependencyDir 'Sounds') -Filter '*.mp3' -File |
    Sort-Object Name | ForEach-Object BaseName
[IO.File]::WriteAllText("$web/sound-tokens.txt", ($tokens -join "`n"), [Text.UTF8Encoding]::new($false))

$qrLibrary = Join-Path $out 'QrCodeGenerator.dll'
$qrSources = (Get-ChildItem "$PSScriptRoot/third_party/QrCodeGenerator/*.cs").FullName
$qrRefs = 'mscorlib','System','System.Core' | ForEach-Object { '/r:' + (Join-Path $managed "$_.dll") }
& $dotnet $csc /nologo /noconfig /nostdlib+ /target:library /optimize+ "/out:$qrLibrary" $qrRefs $qrSources
if ($LASTEXITCODE -ne 0) { throw '二维码核心库编译失败' }

Copy-Item preview/receipt.css,preview/receipt.js,mod/live.js,mod/live.css $web
Copy-Item preview/assets/transfer-rounded.ttf,mod/unknown.svg "$web/assets"
$html = Get-Content -Raw -Encoding utf8 preview/index.html
$html = $html.Replace('<html lang="zh-CN">','<html lang="zh-CN" data-live>')
$html = $html.Replace('<main>','<link rel="stylesheet" href="live.css"><main><section id="connection-panel"><h1 id="waiting">手机收款</h1><p id="browser-guide" hidden></p><p id="link-label">连接链接</p><a id="connection-link"></a><button id="copy-link" type="button">复制连接链接</button><p id="copy-status" role="status"></p><button id="use-embedded" type="button" hidden>继续使用当前客户端</button><button id="connect">开始收款</button></section><p id="connection-status" role="status"></p>')
$html = $html.Replace('<div class="stage">','<div class="stage" hidden>')
$html = $html.Replace('<div class="money-slip">','<div class="money-slip"><span id="live-sender"></span>')
$html = $html.Replace('</html>','<script src="live.js"></script></html>')
[IO.File]::WriteAllText("$web/index.html", $html)
$resourceFiles = 'index.html','receipt.css','receipt.js','live.css','live.js','sound-tokens.txt','assets/unknown.svg','assets/transfer-rounded.ttf'
$resourceArgs = $resourceFiles | ForEach-Object {
    '/resource:' + (Join-Path $web $_) + ',MobileCasher.Phone.' + $_.Replace('/', '.')
}

$refs = 'mscorlib','System','System.Core','netstandard','Assembly-CSharp','0Harmony','UnityEngine.CoreModule','UnityEngine.UI','UnityEngine.TextRenderingModule','UnityEngine.IMGUIModule','UnityEngine.ImageConversionModule','UnityEngine.JSONSerializeModule'
$refArgs = $refs | ForEach-Object { '/r:' + (Join-Path $managed "$_.dll") }
$refArgs += '/r:' + $qrLibrary
$refArgs += '/r:' + $dependencyDll
$sources = (Get-ChildItem "$PSScriptRoot/mod/*.cs").FullName
& $dotnet $csc /nologo /noconfig /nostdlib+ /target:library /optimize+ "/out:$out/MobileCasher.dll" $refArgs $resourceArgs $sources
if ($LASTEXITCODE -ne 0) { throw '编译失败' }

Copy-Item mod/manifest.xml,lib/QrCodeGenerator-LICENSE.txt,preview/assets/Chiron-GoRound-LICENSE.txt $out
Copy-Item "$out/MobileCasher.dll","$out/manifest.xml","$out/QrCodeGenerator.dll","$out/QrCodeGenerator-LICENSE.txt","$out/Chiron-GoRound-LICENSE.txt" $install
Copy-Item mod/MobileCasher-使用说明.txt,README.md,workshop-description.txt $release
Compress-Archive -Path "$release/*" -DestinationPath "dist/MobileCasher-v$version.zip" -Force
Copy-Item "dist/MobileCasher-v$version.zip" dist/MobileCasher.zip -Force
Write-Output "工坊内容目录：$out"
Write-Output "本地安装包：dist/MobileCasher-v$version.zip"
