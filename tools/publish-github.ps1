# 发布到 GitHub
#
#   前置：装好 GitHub CLI 并登录过一次
#     winget install GitHub.cli
#     gh auth login
#
#   然后：
#     .\tools\publish-github.ps1 -Repo 你的用户名/MdCoinWatch
#
#   会做四件事：建仓库（已存在就跳过）、推代码、出两个 exe、发 Release
#
param(
    [Parameter(Mandatory = $true)][string]$Repo,
    [string]$Tag = 'v0.3.0',
    [string]$Title = 'MD 投币运势 v0.3.0',
    [switch]$Private,
    [switch]$SkipRelease
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Push-Location $root
try {
    $gh = Get-Command gh -ErrorAction SilentlyContinue
    if (-not $gh) { throw '没装 GitHub CLI。先跑：winget install GitHub.cli，然后重开终端' }

    & gh auth status 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'gh 还没登录。先跑：gh auth login' }

    Write-Host "== 1/4 检查仓库 $Repo =="
    & gh repo view $Repo 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        $vis = if ($Private) { '--private' } else { '--public' }
        Write-Host "   仓库不存在，创建中（$vis）"
        & gh repo create $Repo $vis --source . --remote origin --push
        if ($LASTEXITCODE -ne 0) { throw '创建仓库失败' }
    }
    else {
        Write-Host '   仓库已存在'
        $url = "https://github.com/$Repo.git"
        & git remote set-url origin $url 2>$null
        if ($LASTEXITCODE -ne 0) { & git remote add origin $url }
    }

    Write-Host '== 2/4 推代码 =='
    & git push -u origin main
    if ($LASTEXITCODE -ne 0) { throw '推送失败' }

    if ($SkipRelease) { Write-Host '== 跳过 Release =='; return }

    Write-Host '== 3/4 出两个 exe =='
    & (Join-Path $root 'build.ps1') -Both
    if ($LASTEXITCODE -ne 0) { throw '编译失败' }

    $full = Join-Path $root 'dist-standalone\YuGiOh-MDCoinWatch.exe'
    $lite = Join-Path $root 'dist\YuGiOh-MDCoinWatch.exe'
    if (-not (Test-Path $full) -or -not (Test-Path $lite)) { throw '找不到编译产物' }

    $tmp = Join-Path $env:TEMP ('mdcoinwatch-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Force -Path $tmp | Out-Null
    Copy-Item $full (Join-Path $tmp 'YuGiOh-MDCoinWatch.exe')
    Copy-Item $lite (Join-Path $tmp 'YuGiOh-MDCoinWatch-lite.exe')

    Write-Host "== 4/4 发 Release $Tag =="
    $nl = [Environment]::NewLine
    $notes = @(
        '## 下载',
        '',
        '| 文件 | 大小 | 说明 |',
        '| --- | --- | --- |',
        '| `YuGiOh-MDCoinWatch.exe` | 约 11 MB | 自带运行时，双击就能跑，什么都不用装 |',
        '| `YuGiOh-MDCoinWatch-lite.exe` | 约 0.8 MB | 需要 .NET 8 运行时，启动更快 |',
        '',
        '丢进单独一个文件夹再运行，会在 exe 旁边生成 `duel_stats.csv`、`YuGiOh-MDCoinWatch.ini`、`YuGiOh-MDCoinWatch.log`。',
        '',
        '## 用法',
        '',
        '游戏用 16:9 分辨率（1600x900 / 1920x1080 / 2560x1440）的窗口或无边框窗口运行。',
        '双击后浮窗出现在屏幕右上角，切回游戏正常打即可。',
        '',
        '**右键浮窗**可以改文字颜色、强调色、不透明度、大小，以及锁定位置和退出。'
    ) -join $nl
    $notesPath = Join-Path $tmp 'notes.md'
    [System.IO.File]::WriteAllText($notesPath, $notes, (New-Object System.Text.UTF8Encoding($false)))

    & gh release create $Tag (Join-Path $tmp 'MdCoinWatch.exe') (Join-Path $tmp 'MdCoinWatch-lite.exe') --title $Title --notes-file $notesPath
    if ($LASTEXITCODE -ne 0) { throw '发 Release 失败' }

    Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host ''
    Write-Host "完成：https://github.com/$Repo/releases/tag/$Tag"
}
finally {
    Pop-Location
}
