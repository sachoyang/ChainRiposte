# ChainRiposte · Core 퍼즈 하네스 — 빌드 + 실행
#
# Unity를 켜지 않는다. Core에 UnityEngine 참조가 하나도 없어서
# Core/**/*.cs 를 그대로 콘솔 앱에 넣고 컴파일할 수 있다.
#
#   사용:  .\run.ps1              (기본 6000판)
#          .\run.ps1 -Boards 500  (짧게)
#
# 준비물은 유니티 설치본에 들어 있는 Roslyn 컴파일러와 .NET 런타임뿐이다.
# (.NET SDK 는 필요 없다 — dotnet 런타임만 있으면 된다)

param(
    [int]$Boards = 6000,
    [string]$UnityRoot = "E:\Unity\2022.3.62f3\Editor\Data"
)

$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding $false

$here    = Split-Path -Parent $MyInvocation.MyCommand.Path
$repo    = Resolve-Path (Join-Path $here "..\..")
$coreDir = Join-Path $repo "Assets\_Project\Scripts\Core"
$outDir  = Join-Path $here "bin"

$csc = Join-Path $UnityRoot "DotNetSdkRoslyn\csc.dll"
if (-not (Test-Path $csc)) { throw "Roslyn 컴파일러를 못 찾았다: $csc  (-UnityRoot 로 유니티 경로를 지정할 것)" }

# .NET 런타임의 관리 어셈블리만 참조로 쓴다 (네이티브 dll 은 걸러야 한다)
$runtimeDir = Get-ChildItem "C:\Program Files\dotnet\shared\Microsoft.NETCore.App" -Directory |
    Sort-Object Name -Descending | Select-Object -First 1
if (-not $runtimeDir) { throw ".NET 런타임을 못 찾았다." }

$refs = Get-ChildItem $runtimeDir.FullName -Filter *.dll |
    Where-Object { ($_.Name -like "System.*" -or $_.Name -eq "netstandard.dll") -and $_.Name -notlike "*Native*" } |
    ForEach-Object { "-r:$($_.FullName)" }

$sources = @()
$sources += Get-ChildItem $coreDir -Filter *.cs -Recurse | ForEach-Object { $_.FullName }
$sources += Join-Path $here "Program.cs"

if (-not (Test-Path $outDir)) { New-Item -ItemType Directory $outDir | Out-Null }

Write-Host "  컴파일: Core $(($sources.Count) - 1)개 + 하네스 1개 ..." -ForegroundColor DarkGray
& dotnet $csc -nologo -nostdlib -langversion:9.0 -optimize+ -target:exe `
    -out:"$outDir\CoreFuzz.dll" $refs $sources
if ($LASTEXITCODE -ne 0) { throw "컴파일 실패" }

# SDK 없이 만든 exe 라 apphost 가 없다 — 런타임 설정을 직접 적어 dotnet 으로 띄운다
$config = '{ "runtimeOptions": { "tfm": "net8.0", "framework": { "name": "Microsoft.NETCore.App", "version": "' + $runtimeDir.Name + '" } } }'
# BOM 이 붙으면 런타임이 "빈 문서"로 읽는다 — BOM 없는 UTF-8 로 쓴다
[System.IO.File]::WriteAllText((Join-Path $outDir "CoreFuzz.runtimeconfig.json"), $config, (New-Object System.Text.UTF8Encoding $false))

& dotnet "$outDir\CoreFuzz.dll" --boards $Boards
exit $LASTEXITCODE
