@echo off
rem Запуск кликера от имени администратора.
rem Нужно, если игра/Steam запущены с правами администратора:
rem иначе Windows блокирует инъекцию кликов в такое окно (UIPI).
cd /d "%~dp0"

if not exist "bin\Release\net9.0-windows\QuickClicker.exe" (
    dotnet build -c Release -v quiet >nul 2>nul
)

powershell -NoProfile -Command "Start-Process -FilePath '%~dp0bin\Release\net9.0-windows\QuickClicker.exe' -Verb RunAs"
