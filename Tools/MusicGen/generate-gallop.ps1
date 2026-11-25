# HellGallop.cs'yi derleyip Assets/Resources/Audio/HellGallop.wav dosyasini uretir.
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$out = [System.IO.Path]::GetFullPath((Join-Path $here "..\..\Assets\Resources\Audio\HellGallop.wav"))
Add-Type -TypeDefinition (Get-Content -Raw (Join-Path $here "HellGallop.cs")) -Language CSharp
[HellGallop]::Run($out)
