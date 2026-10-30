const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const {pathToFileURL}=require('node:url');
const {chromium}=require(process.env.GOJO_PLAYWRIGHT_MODULE||'playwright');
let browser;
(async()=>{
 const source=path.resolve(process.argv[2]),output=path.resolve(process.argv[3]);fs.mkdirSync(output,{recursive:true});
 browser=await chromium.launch({headless:true,...(process.env.GOJO_BROWSER?{executablePath:process.env.GOJO_BROWSER}:{})});
 const page=await browser.newPage({viewport:{width:1600,height:1100}}),errors=[],network=[];
 page.on('pageerror',e=>errors.push(e.message));await page.route('http**',r=>{network.push(r.request().url());return r.abort()});
 await page.goto(pathToFileURL(source).href);await page.waitForSelector('.panel');
 const retained=await page.evaluate(()=>JSON.stringify(DATA));const count=await page.locator('#drawing option').count();assert(count>0);
 assert.equal(await page.locator('#ink').isChecked(),false,'Green working ink should be optional');
 let panels=0;
 for(let i=0;i<count;i++){
  await page.selectOption('#drawing',String(i));assert.equal(await page.locator('.panel').count(),6);
  const frames=await page.locator('.panel svg').evaluateAll(rows=>rows.map(r=>r.getAttribute('viewBox')));assert.equal(new Set(frames).size,1);
  assert.equal(await page.locator('.working').count(),0);await page.check('#ink');
  await page.waitForFunction(()=>document.querySelectorAll('.working').length>0);
  assert(await page.locator('.working').count()>0,'Working ink did not render for drawing '+(i+1));
  await page.check('#unknown');await page.check('#missing');await page.check('#joints');
  const expected=await page.evaluate(i=>DATA[i].panels.reduce((a,b)=>a+b.unknown.length,0),i);assert.equal(await page.locator('.unresolved').count(),expected);
  const expectedMissing=await page.evaluate(i=>DATA[i].panels.reduce((a,b)=>a+b.missing.length,0),i);assert.equal(await page.locator('.missing').count(),expectedMissing);
  const html=await page.locator('#panels').innerHTML();assert(!html.includes('NaN')&&!html.includes('Infinity'));
  const stages=await page.locator('#pass option').evaluateAll(o=>o.map(e=>e.value));
  for(const stage of stages){await page.selectOption('#pass',stage);assert((await page.locator('.caption').last().innerText()).includes('Pass '+stage))}
  await page.selectOption('#pass',stages.at(-1));
  await page.uncheck('#ink');await page.uncheck('#unknown');await page.uncheck('#missing');await page.uncheck('#joints');panels+=6;
  if(await page.evaluate(i=>(DATA[i].cadGuides||[]).length>0,i)){
   await page.check('#guides');await page.waitForFunction(()=>document.querySelectorAll('.alignment-guide').length>0);
   assert(!(await page.locator('#panels').innerHTML()).includes('NaN'));
   await page.screenshot({path:path.join(output,'alignment-'+(i+1)+'.png'),fullPage:true});
   await page.uncheck('#guides');
  }
  if([5,7,9,10,11,14,15].includes(i))await page.screenshot({path:path.join(output,'case-'+(i+1)+'.png'),fullPage:true});
  if(await page.evaluate(i=>(DATA[i].recovery?.decisions||[]).length>0,i)){
   assert((await page.locator('#recovery').textContent()).includes('recovered'));
   await page.screenshot({path:path.join(output,'recovered-'+(i+1)+'.png'),fullPage:true});
  }
  const captures=await page.evaluate(i=>DATA[i].steps.at(-1).stage.captures?.length||0,i);
  if(captures){
   await page.locator('details').last().evaluate(d=>d.open=true);
   assert.equal(await page.locator('#connections tr').count(),captures+1);
   assert(!(await page.locator('#connections').innerText()).includes('NaN'));
   if(i===5)await page.screenshot({path:path.join(output,'connection-details.png'),fullPage:true});
  }
 }
 await page.selectOption('#drawing',String(Math.min(count-1,9)));await page.screenshot({path:path.join(output,'comparison.png'),fullPage:true});
 if(count>1){await page.selectOption('#drawing','0');await page.locator('#next').click();assert.equal(await page.locator('#drawing').inputValue(),'1');await page.locator('h1').click();await page.keyboard.press('ArrowLeft');assert.equal(await page.locator('#drawing').inputValue(),'0')}
 await page.check('#unknown');await page.check('#missing');await page.screenshot({path:path.join(output,'ink-coverage.png'),fullPage:true});
 assert.equal(await page.evaluate(()=>JSON.stringify(DATA)),retained);
 await page.setViewportSize({width:390,height:844});assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),'Horizontal overflow');
 await page.screenshot({path:path.join(output,'mobile.png'),fullPage:true});assert.deepEqual(errors,[]);assert.deepEqual(network,[]);
 fs.writeFileSync(path.join(output,'summary.json'),JSON.stringify({drawings:count,panels,errors,network},null,2));await browser.close();
})().catch(async e=>{console.error(e);if(browser)await browser.close();process.exitCode=1});
