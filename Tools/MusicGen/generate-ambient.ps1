# HellAmbient.cs'yi derleyip Assets/Resources/Audio/HellAmbient.wav dosyasini uretir.
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$out = [System.IO.Path]::GetFullPath((Join-Path $here "..\..\Assets\Resources\Audio\HellAmbient.wav"))
Add-Type -TypeDefinition (Get-Content -Raw (Join-Path $here "HellAmbient.cs")) -Language CSharp
[HellAmbient]::Run($out)
