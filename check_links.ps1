# Markdown Link Checker
`$brokenLinks = @()
`$totalLinks = 0
`$validLinks = 0

`$mdFiles = Get-ChildItem -Recurse -Filter "*.md"
Write-Host "Checking markdown links in $(`$mdFiles.Count) files..."

foreach (`$file in `$mdFiles) {
    `$content = Get-Content `$file.FullName -Raw -ErrorAction SilentlyContinue
    if (-not `$content) { continue }
    
    `$linkPattern = '\[([^\]]*)\]\(([^)]+)\)'
    `$matches = [regex]::Matches(`$content, `$linkPattern)
    
    foreach (`$match in `$matches) {
        `$target = `$match.Groups[2].Value
        `$totalLinks++
        
        if (`$target -match '^(https?://|mailto:|javascript:|#)') {
            continue
        }
        
        `$cleanTarget = `$target -replace '#.*$', '' -replace '\?.*$', ''
        
        if ([string]::IsNullOrWhiteSpace(`$cleanTarget)) {
            continue
        }
        
        `$fileDir = Split-Path `$file.FullName
        `$resolvedPath = Join-Path `$fileDir `$cleanTarget
        `$resolvedPath = [System.IO.Path]::GetFullPath(`$resolvedPath)
        
        if (Test-Path `$resolvedPath) {
            `$validLinks++
        } else {
            `$brokenLinks += "$(`$file.FullName) -> `$target"
        }
    }
}

Write-Host ""
Write-Host "Link Check Summary:"
Write-Host "==================="
Write-Host "Total links found: `$totalLinks"
Write-Host "Valid local links: `$validLinks"
Write-Host "Broken links: $(`$brokenLinks.Count)"

if (`$brokenLinks.Count -gt 0) {
    Write-Host ""
    Write-Host "Broken Links:"
    foreach (`$broken in `$brokenLinks) {
        Write-Host `$broken
    }
}

Write-Host ""
Write-Host "Running: git grep -n '2-core-guided.md'"
Write-Host "=========================================="
git grep -n "2-core-guided.md" 2>&1

if (`$brokenLinks.Count -gt 0) {
    exit 1
}
exit 0
