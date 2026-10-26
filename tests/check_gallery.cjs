/* Read-only comparison coverage, compact layout, final-step links and paging. */
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const {pathToFileURL} = require('node:url');
const {chromium} = require(process.env.GOJO_PLAYWRIGHT_MODULE || 'playwright');
const entry = path.resolve(process.argv[2]), output = path.resolve(process.argv[3]);
let browser;
(async () => {
  fs.mkdirSync(output, {recursive:true});
  browser = await chromium.launch({headless:true, ...(process.env.GOJO_BROWSER ? {executablePath:process.env.GOJO_BROWSER} : {})});
  const page = await browser.newPage({viewport:{width:1440,height:1100}}), errors = [], requests = [];
  page.on('pageerror', e => errors.push(e.message));
  await page.route('http**', route => {requests.push(route.request().url()); return route.abort();});
  await page.goto(pathToFileURL(entry).href+'?view=final');
  const navigation = await page.evaluate(() => PAGING), identities = [], pageSizes = [], details = [];
  let finalImages = 0;
  for (let i=0; i<navigation.pages.length; i++) {
    if(i){await page.click('#nextPage');await page.waitForFunction(n => typeof PAGING!=='undefined'&&PAGING.page===n,i+1);}
    const records = await page.evaluate(() => DATA.records);
    assert.equal(await page.locator('.comparison').count(), records.length);
    assert(records.length<=12);
    identities.push(...records.map(r=>r.selectionId)); pageSizes.push(records.length);
    const previews = await page.locator('.comparison').evaluateAll(cards => cards.map(card => ({
      id:card.dataset.item,sourceStrokes:card.querySelectorAll('.input .final-output [data-stroke]').length,
      finalEdges:card.querySelectorAll('.output .final').length,residueEdges:card.querySelectorAll('.output .residue').length,
      missing:!!card.querySelector('.final-output .missing'),
      invalid:/NaN|Infinity/.test(card.innerHTML),
      clipped:[...card.querySelectorAll('svg')].some(svg => {
        const b=svg.getBBox(),v=svg.viewBox.baseVal;
        return b.width+b.height>0&&(b.x<v.x-.001||b.y<v.y-.001||b.x+b.width>v.x+v.width+.001||b.y+b.height>v.y+v.height+.001);
      })
    })));
    previews.forEach((p,j) => {
      const r=records[j]; assert.equal(p.id,r.selectionId); assert.equal(p.sourceStrokes,r.sourceStrokes);
      assert.equal(p.finalEdges,r.finalEdges);assert.equal(p.residueEdges,r.residueEdges);
      assert.equal(p.missing,!r.finalStepId);assert(!p.invalid&&!p.clipped,'Invalid or clipped thumbnail '+r.keyId);
      finalImages+=Number(!!r.finalStepId);
    });
    if(i===0){
      const boxes=await page.locator('.comparison').evaluateAll(cards=>cards.slice(0,4).map(c=>{const r=c.getBoundingClientRect();return {x:r.x,y:r.y}}));
      if(boxes.length>=4){assert.equal(boxes[0].y,boxes[1].y);assert.equal(boxes[1].y,boxes[2].y);assert(boxes[3].y>boxes[0].y);}
      await page.screenshot({path:path.join(output,'desktop.png'),fullPage:true});
      await page.setViewportSize({width:390,height:844});
      assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),'Mobile horizontal overflow');
      await page.screenshot({path:path.join(output,'mobile.png'),fullPage:true});
      await page.setViewportSize({width:1440,height:1100});
      await page.uncheck('#retainedDetail');
    }else assert.equal(await page.locator('#retainedDetail').isChecked(),false,'Detail selection reset on paging');
    if([0,Math.floor(navigation.pages.length/2),navigation.pages.length-1].includes(i)){
      const r=records.find(r=>r.detailsHref&&r.finalStepId);
      if(r)details.push({url:new URL(r.detailsHref,page.url()).href,...r});
    }
  }
  assert.equal(identities.length,navigation.total);assert.equal(new Set(identities).size,navigation.total);
  if(navigation.pages.length>1){
    await page.click('#firstPage');await page.waitForFunction(()=>typeof PAGING!=='undefined'&&PAGING.page===1);
    await page.click('#lastPage');await page.waitForFunction(n=>typeof PAGING!=='undefined'&&PAGING.page===n,navigation.pages.length);
    await page.selectOption('#page','0');await page.waitForFunction(()=>typeof PAGING!=='undefined'&&PAGING.page===1);
  }
  const detail=await browser.newPage();
  detail.on('pageerror',e=>errors.push(e.message));
  await detail.route('http**',route=>{requests.push(route.request().url());return route.abort();});
  for(const record of details){
    await detail.goto(record.url);
    assert.equal(await detail.evaluate(()=>DATA.records[index].index.itemId),record.itemId);
    assert.equal(await detail.evaluate(()=>packet().source.key_id),record.keyId);
    assert.equal(await detail.evaluate(()=>proposalStep()?.id),record.finalStepId);
    assert.equal(await detail.locator('#fit [data-proposal]').count(),record.finalEdges);
  }
  assert.deepEqual(errors,[]);assert.deepEqual(requests,[]);
  const result={drawings:navigation.total,finalImages,pages:navigation.pages.length,pageSizes,detailLinksChecked:details.length,
    columns:3,pairsPerPage:12,errors,requests,unclipped:true,mobileOverflow:false};
  fs.writeFileSync(path.join(output,'summary.json'),JSON.stringify(result,null,2));console.log(JSON.stringify(result));
})().catch(e=>{console.error(e);process.exitCode=1;}).finally(async()=>{if(browser)await browser.close();});
