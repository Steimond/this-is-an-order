const fs=require('node:fs'),path=require('node:path'),{createRequire}=require('node:module');
const sharp=createRequire(path.join(path.dirname(process.execPath),'../node_modules/package.json'))('sharp');
const dir=path.resolve(__dirname,'../../docs/concepts/human-howitzer-directions/round-2/modeling');
module.exports=async function(){
 const data=JSON.parse(fs.readFileSync(path.join(dir,'views-generation.json'),'utf8'));
 for(const [name,v] of Object.entries(data.views)){fs.copyFileSync(v.source,path.join(dir,name+'.png'));fs.writeFileSync(path.join(dir,name+'.prompt.md'),v.prompt+'\n');}
 const cells=[['deployed','ОСНОВНОЙ C · РАЗВЁРНУТОЕ СОСТОЯНИЕ'],['travel','ПОХОДНОЕ · СТВОЛ ВТЯНУТ'],['front','СПЕРЕДИ · РАЗВЁРНУТОЕ'],['rear','СЗАДИ · ОРУДИЕ НАПРАВЛЕНО ВПЕРЁД'],['side','СБОКУ · НОС СЛЕВА'],['top','СВЕРХУ · НОС ВВЕРХ']];
 const layers=[{input:Buffer.from('<svg width="2400" height="100"><text x="20" y="38" font-family="Arial" font-size="32" fill="#fff">ГАУБИЦА C «СТРЕЛА» · РЕФЕРЕНСЫ ДЛЯ МОДЕЛИРОВАНИЯ</text><text x="20" y="78" font-family="Arial" font-size="23" fill="#bac6cd">Основной C задаёт форму. Художественные виды свести на одной сетке; масштаб ячеек различается.</text></svg>'),left:0,top:0}];
 for(let i=0;i<cells.length;i++){
  const [file,label]=cells[i],left=i%2*1200,top=100+Math.floor(i/2)*850;
  layers.push({input:Buffer.from(`<svg width="1200" height="45"><text x="16" y="31" font-family="Arial" font-size="24" fill="#efba78">${label}</text></svg>`),left,top});
  layers.push({input:await sharp(path.join(dir,file+'.png')).resize(1180,790,{fit:'contain',background:'#20282e'}).png().toBuffer(),left:left+10,top:top+45});
 }
 await sharp({create:{width:2400,height:2650,channels:3,background:'#20282e'}}).composite(layers).png().toFile(path.join(dir,'model-sheet.png'));
 const manifestPath=path.join(dir,'model-inputs.json'),m=JSON.parse(fs.readFileSync(manifestPath,'utf8'));
 m.supportingViews=['front.png','side.png','rear.png','top.png'];m.exclude.push('model-sheet.png');
 m.priority='Deployed is exact selected C and primary single-image input. Travel is accepted artistic folding reference. Other elevations are manual construction aids, not exact projections of a common mesh. Do not mix states as camera views.';
 m.referenceStatus='Six reference images and overview prepared. Reconcile stance/proportions/panel joints; hidden rear/roof details are constructive proposals, not separately approved.';
 fs.writeFileSync(manifestPath,JSON.stringify(m,null,2)+'\n');
 const gallery=path.join(dir,'gallery.html');let html=fs.readFileSync(gallery,'utf8');
 html=html.replace('<nav>','<nav><a href="model-sheet.png">Полный модельный лист</a>');
 html=html.replace('Два художественных вида не подтверждают зазоры, вложение ствола и кинематику:', 'Основной и походный виды дополнены передним, боковым, задним и верхним ракурсами. Это художественные референсы: они не подтверждают зазоры, вложение ствола и кинематику:');
 html=html.replace('Походный рисунок подготовлен по решению автора, но отдельно им ещё не оценён.','Походный рисунок принят как художественный референс складывания.');
 html=html.replace('</main>',cells.slice(2).map(([file,label])=>`<article id="${file}"><h2>${label}</h2><a href="${file}.png"><img loading="lazy" src="${file}.png" alt="Гаубица Стрела — ${label}"></a></article>`).join('')+'<p>Дополнительные виды требуют сведения пропорций, опор и панельных стыков на одной сетке; основной C имеет приоритет. Скрытые прежде детали кормы/крыши предложены для сборки объёма и отдельно не утверждены. Ни один вид сзади не означает разворот орудия назад: во всех модельных ракурсах оно направлено вперёд по корпусу.</p></main>');
 fs.writeFileSync(gallery,html);
 console.log('Built complete howitzer reference pack: 6 images, sheet, provenance, gallery and manifest.');
};
if(require.main===module)module.exports().catch(e=>{console.error(e);process.exitCode=1;});
