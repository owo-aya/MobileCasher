param(
    [string]$GameDir = 'D:\Steam\steamapps\common\Probably Stolen',
    [string]$CoverFile = (Join-Path $PSScriptRoot 'output/imagegen/mobilecasher-cover-114514-v3.png')
)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
& "$PSScriptRoot/build.ps1" -GameDir $GameDir
if ($LASTEXITCODE -ne 0) { throw '构建工坊内容失败' }

$content = Join-Path $PSScriptRoot 'dist/workshop/MobileCasher'
$preview = Join-Path $content 'preview.jpg'
Add-Type -AssemblyName System.Drawing
$image = [Drawing.Image]::FromFile([IO.Path]::GetFullPath($CoverFile))
$parameters = New-Object Drawing.Imaging.EncoderParameters(1)
$quality = New-Object Drawing.Imaging.EncoderParameter([Drawing.Imaging.Encoder]::Quality, [long]85)
try {
    $parameters.Param[0] = $quality
    $encoder = [Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object MimeType -eq 'image/jpeg'
    $image.Save($preview, $encoder, $parameters)
} finally {
    $quality.Dispose()
    $parameters.Dispose()
    $image.Dispose()
}

function ConvertTo-VdfText([string]$Value) {
    $Value.Replace('\', '\\').Replace('"', '\"').Replace("`r", '').Replace("`n", '\n')
}
$vdfPath = Join-Path $PSScriptRoot 'dist/mobilecasher.vdf'
$publishedId = '0'
$visibility = '2'
if (Test-Path -LiteralPath $vdfPath) {
    $old = Get-Content -LiteralPath $vdfPath -Raw
    $idMatch = [regex]::Match($old, '"publishedfileid"\s+"(\d+)"')
    if ($idMatch.Success) { $publishedId = $idMatch.Groups[1].Value }
    $visibilityMatch = [regex]::Match($old, '"visibility"\s+"([012])"')
    if ($visibilityMatch.Success) { $visibility = $visibilityMatch.Groups[1].Value }
}
$contentText = ConvertTo-VdfText $content
$previewText = ConvertTo-VdfText $preview
$description = ConvertTo-VdfText (Get-Content workshop-description.txt -Raw -Encoding utf8)
$version = ([xml](Get-Content mod/manifest.xml -Raw)).Manifest.ModVersion
$vdf = @"
"workshopitem"
{
    "appid"            "4348910"
    "publishedfileid"  "$publishedId"
    "contentfolder"    "$contentText"
    "previewfile"      "$previewText"
    "visibility"       "$visibility"
    "title"            "手机收款 / MobileCasher"
    "description"      "$description"
    "changenote"       "MobileCasher $version"
}
"@
[IO.File]::WriteAllText($vdfPath, $vdf)
Write-Output "上传配置：$vdfPath"
