param([switch]$OnlyImpossivel)

Add-Type -AssemblyName System.Drawing

$width = 112
$height = 88
$spacing = 18
$assetNames = 'Dificuldade_Facil', 'Dificuldade_Medio', 'Dificuldade_Dificil', 'Dificuldade_Impossivel'

$outline = [Drawing.Color]::FromArgb(113, 69, 42)
$shadedEdge = [Drawing.Color]::FromArgb(160, 103, 60)
$wood = [Drawing.Color]::FromArgb(195, 137, 79)
$litFace = [Drawing.Color]::FromArgb(214, 157, 96)
$highlight = [Drawing.Color]::FromArgb(239, 188, 121)
$grain = [Drawing.Color]::FromArgb(177, 117, 68)

function Draw-Hashi([Drawing.Bitmap]$bitmap, [int]$center, [int]$tipOffset = 0) {
    for ($row = 0; $row -le 73; $row++) {
        $rowCenter = $center + [int][Math]::Round($tipOffset * $row / 73.0)
        # Flat handle with a small bevel; the lower half tapers towards a blunt tip.
        $left = if ($row -eq 0) { -2 }
            elseif ($row -lt 37) { -3 }
            elseif ($row -lt 60) { -2 }
            elseif ($row -lt 73) { -1 }
            else { 0 }
        $right = if ($row -eq 0) { 2 }
            elseif ($row -lt 32) { 3 }
            elseif ($row -lt 54) { 2 }
            elseif ($row -lt 73) { 1 }
            else { 0 }

        for ($offset = $left; $offset -le $right; $offset++) {
            $color = $wood

            if ($row -eq 0) { $color = $highlight }
            elseif ($row -eq 73) { $color = $shadedEdge }
            elseif ($offset -eq $right) { $color = $outline }
            elseif ($offset -eq $left) { $color = $shadedEdge }
            elseif ($offset -eq ($left + 1)) { $color = $highlight }
            elseif ($offset -lt 0) { $color = $litFace }

            # Narrow breaks in the midtones suggest longitudinal wood grain.
            if (($offset -eq 1 -and $row -ge 8 -and $row -le 16) -or
                ($offset -eq 0 -and $row -ge 24 -and $row -le 33) -or
                ($offset -eq 1 -and $row -ge 39 -and $row -le 45)) {
                $color = $grain
            }

            $bitmap.SetPixel(($rowCenter + $offset), (7 + $row), $color)
        }
    }
}

for ($index = 0; $index -lt $assetNames.Count; $index++) {
    $isImpossivel = $assetNames[$index] -eq 'Dificuldade_Impossivel'
    if ($OnlyImpossivel -and -not $isImpossivel) { continue }

    $bitmap = [Drawing.Bitmap]::new($width, $height, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        if ($isImpossivel) {
            # One upright hashi for I and two meeting at the tip for V.
            Draw-Hashi $bitmap 28
            Draw-Hashi $bitmap 45 19
            Draw-Hashi $bitmap 84 (-20)
        }
        else {
            $firstCenter = 56 - ($index * $spacing / 2)
            for ($stick = 0; $stick -le $index; $stick++) {
                Draw-Hashi $bitmap ($firstCenter + $stick * $spacing)
            }
        }

        $path = Join-Path $PSScriptRoot ($assetNames[$index] + '.png')
        $bitmap.Save($path, [Drawing.Imaging.ImageFormat]::Png)
        Write-Host "Gerado: $($assetNames[$index]).png"
    }
    finally {
        $bitmap.Dispose()
    }
}
