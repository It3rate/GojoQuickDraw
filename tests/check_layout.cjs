/* Area estimates preserve every acquired point; paging and both result modes. */
const assert = require('node:assert/strict'), fs = require('node:fs'), path = require('node:path');
const {pathToFileURL} = require('node:url');
const {chromium} = require(process.env.GOJO_PLAYWRIGHT_MODULE || 'playwright');
const entry=path.resolve(process.argv[2]), output=path.resolve(process.argv[3]);
let browser;
(async()=>{
  fs.mkdirSync(output,{recursive:true});
  browser=await chromium.launch({headless:true,...(process.env.GOJO_BROWSER?{executablePath:process.env.GOJO_BROWSER}:{})});
  const page=await browser.newPage({viewport:{width:1600,height:1100}}),errors=[],requests=[],links=[];
  page.on('pageerror',e=>errors.push(e.message));
  await page.route('http**',r=>{requests.push(r.request().url());return r.abort()});
  await page.goto(pathToFileURL(entry).href+'?view=layout');
  const totalPages=await page.evaluate(()=>PAGING.pages.length);
  for(let i=0;i<totalPages;i++){
    if(i){await page.click('#nextPage');await page.waitForFunction(n=>typeof PAGING!=='undefined'&&PAGING.page===n,i+1)}
    assert.equal(await page.locator('#resultView').inputValue(),'layout');
    assert.equal(new URL(page.url()).searchParams.get('view'),'layout');
    const rows=await page.locator('.comparison').evaluateAll(cards=>cards.map(card=>({
      href:card.href,key:card.querySelector('.key').textContent,
      original:[...card.querySelectorAll('.input .layout-output [data-stroke]')].map(n=>n.getAttribute('points')),
      overlay:[...card.querySelectorAll('.output .layout-output [data-stroke]')].map(n=>n.getAttribute('points')),
      left:card.querySelector('.input .layout-output svg').getAttribute('viewBox'),
      right:card.querySelector('.output .layout-output svg').getAttribute('viewBox'),
      fill:[...card.querySelectorAll('.output .layout-output polygon')].map(n=>Number(n.getAttribute('fill-opacity'))),
      layouts:card.querySelectorAll('.output [data-layout]').length
    })));
    for(const r of rows){assert(r.layouts>=5);assert.deepEqual(r.original,r.overlay);assert.equal(r.left,r.right);assert(r.fill.every(v=>v>0&&v<=.1));assert.equal(new URL(r.href).searchParams.get('view'),'layout');links.push(r)}
    await page.screenshot({path:path.join(output,`gallery-${i+1}.png`),fullPage:true});
  }
  await page.setViewportSize({width:390,height:844});
  assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));
  await page.screenshot({path:path.join(output,'gallery-mobile.png'),fullPage:true});
  await page.setViewportSize({width:1600,height:1100});
  for(const [i,r]of links.entries()){
    await page.goto(r.href);await page.reload();await page.waitForSelector('#fit [data-layout="layout-building"]');
    assert.equal(await page.evaluate(()=>packet().source.key_id),r.key);
    assert.equal(await page.locator('#readMode').inputValue(),'layout');
    const checked=await page.evaluate(()=>({
      actual:[...document.querySelectorAll('#fit [data-stroke]')].map(n=>n.getAttribute('points')),
      original:packet().observation.strokes.map(s=>s.points.map(p=>p.join(',')).join(' ')),
      raw:[...document.querySelectorAll('#raw [data-stroke]')].map(n=>n.getAttribute('points')),
      priorOnly:packet().layout.parts.filter(p=>p.status==='prior-only').map(p=>p.id),
      fitted:document.querySelectorAll('#fit [data-span],#fit [data-proposal]').length,
      chosen:document.querySelectorAll('#fit [data-layout]').length,expected:packet().layout.parts.length
    }));
    assert.deepEqual(checked.actual,checked.original);assert.deepEqual(checked.raw,checked.original);
    assert.equal(checked.fitted,0);assert.equal(checked.chosen,checked.expected);
    await page.locator('#fit [data-layout="layout-building"]').focus();
    assert((await page.locator('#detail').innerText()).includes('regional-estimate'));
    if(i===0||['6039787817402368','5165838967504896','5650365196271616'].includes(r.key))
      await page.screenshot({path:path.join(output,`detail-${r.key}.png`),fullPage:true});
  }
  await page.goto(links[0].href);
  await page.click('#nextPage');await page.waitForFunction(()=>typeof PAGING!=='undefined'&&PAGING.page===2);
  assert.equal(await page.locator('#readMode').inputValue(),'layout');
  await page.click('#prevPage');await page.waitForFunction(()=>typeof PAGING!=='undefined'&&PAGING.page===1);
  assert.equal(await page.locator('#readMode').inputValue(),'layout');
  await page.selectOption('#readMode','observed-fit');assert(await page.locator('#fit [data-span]').count()>0);
  await page.selectOption('#readMode','layout');assert.equal(await page.locator('#fit [data-span]').count(),0);
  await page.setViewportSize({width:390,height:844});
  assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));
  await page.screenshot({path:path.join(output,'detail-mobile.png'),fullPage:true});
  assert.deepEqual(errors,[]);assert.deepEqual(requests,[]);
  fs.writeFileSync(path.join(output,'summary.json'),JSON.stringify({drawings:links.length,galleryPages:totalPages,originalsUnchanged:true,pagingPreserved:true,errors,externalRequests:requests.length},null,2));
  console.log(JSON.stringify({drawings:links.length,galleryPages:totalPages,errors,output}));
})().catch(e=>{console.error(e);process.exitCode=1}).finally(async()=>{if(browser)await browser.close()});
