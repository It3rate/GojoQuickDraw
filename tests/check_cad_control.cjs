const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const {pathToFileURL}=require('node:url');
const {chromium}=require(process.env.GOJO_PLAYWRIGHT_MODULE||'playwright');
let browser;
(async()=>{
 const source=path.resolve(process.argv[2]),output=path.resolve(process.argv[3]);fs.mkdirSync(output,{recursive:true});
 browser=await chromium.launch({headless:true,...(process.env.GOJO_BROWSER?{executablePath:process.env.GOJO_BROWSER}:{})});
 const page=await browser.newPage({viewport:{width:1500,height:1050}}),errors=[],network=[];
 page.on('pageerror',e=>errors.push(e.message));await page.route('http**',r=>{network.push(r.request().url());return r.abort()});
 await page.goto(pathToFileURL(source).href);await page.waitForSelector('#selected svg');
 const initial=await page.locator('#initial').innerHTML(),baseline=await page.locator('#baseline').innerHTML(),retained=await page.evaluate(()=>JSON.stringify(DATA));
 assert.equal(initial,baseline);
 for(let n=0;n<=16;n++){
  await page.locator('#round').evaluate((e,n)=>{e.value=n;e.dispatchEvent(new Event('input'))},n);
  assert.equal(await page.locator('#selected rect').count(),10);
  assert.equal(await page.locator('#label').innerText(),'Round '+n+' of 16');
  assert(!(await page.locator('#selected').innerHTML()).includes('NaN'));
  assert.equal(await page.locator('#baseline').innerHTML(),baseline);
 }
 assert.equal(await page.locator('#metrics tr').count(),5);
 await page.locator('#prev').click();assert.equal(await page.locator('#round').inputValue(),'15');
 await page.locator('#next').click();assert.equal(await page.locator('#round').inputValue(),'16');
 await page.screenshot({path:path.join(output,'native-repetition.png'),fullPage:true});
 await page.setViewportSize({width:390,height:844});assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));
 await page.screenshot({path:path.join(output,'native-repetition-mobile.png'),fullPage:true});
 assert.equal(await page.evaluate(()=>JSON.stringify(DATA)),retained);assert.deepEqual(errors,[]);assert.deepEqual(network,[]);
 fs.writeFileSync(path.join(output,'summary.json'),JSON.stringify({rounds:17,baselineUnchanged:true,errors,network},null,2));await browser.close();
})().catch(async e=>{console.error(e);if(browser)await browser.close();process.exitCode=1});
