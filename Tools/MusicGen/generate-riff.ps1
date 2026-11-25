# HellGallop.cs'deki riff duzenlemesini (144 BPM) Assets/Resources/Audio/HellRiff.wav olarak uretir.
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$out = [System.IO.Path]::GetFullPath((Join-Path $here "..\..\Assets\Resources\Audio\HellRiff.wav"))
Add-Type -TypeDefinition (Get-Content -Raw (Join-Path $here "HellGallop.cs")) -Language CSharp
[HellGallop]::RunRiff($out)
