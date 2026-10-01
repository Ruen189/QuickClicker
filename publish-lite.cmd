@echo off
rem Собирает "лёгкий" одиночный .exe (~185 КБ) в папку publish-lite.
rem Рантайм .NET внутрь НЕ входит, поэтому на той машине, где вы запускаете
rem exe, должен быть установлен .NET 9 Desktop Runtime
rem (Microsoft.WindowsDesktop.App 9.x), иначе exe не стартует.
cd /d "%~dp0"
dotnet publish -c Release -r win-x64 --self-contained false ^
  -p:PublishSingleFile=true ^
  -o publish-lite
echo.
echo Готово: publish-lite\QuickClicker.exe (нужен установленный .NET 9 Desktop Runtime)
pause
