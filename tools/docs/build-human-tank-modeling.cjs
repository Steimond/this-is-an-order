const fs=require('node:fs'),path=require('node:path'),{createRequire}=require('node:module');
const sharp=createRequire(path.join(path.dirname(process.execPath),'../node_modules/package.json'))('sharp');
const dir=path.resolve(__dirname,'../../docs/concepts/human-tank-directions/round-2/modeling');
require('./build-human-tank-selected.cjs');
const cells=[['hero-neutral.png','ОСНОВНОЙ ВИД · A + СТВОЛ D',0,100,1400,920],['top.png','СВЕРХУ · НОС ВВЕРХ',1440,100,900,1960],['front.png','СПЕРЕДИ',0,1050,700,510],['rear.png','СЗАДИ',710,1050,700,510],['side.png','СБОКУ · НОС СЛЕВА',0,1590,1400,470]];
const esc=s=>s.replaceAll('&','&amp;').replaceAll('<','&lt;');
(async()=>{
 const layers=[];
 for(const [file,label,left,top,width,height] of cells){
  layers.push({input:Buffer.from(`<svg width="${width}" height="40"><text x="12" y="29" font-family="Arial" font-size="24" fill="#efba78">${esc(label)}</text></svg>`),left,top});
  layers.push({input:await sharp(path.join(dir,file)).resize(width,height-50,{fit:'contain',background:'#20282e'}).png().toBuffer(),left,top:top+45});
 }
 layers.push({input:Buffer.from('<svg width="2340" height="90"><text x="20" y="38" font-family="Arial" font-size="32" fill="#ffffff">ТАНК ЛЮДЕЙ · СКОБА · РЕФЕРЕНСЫ ДЛЯ МОДЕЛИРОВАНИЯ</text><text x="20" y="76" font-family="Arial" font-size="23" fill="#bdc8ce">Основной вид задаёт форму. Ракурсы художественные; точную геометрию свести в одной 3D-модели.</text></svg>'),left:0,top:0});
 await sharp({create:{width:2340,height:2100,channels:3,background:'#20282e'}}).composite(layers).png().toFile(path.join(dir,'model-sheet.png'));
 console.log('Tank reference sheet and gallery built: 5 views; canonical single-image input retained.');
})().catch(e=>{console.error(e);process.exitCode=1;});
