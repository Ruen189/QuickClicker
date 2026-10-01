@echo off
rem Собирает (если надо) и запускает кликер БЕЗ чёрного окна cmd:
rem после сборки окно закрывается, приложение работает отдельно.
cd /d "%~dp0"

if not exist "bin\Release\net9.0-windows\QuickClicker.exe" (
    dotnet build -c Release -v quiet >nul 2>nul
)

if not exist "bin\Release\net9.0-windows\QuickClicker.exe" (
    echo Не удалось собрать проект. Убедитесь, что установлен .NET 9 SDK.
    pause
    exit /b 1
)

start "" "bin\Release\net9.0-windows\QuickClicker.exe"
