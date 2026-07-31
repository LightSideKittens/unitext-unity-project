const { test, expect } = require('@playwright/test');
const fs = require('fs');
const path = require('path');
const timeout = parseInt(process.env.APP_TIMEOUT || '1800') * 1000;
const mode = process.env.APP_MODE || 'test';
const suite = process.env.APP_SUITE || 'all';

function saveScreenshots(screenshots) {
  if (!screenshots || screenshots.length === 0) return;
  fs.mkdirSync('screenshots', { recursive: true });
  for (const s of screenshots) {
    fs.writeFileSync(path.join('screenshots', `${s.name}.png`), Buffer.from(s.data, 'base64'));
  }
  console.log(`Saved ${screenshots.length} screenshot(s)`);
}

async function run(page) {
  const url = mode === 'benchmark'
    ? `https://localhost:8080/?suite=${encodeURIComponent(suite)}`
    : 'https://localhost:8080';
  await page.goto(url, { waitUntil: 'networkidle', timeout: 60000 });

  if (mode === 'benchmark') {
    await page.waitForFunction(() => window.unityBenchmarkComplete === true, { timeout });
    const json = await page.evaluate(() => window.unityBenchmarkResults);
    expect(json).toBeTruthy();
    fs.writeFileSync('benchmarkResults.json', json);
    console.log('Benchmark results saved');

    saveScreenshots(await page.evaluate(() => window.unityTestScreenshots || []));
  } else {
    await page.waitForFunction(() => window.unityTestsComplete === true, { timeout });
    const results = await page.evaluate(() => window.unityTestResults);
    fs.writeFileSync('testResults.xml', results.xml);
    console.log(`Tests: ${results.passed}/${results.total} passed`);

    saveScreenshots(await page.evaluate(() => window.unityTestScreenshots || []));

    expect(results.allPassed).toBe(true);
  }
}

test('UniText WebGL ' + mode, async ({ page }) => {
  let fail;
  const browserFailure = new Promise((_, reject) => fail = reject);
  page.on('console', msg => console.log('[Browser]', msg.text()));
  page.on('pageerror', fail);

  await Promise.race([run(page), browserFailure]);
});
