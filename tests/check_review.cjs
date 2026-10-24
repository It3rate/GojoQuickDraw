/* Optional browser verification: install Playwright separately, then run
   GOJO_PLAYWRIGHT_MODULE=/path/to/playwright node tests/check_review.cjs review.html output-dir
   GOJO_BROWSER may point at an installed Chromium-compatible executable. */
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const {pathToFileURL} = require('node:url');
const {chromium} = require(process.env.GOJO_PLAYWRIGHT_MODULE || 'playwright');
const review = path.resolve(process.argv[2]);
const output = path.resolve(process.argv[3] || path.join(path.dirname(review), 'review-qa'));
let browser;
(async () => {
  fs.mkdirSync(output, {recursive: true});
  browser = await chromium.launch({headless: true, ...(process.env.GOJO_BROWSER ? {executablePath: process.env.GOJO_BROWSER} : {})});
  const page = await browser.newPage({viewport: {width: 1440, height: 1100}});
  const errors = [], requests = [];
  page.on('pageerror', error => errors.push(error.message));
  await page.route('http**', route => { requests.push(route.request().url()); return route.abort(); });
  await page.goto(pathToFileURL(review).href);
  const original = await page.evaluate(() => JSON.stringify(DATA));
  const inventory = await page.evaluate(() => DATA.records.map((r,i) => ({index: i, id: r.index.itemId, strokes: r.packet.observation.strokes.length, spans: r.packet.observation.spans.length})));
  let inspected = 0, downloads = 0;
  for (const row of inventory) {
    await page.selectOption('#drawing', String(row.index));
    assert.equal(await page.locator('#raw [data-stroke]').count(), row.strokes);
    assert.equal(await page.locator('#fit [data-span]').count(), row.spans);
    const numerical = await page.locator('#fit').innerHTML();
    assert(!numerical.includes('NaN') && !numerical.includes('Infinity'), 'Non-finite rendered geometry');
    await page.check('#seedLayer');
    await page.check('#mirrorLayer');
    const groups = await page.locator('#filter option').evaluateAll(options => options.map(o => o.value));
    for (const group of groups) {
      await page.selectOption('#filter', group);
      const first = page.locator('#list [data-evidence]').first();
      if (await first.count()) {
        await first.click();
        assert((await page.locator('#detail').innerText()).includes('supporting interval'));
        if (['parts','contextRelations','assumptions','views'].includes(group)) {
          assert((await page.locator('#detail').innerText()).includes('Seeded / provisional'));
        }
        inspected++;
      }
    }
    await page.selectOption('#filter', 'all');
    const downloadPromise = page.waitForEvent('download');
    await page.click('#downloadSource');
    const download = await downloadPromise;
    const text = fs.readFileSync(await download.path(), 'utf8');
    const expected = await page.evaluate(() => packet().sourceOriginalJson);
    assert.equal(text, expected, 'Original source download must preserve exact JSON text');
    downloads++;
    if (row.strokes > 1) {
      await page.locator('#strokeLimit').evaluate(input => { input.value = '1'; input.dispatchEvent(new Event('input')); });
      assert.equal(await page.locator('#raw [data-stroke]').count(), 1);
      const onlyFirst = await page.locator('#fit [data-span]').evaluateAll(paths => paths.map(p => p.dataset.span));
      const expectedFirst = await page.evaluate(() => packet().observation.spans.filter(s => s.strokeIndex === 0).map(s => s.id));
      assert.deepEqual(onlyFirst, expectedFirst);
    }
    await page.selectOption('#drawing', String(row.index));
    await page.uncheck('#seedLayer');
    await page.uncheck('#mirrorLayer');
    if (row.index === 0) await page.screenshot({path: path.join(output, 'desktop.png'), fullPage: true});
  }
  assert.equal(await page.evaluate(() => JSON.stringify(DATA)), original, 'Inspection mutated cached evidence');
  await page.setViewportSize({width: 390, height: 844});
  assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), 'Mobile horizontal overflow');
  await page.screenshot({path: path.join(output, 'mobile.png'), fullPage: true});
  assert.deepEqual(errors, []);
  assert.deepEqual(requests, []);
  const result = {drawings: inventory.length, groupsInspected: inspected, downloads, errors, requests, immutable: true, mobileOverflow: false};
  fs.writeFileSync(path.join(output, 'summary.json'), JSON.stringify(result, null, 2));
  console.log(JSON.stringify(result));
})().catch(error => { console.error(error); process.exitCode = 1; }).finally(async () => { if (browser) await browser.close(); });
