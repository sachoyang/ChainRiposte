@echo off
rem 더블클릭하면 퍼즈 하네스가 도는 콘솔 창이 열린다 (.ps1 을 직접 열면 메모장이 뜬다).
start "" wt.exe --title "ChainRiposte Core Fuzz" powershell -NoProfile -NoExit -ExecutionPolicy Bypass -File "%~dp0screenshot.ps1"
