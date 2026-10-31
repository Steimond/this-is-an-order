const fs=require('node:fs'),path=require('node:path'),{createRequire}=require('node:module');
const sharp=createRequire(path.join(path.dirname(process.execPath),'../node_modules/package.json'))('sharp');
const dir=path.resolve(__dirname,'../../docs/concepts/human-howitzer-directions/round-2');
const data=JSON.parse(fs.readFileSync(path.join(dir,'generation.json'),'utf8'));
const results=JSON.parse(fs.readFileSync(path.join(dir,'results.json'),'utf8'));
data.variants=data.variants.map(v=>({...v,...results.find(r=>r.id===v.id)}));data.status='eight new illustrated proposals; none selected';
const esc=s=>s.replaceAll('&','&amp;').replaceAll('<','&lt;');
(async()=>{
 const layers=[];
 for(let i=0;i<data.variants.length;i++){
  const v=data.variants[i];fs.copyFileSync(v.source,path.join(dir,v.id+'.png'));fs.writeFileSync(path.join(dir,v.id+'.prompt.md'),v.prompt+'\n');
  const left=i%4*768,top=Math.floor(i/4)*560;
  layers.push({input:Buffer.from(`<svg width="768" height="48"><text x="20" y="33" font-family="Arial" font-size="26" fill="#efba78">${esc(v.name)}</text></svg>`),left,top});
  layers.push({input:await sharp(path.join(dir,v.id+'.png')).resize(768,512,{fit:'contain',background:'#20282e'}).png().toBuffer(),left,top:top+48});
 }
 await sharp({create:{width:3072,height:1120,channels:3,background:'#20282e'}}).composite(layers).png().toFile(path.join(dir,'overview.png'));
 fs.writeFileSync(path.join(dir,'generation.json'),JSON.stringify(data,null,2)+'\n');
 const css='body{margin:0;background:#182027;color:#e4e6e7;font:16px/1.6 system-ui}header,main{max-width:1680px;margin:auto;padding:24px}a{color:#efba78}nav{display:flex;gap:20px;flex-wrap:wrap}.grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:24px}article{background:#25313a;padding:18px;border-radius:12px;scroll-margin-top:20px}img{display:block;width:100%;height:auto}h1,h2{line-height:1.25}.status{color:#aab9c2}.note{border-left:4px solid #efba78;padding:14px 18px;background:#25313a}#overview{margin-bottom:30px}@media(max-width:800px){.grid{grid-template-columns:1fr}}';
 fs.writeFileSync(path.join(dir,'gallery.html'),`<!doctype html><html lang="ru"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Гаубица людей · восемь новых вариантов A–H</title><style>${css}</style><header><h1>Гаубица людей · восемь новых вариантов</h1><p>Второй проход · 29 октября 2026. Более выразительные силуэты, открытые силовые рамы, контраст материалов и разные пропорции. Все восемь созданы заново; прежние A/C находятся в первом проходе. Буквы A–H относятся только к этому набору.</p><nav>${data.variants.map(v=>`<a href="#${v.id}">${v.name}</a>`).join('')}<a href="overview.png">Общий лист в полном размере</a><a href="README.md">Описание и статус</a><a href="../gallery.html">Первый проход</a></nav><p class="note">Ни один вариант не выбран. Сейчас сравниваем внешний вид в развёрнутом состоянии. Обязательны круговое наведение и стрельба на 360° при неподвижных шасси и опорах. Видимые погоны и свободная палуба учитывают это требование, но один ракурс не подтверждает зазоры: после выбора нужны походная форма, вид вбок/назад и проверка полного круга на общей геометрии.</p></header><main><a id="overview" href="overview.png"><img src="overview.png" alt="Все восемь гаубиц A–H рядом"></a><div class="grid">${data.variants.map(v=>`<article id="${v.id}"><h2>${v.name}</h2><a href="${v.id}.png"><img loading="lazy" src="${v.id}.png" alt="Новый вариант гаубицы ${v.name} в развёрнутом состоянии"></a><p>${v.description}</p><p class="status">Предложение · не выбрано · <a href="${v.id}.png">Открыть крупно</a> · <a href="${v.id}.prompt.md">Промпт</a></p></article>`).join('')}</div></main></html>`);
 const firstGallery=path.join(dir,'../gallery.html');let first=fs.readFileSync(firstGallery,'utf8');
 if(!first.includes('Восемь новых вариантов A–H')){first=first.replace('<nav>','<nav><a href="round-2/gallery.html">Восемь новых вариантов A–H · второй проход</a>');fs.writeFileSync(firstGallery,first);}
 console.log('Built all 8 new howitzer variants, overview, prompts and gallery. None selected.');
 if(fs.existsSync(path.join(dir,'selection.json')))require('./build-human-howitzer-selected.cjs');
})().catch(e=>{console.error(e);process.exitCode=1;});
