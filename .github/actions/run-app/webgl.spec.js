const { test, expect } = require('@playwright/test');
const fs = require('fs');
const path = require('path');
const timeout = parseInt(process.env.APP_TIMEOUT || '600') * 1000;
const mode = process.env.APP_MODE || 'test';

test('UniText WebGL ' + mode, async ({ page }) => {
  page.on('console', msg => console.log('[Browser]', msg.text()));
  await page.goto('https://localhost:8080', { waitUntil: 'networkidle', timeout: 60000 });

  if (mode === 'benchmark') {
    await page.waitForFunction(() => window.unityBenchmarkComplete === true, { timeout });
    const json = await page.evaluate(() => window.unityBenchmarkResults);
    expect(json).toBeTruthy();
    fs.writeFileSync('benchmarkResults.json', json);
    console.log('Benchmark results saved');
  } else {
    await page.waitForFunction(() => window.unityTestsComplete === true, { timeout });
    const results = await page.evaluate(() => window.unityTestResults);
    fs.writeFileSync('testResults.xml', results.xml);
    console.log(`Tests: ${results.passed}/${results.total} passed`);

    const screenshots = await page.evaluate(() => window.unityTestScreenshots || []);
    if (screenshots.length > 0) {
      fs.mkdirSync('screenshots', { recursive: true });
      for (const s of screenshots) {
        fs.writeFileSync(path.join('screenshots', `${s.name}.png`), Buffer.from(s.data, 'base64'));
      }
    }

    expect(results.allPassed).toBe(true);
  }
});
