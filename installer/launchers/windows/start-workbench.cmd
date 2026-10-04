@echo off
chcp 65001 >nul
setlocal
set "URL=http://127.0.0.1:5107"
call :portopen
if "%PORTOPEN%"=="1" (
  echo 析庫已在執行，正在開啟瀏覽器。
  start "" "%URL%"
  exit /b 0
)

set "ASPNETCORE_ENVIRONMENT=Production"
set "ASPNETCORE_URLS=%URL%"
set "DOTNET_NOLOGO=1"
set "DOTNET_CLI_TELEMETRY_OPTOUT=1"
echo 正在啟動析庫。關掉標題為「析庫」的視窗就會停止工作台。
start "析庫" /D "%~dp0web" "%~dp0web\ExportDataWeb.exe"

set /a N=0
:waitloop
set /a N+=1
if %N% GTR 40 goto notready
call :portopen
if "%PORTOPEN%"=="1" goto openbrowser
timeout /t 1 /nobreak >nul
goto waitloop

:openbrowser
echo 工作台：%URL%
start "" "%URL%"
exit /b 0

:notready
echo 析庫沒有在 40 秒內啟動。請看「析庫」視窗裡的錯誤訊息。
exit /b 1

:portopen
set "PORTOPEN=0"
powershell -NoProfile -ExecutionPolicy Bypass -Command "try { $c = New-Object System.Net.Sockets.TcpClient; $c.Connect('127.0.0.1', 5107); $c.Close(); exit 0 } catch { exit 1 }"
if %ERRORLEVEL%==0 set "PORTOPEN=1"
exit /b 0
