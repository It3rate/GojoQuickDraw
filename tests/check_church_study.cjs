const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const {pathToFileURL}=require('node:url');
const {chromium}=require(process.env.GOJO_PLAYWRIGHT_MODULE||'playwright');
const source=path.resolve(process.argv[2]),output=path.resolve(process.argv[3]);
let browser;
(async()=>{
 fs.mkdirSync(output,{recursive:true});browser=await chromium.launch({headless:true,...(process.env.GOJO_BROWSER?{executablePath:process.env.GOJO_BROWSER}:{})});
 const page=await browser.newPage({viewport:{width:1600,height:1050}}),errors=[],network=[];
 page.on('pageerror',e=>errors.push(e.message));await page.route('http**',r=>{network.push(r.request().url());return r.abort()});
 await page.goto(pathToFileURL(source).href);await page.waitForSelector('.card');
 const initial=await page.evaluate(()=>JSON.stringify(window.CHURCH_PAGE));
 const count=await page.locator('.card').count();assert(count>0&&count<=12);
 let inspected=0;
 for(const arm of ['local-models','shapes-only','fixed-scaffold','bidirectional']){
  await page.selectOption('#arm',arm);
  for(const pass of [0,1,3,6]){
   await page.locator('#step').evaluate((e,v)=>{e.value=String(v);e.dispatchEvent(new Event('input'))},pass);
   await page.check('#joints');const svg=await page.locator('#grid').innerHTML();
   assert(!svg.includes('NaN')&&!svg.includes('Infinity'));inspected+=count;
  }
 }
 await page.uncheck('#joints');await page.screenshot({path:path.join(output,'gallery.png'),fullPage:true});await page.check('#joints');
 await page.locator('.card').first().click();assert(await page.locator('#dialog').isVisible());assert.equal(await page.locator('#detailViews .view').count(),4);
 await page.locator('#stagePrev').click();let step=Number(await page.locator('#step').inputValue());
 await page.keyboard.press('ArrowUp');assert.equal(Number(await page.locator('#step').inputValue()),Math.max(0,step-1));
 if(count>1){let title=await page.locator('#title').innerText();await page.keyboard.press('ArrowRight');assert.notEqual(await page.locator('#title').innerText(),title)}
 await page.screenshot({path:path.join(output,'detail.png'),fullPage:false});await page.locator('#close').click();
 assert.equal(await page.evaluate(()=>JSON.stringify(window.CHURCH_PAGE)),initial,'Review mutated retained projections');
 if(await page.locator('#page option').count()>1){
  const selected=await page.locator('#step').inputValue(),arm=await page.locator('#arm').inputValue();
  await page.locator('#next').click();await page.waitForFunction(()=>document.getElementById('page').value==='2'&&document.querySelector('.card'));
  await page.waitForTimeout(200);assert.equal(await page.locator('#step').inputValue(),selected);assert.equal(await page.locator('#arm').inputValue(),arm);
 }
 await page.setViewportSize({width:390,height:844});assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),'Horizontal overflow');
 await page.screenshot({path:path.join(output,'mobile.png'),fullPage:true});
 assert.deepEqual(errors,[]);assert.deepEqual(network,[]);
 const result={drawingsOnPage:count,projectionsInspected:inspected,errors,network,immutable:true,keyboard:true,mobileOverflow:false};
 fs.writeFileSync(path.join(output,'summary.json'),JSON.stringify(result,null,2));console.log(JSON.stringify(result));
})().catch(e=>{console.error(e);process.exitCode=1}).finally(async()=>{if(browser)await browser.close()});
