# The screen of the Lapin test VM's user session, saved to C:\Users\lapin\lapin-shot.png (test-build.sh runs it as a
# scheduled task in that session: what SSH starts has no desktop to capture).
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
$b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$i = New-Object System.Drawing.Bitmap $b.Width, $b.Height
[System.Drawing.Graphics]::FromImage($i).CopyFromScreen($b.Location, [System.Drawing.Point]::Empty, $b.Size)
$i.Save('C:\Users\lapin\lapin-shot.png')
