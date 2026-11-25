# HellTheme.cs'yi derleyip Assets/Resources/Audio/HellTheme.wav dosyasini uretir.
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$out = Join-Path $here "..\..\Assets\Resources\Audio\HellTheme.wav"
$out = [System.IO.Path]::GetFullPath($out)
Add-Type -TypeDefinition (Get-Content -Raw (Join-Path $here "HellTheme.cs")) -Language CSharp
[HellTheme]::Run($out)
