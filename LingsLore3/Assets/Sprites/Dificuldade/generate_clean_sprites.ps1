Add-Type -AssemblyName System.Drawing

$dir = "Assets\Sprites\Dificuldade"
$ling = [System.Drawing.Bitmap]::FromFile("$dir\base_ling.png")
$fork = [System.Drawing.Bitmap]::FromFile("$dir\base_fork.png")
$chops = [System.Drawing.Bitmap]::FromFile("$dir\base_dois_chopsticks.png")
$umChop = [System.Drawing.Bitmap]::FromFile("$dir\base_um_chopstick.png")

# Função que gera uma composição limpa onde Ling fica na frente e os utensílios nas laterais
function Create-CleanSprite($outPath, $damageLevel) {
    $w = 160
    $h = 128
    $bmp = New-Object System.Drawing.Bitmap($w, $h, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
    $g.Clear([System.Drawing.Color]::Transparent)

    # Coordenadas:
    # Ling no centro, base na parte inferior
    $lingX = 40
    $lingY = 24

    # 1. GARFO NA ESQUERDA (desenhado atrás de Ling, deslocado para a esquerda)
    $forkBmp = New-Object System.Drawing.Bitmap($fork)
    if ($damageLevel -eq "medio") {
        # Pequeno arranhão
        for ($x = 4; $x -lt 10; $x++) {
            $forkBmp.SetPixel($x, 14, [System.Drawing.Color]::FromArgb(255, 75, 75, 85))
        }
    } elseif ($damageLevel -eq "dificil") {
        # Dente lateral entortado
        for ($y = 0; $y -lt 8; $y++) {
            $forkBmp.SetPixel(2, $y, [System.Drawing.Color]::Transparent)
            $forkBmp.SetPixel(3, $y, [System.Drawing.Color]::Transparent)
        }
        for ($i = 0; $i -lt 7; $i++) {
            $px = [Math]::Max(0, 5 - $i)
            $forkBmp.SetPixel($px, $i, [System.Drawing.Color]::FromArgb(255, 200, 205, 220))
        }
    } elseif ($damageLevel -eq "impossivel") {
        # Dente quebrado no meio
        for ($y = 0; $y -lt 12; $y++) {
            for ($x = 0; $x -lt $forkBmp.Width; $x++) {
                if ($y -lt 7 -or ($x % 2 -eq 0 -and $y -lt 11)) {
                    $forkBmp.SetPixel($x, $y, [System.Drawing.Color]::Transparent)
                }
            }
        }
        for ($x = 0; $x -lt $forkBmp.Width; $x++) {
            if ($forkBmp.GetPixel($x, 11).A -gt 50) {
                $forkBmp.SetPixel($x, 11, [System.Drawing.Color]::FromArgb(255, 45, 40, 50))
            }
        }
    }
    # Deixar o garfo visível na lateral sem competir com o rosto.
    $forkX = 10
    $forkY = 48
    $g.DrawImage($forkBmp, $forkX, $forkY, 40, 40)
    $forkBmp.Dispose()

    # 2. HASHIS NA DIREITA (desenhados na lateral direita)
    # Pegamos o corte do hashi de base_dois_chopsticks ou desenhamos hashis limpos em ângulo
    # O chopstick original é 96x96, vamos usar uma sub-região ou desenhar os hashis apontando para cima na direita:
    $chopBmp = New-Object System.Drawing.Bitmap(40, 70, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $cg = [System.Drawing.Graphics]::FromImage($chopBmp)
    $cg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $cg.Clear([System.Drawing.Color]::Transparent)

    # Cores da madeira do hashi original
    $cCorpo = [System.Drawing.Color]::FromArgb(255, 178, 128, 89)
    $cBorda = [System.Drawing.Color]::FromArgb(255, 75, 45, 25)
    $cLuz = [System.Drawing.Color]::FromArgb(255, 218, 170, 125)

    if ($damageLevel -eq "nenhum" -or $damageLevel -eq "medio" -or $damageLevel -eq "dificil" -or $damageLevel -eq "impossivel") {
        # Hashi 1 (Principal)
        # Traço diagonal de baixo-esquerda para cima-direita
        for ($t = 0; $t -lt 61; $t++) {
            $x = 5 + [int]($t * 0.5)
            $y = 65 - $t

            # No impossível, quebra na metade!
            if ($damageLevel -eq "impossivel" -and $t -gt 26) {
                # Ponta lascada
                if ($t -eq 27) {
                    $chopBmp.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(255, 230, 200, 160))
                }
                continue
            }

            # No difícil, fissura escura aos t=20..26
            if ($damageLevel -eq "dificil" -and $t -ge 20 -and $t -le 24) {
                $chopBmp.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(255, 50, 30, 15))
                $chopBmp.SetPixel($x + 1, $y, [System.Drawing.Color]::FromArgb(255, 60, 35, 20))
                continue
            }

            $chopBmp.SetPixel($x - 1, $y, $cBorda)
            $chopBmp.SetPixel($x, $y, $cCorpo)
            $chopBmp.SetPixel($x + 1, $y, $cLuz)
            $chopBmp.SetPixel($x + 2, $y, $cBorda)
        }

        # Hashi 2 (Segundo hashi)
        # No fácil e difícil tem 2 hashis. No médio o 2º tem ponta gasta. No impossível o 2º foi arremessado/ausente.
        if ($damageLevel -ne "impossivel") {
            for ($t = 0; $t -lt 56; $t++) {
                $x = 10 + [int]($t * 0.46)
                $y = 65 - $t

                if ($damageLevel -eq "medio" -and $t -gt 38) {
                    # Ponta gasta
                    $chopBmp.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(255, 110, 80, 50))
                    continue
                }

                $chopBmp.SetPixel($x - 1, $y, $cBorda)
                $chopBmp.SetPixel($x, $y, $cCorpo)
                $chopBmp.SetPixel($x + 1, $y, $cLuz)
                $chopBmp.SetPixel($x + 2, $y, $cBorda)
            }
        }
    }
    $cg.Dispose()

    # Manter os hashis altos e separados do rosto.
    $chopX = 110
    $chopY = 25
    $g.DrawImage($chopBmp, $chopX, $chopY, 40, 70)
    $chopBmp.Dispose()

    # 3. LING NO CENTRO (EM CIMA, 100% VISÍVEL E LIMPA!)
    $g.DrawImage($ling, $lingX, $lingY, 80, 80)

    $g.Dispose()
    $bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Output "Salvo: $outPath"
}

Create-CleanSprite "$dir\Dificuldade_Facil.png" "nenhum"
Create-CleanSprite "$dir\Dificuldade_Medio.png" "medio"
Create-CleanSprite "$dir\Dificuldade_Dificil.png" "dificil"
Create-CleanSprite "$dir\Dificuldade_Impossivel.png" "impossivel"

$ling.Dispose()
$fork.Dispose()
$chops.Dispose()
$umChop.Dispose()
