param(
    [switch]$Rapido,
    [string]$Pacote,
    [string]$PastaTraducao,
    [string]$DiretorioTestes
)
$ErrorActionPreference = 'Stop'
$buildDirectory = Join-Path $PSScriptRoot 'build'
New-Item -ItemType Directory -Path $buildDirectory -Force | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$testProgram = Join-Path $buildDirectory 'InstallerTests.exe'
& $compiler /nologo /target:exe /platform:x64 /optimize+ ('/out:' + $testProgram) /r:System.dll /r:System.Core.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll (Join-Path $PSScriptRoot 'InstallerCore.cs') (Join-Path $PSScriptRoot 'InstallerTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Falha ao compilar os testes.' }
if ([string]::IsNullOrWhiteSpace($DiretorioTestes)) { $DiretorioTestes = Join-Path ([System.IO.Path]::GetTempPath()) 'PW' }
New-Item -ItemType Directory -Path $DiretorioTestes -Force | Out-Null
$fixtureDirectory = (Resolve-Path -LiteralPath $DiretorioTestes).Path
if ($Rapido) { & $testProgram $fixtureDirectory }
else {
    $sourceDirectory = Split-Path -Parent $PSScriptRoot
    if ([string]::IsNullOrWhiteSpace($PastaTraducao)) { $PastaTraducao = $sourceDirectory }
    if ([string]::IsNullOrWhiteSpace($Pacote)) { $Pacote = Join-Path $sourceDirectory 'Project Wingman - Traducao PT-BR v0.91 - Instalador.exe' }
    & $testProgram $fixtureDirectory (Resolve-Path -LiteralPath $Pacote).Path (Resolve-Path -LiteralPath $PastaTraducao).Path
}
if ($LASTEXITCODE -ne 0) { throw 'Os testes falharam.' }
