const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const expected=[16,24,32,48,64,128,256],ico=fs.readFileSync(path.join(__dirname,'JingYue.ico'));
assert.equal(ico.readUInt16LE(4),expected.length);assert.deepEqual(expected.map((_,i)=>ico[6+i*16]||256),expected);
const exe=fs.readFileSync(process.argv[2]||path.join(__dirname,'JingYue.exe')),pe=exe.readUInt32LE(0x3c),optional=pe+24,sectionCount=exe.readUInt16LE(pe+6),sectionBase=optional+exe.readUInt16LE(pe+20),dataBase=optional+(exe.readUInt16LE(optional)===0x20b?112:96);
function offset(rva){for(let i=0;i<sectionCount;i++){const p=sectionBase+i*40,start=exe.readUInt32LE(p+12),size=Math.max(exe.readUInt32LE(p+8),exe.readUInt32LE(p+16));if(rva>=start&&rva<start+size)return exe.readUInt32LE(p+20)+rva-start;}throw Error('Unknown resource RVA');}
const root=offset(exe.readUInt32LE(dataBase+16));
function entries(at){const n=exe.readUInt16LE(at+12)+exe.readUInt16LE(at+14),out=[];for(let i=0;i<n;i++){let p=at+16+i*8;out.push({id:exe.readUInt32LE(p),target:exe.readUInt32LE(p+4)});}return out;}
function payload(entry){if(entry.target&0x80000000)return payload(entries(root+(entry.target&0x7fffffff))[0]);const at=root+entry.target,start=offset(exe.readUInt32LE(at)),length=exe.readUInt32LE(at+4);return exe.subarray(start,start+length);}
const group=entries(root).find(e=>e.id===14);assert(group,'EXE has no icon group');const data=payload(group),n=data.readUInt16LE(4);assert.equal(n,7);assert.deepEqual(Array.from({length:n},(_,i)=>data[6+i*14]||256),expected);console.log('PASS original JingYue icon embedded at '+expected.join(', ')+' px');
