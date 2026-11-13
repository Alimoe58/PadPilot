# Writes the three winget manifest files for a published PadPilot release.
# The checksum is taken from the local release exe, which must be the same file as on GitHub.
#
#   powershell -File packaging\winget\make-manifest.ps1 -Version 1.3.0
#
# Result: packaging\winget\manifests\a\Alimoe58\PadPilot\<version>\ - the same folder layout as in
# github.com/microsoft/winget-pkgs. Check it with:  winget validate --manifest <that folder>
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$Exe = (Join-Path $PSScriptRoot "..\..\publish\PadPilot.exe"),
    [string]$ReleaseDate = (Get-Date -Format "yyyy-MM-dd")
)
$ErrorActionPreference = "Stop"

$id = "Alimoe58.PadPilot"
$schema = "1.6.0"
$sha = (Get-FileHash $Exe -Algorithm SHA256).Hash
$url = "https://github.com/Alimoe58/PadPilot/releases/download/v$Version/PadPilot.exe"
$dir = Join-Path $PSScriptRoot "manifests\a\Alimoe58\PadPilot\$Version"
New-Item -ItemType Directory -Force $dir | Out-Null

$versionYaml = @"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.version.$schema.schema.json
PackageIdentifier: $id
PackageVersion: $Version
DefaultLocale: en-US
ManifestType: version
ManifestVersion: $schema
"@

$installerYaml = @"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.installer.$schema.schema.json
PackageIdentifier: $id
PackageVersion: $Version
InstallerType: portable
Commands:
- padpilot
MinimumOSVersion: 10.0.17763.0
ReleaseDate: $ReleaseDate
Installers:
- Architecture: x64
  InstallerUrl: $url
  InstallerSha256: $sha
ManifestType: installer
ManifestVersion: $schema
"@

$localeYaml = @"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.defaultLocale.$schema.schema.json
PackageIdentifier: $id
PackageVersion: $Version
PackageLocale: en-US
Publisher: Alimoe58
PublisherUrl: https://github.com/Alimoe58
PublisherSupportUrl: https://github.com/Alimoe58/PadPilot/issues
PackageName: PadPilot
PackageUrl: https://github.com/Alimoe58/PadPilot
License: MIT
LicenseUrl: https://github.com/Alimoe58/PadPilot/blob/main/LICENSE
ShortDescription: Free, open-source tool for the PlayStation 5 DualSense controller on Windows.
Description: |-
  Adaptive triggers, lightbar effects, Xbox 360 / DualShock 4 emulation (with the ViGEmBus driver),
  button mapping and controller-to-keyboard, touchpad and gyro as mouse, deadzones, profiles that
  switch automatically per game, and a UDP interface for mods. English and German.
Moniker: padpilot
Tags:
- dualsense
- controller
- gamepad
- ps5
- adaptive-triggers
- vigem
ReleaseNotesUrl: https://github.com/Alimoe58/PadPilot/releases/tag/v$Version
ManifestType: defaultLocale
ManifestVersion: $schema
"@

$utf8 = New-Object Text.UTF8Encoding $false
[IO.File]::WriteAllText((Join-Path $dir "$id.yaml"), $versionYaml + "`n", $utf8)
[IO.File]::WriteAllText((Join-Path $dir "$id.installer.yaml"), $installerYaml + "`n", $utf8)
[IO.File]::WriteAllText((Join-Path $dir "$id.locale.en-US.yaml"), $localeYaml + "`n", $utf8)
"Written to $dir (SHA256 $sha)"
