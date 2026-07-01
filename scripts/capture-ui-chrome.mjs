import { chromium } from "playwright";
import path from "node:path";
import fs from "node:fs/promises";

const appUrl = process.env.APP_URL ?? "http://localhost:5075";
const outputDir = process.env.SCREENSHOT_DIR ?? "labs/lab-5-ui/images";
const width = Number.parseInt(process.env.SCREENSHOT_WIDTH ?? "1920", 10);
const height = Number.parseInt(process.env.SCREENSHOT_HEIGHT ?? "1080", 10);
const promptText =
  process.env.SCREENSHOT_PROMPT ??
  "Create a launch checklist for an agent named triage-coach in the pilot environment.";

await fs.mkdir(outputDir, { recursive: true });

const browser = await chromium.launch({
  channel: "chrome",
  headless: false,
  args: [`--window-size=${width},${height}`, "--start-maximized"]
});

const context = await browser.newContext({ viewport: { width, height } });
const page = await context.newPage();

try {
  await page.goto(appUrl, { waitUntil: "networkidle" });

  const shot1 = path.join(outputDir, "01-chat-ui-landing.png");
  await page.screenshot({ path: shot1, fullPage: false });

  const promptBox = page.locator("textarea");
  await promptBox.fill(promptText);

  const shot2 = path.join(outputDir, "02-chat-ui-prompt-entered.png");
  await page.screenshot({ path: shot2, fullPage: false });

  await page.getByRole("button", { name: "Send Prompt" }).click();

  // Wait for the real agent reply: the user message must appear, a second
  // assistant bubble (the reply, not the greeting) must render, and the
  // "…thinking" indicator must be gone. Matching only "ASSISTANT" text is not
  // enough because the initial greeting is already an assistant bubble.
  await page.waitForFunction(
    () => {
      const assistantReplies = document.querySelectorAll(
        ".chat-item.assistant:not(.thinking)"
      ).length;
      const userMessages = document.querySelectorAll(".chat-item.user").length;
      const stillThinking = document.querySelector(".chat-item.thinking") !== null;
      return userMessages >= 1 && assistantReplies >= 2 && !stillThinking;
    },
    undefined,
    { timeout: 120000 }
  );

  // Small settle delay so the reply text is fully painted before capture.
  await page.waitForTimeout(750);

  const shot3 = path.join(outputDir, "03-chat-ui-response-hd.png");
  await page.screenshot({ path: shot3, fullPage: false });

  console.log(`Saved: ${shot1}`);
  console.log(`Saved: ${shot2}`);
  console.log(`Saved: ${shot3}`);
}
finally {
  await context.close();
  await browser.close();
}
