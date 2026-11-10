param([switch]$Test, [switch]$Package)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $compiler)) { throw 'The .NET Framework C# compiler was not found. Install .NET Framework 4.8.' }
$out = Join-Path $root 'artifacts'
New-Item -ItemType Directory -Path $out -Force | Out-Null
$icon = Join-Path $root 'assets\app.ico'
if (-not (Test-Path -LiteralPath $icon)) { & (Join-Path $root 'scripts\New-AppIcon.ps1') }
$sources = @(Get-ChildItem -LiteralPath (Join-Path $root 'src') -Filter '*.cs' | ForEach-Object { $_.FullName })
$exe = Join-Path $out 'WendyQuestDashPatcher.exe'
& $compiler /nologo /target:winexe /platform:anycpu /optimize+ /langversion:5 /warn:4 /warnaserror+ "/out:$exe" "/win32icon:$icon" "/win32manifest:$(Join-Path $root 'src\app.manifest')" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll $sources
if ($LASTEXITCODE -ne 0) { throw 'Application build failed.' }
Copy-Item -LiteralPath (Join-Path $root 'src\App.config') -Destination ($exe + '.config') -Force
Write-Host "Built: $exe"
if ($Test) {
    $testExe = Join-Path $out 'WendysDash.Tests.exe'
    & $compiler /nologo /target:exe /platform:anycpu /optimize+ /langversion:5 /warn:4 /warnaserror+ "/out:$testExe" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll (Join-Path $root 'src\PatchService.cs') (Join-Path $root 'src\GameProcessGuard.cs') (Join-Path $root 'src\SteamLocator.cs') (Join-Path $root 'src\MainForm.cs') (Join-Path $root 'tests\PatchServiceTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Test build failed.' }
    & $testExe
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
}
if ($Package) {
    $release = Join-Path $out 'WendysDash-1.0.0-win'
    New-Item -ItemType Directory -Path $release -Force | Out-Null
    Copy-Item -LiteralPath $exe,($exe + '.config'),(Join-Path $root 'LICENSE'),(Join-Path $root 'docs\USAGE.txt') -Destination $release -Force
    $hash = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash.ToLowerInvariant()
    [IO.File]::WriteAllText((Join-Path $release 'SHA256SUMS.txt'), "$hash  WendyQuestDashPatcher.exe`r`n", [Text.UTF8Encoding]::new($false))
    $zip = Join-Path $out 'WendysDash-1.0.0-win.zip'
    $packageFiles = @('WendyQuestDashPatcher.exe', 'WendyQuestDashPatcher.exe.config', 'LICENSE', 'USAGE.txt', 'SHA256SUMS.txt') | ForEach-Object { Join-Path $release $_ }
    Compress-Archive -LiteralPath $packageFiles -DestinationPath $zip -Force
    Write-Host "Packaged: $zip"
}
