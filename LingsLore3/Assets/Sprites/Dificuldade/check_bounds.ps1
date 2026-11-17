Add-Type -AssemblyName System.Drawing

$dir = "Assets\Sprites\Dificuldade"
function Get-Bounds($bmp) {
    $minX = $bmp.Width; $maxX = 0; $minY = $bmp.Height; $maxY = 0
    for ($x = 0; $x -lt $bmp.Width; $x++) {
        for ($y = 0; $y -lt $bmp.Height; $y++) {
            if ($bmp.GetPixel($x, $y).A -gt 10) {
                if ($x -lt $minX) { $minX = $x }
                if ($x -gt $maxX) { $maxX = $x }
                if ($y -lt $minY) { $minY = $y }
                if ($y -gt $maxY) { $maxY = $y }
            }
        }
    }
    return "$minX,$minY -> $maxX,$maxY (Size: $($maxX - $minX + 1)x$($maxY - $minY + 1))"
}

$files = @("base_ling.png", "base_fork.png", "base_dois_chopsticks.png", "base_um_chopstick.png")
foreach ($f in $files) {
    $bmp = [System.Drawing.Bitmap]::FromFile("$dir\$f")
    $b = Get-Bounds $bmp
    Write-Output "$f`: $($bmp.Width)x$($bmp.Height) | Visible: $b"
    $bmp.Dispose()
}
