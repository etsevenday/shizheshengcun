[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$Destination)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$catalog = Get-Content -LiteralPath (Join-Path $PSScriptRoot '全量视觉清单.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$groups = @{ actor = '人物立绘'; enemy = '敌人战斗图'; dungeon = '地牢背景' }
$target = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Destination)
foreach ($subfolder in @($groups.Values) + @('人物肖像')) {
    $null = New-Item -ItemType Directory -Path (Join-Path $target $subfolder) -Force
}
function Write-Crop {
    param([Drawing.Bitmap]$Image, [object[]]$Crop, [string]$Path)
    if (Test-Path -LiteralPath $Path) { throw "输出文件已存在：$Path" }
    $rect = [Drawing.Rectangle]::new([int]$Crop[0],[int]$Crop[1],[int]$Crop[2],[int]$Crop[3])
    $bitmap = $Image.Clone($rect,$Image.PixelFormat)
    try { $bitmap.Save($Path,[Drawing.Imaging.ImageFormat]::Png) }
    finally { $bitmap.Dispose() }
}
foreach ($entry in $catalog.assets) {
    if (!$groups.ContainsKey($entry.category)) { continue }
    if ($entry.id -notmatch '^[a-z0-9_-]+$') { throw "无效素材 ID：$($entry.id)" }
    $art = $entry.asset
    $bodyPath = Join-Path (Join-Path $target $groups[$entry.category]) "$($entry.id).png"
    if ($art.format -eq 'atlas') {
        $img = [Drawing.Bitmap]::new((Join-Path $PSScriptRoot $art.file))
        try {
            Write-Crop -Image $img -Crop $art.crop -Path $bodyPath
            if ($art.portraitCrop) {
                Write-Crop -Image $img -Crop $art.portraitCrop -Path (Join-Path $target "人物肖像/$($entry.id).png")
            }
        } finally { $img.Dispose() }
    } elseif ($entry.id -eq 'p0') {
        if (Test-Path -LiteralPath $bodyPath) { throw "输出文件已存在：$bodyPath" }
        $src = Join-Path $PSScriptRoot $art.file
        Copy-Item -LiteralPath $src -Destination $bodyPath
        $img = [Drawing.Bitmap]::new($src)
        try {
            $relative = $entry.portrait.relativeCrop
            $crop = @([int]($img.Width*$relative[0]/100),[int]($img.Height*$relative[1]/100),
                [int]($img.Width*$relative[2]/100),[int]($img.Height*$relative[3]/100))
            Write-Crop -Image $img -Crop $crop -Path (Join-Path $target '人物肖像/p0.png')
        } finally { $img.Dispose() }
    }
}
Write-Output "导出完成：$target"
