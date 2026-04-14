@echo off
setlocal

set VERSION_URL=https://raw.githubusercontent.com/typorium/RotationDePetits/main/remote_version.txt
set ZIP_URL=https://github.com/typorium/RotationDePetits/releases/latest/download/build.zip

set LOCAL_VERSION_FILE=version.txt
set REMOTE_VERSION_FILE=remote_version.txt
set ZIP_FILE=update.zip

echo Attends controle de police je regarde si y'a une mise a jour

:: Télécharger version distante
powershell -Command "Invoke-WebRequest %VERSION_URL% -OutFile %REMOTE_VERSION_FILE%"

:: Lire versions
set /p LOCAL_VERSION=<%LOCAL_VERSION_FILE% 2>nul
set /p REMOTE_VERSION=<%REMOTE_VERSION_FILE%

if "%LOCAL_VERSION%"=="%REMOTE_VERSION%" (
    echo Ok le jeu est a jour bg bon mario a toi
) else (
    echo Ah pas de chance y'a une mise a jour !!!
    powershell -Command "Invoke-WebRequest %ZIP_URL% -OutFile %ZIP_FILE%"
    echo Je me extrais le .zip la
    :: Supprimer ancien jeu
    rmdir /s /q Game
    :: Extraire
    powershell -Command "Expand-Archive %ZIP_FILE% -DestinationPath Game"
    :: Mettre à jour version
    echo %REMOTE_VERSION% > %LOCAL_VERSION_FILE%
    del %ZIP_FILE%
)
:: Lancer le jeu
echo Ok le jeu se lance bg
start Game\NSMB-MarioVsLuigi.exe

endlocal
pause