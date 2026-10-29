const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const {pathToFileURL}=require('node:url'),{chromium}=require(process.env.GOJO_PLAYWRIGHT_MODULE||'playwright');
let browser;
(async()=>{
 const root=path.resolve(process.argv[2]),retention=JSON.parse(fs.readFileSync(path.join(root,'retention.json'),'utf8'));
 const identities=fs.readdirSync(path.join(root,'drawings')).filter(id=>fs.existsSync(path.join(root,'drawings',id,'view.json'))).sort();
 browser=await chromium.launch({headless:true,...(process.env.GOJO_BROWSER?{executablePath:process.env.GOJO_BROWSER}:{})});
 const page=await browser.newPage({viewport:{width:1500,height:1000}}),errors=[];page.on('pageerror',e=>errors.push(e.message));
 async function check(id,arm,retired){
  const i=identities.indexOf(id);assert(i>=0);
  await page.goto(pathToFileURL(path.join(root,'review.html')).href+`#page=${Math.floor(i/12)+1}&arm=${arm}&pass=6`);
  await page.locator('.card').nth(i%12).click();await page.waitForSelector('#dialog[open]');
  const href=await page.locator('#archive').getAttribute('href');
  assert.equal(href,retired?'retention.json':`drawings/${id}/${arm}.gojo.bin.gz`);
  assert(fs.existsSync(path.join(root,href)));await page.locator('#close').click();
 }
 const retired=retention.retiredNativePaths[0].split('/');await check(retired[1],retired[2].split('.')[0],true);
 await check(retention.retainedNativeIds[0],'bidirectional',false);
 assert.deepEqual(errors,[]);await browser.close();console.log('Retired and retained archive links verified');
})().catch(async e=>{console.error(e);if(browser)await browser.close();process.exitCode=1});
