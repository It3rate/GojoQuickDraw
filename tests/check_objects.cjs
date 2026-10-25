/* Read-only object candidates, source memberships, color toggles and paging.
   Usage: node check_objects.cjs evidence.html comparison.html qa-output */
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const {pathToFileURL} = require('node:url');
const {chromium} = require(process.env.GOJO_PLAYWRIGHT_MODULE || 'playwright');
assert(process.argv.length >= 5, 'Provide inspector, gallery and QA output paths');
const inspector = path.resolve(process.argv[2]), gallery = path.resolve(process.argv[3]);
const output = path.resolve(process.argv[4]);
let browser;
(async () => {
  fs.mkdirSync(output, {recursive:true});
  browser = await chromium.launch({headless:true, ...(process.env.GOJO_BROWSER ? {executablePath:process.env.GOJO_BROWSER} : {})});
  const page = await browser.newPage({viewport:{width:1440,height:1100}}), errors = [], requests = [];
  page.on('pageerror', error => errors.push(error.message));
  await page.route('http**', route => {requests.push(route.request().url()); return route.abort();});
  const navigate = async (control, number) => {
    await page.click('#' + control);
    await page.waitForFunction(n => typeof PAGING !== 'undefined' && PAGING.page === n, number);
  };
  const sourceGeometry = () => page.locator('#raw [data-stroke]').evaluateAll(nodes => nodes.map(n => [n.dataset.stroke,n.getAttribute('points')]));
  const fittedGeometry = () => page.locator('#fit [data-span],#fit [data-proposal]').evaluateAll(nodes => nodes.map(n => [n.dataset.span||n.dataset.proposal,n.getAttribute('d')]));
  await page.goto(pathToFileURL(inspector).href);
  const navigation = await page.evaluate(() => PAGING);
  assert(navigation && navigation.pages.length, 'Use a paged inspector');
  let inspected = 0, candidatesChecked = 0, membershipsChecked = 0, nestingChecked = 0, unknownChecked = 0, finalBoundsChecked = 0;
  let reference = null;
  for (let p=0; p<navigation.pages.length; p++) {
    if (p) await navigate('nextPage', p+1);
    const before = await page.evaluate(() => JSON.stringify(DATA));
    const inventory = await page.evaluate(() => DATA.records.map((r,i) => ({i,available:r.objectView?.available,candidates:r.objectView?.candidates||0})));
    for (const row of inventory) {
      await page.selectOption('#drawing', String(row.i));
      await page.selectOption('#readMode', 'observed-fit');
      assert.equal(await page.locator('#objectPanel').isVisible(), !!row.available);
      if (!row.available) continue;
      inspected++;
      await page.check('#objectColor');
      assert.equal(await page.locator('#objectList button').count(), row.candidates);
      const ink = await sourceGeometry(), fit = await fittedGeometry();
      const candidates = await page.evaluate(() => objectEntries().map(e => ({id:e.row.id,key:e.key,parents:e.row.parentIds||[]})));
      const choices = [...new Set([0,candidates.findIndex(c=>c.parents.length)].filter(i=>i>=0&&i<candidates.length))];
      for (const i of choices) {
        await page.locator('#objectList button').nth(i).click();
        const detail = await page.locator('#detail').innerText();
        assert(detail.includes('Seeded / provisional'), 'Object role was presented as measured truth');
        assert(detail.includes('Instance support:'), 'Object instance identity is missing');
        if (candidates[i].parents.length) {
          assert(detail.includes('Parent / attachment candidates'));
          assert(await page.locator('#detail button:not(:disabled)').count() >= candidates[i].parents.length);
          nestingChecked++;
        }
        candidatesChecked++;
      }
      const line = await page.evaluate(() => entries.find(e => e.group==='lines' && spansOf(e).some(id=>objectView().spans[id]?.candidates?.length))?.key);
      if (line) {
        await page.selectOption('#filter', 'lines');
        await page.locator(`#list [data-evidence="${line}"]`).click();
        assert((await page.locator('#detail').innerText()).includes('Object memberships'));
        assert(await page.locator('#detail button:not(:disabled)').count() > 0);
        membershipsChecked++;
      }
      const unknown = await page.evaluate(() => Object.entries(objectView().spans).filter(([,v])=>v.role==='unknown').map(([id])=>id));
      for (const id of unknown) {
        assert.equal(await page.locator(`#fit [data-span="${id}"]`).getAttribute('stroke'), '#92999e');
        unknownChecked++;
      }
      await page.uncheck('#objectColor');
      assert.deepEqual(await sourceGeometry(), ink, 'Object toggle changed original ink');
      assert.deepEqual(await fittedGeometry(), fit, 'Object toggle changed fitted geometry');
      assert(await page.locator('#fit [data-span]').evaluateAll(nodes=>nodes.every(n=>n.getAttribute('stroke')==='#287b6f')));
      await page.check('#objectColor');
      const finalMode = await page.locator('#readMode option').evaluateAll(options=>options.map(o=>o.value).filter(v=>v.startsWith('step:')).at(-1));
      if (finalMode && candidates.length) {
        reference ||= {page:p+1,drawing:row.i,mode:finalMode};
        await page.selectOption('#readMode', finalMode);
        await page.locator('#objectList button').first().click();
        const key = await page.evaluate(() => selected.key);
        assert.equal(await page.locator(`#fit [data-entry="${key}"]`).count(),0,'Source-frame object boundary appeared on transformed final geometry');
        const final = await fittedGeometry();
        await page.uncheck('#objectColor');
        assert.deepEqual(await fittedGeometry(), final, 'Color toggle changed final geometry');
        assert.deepEqual(await sourceGeometry(), ink);
        await page.check('#objectColor');
        finalBoundsChecked++;
      }
    }
    assert.equal(await page.evaluate(() => JSON.stringify(DATA)), before, 'Object inspection mutated saved evidence');
  }
  assert(inspected>0 && candidatesChecked>0 && membershipsChecked>0 && finalBoundsChecked>0, 'Pilot lacks inspectable object/final coverage');
  await page.goto(new URL(navigation.pages[reference.page-1], pathToFileURL(inspector)).href);
  await page.selectOption('#drawing', String(reference.drawing));
  await page.selectOption('#readMode', reference.mode);
  await page.check('#objectColor');
  await page.locator('#objectList button').first().click();
  await page.screenshot({path:path.join(output,'objects-desktop.png'),fullPage:true});
  await page.setViewportSize({width:390,height:844});
  assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),'Inspector mobile overflow');
  await page.screenshot({path:path.join(output,'objects-mobile.png'),fullPage:true});
  await page.setViewportSize({width:1440,height:1100});
  await page.uncheck('#objectColor');
  let inspectorPaging = false;
  if (navigation.pages.length>1) {
    const destination = reference.page===navigation.pages.length ? reference.page-1 : reference.page+1;
    await navigate(destination<reference.page?'prevPage':'nextPage',destination);
    assert.equal(await page.locator('#objectColor').isChecked(),false);
    assert.equal(new URL(page.url()).searchParams.get('view'),reference.mode);
    await navigate(destination<reference.page?'nextPage':'prevPage',reference.page);
    await page.selectOption('#drawing',String(reference.drawing));
    await page.check('#objectColor');
    await navigate(destination<reference.page?'prevPage':'nextPage',destination);
    assert.equal(await page.locator('#objectColor').isChecked(),true);
    assert.equal(new URL(page.url()).searchParams.get('view'),reference.mode);
    await navigate(destination<reference.page?'nextPage':'prevPage',reference.page);
    await page.selectOption('#drawing',String(reference.drawing));
    assert.equal(await page.locator('#objectColor').isChecked(),true);
    assert.equal(new URL(page.url()).searchParams.get('view'),reference.mode);
    inspectorPaging=true;
  }
  await page.reload();
  assert.equal(await page.locator('#objectColor').isChecked(),inspectorPaging);
  assert.equal(new URL(page.url()).searchParams.get('view'),reference.mode);

  await page.goto(pathToFileURL(gallery).href);
  const galleryNavigation = await page.evaluate(()=>PAGING);
  let galleryCards=0, galleryObjectProfiles=0, galleryPaging=false;
  for (let p=0;p<galleryNavigation.pages.length;p++) {
    if (p) await navigate('nextPage',p+1);
    const before = await page.evaluate(()=>JSON.stringify(DATA));
    const geometry = () => page.locator('.comparison svg [points],.comparison svg [d]').evaluateAll(nodes=>nodes.map(n=>[n.getAttribute('points'),n.getAttribute('d')]));
    const profiles = await geometry();
    if (p) {assert.equal(await page.locator('#objectColors').isChecked(),true);galleryPaging=true;}
    await page.check('#objectColors');
    assert(await page.locator('#objectLegend').isVisible());
    galleryObjectProfiles+=await page.locator('.object-ink').count();
    assert(await page.locator('.object-ink').evaluateAll(nodes=>nodes.every(n=>getComputedStyle(n).display!=='none')));
    assert(await page.locator('.output [style*="--object-color"]').evaluateAll(nodes=>nodes.every(n=>getComputedStyle(n).stroke!=='rgb(40, 123, 111)')));
    assert(await page.locator('a.comparison').evaluateAll(nodes=>nodes.every(n=>new URL(n.href).searchParams.get('objects')==='1')));
    await page.uncheck('#objectColors');
    assert(await page.locator('.object-ink').evaluateAll(nodes=>nodes.every(n=>getComputedStyle(n).display==='none')));
    assert.deepEqual(await geometry(),profiles,'Gallery toggle changed drawing geometry');
    await page.check('#objectColors');
    assert.equal(await page.evaluate(()=>JSON.stringify(DATA)),before,'Gallery toggle changed metadata');
    galleryCards+=await page.locator('.comparison').count();
    if (!p) await page.screenshot({path:path.join(output,'gallery-objects.png'),fullPage:true});
  }
  await page.reload();assert.equal(await page.locator('#objectColors').isChecked(),true);
  assert.equal(galleryCards,galleryNavigation.total);
  assert(galleryObjectProfiles>0,'Gallery contains no source object overlays');
  assert.deepEqual(errors,[]);assert.deepEqual(requests,[]);
  const result={inspected,candidatesChecked,membershipsChecked,nestingChecked,unknownChecked,finalBoundsChecked,
    inspectorPages:navigation.pages.length,inspectorPaging,galleryPages:galleryNavigation.pages.length,galleryPaging,galleryCards,galleryObjectProfiles,
    immutable:true,mobileOverflow:false,errors,requests};
  fs.writeFileSync(path.join(output,'summary.json'),JSON.stringify(result,null,2));console.log(JSON.stringify(result));
})().catch(error=>{console.error(error);process.exitCode=1;}).finally(async()=>{if(browser)await browser.close();});
