/* Development examples and interactive mapping, not an unseen accuracy benchmark. */
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const {pathToFileURL}=require('node:url');
const {chromium}=require(process.env.GOJO_PLAYWRIGHT_MODULE||'playwright');
const entry=path.resolve(process.argv[2]),output=path.resolve(process.argv[3]);
let browser;
(async()=>{
 fs.mkdirSync(output,{recursive:true});browser=await chromium.launch({headless:true,...(process.env.GOJO_BROWSER?{executablePath:process.env.GOJO_BROWSER}:{})});
 const page=await browser.newPage({viewport:{width:1600,height:1100}}),errors=[],requests=[],links=[],checks=[];
 page.on('pageerror',e=>errors.push(e.message));await page.route('http**',r=>{requests.push(r.request().url());return r.abort()});
 await page.goto(pathToFileURL(entry).href+'?view=mapping');
 const pages=await page.evaluate(()=>PAGING.pages.length);
 for(let i=0;i<pages;i++){
  if(i){await page.click('#nextPage');await page.waitForFunction(n=>typeof PAGING!=='undefined'&&PAGING.page===n,i+1)}
  assert.equal(await page.locator('#resultView').inputValue(),'mapping');
  for(const c of await page.locator('.comparison').all()){
   assert(await c.locator('.mapping-output').isVisible());assert(!(await c.locator('.output .layout-output').isVisible()));
   assert.deepEqual(await c.locator('.input .layout-output [data-stroke]').evaluateAll(ns=>ns.map(n=>n.getAttribute('points'))),await c.locator('.mapping-output [data-stroke]').evaluateAll(ns=>ns.map(n=>n.getAttribute('points'))));
   assert(await c.locator('[data-mapping-range]').count()>0);
   const href=await c.getAttribute('href'),key=await c.locator('.key').innerText();assert.equal(new URL(href,page.url()).searchParams.get('view'),'mapping');links.push({href:new URL(href,page.url()).href,key});
  }
  await page.screenshot({path:path.join(output,`gallery-${i+1}.png`),fullPage:true});
 }
 for(const [i,l]of links.entries()){
  await page.goto(l.href);await page.reload();await page.waitForFunction(k=>typeof packet==='function'&&packet().source.key_id===k,l.key);
  assert.equal(await page.locator('#readMode').inputValue(),'mapping');
  const data=await page.evaluate(()=>({parts:packet().layout.parts,m:packet().boundaryMapping,original:packet().observation.strokes.map(s=>s.points.map(p=>p.join(',')).join(' ')),actual:[...document.querySelectorAll('#fit [data-stroke]')].map(n=>n.getAttribute('points'))}));
  assert.deepEqual(data.actual,data.original);assert(data.m.targets.length>0);
  for(const t of data.m.targets.filter(t=>t.part==='layout-ground'||t.part.includes('cross-level')||t.name==='outline thickness'))assert.equal(t.ink.length,0);
  const g=page.locator('#fit [data-map-target="mapping-layout-ground-0"]').first();await g.focus();
  assert((await page.locator('#detail').innerText()).includes('No physical line is claimed'));
  const prior=data.parts.filter(p=>p.status==='prior-only');assert(data.m.targets.filter(t=>prior.some(p=>p.id===t.part)).every(t=>t.ink.length===0));
  const role=r=>data.parts.filter(p=>p.role===r);
  if(l.key==='4835826410192896'){
   assert.equal(role('roof').length,0);assert.equal(role('tower').length,1);assert.equal(role('door').length,1);assert.equal(role('cross')[0].placement,'above-tower');
   const shared=data.m.sourceCoverage.find(r=>r.parts.includes('layout-tower-1')&&r.parts.includes('layout-building'));assert(shared);assert(shared.from>0&&shared.to<1);
   const target=data.m.targets.find(t=>t.part==='layout-tower-1'&&t.ink.some(m=>m.span===shared.span&&m.from<1&&m.to>0));
   const boundary=page.locator(`#fit [data-map-target="${target.id}"]`).first();await boundary.focus();await boundary.press('Enter');assert(await page.locator('#fit [data-map-range][stroke="#e0a12a"]').count()>0);
  }
  if(l.key==='5026742236348416'){assert.equal(role('roof').length,0);assert.equal(role('tower').length,0);assert.equal(role('cross').length,1)}
  if(['5320526270562304','4707059385761792'].includes(l.key))assert(role('door').some(d=>d.shape==='arch'));
  if(l.key==='6291594720313344'){
   const tower=role('tower')[0],body=role('building')[0];assert(tower&&tower.bounds[3]===body.bounds[1]);
   assert.equal(role('window').length,0); // Writing is retained, without a forced window label.
   assert(data.m.sourceCoverage.some(r=>r.status==='unresolved'&&r.span.startsWith('stroke-006')));
  }
  if(l.key==='5521537635450880'){assert.equal(role('tower').length,1);assert.equal(role('roof').length,1)}
  await page.selectOption('#filter','unresolved');assert.equal(await page.locator('#list button').count(),data.m.summary.unresolvedSourceRanges+data.m.summary.unprofiledSamples);
  await page.selectOption('#filter','all');
  if([0,1,5,6,14,16,21].includes(i))await page.screenshot({path:path.join(output,`sample-${i+1}.png`),fullPage:true});
  checks.push({sample:i+1,key:l.key,...data.m.summary});
 }
 await page.goto(links[0].href);await page.reload();await page.click('#nextPage');await page.waitForFunction(()=>typeof PAGING!=='undefined'&&PAGING.page===2);assert.equal(await page.locator('#readMode').inputValue(),'mapping');
 await page.click('#prevPage');await page.waitForFunction(()=>typeof PAGING!=='undefined'&&PAGING.page===1);assert.equal(await page.locator('#readMode').inputValue(),'mapping');
 await page.selectOption('#readMode','observed-fit');assert(await page.locator('#fit [data-span]').count()>0);await page.selectOption('#readMode','mapping');
 await page.setViewportSize({width:390,height:844});assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));await page.screenshot({path:path.join(output,'mobile.png'),fullPage:true});
 assert.deepEqual(errors,[]);assert.deepEqual(requests,[]);
 const result={drawings:links.length,galleryPages:pages,originalsUnchanged:true,pagingPreserved:true,errors,externalRequests:requests.length,checks};
 fs.writeFileSync(path.join(output,'summary.json'),JSON.stringify(result,null,2));console.log(JSON.stringify({drawings:links.length,errors,output}));
})().catch(e=>{console.error(e);process.exitCode=1}).finally(async()=>{if(browser)await browser.close()});
