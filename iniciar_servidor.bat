@echo off
title Servidor Stream To Earn - TikTok y YouTube Live GTA V
cd /d "%~dp0server"

where node >nul 2>nul
if %errorlevel% neq 0 (
    echo [ERROR] Node.js no esta instalado o no esta en el PATH.
    pause
    exit /b 1
)

if not exist node_modules (
    echo [INFO] Instalando dependencias...
    call npm install
)

echo ========================================================
echo   Iniciando Servidor TikTok y YouTube Live + GTA V API
echo   Puerto: 3000 - Control: http://localhost:3000/admin.html
echo ========================================================
node index.js

if %errorlevel% neq 0 (
    echo.
    echo [ERROR] El servidor se detuvo con errores.
)
pause
