@echo off
setlocal
cd /d "%~dp0.."
python "%~dp0request_v2_editor.py" launch
if errorlevel 1 pause
