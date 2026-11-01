// Run against the same offline Native fixture used by the reader's browser checks.
const fs=require('node:fs'),path=require('node:path');
const original=fs.readFileSync(path.join(__dirname,'browser-test.cjs'),'utf8');
const prefix=original.split("await page.goto('https://app.jingyue.local/');")[0];
const checks=String.raw`
await page.goto('https://app.jingyue.local/');
const cdp=await context.newCDPSession(page);
async function dragHandle(distance,cancel){
  const r=await page.locator('#sheetDrag').boundingBox(),x=r.x+r.width/2,y=r.y+r.height/2;
  await cdp.send('Input.dispatchTouchEvent',{type:'touchStart',touchPoints:[{x,y}]});
  for(let i=1;i<=8;i++){await cdp.send('Input.dispatchTouchEvent',{type:'touchMove',touchPoints:[{x,y:y+distance*i/8}]});await page.waitForTimeout(18);}
  if(distance>0)assert(await page.locator('#sheet').evaluate(e=>new DOMMatrixReadOnly(getComputedStyle(e).transform).m42)>0,'sheet follows the finger');
  await cdp.send('Input.dispatchTouchEvent',{type:cancel?'touchCancel':'touchEnd',touchPoints:[]});
}
for(const width of [320,393]){
  await page.setViewportSize({width,height:740});await page.locator('#filter').fill('山间');
  const result=await page.evaluate(()=>{const input=$('filter'),outer=input.parentElement,clear=$('clearSearch'),a=input.getBoundingClientRect(),b=clear.getBoundingClientRect(),o=outer.getBoundingClientRect();return {outline:getComputedStyle(input).outlineStyle,shadow:getComputedStyle(input).boxShadow,nativeClear:getComputedStyle(input,'::-webkit-search-cancel-button').display,inside:a.left>=o.left&&b.right<=o.right,noOverlap:a.right<=b.left,focus:document.activeElement===input};});
  await page.screenshot({path:path.join(out,'search-fixed-'+width+'.png')});
  assert.equal(result.outline,'none');assert.equal(result.shadow,'none');assert(result.inside&&result.noOverlap&&result.focus);
  assert(await page.evaluate(()=>[...document.styleSheets].some(s=>[...s.cssRules].some(r=>r.selectorText&&r.selectorText.includes('#filter::-webkit-search-cancel-button')&&r.style.display==='none'))));
  await page.click('#clearSearch');assert.equal(await page.locator('#filter').inputValue(),'');assert.equal(await page.locator('.book-card').count(),1);assert.equal(await page.locator('#clearSearch').evaluate(e=>getComputedStyle(e).visibility),'hidden');
  console.log('PASS focused search has one border and one clear control '+width);
}
await page.click('#homeSettings');await page.locator('#sheetBody').evaluate(e=>e.scrollTop=220);
await dragHandle(18,false);await page.waitForTimeout(240);assert(await page.locator('#overlay').isVisible());assert.equal(await page.locator('#sheet').evaluate(e=>getComputedStyle(e).transform),'none');assert.equal(await page.locator('#sheetBody').evaluate(e=>e.scrollTop),220);console.log('PASS short handle drag springs back and retains panel scroll');
await dragHandle(160,false);await page.waitForTimeout(240);assert.equal(await page.locator('#overlay').isVisible(),false);console.log('PASS dragging the handle dismisses scrolled settings');
await page.click('#homeSettings');await dragHandle(140,true);await page.waitForTimeout(240);assert(await page.locator('#overlay').isVisible());console.log('PASS cancelled touch springs back');
await page.click('#sheetDrag');assert.equal(await page.locator('#overlay').isVisible(),false);console.log('PASS handle tap also closes');
for(const kind of ['toc','marks','find','license']){
  await page.evaluate(kind=>showSheet(kind,Array.from({length:80},(_,i)=>'<p style="padding:14px">第 '+i+' 项内容</p>').join(''),kind),kind);
  assert.equal(await page.locator('#sheetBody').evaluate(e=>getComputedStyle(e,'::-webkit-scrollbar').display),'none');
  const b=await page.locator('#sheetBody').boundingBox(),x=b.x+8,y=b.y+b.height-20;
  await cdp.send('Input.dispatchTouchEvent',{type:'touchStart',touchPoints:[{x,y}]});
  for(let i=1;i<=8;i++)await cdp.send('Input.dispatchTouchEvent',{type:'touchMove',touchPoints:[{x,y:y-25*i}]});
  await cdp.send('Input.dispatchTouchEvent',{type:'touchEnd',touchPoints:[]});await page.waitForTimeout(250);
  assert(await page.locator('#sheetBody').evaluate(e=>e.scrollTop)>0);assert(await page.locator('#overlay').isVisible());
  await dragHandle(160,false);await page.waitForTimeout(240);assert.equal(await page.locator('#overlay').isVisible(),false);console.log('PASS '+kind+' content scrolls and handle closes');
}
await page.click('#homeSettings');await dragHandle(160,false);await page.evaluate(()=>showSheet('新的面板','新的内容','toc'));await page.waitForTimeout(240);assert(await page.locator('#overlay').isVisible());assert.equal(await page.locator('#sheetTitle').textContent(),'新的面板');await page.click('#closeSheet');assert.equal(await page.locator('#overlay').isVisible(),false);console.log('PASS opening another panel cancels a pending dismissal');
assert.deepEqual(errors,[]);console.log('ALL MOBILE PANEL CHECKS PASSED');await browser.close();
})().catch(e=>{console.error(e);process.exit(1)});
`;
fs.mkdirSync(path.join(__dirname,'../build'),{recursive:true});
const target=path.join(__dirname,'../build/mobile-panels-run.cjs');
fs.writeFileSync(target,prefix+checks);require(target);
