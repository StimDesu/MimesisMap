# Builds the mod and creates the Thunderstore package (dist/MimesisMap-<version>.zip).
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
dotnet build "$root/MimesisMap.csproj" -c Release
$version = (Get-Content "$root/manifest.json" -Raw | ConvertFrom-Json).version_number
$stage = Join-Path $root 'dist/package'
Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $stage | Out-Null
Copy-Item "$root/bin/Release/netstandard2.1/MimesisMap.dll", "$root/manifest.json", "$root/CHANGELOG.md", "$root/icon.png" $stage
# Thunderstore shows the package README: English only (the GitHub README has both languages)
Copy-Item "$root/README.thunderstore.md" (Join-Path $stage 'README.md')
$zip = Join-Path $root "dist/MimesisMap-$version.zip"
Remove-Item $zip -ErrorAction SilentlyContinue
Compress-Archive -Path "$stage/*" -DestinationPath $zip
Write-Host "Package: $zip"
