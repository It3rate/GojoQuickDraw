/* Offline paging, proposal modes, endpoint inspection and dependent opinions. */
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const {pathToFileURL} = require('node:url');
const {chromium} = require(process.env.GOJO_PLAYWRIGHT_MODULE || 'playwright');
const entry = path.resolve(process.argv[2]), output = path.resolve(process.argv[3]);
let browser;
(async () => {
  fs.mkdirSync(output, {recursive: true});
  browser = await chromium.launch({headless: true, ...(process.env.GOJO_BROWSER ? {executablePath: process.env.GOJO_BROWSER} : {})});
  const page = await browser.newPage({viewport: {width: 1440, height: 1100}}), errors = [], requests = [];
  page.on('pageerror', error => errors.push(error.message));
  await page.route('http**', route => {requests.push(route.request().url()); return route.abort();});
  await page.goto(pathToFileURL(entry).href);
  const navigation = await page.evaluate(() => DATA.pagination);
  assert(navigation && navigation.pages.length);
  const identities = [], pageSizes = [];
  let modesChecked = 0, drawingsInspected = 0, dependenciesInspected = 0;
  for (let number = 0; number < navigation.pages.length; number++) {
    if (number) {await page.click('#nextPage'); await page.waitForFunction(n => typeof PAGING !== 'undefined' && PAGING.page === n, number + 1);}
    const before = await page.evaluate(() => JSON.stringify(DATA));
    const inventory = await page.evaluate(() => DATA.records.map((r,i) => ({index:i, id:r.index.selectionId || r.index.itemId, strokes:r.packet.observation.strokes.length, spans:r.packet.observation.spans.length})));
    pageSizes.push(inventory.length); identities.push(...inventory.map(r => r.id));
    assert(inventory.length <= navigation.pageSize);
    const inspected = navigation.total <= 20 ? inventory : [inventory[0], inventory.at(-1)].filter(Boolean);
    for (const row of inspected) {
      await page.selectOption('#drawing', String(row.index));
      assert.equal(await page.locator('#raw [data-stroke]').count(), row.strokes);
      assert.equal(await page.locator('#fit [data-span]').count(), row.spans);
      await page.check('#directionLayer');
      await page.selectOption('#filter', 'lines');
      if (await page.locator('#list button').count()) {
        await page.locator('#list button').first().click();
        assert((await page.locator('#detail').innerText()).includes('Start (t=0)'));
        assert((await page.locator('#detail').innerText()).includes('End (t=1)'));
      }
      await page.selectOption('#filter', 'claims');
      const dependent = await page.evaluate(() => entries.find(e => e.group === 'claims' && e.row.dependencies?.length)?.key);
      if (dependent) {
        await page.locator(`#list [data-evidence="${dependent}"]`).click();
        assert((await page.locator('#detail').innerText()).includes('Earlier claim dependencies'));
        assert((await page.locator('#detail').innerText()).includes('opposition:'));
        dependenciesInspected++;
      }
      const modes = await page.locator('#readMode option').evaluateAll(options => options.map(o => o.value));
      for (const mode of modes) {
        await page.selectOption('#readMode', mode);
        const expected = await page.evaluate(() => proposalStep()?.edges.length ?? packet().observation.spans.length);
        assert.equal(await page.locator(mode.startsWith('step:') ? '#fit [data-proposal]' : '#fit [data-span]').count(), expected);
        assert(!/NaN|Infinity/.test(await page.locator('#fit').innerHTML()));
        modesChecked++;
      }
      await page.selectOption('#readMode', 'observed-fit');
      drawingsInspected++;
    }
    assert.equal(await page.evaluate(() => JSON.stringify(DATA)), before, 'Inspection changed evidence');
    if (!number) {
      await page.selectOption('#filter', 'claims');
      await page.screenshot({path:path.join(output, 'desktop.png'), fullPage:true});
      await page.setViewportSize({width:390, height:844});
      assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), 'Mobile horizontal overflow');
      await page.screenshot({path:path.join(output, 'mobile.png'), fullPage:true});
      await page.setViewportSize({width:1440, height:1100});
    }
  }
  assert.equal(identities.length, navigation.total);
  assert.equal(new Set(identities).size, navigation.total, 'Missing or repeated page membership');
  if (navigation.pages.length > 1) {
    await page.selectOption('#page', '0'); await page.waitForFunction(() => typeof PAGING !== 'undefined' && PAGING.page === 1);
    await page.click('#lastPage'); await page.waitForFunction(n => typeof PAGING !== 'undefined' && PAGING.page === n, navigation.pages.length);
    await page.click('#firstPage'); await page.waitForFunction(() => typeof PAGING !== 'undefined' && PAGING.page === 1);
  }
  assert.deepEqual(errors, []); assert.deepEqual(requests, []);
  const result = {drawings:navigation.total, pages:navigation.pages.length, pageSizes, drawingsInspected, modesChecked, dependenciesInspected, errors, requests, immutable:true, mobileOverflow:false};
  fs.writeFileSync(path.join(output, 'summary.json'), JSON.stringify(result, null, 2)); console.log(JSON.stringify(result));
})().catch(error => {console.error(error); process.exitCode=1;}).finally(async () => {if(browser)await browser.close();});
