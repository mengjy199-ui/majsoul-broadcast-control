param(
    [string]$PublishDirectory = (Join-Path $PSScriptRoot 'dist\雀魂直播控制中心-v29.2-win-x64')
)

$ErrorActionPreference = 'Stop'
$executable = Join-Path $PublishDirectory '雀魂直播控制中心-v29.2.exe'
if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) { throw "未找到发布程序：$executable" }
foreach ($port in 7998, 17657) {
    if (Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue) {
        throw "$port 端口已被占用，未执行整包测试。"
    }
}

$process = $null
try {
    $startInfo = [Diagnostics.ProcessStartInfo]::new($executable)
    $startInfo.WorkingDirectory = $PublishDirectory
    $startInfo.UseShellExecute = $true
    $startInfo.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
    $process = [Diagnostics.Process]::Start($startInfo)
    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    do {
        Start-Sleep -Milliseconds 250
        $process.Refresh()
        $apiReady = $null -ne (Get-NetTCPConnection -LocalPort 7998 -State Listen -ErrorAction SilentlyContinue)
        $obsReady = $null -ne (Get-NetTCPConnection -LocalPort 17657 -State Listen -ErrorAction SilentlyContinue)
    } while ([DateTime]::UtcNow -lt $deadline -and -not ($apiReady -and $obsReady -and $process.MainWindowHandle -ne 0))

    if ($process.HasExited) { throw "v29 启动后退出，退出码 $($process.ExitCode)。" }
    if (-not $apiReady) { throw 'v29 后台未监听 7998。' }
    if (-not $obsReady) { throw 'v29 OBS 服务未监听 17657。' }
    if ($process.MainWindowHandle -eq 0) { throw 'v29 统一主窗口未创建。' }

    $overlay = Invoke-WebRequest -UseBasicParsing -Uri 'http://127.0.0.1:17657/overlay' -TimeoutSec 5
    $state = Invoke-WebRequest -UseBasicParsing -Uri 'http://127.0.0.1:17657/api/state' -TimeoutSec 5
    $previewState = Invoke-WebRequest -UseBasicParsing -Uri 'http://127.0.0.1:17657/api/preview-state' -TimeoutSec 5
    if ($overlay.StatusCode -ne 200 -or $state.StatusCode -ne 200 -or $previewState.StatusCode -ne 200) { throw 'OBS HTTP 接口未正常返回。' }
    foreach ($asset in 'art-avatar-frame', 'art-name-divider') {
        $image = Invoke-WebRequest -UseBasicParsing -Uri "http://127.0.0.1:17657/asset/$asset" -TimeoutSec 5
        if ($image.StatusCode -ne 200 -or $image.Headers['Content-Type'] -notmatch 'image/png') {
            throw "新美术素材未正常返回：$asset"
        }
    }

    if (-not $process.CloseMainWindow()) { throw '无法向 v29 主窗口发送关闭请求。' }
    if (-not $process.WaitForExit(8000)) { throw 'v29 主程序未在 8 秒内退出。' }
    Start-Sleep -Milliseconds 500
    $apiStillListening = $null -ne (Get-NetTCPConnection -LocalPort 7998 -State Listen -ErrorAction SilentlyContinue)
    $obsStillListening = $null -ne (Get-NetTCPConnection -LocalPort 17657 -State Listen -ErrorAction SilentlyContinue)
    if ($apiStillListening -or $obsStillListening) { throw '关闭 v29 后仍有服务端口未释放。' }

    "PACKAGE_SMOKE_OK overlay=$($overlay.StatusCode) state=$($state.StatusCode) preview=$($previewState.StatusCode) ports=released"
}
finally {
    if ($null -ne $process) {
        if (-not $process.HasExited) { $process.Kill($true) }
        $process.Dispose()
    }
}
