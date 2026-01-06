const { test, expect } = require('@playwright/test');
const fs = require('fs');
const path = require('path');
const timeout = parseInt(process.env.TEST_TIMEOUT || '300') * 1000;

test('UniText WebGL Tests', async ({ page }) => {
  page.on('console', msg => console.log('[Browser]', msg.text()));
  await page.goto('https://localhost:8080', { waitUntil: 'networkidle', timeout: 60000 });
  await page.waitForFunction(() => window.unityTestsComplete === true, { timeout });

  const results = await page.evaluate(() => window.unityTestResults);
  fs.writeFileSync('testResults.xml', results.xml);
  console.log(`Tests: ${results.passed}/${results.total} passed`);

  const screenshots = await page.evaluate(() => window.unityTestScreenshots || []);
  if (screenshots.length > 0) {
    fs.mkdirSync('screenshots', { recursive: true });
    for (const s of screenshots) {
      const buffer = Buffer.from(s.data, 'base64');
      fs.writeFileSync(path.join('screenshots', `${s.name}.png`), buffer);
      console.log(`Screenshot saved: ${s.name}.png`);
    }
  }

  expect(results.allPassed).toBe(true);
});
