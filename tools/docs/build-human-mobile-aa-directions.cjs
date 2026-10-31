const fs=require('node:fs'),path=require('node:path'),{createRequire}=require('node:module');
const sharp=createRequire(path.join(path.dirname(process.execPath),'../node_modules/package.json'))('sharp');
const dir=path.resolve(__dirname,'../../docs/concepts/human-mobile-aa-directions');
const data=JSON.parse(fs.readFileSync(path.join(dir,'generation.json'),'utf8'));
const results=JSON.parse(fs.readFileSync(path.join(dir,'results.json'),'utf8'));
const esc=s=>s.replaceAll('&','&amp;').replaceAll('<','&lt;');
(async()=>{
 data.variants=data.variants.map(v=>({...v,...results.find(r=>r.id===v.id)}));data.status='eight generated proposals; none selected';
 const layers=[];
 for(let i=0;i<data.variants.length;i++){
  const v=data.variants[i];fs.copyFileSync(v.source,path.join(dir,v.id+'.png'));fs.writeFileSync(path.join(dir,v.id+'.prompt.md'),v.prompt+'\n');
  const left=i%4*768,top=Math.floor(i/4)*560;
  layers.push({input:Buffer.from(`<svg width="768" height="48"><text x="20" y="33" font-family="Arial" font-size="26" fill="#efba78">${esc(v.name)}</text></svg>`),left,top});
  layers.push({input:await sharp(path.join(dir,v.id+'.png')).resize(768,512,{fit:'contain',background:'#20282e'}).png().toBuffer(),left,top:top+48});
 }
 await sharp({create:{width:3072,height:1120,channels:3,background:'#20282e'}}).composite(layers).png().toFile(path.join(dir,'overview.png'));
 fs.writeFileSync(path.join(dir,'generation.json'),JSON.stringify(data,null,2)+'\n');
 const css='body{margin:0;background:#182027;color:#e4e6e7;font:16px/1.6 system-ui}header,main{max-width:1680px;margin:auto;padding:24px}a{color:#efba78}nav{display:flex;gap:20px;flex-wrap:wrap}.grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:24px}article{background:#25313a;padding:18px;border-radius:12px;scroll-margin-top:20px}img{display:block;width:100%;height:auto}h1,h2{line-height:1.25}.note{border-left:4px solid #efba78;padding:14px 18px;background:#25313a}#overview{margin-bottom:30px;display:block}@media(max-width:800px){.grid{grid-template-columns:1fr}}';
 fs.writeFileSync(path.join(dir,'gallery.html'),`<!doctype html><html lang="ru"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Зенитчик людей · варианты A–H</title><style>${css}</style><header><h1>Зенитчик людей · восемь вариантов A–H</h1><p>Колёсная ПВО для прикрытия марша. Отдельный юнит, не неподвижная зенитная башня. Ни один вариант не выбран.</p><nav>${data.variants.map(v=>`<a href="#${v.id}">${v.name}</a>`).join('')}<a href="overview.png">Все рядом · полный размер</a><a href="README.md">Роль и статус</a><a href="../../PROJECT.html#art">Дизайн</a></nav><p class="note">Основная роль — борьба с воздухом, небольшой вспомогательный пулемёт — против земли. Ракетные кассеты, тип шасси и число колёс здесь предложены для выбора. Число ячеек не задаёт залп или конечный боезапас. Характер ракетного пуска и движения нужно согласовать после выбора. Развёртывание с опорами и осадный режим не добавлены.</p></header><main><a id="overview" href="overview.png"><img src="overview.png" alt="Восемь колёсных зенитчиков людей A–H рядом"></a><div class="grid">${data.variants.map(v=>`<article id="${v.id}"><h2>${v.name}</h2><a href="${v.id}.png"><img src="${v.id}.png" alt="Колёсный зенитчик ${v.name}, вариант для обсуждения" loading="lazy"></a><p>${v.description}</p><p><a href="${v.id}.png">Открыть крупно</a> · <a href="${v.id}.prompt.md">Промпт</a></p></article>`).join('')}</div><p>У C и F генератор сделал верхний блок крупнее задуманного для лёгкого сопровождения: после выбора его пропорции нужно уточнить. Это художественные предложения, не готовые модельные входы или проверенные механизмы.</p></main></html>`);
 console.log('Built 8 mobile AA proposals, overview, gallery, exact prompts and provenance.');
 if(fs.existsSync(path.join(dir,'selection.json')))require('./build-human-mobile-aa-selected.cjs');
})().catch(e=>{console.error(e);process.exitCode=1;});
