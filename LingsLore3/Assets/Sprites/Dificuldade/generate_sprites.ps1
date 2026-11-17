Add-Type -AssemblyName System.Drawing

$dir = "Assets\Sprites\Dificuldade"
$ling = [System.Drawing.Bitmap]::FromFile("$dir\base_ling.png")
$fork = [System.Drawing.Bitmap]::FromFile("$dir\base_fork.png")
$chops = [System.Drawing.Bitmap]::FromFile("$dir\base_dois_chopsticks.png")

function Create-CardSprite([string]$outPath, [string]$damageLevel) {
    $canvasSize = 96
    $bmp = New-Object System.Drawing.Bitmap($canvasSize, $canvasSize, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
    $g.Clear([System.Drawing.Color]::Transparent)

    # 1. Ling no centro
    $lingX = [int](($canvasSize - $ling.Width) / 2)
    $lingY = [int](($canvasSize - $ling.Height) / 2 - 2)
    $g.DrawImage($ling, $lingX, $lingY, $ling.Width, $ling.Height)

    # 2. Garfo à esquerda
    # Clone do garfo para aplicar dano nos pixels
    $forkBmp = New-Object System.Drawing.Bitmap($fork)
    if ($damageLevel -eq "medio") {
        # Pequeno arranhão
        for ($x = 2; $x -lt 8; $x++) {
            $forkBmp.SetPixel($x, 14, [System.Drawing.Color]::FromArgb(255, 80, 80, 90))
        }
    } elseif ($damageLevel -eq "dificil") {
        # Dente lateral entortado
        for ($y = 0; $y -lt 8; $y++) {
            $forkBmp.SetPixel(2, $y, [System.Drawing.Color]::Transparent)
            $forkBmp.SetPixel(3, $y, [System.Drawing.Color]::Transparent)
        }
        for ($i = 0; $i -lt 6; $i++) {
            $px = [Math]::Max(0, 4 - $i)
            $forkBmp.SetPixel($px, $i, [System.Drawing.Color]::FromArgb(255, 200, 205, 220))
        }
    } elseif ($damageLevel -eq "impossivel") {
        # Dente quebrado e pontas lascadas
        for ($y = 0; $y -lt 10; $y++) {
            for ($x = 0; $x -lt $forkBmp.Width; $x++) {
                if ($y -lt 6 -or ($x % 2 -eq 0 -and $y -lt 9)) {
                    $forkBmp.SetPixel($x, $y, [System.Drawing.Color]::Transparent)
                }
            }
        }
        for ($x = 0; $x -lt $forkBmp.Width; $x++) {
            if ($forkBmp.GetPixel($x, 9).A -gt 50) {
                $forkBmp.SetPixel($x, 9, [System.Drawing.Color]::FromArgb(255, 50, 45, 55))
            }
        }
    }
    $forkX = $lingX - [int]($fork.Width / 2) + 6
    $forkY = $lingY + 6
    $g.DrawImage($forkBmp, $forkX, $forkY, $fork.Width, $fork.Height)
    $forkBmp.Dispose()

    # 3. Hashis à direita
    $chopBmp = New-Object System.Drawing.Bitmap($chops)
    if ($damageLevel -eq "medio") {
        # Ponta gasta
        for ($x = [int]($chopBmp.Width / 2); $x -lt $chopBmp.Width; $x++) {
            for ($y = 0; $y -lt 3; $y++) {
                if ($chopBmp.GetPixel($x, $y).A -gt 50) {
                    $chopBmp.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(255, 130, 95, 65))
                }
            }
        }
    } elseif ($damageLevel -eq "dificil") {
        # Fissura diagonal na madeira
        for ($i = 0; $i -lt 8; $i++) {
            $px = [Math]::Min([Math]::Max([int]($chopBmp.Width / 2 + ($i % 3)), 0), $chopBmp.Width - 1)
            $py = [Math]::Min([Math]::Max([int]($chopBmp.Height / 2 + $i), 0), $chopBmp.Height - 1)
            if ($chopBmp.GetPixel($px, $py).A -gt 50) {
                $chopBmp.SetPixel($px, $py, [System.Drawing.Color]::FromArgb(255, 55, 35, 20))
            }
        }
    } elseif ($damageLevel -eq "impossivel") {
        # Um hashi partido ao meio com madeira lascada
        $midH = [int]($chopBmp.Height / 2)
        for ($y = 0; $y -lt $midH; $y++) {
            for ($x = [int]($chopBmp.Width / 2); $x -lt $chopBmp.Width; $x++) {
                if ($y -lt $midH - 3 -or ($x % 2 -eq 0)) {
                    $chopBmp.SetPixel($x, $y, [System.Drawing.Color]::Transparent)
                }
            }
        }
        for ($x = [int]($chopBmp.Width / 2); $x -lt $chopBmp.Width; $x++) {
            $col = if ($x % 2 -eq 0) { [System.Drawing.Color]::FromArgb(255, 215, 185, 140) } else { [System.Drawing.Color]::FromArgb(255, 50, 30, 15) }
            $chopBmp.SetPixel($x, $midH, $col)
        }
    }
    $chopX = $lingX + $ling.Width - [int]($chops.Width / 2) - 6
    $chopY = $lingY + 6
    $g.DrawImage($chopBmp, $chopX, $chopY, $chops.Width, $chops.Height)
    $chopBmp.Dispose()

    $g.Dispose()
    $bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Output "Gerado: $outPath"
}

Create-CardSprite "$dir\Dificuldade_Facil.png" "nenhum"
Create-CardSprite "$dir\Dificuldade_Medio.png" "medio"
Create-CardSprite "$dir\Dificuldade_Dificil.png" "dificil"
Create-CardSprite "$dir\Dificuldade_Impossivel.png" "impossivel"

$ling.Dispose()
$fork.Dispose()
$chops.Dispose()
