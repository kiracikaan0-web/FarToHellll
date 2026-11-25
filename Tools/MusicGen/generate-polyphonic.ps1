# HellAmbient.cs'deki cok sesli versiyonu Assets/Resources/Audio/HellPolyphonic.wav olarak uretir.
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$out = [System.IO.Path]::GetFullPath((Join-Path $here "..\..\Assets\Resources\Audio\HellPolyphonic.wav"))
Add-Type -TypeDefinition (Get-Content -Raw (Join-Path $here "HellAmbient.cs")) -Language CSharp
[HellAmbient]::RunPolyphonic($out)
