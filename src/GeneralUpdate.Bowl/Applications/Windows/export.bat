@echo off
setlocal

if "%~1"=="" (
    echo Please provide the export path as the first parameter.
    exit /b 1
)

set exportDir=%~1

if not exist "%exportDir%" (
    mkdir "%exportDir%"
)

set outputFile=%exportDir%\driverInfo.txt

:: 导出驱动信息
driverquery /v /fo table > "%outputFile%"
echo %outputFile% Export successfully.

:: 导出系统信息
set systemInfoFile=%exportDir%\systeminfo.txt
systeminfo > "%systemInfoFile%"
echo %systemInfoFile% Export successfully.

:: 获取当前日期
for /f "tokens=1-4 delims=/- " %%i in ('date /t') do (
    set yyyy=%%i
    set mm=%%j
    set dd=%%k
)

:: 设置日志文件名
set logFile=%exportDir%\systemlog.evtx

:: 导出系统日志
wevtutil epl System "%logFile%" /q:"*[System[TimeCreated[timediff(@SystemTime) <= 86400000]]]"
echo %logFile% Export successfully.

endlocal