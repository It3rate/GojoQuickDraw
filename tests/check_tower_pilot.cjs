/* Acceptance checks for the five user-reviewed development examples, not an accuracy benchmark. */
const assert = require('node:assert/strict'), fs = require('node:fs'), path = require('node:path');
const {pathToFileURL} = require('node:url');
const {chromium} = require(process.env.GOJO_PLAYWRIGHT_MODULE || 'playwright');
const entry=path.resolve(process.argv[2]), output=path.resolve(process.argv[3]);
const expected={
  '6082729202417664':{sample:13,tower:0,roof:1,cross:1},
  '5650520033198080':{sample:14,tower:1,roof:1,cross:1},
  '4707059385761792':{sample:15,tower:2,roof:2,cross:1},
  '6280306992611328':{sample:16,tower:1,roof:1,cross:1},
  '6680427051876352':{sample:22,tower:2,roof:2,cross:2}
};
let browser;
(async()=>{
  fs.mkdirSync(output,{recursive:true});
  browser=await chromium.launch({headless:true,...(process.env.GOJO_BROWSER?{executablePath:process.env.GOJO_BROWSER}:{})});
  const page=await browser.newPage({viewport:{width:1600,height:1100}}),errors=[],links=[],checks=[];
  page.on('pageerror',e=>errors.push(e.message));
  await page.goto(pathToFileURL(entry).href+'?view=layout');
  const pages=await page.evaluate(()=>PAGING.pages.length);
  for(let i=0;i<pages;i++){
    if(i){await page.click('#nextPage');await page.waitForFunction(n=>typeof PAGING!=='undefined'&&PAGING.page===n,i+1)}
    links.push(...await page.locator('.comparison').evaluateAll(cards=>cards.map(c=>({key:c.querySelector('.key').textContent,href:c.href}))));
  }
  for(const [key,wanted] of Object.entries(expected)){
    const link=links.find(l=>l.key===key);assert(link,`missing sample ${wanted.sample}`);
    await page.goto(link.href);await page.reload();
    await page.waitForFunction(k=>typeof packet==='function'&&packet().source.key_id===k,key);
    const parts=await page.evaluate(()=>packet().layout.parts);
    const role=r=>parts.filter(p=>p.role===r);
    for(const r of ['tower','roof','cross']) assert.equal(role(r).length,wanted[r],`${wanted.sample}: ${r}`);
    for(const p of parts){
      assert.equal(await page.locator(`#fit [data-layout="${p.id}"]`).count(),1);
      if(p.role==='tower'){
        assert.equal(p.lowerBoundary,'inferred-level');
        await page.locator(`#fit [data-layout="${p.id}"]`).focus();
        assert((await page.locator('#detail').innerText()).includes('no drawn divider is required'));
      }
    }
    if(wanted.sample===13){
      const building=role('building')[0].bounds,roof=role('roof')[0].bounds;
      assert(roof[2]-roof[0]<.7*(building[2]-building[0]));
    }
    if(wanted.sample===15) assert.equal(role('cross')[0].placement,'above-building');
    if(wanted.sample===22){
      assert(role('roof').every(r=>r.shape==='dome'&&r.construction.partitions===32));
      const partial=role('cross').find(c=>c.status==='partial-marker-prior');
      assert(partial&&partial.missingHorizontalArm);
      await page.locator(`#fit [data-layout="${partial.id}"]`).focus();
      const detail=await page.locator('#detail').innerText();
      assert(detail.includes('partial-marker-prior'));
      assert(detail.includes('The horizontal arm is assumed'));
    }
    await page.screenshot({path:path.join(output,`sample-${wanted.sample}.png`),fullPage:true});
    checks.push({sample:wanted.sample,key,...Object.fromEntries(['tower','roof','cross'].map(r=>[r,role(r).length])),
      shapes:role('roof').map(r=>r.shape),partialCrosses:role('cross').filter(c=>c.status==='partial-marker-prior').length});
  }
  assert.deepEqual(errors,[]);
  const result={checks,errors,qualification:'Known development examples, not unseen evaluation.'};
  fs.writeFileSync(path.join(output,'summary.json'),JSON.stringify(result,null,2));
  console.log(JSON.stringify(result));
})().catch(e=>{console.error(e);process.exitCode=1}).finally(async()=>{if(browser)await browser.close()});
