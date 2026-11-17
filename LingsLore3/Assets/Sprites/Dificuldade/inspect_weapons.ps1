Add-Type -AssemblyName System.Drawing

$fork = [System.Drawing.Bitmap]::FromFile("Assets\Sprites\Dificuldade\base_fork.png")
Write-Output "--- FORK 32x32 ---"
for ($y = 0; $y -lt $fork.Height; $y += 2) {
    $row = ""
    for ($x = 0; $x -lt $fork.Width; $x += 1) {
        $c = $fork.GetPixel($x, $y)
        if ($c.A -lt 10) { $row += " " } else { $row += "#" }
    }
    Write-Output ("{0:D2}: {1}" -f $y, $row)
}
$fork.Dispose()

$chop = [System.Drawing.Bitmap]::FromFile("Assets\Sprites\Dificuldade\base_dois_chopsticks.png")
Write-Output "--- CHOPSTICKS 96x96 ---"
for ($y = 0; $y -lt $chop.Height; $y += 4) {
    $row = ""
    for ($x = 0; $x -lt $chop.Width; $x += 2) {
        $c = $chop.GetPixel($x, $y)
        if ($c.A -lt 10) { $row += " " } else { $row += "/" }
    }
    Write-Output ("{0:D2}: {1}" -f $y, $row)
}
$chop.Dispose()
