/* Run with the separately installed Playwright/Chromium paths used by the
   other portable-review audits. This is not a converter dependency. */
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const {pathToFileURL}=require('node:url');
const {chromium}=require(process.env.GOJO_PLAYWRIGHT_MODULE||'playwright');
(async()=>{
 const output=path.resolve(process.argv[3]);fs.mkdirSync(output,{recursive:true});
 const browser=await chromium.launch({headless:true,...(process.env.GOJO_BROWSER?{executablePath:process.env.GOJO_BROWSER}:{})});
 try{
  const page=await browser.newPage({viewport:{width:1440,height:1050}}),errors=[],requests=[];
  page.on('pageerror',error=>errors.push(error.message));
  await page.route('http**',route=>{requests.push(route.request().url());return route.abort()});
  await page.goto(pathToFileURL(path.resolve(process.argv[2])).href);
  const total=await page.evaluate(()=>data.length);
  assert.equal(await page.locator('article.card').count(),Math.min(12,total));
  assert.equal(await page.locator('#grid svg').count(),2*Math.min(12,total));
  const initial=await page.locator('#stage').inputValue();
  const endpointCount=await page.locator('#grid [data-kind="endpoint"]').count();
  assert(endpointCount>0);await page.locator('#endpoints').uncheck();assert.equal(await page.locator('#grid [data-kind="endpoint"]').count(),0);
  await page.locator('#endpoints').check();await page.locator('#projections').check();
  const hasProjections=await page.evaluate(()=>data.some(d=>d.stages.some(s=>(s.graph.projections||[]).length)));
  if(hasProjections)assert(await page.locator('#grid [data-projection]').count()>0);
  await page.locator('article').first().click();assert(await page.locator('dialog').evaluate(d=>d.open));
  await page.locator('#detailStage').selectOption('1');assert.equal(await page.locator('#stage').inputValue(),'1');
  await page.locator('#forward').click();assert.equal(await page.locator('#detailStage').inputValue(),'1');
  await page.locator('#forward').blur();await page.keyboard.press('ArrowDown');
  assert.equal(await page.locator('#detailStage').inputValue(),String(Math.min(2,Number(initial))));
  await page.locator('#detailStage').selectOption(initial);
  const extra=await page.evaluate(()=>{
   const inferred=data.reduce((n,d)=>n+(d.stages.at(-1).graph.inferredCurves||[]).length,0);
   const dormant=data.reduce((n,d)=>n+(d.stages.at(-1).graph.continuations||[]).length,0);
   const jointProjection=data.some(d=>d.stages.some(s=>(s.graph.projections||[]).some(p=>s.graph.sites.find(v=>v.id===p.endpoint)?.degree>1)));
   return {inferred,dormant,jointProjection};
  });
  await page.screenshot({path:path.join(output,'detail.png')});
  await page.locator('#close').click();await page.locator('#next').click();
  assert.equal(await page.locator('#stage').inputValue(),initial);
  if(total>24){await page.locator('#next').click();assert.equal(await page.locator('article').count(),Math.min(12,total-24))}
  await page.screenshot({path:path.join(output,'last-page.png')});
  await page.locator('#prev').click();await page.locator('#prev').click();
  await page.screenshot({path:path.join(output,'gallery.png'),fullPage:true});
  const malformed=await page.locator('svg path').evaluateAll(nodes=>nodes.some(n=>/NaN|Infinity/.test(n.getAttribute('d'))));
  assert(!malformed);assert.equal(errors.length,0);assert.equal(requests.length,0);
  if(extra.inferred){const i=await page.evaluate(()=>data.findIndex(d=>(d.stages.at(-1).graph.inferredCurves||[]).length));await page.evaluate(i=>{stage=maxStage;open(i)},i);assert(await page.locator('#large [data-inferred="closure"]').count()>0);await page.screenshot({path:path.join(output,'inferred-closure.png')})}
  if(extra.dormant){const i=await page.evaluate(()=>data.findIndex(d=>(d.stages.at(-1).graph.continuations||[]).length));await page.evaluate(i=>{stage=maxStage;open(i)},i);assert(await page.locator('#large [data-dormant="true"]').count()>0)}
  assert.equal(errors.length,0);assert.equal(requests.length,0);
  fs.writeFileSync(path.join(output,'summary.json'),JSON.stringify({total,errors,requests,preservedStage:initial,actualGraphPaths:true,endpointCount,nearestPointOverlay:hasProjections,...extra},null,2));
 }finally{await browser.close()}
})().catch(error=>{console.error(error);process.exitCode=1});
