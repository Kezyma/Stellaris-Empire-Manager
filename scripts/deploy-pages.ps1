<#
.SYNOPSIS
    Builds the web app and stages it for GitHub Pages.

.DESCRIPTION
    The site cannot be built by a hosted runner: extraction reads a Stellaris installation and no
    runner has one. So the whole thing is built here and the result pushed.

    This prepares the branch and stops. Pushing is left to you, because it is the step that makes
    the artwork public and that should be a decision rather than a side effect.

.PARAMETER Branch
    The branch GitHub Pages serves from.

.PARAMETER Base
    Where the site will live. The repository's name for a project site; "/" for a custom domain.

.PARAMETER SkipExtract
    Reuse the game data already under wwwroot instead of reading the installation again.

.EXAMPLE
    ./scripts/deploy-pages.ps1
    git -C .pages push --force origin gh-pages
#>

[CmdletBinding()]
param(
    [string] $Branch = 'gh-pages',
    [string] $Base = '/Stellaris-Empire-Manager/',
    [switch] $SkipExtract
)

$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
Push-Location $repo

try {
    if (-not $SkipExtract) {
        # The wardrobe is the pieces a ruler's likeness is drawn from. It is the largest thing here
        # by count and the site cannot draw a face without it, so it goes up with the rest.
        Write-Host 'Reading the game installation...' -ForegroundColor Cyan
        dotnet run --project src/Sem.Cli -- extract --web --wardrobe
        if ($LASTEXITCODE -ne 0) { throw 'Extraction failed.' }
    }

    Write-Host "Publishing for $Base ..." -ForegroundColor Cyan
    $publish = Join-Path $repo 'artifacts/pages'
    if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }

    dotnet publish src/Sem.Web -c Release -o $publish "-p:PagesBase=$Base"
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }

    $site = Join-Path $publish 'wwwroot'

    # Worth failing loudly on: without it the site is a blank page, and the cause is not obvious
    # from anything the browser reports.
    if (-not (Test-Path (Join-Path $site '.nojekyll'))) {
        throw 'The published site has no .nojekyll; GitHub Pages would drop the _framework folder.'
    }

    # A worktree keeps the deployed branch out of the working copy, so a half-finished deploy cannot
    # be confused with the source tree.
    $tree = Join-Path $repo '.pages'
    if (Test-Path $tree) { git worktree remove --force $tree }

    if (git show-ref --verify --quiet "refs/heads/$Branch") {
        git worktree add $tree $Branch
    }
    else {
        git worktree add --detach $tree
        git -C $tree checkout --orphan $Branch
    }

    # Replaced wholesale rather than added to: the site is forty megabytes, and a branch that keeps
    # every version of it grows by that much on each deploy.
    Get-ChildItem $tree -Force |
        Where-Object { $_.Name -ne '.git' } |
        Remove-Item -Recurse -Force

    Copy-Item "$site/*" $tree -Recurse -Force

    git -C $tree add --all
    git -C $tree commit --quiet --message "Publish the site" --allow-empty

    Write-Host ''
    Write-Host "Staged in $tree on $Branch." -ForegroundColor Green
    Write-Host 'This carries Stellaris artwork and text. Publishing it makes that public.'
    Write-Host ''
    Write-Host "  git -C .pages push --force origin $Branch"
    Write-Host ''
    Write-Host 'The push is the last manual step: .github/workflows/pages.yml takes it from there.'
}
finally {
    Pop-Location
}
