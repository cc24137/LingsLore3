Add-Type -AssemblyName System.Drawing

$ling = [System.Drawing.Bitmap]::FromFile("Assets\Sprites\Dificuldade\base_ling.png")
# Encontrar onde estão os olhos e o centro
# Imprimir linhas onde há pixels não-transparentes
for ($y = 0; $y -lt $ling.Height; $y += 4) {
    $row = ""
    for ($x = 0; $x -lt $ling.Width; $x += 2) {
        $c = $ling.GetPixel($x, $y)
        if ($c.A -lt 10) { $row += " " }
        elseif ($c.R -gt 200 -and $c.G -gt 150) { $row += "^" } # pele/chapéu
        elseif ($c.R -lt 50 -and $c.G -lt 50) { $row += "#" }   # olhos/contorno
        else { $row += "*" }
    }
    Write-Output ("{0:D2}: {1}" -f $y, $row)
}
$ling.Dispose()
