@echo off
setlocal enabledelayedexpansion

REM Usage: analyze_all.bat <protocol.json> <log_or_dir> [log_or_dir ...]
REM   analyze_all.bat ..\protocols\mmorpg_simulator.json ..\captures\archive
REM   analyze_all.bat ..\protocols\mmorpg_simulator.json capture1.log capture2.log

if "%~2"=="" (
    echo Usage: %~nx0 ^<protocol.json^> ^<log_or_dir^> [log_or_dir ...]
    exit /b 1
)

set PROTOCOL=%~1
set EXE=%~dp0bin\Release\net9.0\PacketCaptureAgent.exe
set COUNT=0

shift
:loop
if "%~1"=="" goto done

if exist "%~1\*" (
    REM Directory — process all .log files
    for %%f in ("%~1\*.log") do (
        set /a COUNT+=1
        echo [!COUNT!] Analyzing %%f
        "%EXE%" -p "%PROTOCOL%" --analyze "%%f"
    )
) else (
    REM Single file
    set /a COUNT+=1
    echo [!COUNT!] Analyzing %~1
    "%EXE%" -p "%PROTOCOL%" --analyze "%~1"
)

shift
goto loop

:done
echo.
echo Done: %COUNT% log(s) analyzed.
echo Run --build-behavior to regenerate BT.
