@echo off
rem Собирает автономный (self-contained) одиночный .exe в папку publish.
rem EnableCompressionInSingleFile сжимает рантайм внутри exe: ~99 МБ -> ~46 МБ.
cd /d "%~dp0"
dotnet publish -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true ^
  -o publish
echo.
echo Готово: publish\QuickClicker.exe
pause
