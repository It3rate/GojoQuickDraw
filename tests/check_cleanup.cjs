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
   styles:[...card.querySelectorAll('.output .cleanup-output [data-cleanup]')].map(e=>({id:e.dataset.cleanup,stroke:e.querySelector('path')?.getAttribute('stroke'),dash:e.querySelector('path')?.getAttribute('stroke-dasharray')||'none'})),
   shapes:card.querySelectorAll('.output [data-cleanup]').length,
   left:card.querySelector('.input .cleanup-output svg').getAttribute('viewBox'),right:card.querySelector('.output .cleanup-output svg').getAttribute('viewBox')})));
  for(const r of rows){assert(r.shapes>=4);assert.deepEqual(r.original,r.faint);assert(r.opacity.every(n=>n>0&&n<.3));assert.equal(r.left,r.right);assert.equal(new URL(r.href).searchParams.get('view'),'cleanup');links.push(r)}
  await page.screenshot({path:path.join(output,`gallery-${n+1}.png`),fullPage:true});
 }
 for(const row of links){
  await page.goto(row.href);await page.reload();await page.waitForSelector('#fit [data-cleanup]:has(polygon)');
  const read=await page.evaluate(()=>({key:packet().source.key_id,mode:$('readMode').value,
   original:packet().observation.strokes.map(s=>s.points.map(p=>p.join(',')).join(' ')),
   faint:[...document.querySelectorAll('#fit [data-stroke]')].map(e=>e.getAttribute('points')),
   raw:[...document.querySelectorAll('#raw [data-stroke]')].map(e=>e.getAttribute('points')),
   count:document.querySelectorAll('#fit [data-cleanup]').length,expected:packet().cleanup.parts.length,
   physical:packet().cleanup.parts.reduce((n,p)=>n+p.spans.length,0),contacts:packet().cleanup.attachments.length,
   openings:packet().cleanup.parts.filter(s=>['door','window'].includes(s.role)).map(s=>{const line=document.querySelector('#fit [data-cleanup="'+s.id+'"] line');return {id:s.id,expected:s.status==='prior-only'?'#9aa2a6':s.color,dash:s.status==='prior-only'?'1 4':'none',actual:line.getAttribute('stroke'),actualDash:line.getAttribute('stroke-dasharray')}}),
   checks:(packet().cleanup.checks||[]).length,listedChecks:entries.filter(e=>e.row.nativeRead).length}));
  assert.equal(read.mode,'cleanup');assert.equal(read.key,row.key);assert.deepEqual(read.raw,read.original);assert.deepEqual(read.faint,read.original);assert.equal(read.count,read.expected);assert.equal(read.physical,0);assert(read.contacts>0);
  for(const s of read.openings){assert.equal(s.actual,s.expected);assert.equal(s.actualDash,s.dash);const g=row.styles.find(g=>g.id===s.id);assert.equal(g.stroke,s.expected);assert.equal(g.dash,s.dash)}
  assert.equal(read.listedChecks,read.checks);
  await page.locator('#fit [data-cleanup]:has(polygon)').first().focus();assert((await page.locator('#detail').innerText()).includes('Earlier estimate'));
  if(['6688106352738304','6175065550880768','4745777253449728','5246955158503424','4707059385761792','5521537635450880','5954572780568576','5774941733519360','5247716256907264','5037263295610880','5165838967504896','6680427051876352','4835826410192896','6039787817402368','5389179863171072'].includes(row.key))
   await page.screenshot({path:path.join(output,`example-${row.key}.png`),fullPage:true});
 }
 await page.goto(links[0].href);await page.click('#nextPage');await page.waitForFunction(()=>typeof PAGING!=='undefined'&&PAGING.page===2);assert.equal(await page.locator('#readMode').inputValue(),'cleanup');
 await page.click('#prevPage');await page.waitForFunction(()=>typeof PAGING!=='undefined'&&PAGING.page===1);await page.reload();assert.equal(await page.locator('#readMode').inputValue(),'cleanup');
 for(const mode of ['layout','mapping','observed-fit','cleanup']){await page.selectOption('#readMode',mode);assert.equal(await page.locator('#readMode').inputValue(),mode)}
 await page.setViewportSize({width:390,height:844});assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));await page.screenshot({path:path.join(output,'mobile.png'),fullPage:true});
 assert.deepEqual(errors,[]);assert.deepEqual(requests,[]);
 const result={drawings:links.length,galleryPages:count,sourceUnchanged:true,contactsPresent:true,pagingPreserved:true,optionalOpeningStylesVerified:true,checksListed:true,errors,externalRequests:requests.length};fs.writeFileSync(path.join(output,'summary.json'),JSON.stringify(result,null,2));console.log(JSON.stringify(result));
})().catch(e=>{console.error(e);process.exitCode=1}).finally(async()=>{if(browser)await browser.close()});
