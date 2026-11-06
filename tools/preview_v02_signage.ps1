$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$taskRoot = (Get-Location).Path
$config = Get-Content (Join-Path $taskRoot 'Assets/EverflowPuzzleLab/Presentation/Signage.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$taskOut = Join-Path $taskRoot 'docs/puzzle-lab/signage-previews'
New-Item -ItemType Directory -Force $taskOut | Out-Null
$report = [System.Collections.Generic.List[string]]::new()
$report.Add('Flat GDI preview using installed Malgun Gothic. NOT a Unity Scene/Play screenshot or mesh-bound validation.')
foreach ($entry in $config.boards) {
    $width = [int]($entry.size[0]*100); $height = [int]($entry.size[1]*100)
    $bitmap = [System.Drawing.Bitmap]::new($width,$height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $graphics.Clear([System.Drawing.Color]::FromArgb(9,17,23))
    $band = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(14,31,41))
    $graphics.FillRectangle($band,2,2,($width-4),72)
    $rule = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(59,115,133))
    $graphics.FillRectangle($rule,2,3,5,($height-6))
    $underground = $entry.index -eq 4
    if($underground) {$gy=2.2;$my=1.55;$cy=-.65;$sy=-1.86} else {$gy=1.7;$my=.82;$cy=-.80;$sy=-1.80}
    $goalHeading = '"\uBAA9\uD45C"' | ConvertFrom-Json
    $methodsHeading = '"\uD14C\uC2A4\uD2B8 \uBC29\uBC95"' | ConvertFrom-Json
    $controlsHeading = '"\uC870\uC791 \u00B7 \uC2E4\uD328 \uBCF5\uAD6C"' | ConvertFrom-Json
    $statusHeading = '"\uD604\uC7AC \uC0C1\uD0DC"' | ConvertFrom-Json
    $statusSample = '"\uC9C4\uD589 \uC911 \u00B7 \uBAA9\uD45C \uBBF8\uB3C4\uB2EC"' | ConvertFrom-Json
    $sections = @(
        @('Title',$entry.title,($entry.size[1]/2-.22),36,'White'),
        @('Goal heading',$goalHeading,$gy,16,'LightSteelBlue'),
        @('Goal',$entry.goal,($gy-.25),24,'WhiteSmoke'),
        @('Methods heading',$methodsHeading,$my,16,'LightSteelBlue'),
        @('Methods',$entry.methods,($my-.25),22,'WhiteSmoke'),
        @('Controls heading',$controlsHeading,$cy,16,'LightSteelBlue'),
        @('Controls',$entry.controls,($cy-.23),18,'LightGray'),
        @('Status heading',$statusHeading,$sy,16,'LightSteelBlue'),
        @('Status',$statusSample,($sy-.22),18,'Aquamarine')
    )
    if($underground) {
        $circuit = '"\uC9C1\uC811 \uB04A\uAE40 \u00B7 \uC6B0\uD68C \uB2EB\uD798 \u00B7 \uACF5\uAE09 \uB04A\uAE40\n\uBD84\uAE30 \uBBF8\uC120\uD0DD \u00B7 \uBB38 \uB2EB\uD798 \u00B7 \uC8FC \uC5BC\uC74C \uC5C6\uC74C\nW \uB3C4\uCC29 \uC5C6\uC74C \u00B7 0.0 / 3.0\uCD08"' | ConvertFrom-Json
        $sections += ,@('Circuit',$config.statusPreview,-2.34,18,'Aquamarine')
    }
    $previousEnd=0
    foreach($s in $sections) {
        $font=[System.Drawing.Font]::new('Malgun Gothic',[float]$s[3],[System.Drawing.FontStyle]::Regular,[System.Drawing.GraphicsUnit]::Pixel)
        $brush=[System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromName($s[4]))
        $top=[float](($entry.size[1]/2-$s[2])*100)
        $row=$font.GetHeight($graphics)*.85
        if($top -lt $previousEnd-1) {throw ($entry.key+' overlapping sections: '+$s[0]+' top='+$top+' prior='+$previousEnd)}
        $n=0
        foreach($line in ($s[1] -split "`n")) {
            $measured=$graphics.MeasureString($line,$font,10000,[System.Drawing.StringFormat]::GenericTypographic).Width
            if($measured -gt ($width-60)) {throw ($entry.key+' overflow '+$s[0]+' '+$measured)}
            $graphics.DrawString($line,$font,$brush,[System.Drawing.PointF]::new(30,($top+$row*$n)),[System.Drawing.StringFormat]::GenericTypographic)
            $n++
        }
        $end=$top+$row*[Math]::Max(0,$n-1)+$font.GetHeight($graphics)
        if($end -gt ($height-8)) {throw ($entry.key+' bottom overflow '+$s[0]+' '+$end)}
        $previousEnd=$end
        $font.Dispose();$brush.Dispose()
    }
    $lineY=[float](($entry.size[1]/2-$sy-.09)*100)
    $graphics.FillRectangle($rule,30,$lineY,($width-60),1.2)
    $bitmap.Save((Join-Path $taskOut ($entry.key+'.png')),[System.Drawing.Imaging.ImageFormat]::Png)
    $report.Add('PASS '+$entry.key+': measured text widths, section spacing and bottom bounds in flat preview.')
    $graphics.Dispose();$bitmap.Dispose();$band.Dispose();$rule.Dispose()
}
$report | Set-Content (Join-Path $taskOut 'layout-checks.txt') -Encoding UTF8
$report
