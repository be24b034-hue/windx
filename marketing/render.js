// Renders marketing/reel.html to numbered frames by seeking its timeline, one frame at a time.
// Usage: node render.js <chrome.exe> <outDir> [fps] [onlyTimes: "1,5.5,9"]
const path = require("path");
const fs = require("fs");
const puppeteer = require("puppeteer-core");

(async () => {
  const [chrome, outDir, fpsArg, only] = process.argv.slice(2);
  const fps = Number(fpsArg) || 30;
  fs.mkdirSync(outDir, { recursive: true });

  const browser = await puppeteer.launch({
    executablePath: chrome,
    headless: true,
    args: ["--hide-scrollbars", "--force-device-scale-factor=1", "--font-render-hinting=none", "--allow-file-access-from-files"],
  });
  const page = await browser.newPage();
  await page.setViewport({ width: 1080, height: 1920, deviceScaleFactor: 1 });
  await page.goto("file:///" + path.resolve(__dirname, "reel.html").replace(/\\/g, "/"), { waitUntil: "load" });
  await page.evaluate(() => document.fonts.ready);
  const total = await page.evaluate(() => window.TOTAL);

  const shoot = async (t, file) => {
    await page.evaluate((time) => { window.seek(time); return new Promise((r) => requestAnimationFrame(() => requestAnimationFrame(r))); }, t);
    await page.screenshot({ path: file, type: "jpeg", quality: 96 });
  };

  if (only) {
    for (const t of only.split(",").map(Number)) await shoot(t, path.join(outDir, `still-${t.toFixed(1)}.jpg`));
  } else {
    const frames = Math.round(total * fps);
    for (let f = 0; f < frames; f++) {
      await shoot(f / fps, path.join(outDir, `f${String(f).padStart(4, "0")}.jpg`));
      if (f % 60 === 0) console.log(`frame ${f}/${frames}`);
    }
  }
  await browser.close();
  console.log("done");
})().catch((e) => { console.error(e); process.exit(1); });
