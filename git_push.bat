@echo off

git remote -v

set /p commitmessage=Git commit message:

git add .

git commit -m "%commitMessage%"

echo Pushing to default remote repository...
git push
if %errorlevel% neq 0 (
    echo Failed to push to default remote repository.
    pause
    exit /b %errorlevel%
)

echo Pushing to upstream remote on 'master' branch...
git push upstream master
if %errorlevel% neq 0 (
    echo Failed to push to upstream remote on 'main' branch.
    pause
    exit /b %errorlevel%
)

echo Pushing to 'upstream_gitcode' remote on 'master' branch...
git push upstream_gitcode master
if %errorlevel% neq 0 (
    echo Failed to push to 'upstream_gitcode' remote on 'main' branch.
    pause
    exit /b %errorlevel%
)

echo All pushes completed successfully.
pause