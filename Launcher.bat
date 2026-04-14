@echo off
setlocal

set VERSION_URL=https://raw.githubusercontent.com/typorium/RotationDePetits/master/remote_version.txt
set ZIP_URL=https://github.com/typorium/RotationDePetits/releases/latest/download/build.zip
set LOCAL_VERSION_FILE=version.txt
set REMOTE_VERSION_FILE=remote_version.txt
set ZIP_FILE=update.zip
set GAME_EXE=NSMB-MarioVsLuigi.exe

echo Attends controle de police je regarde si y'a une mise a jour

if not exist %LOCAL_VERSION_FILE% echo 0.0.0 > %LOCAL_VERSION_FILE%

powershell -Command "Invoke-WebRequest %VERSION_URL% -OutFile %REMOTE_VERSION_FILE%"

set /p LOCAL_VERSION=<%LOCAL_VERSION_FILE%
set /p REMOTE_VERSION=<%REMOTE_VERSION_FILE%

set "LOCAL_VERSION=%LOCAL_VERSION: =%"
set "REMOTE_VERSION=%REMOTE_VERSION: =%"

echo Local: %LOCAL_VERSION%
echo Remote: %REMOTE_VERSION%

if "%LOCAL_VERSION%"=="%REMOTE_VERSION%" (
    echo Ok le jeu est a jour belle bite
) else (
    echo Ah pas de chance y'a une mise a jour !!!

    powershell -Command "Invoke-WebRequest %ZIP_URL% -OutFile %ZIP_FILE%"

    echo oh ma gaaaaaaad je télécharge le femboy furry update

    if exist temp rmdir /s /q temp
    powershell -Command "Expand-Archive %ZIP_FILE% -DestinationPath temp"

    if exist Game rmdir /s /q Game
    mkdir Game
    :: Copier game
    xcopy temp\build\Game Game /E /I /Y
    :: Retirer temp
    rmdir /s /q temp
    del %ZIP_FILE%
    
    echo %REMOTE_VERSION% > %LOCAL_VERSION_FILE%
    del %REMOTE_VERSION_FILE%
    echo YIPEE c'est fini
)
echo Le jeu se lance (NOUS SOMMES YACK TENEBREUX)
start Game\%GAME_EXE%

endlocal
pause