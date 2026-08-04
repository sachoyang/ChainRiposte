# 스샷용 런처 — 퍼즈를 돌리고 창을 열어 둔다.
# .ps1 을 더블클릭하면 메모장이 열리므로, 이 파일은 직접 열지 말고
# Tools/CoreFuzz/퍼즈_스샷.bat 을 더블클릭할 것.

$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding $false
$Host.UI.RawUI.WindowTitle = "ChainRiposte · Core 퍼즈 하네스"

$here = Split-Path -Parent $MyInvocation.MyCommand.Path

try {
    & (Join-Path $here "run.ps1") -Boards 6000
}
catch {
    Write-Host ""
    Write-Host "  실패: $_" -ForegroundColor Red
}

Write-Host ""
Write-Host "  ─────────────────────────────────────────────────────────────" -ForegroundColor DarkGray
Write-Host "  스샷 요령" -ForegroundColor DarkGray
Write-Host "    · 글씨가 크면  Ctrl + 마우스휠 아래로  (한 화면에 넣기)" -ForegroundColor DarkGray
Write-Host "    · 창 캡처는  Alt + Print Screen" -ForegroundColor DarkGray
Write-Host "    · 다시 돌리려면  ↑  누르고 Enter" -ForegroundColor DarkGray
Write-Host "  ─────────────────────────────────────────────────────────────" -ForegroundColor DarkGray
Write-Host ""
