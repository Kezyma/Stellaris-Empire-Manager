@echo off
setlocal

rem  Rebuilds the website ready for pushing to main.
rem
rem  Everything the Pages workflow does, it does on a runner that has no Stellaris installation - so
rem  extraction cannot happen there and the extracted data is committed instead. That makes this the
rem  one step a push to main cannot do for itself: re-read the game, then build and test against what
rem  came out, so that whatever is committed is what the site will be published from.
rem
rem  Run it after the game updates, after the extractor changes, and before pushing either.
rem
rem  It deliberately leaves --wardrobe off. That flag re-bakes 8,629 ruler-appearance images,
rem  which is most of the run time and almost never changes - the whole of 4.5 moved one crop
rem  box. But "almost never" is not never, so run it by hand after a major patch:
rem
rem      dotnet run --project src\Sem.Cli -c Release -- extract --web --wardrobe

cd /d "%~dp0"

echo.
echo === Reading the game ===
dotnet run --project src\Sem.Cli -c Release -- extract --web
if errorlevel 1 goto :failed

echo.
echo === Building ===
dotnet build -c Release
if errorlevel 1 goto :failed

echo.
echo === Testing ===
dotnet test -c Release --no-build
if errorlevel 1 goto :failed

rem  The same publish the runner does, so a failure here is found before it is a failed deployment
rem  rather than after. The output is thrown away: what gets published is built on the runner from
rem  what is committed, and a copy of it in the working tree would only be something to gitignore.
echo.
echo === Publishing, as a rehearsal ===
dotnet publish src\Sem.Web -c Release -o "%TEMP%\sem-pages-check"
if errorlevel 1 goto :failed

if not exist "%TEMP%\sem-pages-check\wwwroot\.nojekyll" (
    echo.
    echo FAILED: no .nojekyll in the published site. Pages would drop _framework and serve a blank page.
    goto :failed
)

rmdir /s /q "%TEMP%\sem-pages-check"

echo.
echo === Ready ===
git status --short
echo.
echo Commit whatever is listed above and push to main. Pages publishes on the push.
exit /b 0

:failed
echo.
echo === Not ready. Fix the failure above before pushing. ===
exit /b 1
