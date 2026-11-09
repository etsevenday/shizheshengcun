[CmdletBinding()]
param([Parameter(Mandatory = $true)][string]$Destination)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$target = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Destination)
$sheetList = Get-Content -LiteralPath (Join-Path $PSScriptRoot '替换清单.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$categories = @{
    item = '物品'; skill = '技能'; skillFamily = '熟练度'; action = '战斗动作'; glyph = '界面图标'
    status = '状态'; resource = '资源点'; facility = '设施'; faction = '势力'; duty = '代班'
    recruitTemplate = '招募样例'; actor = '固定人物'
}
function Save-Crop {
    param([Drawing.Bitmap]$Bitmap, [object[]]$Crop, [string]$File)
    if (Test-Path -LiteralPath $File) { throw "文件已存在：$File" }
    $rect = [Drawing.Rectangle]::new([int]$Crop[0], [int]$Crop[1], [int]$Crop[2], [int]$Crop[3])
    $cutout = $Bitmap.Clone($rect, $Bitmap.PixelFormat)
    try { $cutout.Save($File, [Drawing.Imaging.ImageFormat]::Png) }
    finally { $cutout.Dispose() }
}
$bindings = @($sheetList.bindings.PSObject.Properties)
$grouped = $bindings | Group-Object { $_.Value.file }
$portraits = 0
foreach ($sheet in $grouped) {
    $image = [Drawing.Bitmap]::new((Join-Path $PSScriptRoot $sheet.Name))
    try {
        foreach ($entry in $sheet.Group) {
            $category, $id = $entry.Name -split ':', 2
            $folder = $categories[$category]
            if (!$folder -or $id -notmatch '^[a-z0-9:_-]+$') { throw "未知素材键：$($entry.Name)" }
            $name = $id.Replace(':', '--')
            $dest = Join-Path $target $folder
            $null = New-Item -ItemType Directory -Path $dest -Force
            Save-Crop -Bitmap $image -Crop $entry.Value.crop -File (Join-Path $dest "$name.png")
            if ($entry.Value.portraitCrop) {
                $portraitDir = Join-Path $dest '肖像'
                $null = New-Item -ItemType Directory -Path $portraitDir -Force
                Save-Crop -Bitmap $image -Crop $entry.Value.portraitCrop -File (Join-Path $portraitDir "$name.png")
                $portraits++
            }
        }
    } finally { $image.Dispose() }
}
Write-Output "已导出 $($bindings.Count) 项美术与 $portraits 个肖像裁窗。"
