const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const {pathToFileURL}=require('node:url');
const {chromium}=require(process.env.GOJO_PLAYWRIGHT_MODULE||'playwright');
const source=path.resolve(process.argv[2]),output=path.resolve(process.argv[3]);
let browser;
(async()=>{
 fs.mkdirSync(output,{recursive:true});browser=await chromium.launch({headless:true,...(process.env.GOJO_BROWSER?{executablePath:process.env.GOJO_BROWSER}:{})});
 const page=await browser.newPage({viewport:{width:1600,height:1100}}),errors=[],network=[];
 page.on('pageerror',e=>errors.push(e.message));await page.route('http**',r=>{network.push(r.request().url());return r.abort()});
 await page.goto(pathToFileURL(source).href);await page.waitForSelector('.panel');
 const retained=await page.evaluate(()=>JSON.stringify(DATA));
 const count=await page.locator('#drawing option').count();assert(count>0);
 let panels=0;
 for(let index=0;index<count;index++){
  await page.selectOption('#drawing',String(index));assert.equal(await page.locator('.panel').count(),6);
  const frames=await page.locator('.panel svg').evaluateAll(rows=>rows.map(r=>r.getAttribute('viewBox')));
  assert.equal(new Set(frames).size,1,'Milestones use different coordinate frames');
  await page.check('#joints');await page.check('#unknown');
  const unresolved=await page.evaluate(i=>DATA[i].unknown.length,index);
  assert.equal(await page.locator('.panel polyline[stroke="#df9020"]').count(),unresolved*6);
  const html=await page.locator('#panels').innerHTML();assert(!html.includes('NaN')&&!html.includes('Infinity'));
  const interpreted=await page.locator('.panel path[stroke="#307f73"]').count();
  await page.uncheck('#ink');assert.equal(await page.locator('.panel path[stroke="#307f73"]').count(),0);
  assert(await page.locator('.panel').first().locator('path').count()>0,'Hiding interpreted ink hid the original');
  await page.check('#ink');assert.equal(await page.locator('.panel path[stroke="#307f73"]').count(),interpreted);
  assert((await page.locator('.panel h2').last().innerText()).includes('reference only'));
  panels+=6;await page.uncheck('#joints');await page.uncheck('#unknown');
 }
 await page.selectOption('#drawing','0');await page.screenshot({path:path.join(output,'comparison.png'),fullPage:true});
 if(count>1){await page.locator('#next').click();assert.equal(await page.locator('#drawing').inputValue(),'1');await page.keyboard.press('ArrowLeft');assert.equal(await page.locator('#drawing').inputValue(),'0')}
 await page.check('#unknown');await page.screenshot({path:path.join(output,'unexplained-ink.png'),fullPage:true});
 assert.equal(await page.evaluate(()=>JSON.stringify(DATA)),retained,'Comparison mutated retained data');
 await page.setViewportSize({width:390,height:844});assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),'Horizontal overflow');
 await page.screenshot({path:path.join(output,'mobile.png'),fullPage:true});
 assert.deepEqual(errors,[]);assert.deepEqual(network,[]);
 const result={drawings:count,milestonePanels:panels,commonFrame:true,inkToggle:true,unresolvedOverlays:true,referenceQualified:true,immutable:true,errors,network,mobileOverflow:false};
 fs.writeFileSync(path.join(output,'summary.json'),JSON.stringify(result,null,2));console.log(JSON.stringify(result));
})().catch(e=>{console.error(e);process.exitCode=1}).finally(async()=>{if(browser)await browser.close()});
