# 构建 MdCoinWatch
#
#   .\build.ps1                  框架依赖版 -> dist\        （约 0.35 MB，需要 .NET 8 运行时）
#   .\build.ps1 -SelfContained   自带运行时版 -> dist-standalone\（约 11 MB，什么都不用装）
#   .\build.ps1 -Both            两个都出
#   .\build.ps1 -Assets          重新生成内嵌模板和硬币图标（需要 numpy + pillow）
#
param(
    [switch]$SelfContained,
    [switch]$Both,
    [switch]$Assets
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Push-Location $root
try {
    if ($Assets) {
        $py = $null
        foreach ($c in @('python', "$env:USERPROFILE\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe")) {
            $found = Get-Command $c -ErrorAction SilentlyContinue
            if ($found) { $py = $found.Source; break }
        }
        if (-not $py) { throw '找不到 python，无法重新生成资源' }
        Write-Host "生成模板: $py tools\make_templates.py"
        & $py 'tools\make_templates.py'
        if ($LASTEXITCODE -ne 0) { throw '模板生成失败' }
        Write-Host "生成硬币图标: $py tools\make_coin_assets.py"
        & $py 'tools\make_coin_assets.py'
        if ($LASTEXITCODE -ne 0) { throw '硬币图标生成失败' }
    }

    function Publish-Small {
        Write-Host '编译框架依赖版 ...'
        dotnet publish 'app\MdCoinWatch.csproj' -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -v q --nologo -o dist
        if ($LASTEXITCODE -ne 0) { throw '发布失败' }
    }

    function Publish-Full {
        Write-Host '编译自带运行时版（要几十秒）...'
        dotnet publish 'app\MdCoinWatch.csproj' -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:PublishTrimmed=true -p:IncludeNativeLibrariesForSelfExtract=true -v q --nologo -o dist-standalone
        if ($LASTEXITCODE -ne 0) { throw '发布失败' }
    }

    if ($SelfContained) { Publish-Full } elseif ($Both) { Publish-Small; Publish-Full } else { Publish-Small }

    Write-Host ''
    foreach ($d in 'dist', 'dist-standalone') {
        if (Test-Path $d) {
            Write-Host "$d :"
            Get-ChildItem $d -Filter *.exe | Format-Table Name, @{N = 'MB'; E = { [math]::Round($_.Length / 1MB, 2) } } -AutoSize
        }
    }

    Write-Host '离线自检（样本截图）:'
    & 'dist\YuGiOh-MDCoinWatch.exe' --selftest='.' -ErrorAction SilentlyContinue | Out-String | Write-Host
} finally {
    Pop-Location
}
