@echo off
setlocal EnableExtensions DisableDelayedExpansion
if /i "%~1"=="--run" goto run
start "Codex Apps - Auto Deploy" "%ComSpec%" /d /k call "%~f0" --run
exit /b

:run
title Codex Apps - Auto Deploy
rem Always operate on the repository containing this batch file.
cd /d "%~dp0"
if errorlevel 1 goto failed
where git >nul 2>&1
if errorlevel 1 (
    echo [ERROR] Git was not found in PATH.
    exit /b 1
)
if not exist ".git" (
    echo [ERROR] No Git repository exists beside this batch file.
    exit /b 1
)
git rev-parse --verify HEAD >nul 2>&1
if errorlevel 1 goto failed
git symbolic-ref --quiet HEAD >nul 2>&1
if errorlevel 1 (
    echo [ERROR] Detached HEAD. Switch to a branch first.
    exit /b 1
)
git rev-parse --abbrev-ref --symbolic-full-name "@{upstream}" >nul 2>&1
if errorlevel 1 (
    echo [ERROR] No upstream configured. Set the branch upstream first.
    exit /b 1
)
echo ========================================
echo Recording Codex apps in Git
echo Repository: %CD%
echo ========================================
git status --short --branch
if errorlevel 1 goto failed
git add --all
if errorlevel 1 goto failed
git diff --cached --quiet --exit-code
if errorlevel 2 goto failed
if errorlevel 1 goto commit_changes
echo No file changes. Skipping commit.
goto push_changes

:commit_changes
rem Refresh immediately before committing, including when left open overnight.
set "NOW="
for /f %%i in ('powershell.exe -NoProfile -Command "[TimeZoneInfo]::ConvertTimeBySystemTimeZoneId([DateTime]::UtcNow, 'Singapore Standard Time').ToString('yyyyMMdd')"') do set "NOW=%%i"
if not defined NOW (
    echo [ERROR] Could not read the current Malaysia date.
    exit /b 1
)
echo Current date in Asia/Kuala_Lumpur: %NOW%
git commit -m "%NOW% update"
if errorlevel 1 goto failed

:push_changes
rem Push only this branch to its configured upstream. Never force-push.
git -c push.default=upstream -c push.followTags=false push
if errorlevel 1 goto failed
echo.
echo [OK] All completed. You may close this window.
exit /b 0

:failed
echo.
echo [ERROR] Auto deploy stopped. See the Git error above.
echo Any local commit is retained. Fix the issue and run this batch again.
exit /b 1
