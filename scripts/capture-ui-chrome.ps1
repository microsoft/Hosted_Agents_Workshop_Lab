param(
    [string]$AppUrl = "http://localhost:5075",
    # Note: Flask Chat UI runs on port 5075 by default
    [string]$ScreenshotDir = "labs/lab-5-ui/images",
    [int]$Width = 1920,
    [int]$Height = 1080,
    [string]$Prompt = "Create a launch checklist for an agent named triage-coach in the pilot environment."
)

$env:APP_URL = $AppUrl
$env:SCREENSHOT_DIR = $ScreenshotDir
$env:SCREENSHOT_WIDTH = "$Width"
$env:SCREENSHOT_HEIGHT = "$Height"
$env:SCREENSHOT_PROMPT = $Prompt

node .\scripts\capture-ui-chrome.mjs
