/* Final shape stage: faithful faint acquisition, connected contacts, paging. */
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const {pathToFileURL}=require('node:url');
const {chromium}=require(process.env.GOJO_PLAYWRIGHT_MODULE||'playwright');
let browser;
(async()=>{
 const entry=path.resolve(process.argv[2]),output=path.resolve(process.argv[3]);fs.mkdirSync(output,{recursive:true});
 browser=await chromium.launch({headless:true,...(process.env.GOJO_BROWSER?{executablePath:process.env.GOJO_BROWSER}:{})});
 const page=await browser.newPage({viewport:{width:1600,height:1150}}),errors=[],requests=[],links=[];
 page.on('pageerror',e=>errors.push(e.message));await page.route('http**',r=>{requests.push(r.request().url());return r.abort()});
 await page.goto(pathToFileURL(entry).href+'?view=cleanup');const count=await page.evaluate(()=>PAGING.pages.length);
 for(let n=0;n<count;n++){
  if(n){await page.click('#nextPage');await page.waitForFunction(i=>typeof PAGING!=='undefined'&&PAGING.page===i,n+1)}
  assert.equal(await page.locator('#resultView').inputValue(),'cleanup');
  const rows=await page.locator('.comparison').evaluateAll(cards=>cards.map(card=>({href:card.href,key:card.querySelector('.key').textContent,
   original:[...card.querySelectorAll('.input .cleanup-output [data-stroke]')].map(e=>e.getAttribute('points')),
   faint:[...card.querySelectorAll('.output .cleanup-output [data-stroke]')].map(e=>e.getAttribute('points')),
   opacity:[...card.querySelectorAll('.output .cleanup-output [data-stroke]')].map(e=>Number(e.getAttribute('opacity'))),
   shapes:card.querySelectorAll('.output [data-cleanup]').length,
   left:card.querySelector('.input .cleanup-output svg').getAttribute('viewBox'),right:card.querySelector('.output .cleanup-output svg').getAttribute('viewBox')})));
  for(const r of rows){assert(r.shapes>=4);assert.deepEqual(r.original,r.faint);assert(r.opacity.every(n=>n>0&&n<.3));assert.equal(r.left,r.right);assert.equal(new URL(r.href).searchParams.get('view'),'cleanup');links.push(r)}
  await page.screenshot({path:path.join(output,`gallery-${n+1}.png`),fullPage:true});
 }
 for(const row of links){
  await page.goto(row.href);await page.reload();await page.waitForSelector('#fit [data-cleanup="cleanup-building"]');
  const read=await page.evaluate(()=>({key:packet().source.key_id,mode:$('readMode').value,
   original:packet().observation.strokes.map(s=>s.points.map(p=>p.join(',')).join(' ')),
   faint:[...document.querySelectorAll('#fit [data-stroke]')].map(e=>e.getAttribute('points')),
   raw:[...document.querySelectorAll('#raw [data-stroke]')].map(e=>e.getAttribute('points')),
   count:document.querySelectorAll('#fit [data-cleanup]').length,expected:packet().cleanup.parts.length,
   physical:packet().cleanup.parts.reduce((n,p)=>n+p.spans.length,0),contacts:packet().cleanup.attachments.length}));
  assert.equal(read.mode,'cleanup');assert.equal(read.key,row.key);assert.deepEqual(read.raw,read.original);assert.deepEqual(read.faint,read.original);assert.equal(read.count,read.expected);assert.equal(read.physical,0);assert(read.contacts>0);
  await page.locator('#fit [data-cleanup="cleanup-building"]').focus();assert((await page.locator('#detail').innerText()).includes('Earlier estimate'));
  if(['6688106352738304','6175065550880768','4745777253449728','5246955158503424'].includes(row.key))
   await page.screenshot({path:path.join(output,`example-${row.key}.png`),fullPage:true});
 }
 await page.goto(links[0].href);await page.click('#nextPage');await page.waitForFunction(()=>typeof PAGING!=='undefined'&&PAGING.page===2);assert.equal(await page.locator('#readMode').inputValue(),'cleanup');
 await page.click('#prevPage');await page.waitForFunction(()=>typeof PAGING!=='undefined'&&PAGING.page===1);await page.reload();assert.equal(await page.locator('#readMode').inputValue(),'cleanup');
 for(const mode of ['layout','mapping','observed-fit','cleanup']){await page.selectOption('#readMode',mode);assert.equal(await page.locator('#readMode').inputValue(),mode)}
 await page.setViewportSize({width:390,height:844});assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));await page.screenshot({path:path.join(output,'mobile.png'),fullPage:true});
 assert.deepEqual(errors,[]);assert.deepEqual(requests,[]);
 const result={drawings:links.length,galleryPages:count,sourceUnchanged:true,contactsPresent:true,pagingPreserved:true,errors,externalRequests:requests.length};fs.writeFileSync(path.join(output,'summary.json'),JSON.stringify(result,null,2));console.log(JSON.stringify(result));
})().catch(e=>{console.error(e);process.exitCode=1}).finally(async()=>{if(browser)await browser.close()});
