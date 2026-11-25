# SwordSwing.cs'yi derleyip Assets/Resources/SFX/SwordSwing/ klasorune 3 savurma sesi uretir.
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$out = [System.IO.Path]::GetFullPath((Join-Path $here "..\..\Assets\Resources\SFX\SwordSwing"))
Add-Type -TypeDefinition (Get-Content -Raw (Join-Path $here "SwordSwing.cs")) -Language CSharp
[SwordSwing]::Run($out)
