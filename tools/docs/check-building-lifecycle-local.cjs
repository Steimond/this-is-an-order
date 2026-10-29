/* Browser-free verification of schematic rendering and control logic. */
const fs=require('node:fs'),path=require('node:path'),vm=require('node:vm'),{createRequire}=require('node:module');
const req=createRequire(path.join(path.dirname(process.execPath),'../node_modules/package.json')),sharp=req('sharp');
const dir=path.resolve(__dirname,'../../docs/concepts/building-lifecycle');
async function main(){
const html=fs.readFileSync(path.join(dir,'gallery.html'),'utf8'),code=html.match(/<script>([\s\S]*?)<\/script>/)[1];
const els=new Map(),buttons=new Map(),frames=[];
function element(value=''){return {value,innerHTML:'',textContent:'',disabled:false,handlers:{},addEventListener(type,fn){this.handlers[type]=fn;}};}
for(const id of [...html.matchAll(/id="([^"]+)"/g)].map(m=>m[1]))els.set(id,element());
els.get('method').value='hybrid';for(const [mode,value]of [['build',65],['damage',40],['death',55]]){els.get(mode).value=String(value);buttons.set(mode,element());}
const document={getElementById(id){if(!els.has(id))throw Error('Missing element '+id);return els.get(id);},querySelector(q){const m=q.match(/data-play="([^"]+)"/);if(!m||!buttons.has(m[1]))throw Error('Missing selector '+q);return buttons.get(m[1]);}};
vm.runInNewContext(code,{document,performance:{now:()=>0},requestAnimationFrame:fn=>frames.push(fn)},{timeout:1000});
let checks=0;
for(const method of ['shader','models','hybrid']){els.get('method').value=method;els.get('method').handlers.change();for(const mode of ['build','damage','death'])for(const value of mode==='damage'?[1,30,65,100]:[0,15,50,100]){
els.get(mode).value=String(value);els.get(mode).handlers.input();if(els.get(mode+'-value').textContent!==value+'%')throw Error('Control failed '+mode);
for(const faction of ['human','resonance']){const svg=els.get(mode+'-'+faction).innerHTML;if(/NaN|undefined|Infinity/.test(svg))throw Error('Invalid SVG');await sharp(Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="520" height="300" viewBox="0 0 520 300">${svg}</svg>`)).resize(104,60).png().toBuffer();checks++;}
}}
buttons.get('death').handlers.click();frames.shift()(500);if(Number(els.get('death').value)<=0)throw Error('Playback did not advance');frames.shift()(6000);if(buttons.get('death').disabled)throw Error('Playback did not complete');
els.get('method').value='hybrid';els.get('method').handlers.change();for(const [mode,value]of [['build',65],['damage',40],['death',55]]){els.get(mode).value=String(value);els.get(mode).handlers.input();}
const layers=[],labels={build:'CONSTRUCTION',damage:'DAMAGE',death:'DESTRUCTION'};
for(const [row,mode]of ['build','damage','death'].entries())for(const [col,faction]of ['human','resonance'].entries()){
const svg=`<svg xmlns="http://www.w3.org/2000/svg" width="520" height="340" viewBox="0 0 520 340"><rect width="520" height="340" fill="#202a30"/><text x="20" y="28" fill="#e7e5de" font-family="sans-serif" font-size="18">${faction.toUpperCase()} / ${labels[mode]}</text><g transform="translate(0 35)">${els.get(mode+'-'+faction).innerHTML}</g></svg>`;
layers.push({input:await sharp(Buffer.from(svg)).png().toBuffer(),left:col*520,top:row*340});}
await sharp({create:{width:1040,height:1020,channels:4,background:'#171e23'}}).composite(layers).png().toFile(path.join(dir,'schematic-review.png'));
console.log(`PASS: ${checks} SVG states rendered, controls and timed playback verified in a local DOM stub. Browser layout and Unity runtime not verified.`);
}
main().catch(e=>{console.error(e);process.exitCode=1;});
