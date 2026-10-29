/* Publish imagegen-produced corrections. Cropping/tiling only packages the generated panels; no geometry is synthesized here. */
const fs=require('node:fs'),path=require('node:path'),{createRequire}=require('node:module');
const renameFile=fs.renameSync.bind(fs);fs.renameSync=(from,to)=>{for(let i=0;i<12;i++){try{return renameFile(from,to);}catch(e){if(i===11||!['EPERM','EBUSY','UNKNOWN'].includes(e.code))throw e;Atomics.wait(new Int32Array(new SharedArrayBuffer(4)),0,0,100);}}};
// Some desktop previews hold an existing HTML briefly; replace the completed file atomically.
const writeFile=fs.writeFileSync.bind(fs);fs.writeFileSync=(file,data,...options)=>{const temp=file+'.repair-tmp';writeFile(temp,data,...options);fs.renameSync(temp,file);};
const copyFile=fs.copyFileSync.bind(fs);fs.copyFileSync=(from,to)=>{const temp=to+'.repair-tmp';copyFile(from,temp);fs.renameSync(temp,to);};
const sharp=createRequire(path.join(path.dirname(process.execPath),'../node_modules/package.json'))('sharp');
const root=path.resolve(__dirname,'../../docs/concepts'),audit=path.join(root,'human-buildings-consistency-audit');
const entries=JSON.parse(fs.readFileSync(path.join(audit,'repairs-v3.json'),'utf8')).entries;
const rows={hq:450,supply:469,extractor:470,barracks:450,workshop:448,operations:442,factory:480,aerodrome:432,command:488,ground:488,aa:507};
const columns={extractor:[478,1057],ground:[498,1035],aa:[479,1055]};
const views=[['front','FRONT'],['right','RIGHT'],['rear','REAR'],['top','TOP'],['front-three-quarter','FRONT 3/4'],['rear-three-quarter','REAR 3/4']];
const css='*{box-sizing:border-box}body{background:#1c2227;color:#ebe6dc;font:17px/1.5 system-ui;margin:0}main{max-width:1600px;margin:auto;padding:26px}a{color:#e9b87d}img{display:block;width:100%;height:auto;border-radius:6px}h1{font-size:30px}h2{font-size:24px}p{color:#c0c9d0}nav{display:flex;gap:18px;flex-wrap:wrap;margin:20px 0}.pair{display:grid;grid-template-columns:1fr 1.8fr;gap:20px;align-items:start}.views{display:grid;grid-template-columns:repeat(3,1fr);gap:14px}figure{margin:0}figcaption{padding:8px}article{padding:24px 0;border-top:1px solid #47545d}.badge{color:#e6b984}.note{padding:14px;background:#29353e;border-left:3px solid #c48b4c}details{margin:18px 0}button{font:inherit}@media(max-width:850px){.pair,.views{grid-template-columns:1fr}}';
function relative(from,to){return path.relative(from,to).replaceAll('\\','/');}
async function main(){const articles=[];
for(const e of entries){const dir=path.join(root,e.modeling),v3=path.join(dir,'corrected-v3');fs.mkdirSync(v3,{recursive:true});
const raw=path.join(v3,'generation-sheet.png');if(fs.existsSync(e.source))fs.copyFileSync(e.source,raw);if(!fs.existsSync(raw))throw Error('Missing generated source '+e.id);
const hero=path.join(dir,'consistency-v2/hero.png');fs.copyFileSync(hero,path.join(v3,'hero.png'));
fs.writeFileSync(path.join(v3,'generation.prompt.md'),e.prompt+'\n');
const m=await sharp(raw).metadata(),sx=m.width/1536,sy=m.height/1024,boundary=Math.round(rows[e.id]*sy),cw=Math.floor(m.width/3),layers=[];
for(let i=0;i<views.length;i++){const [id,label]=views[i],col=i%3,row=Math.floor(i/3),out=path.join(v3,id+'.png'),single=e.singles[id];
if(id==='front-three-quarter'){fs.copyFileSync(hero,out);}
else if(single){const target=path.join(v3,id+'-generated.png');if(fs.existsSync(single.source))fs.copyFileSync(single.source,target);await sharp(target).png().toFile(out);fs.writeFileSync(path.join(v3,id+'.prompt.md'),single.prompt+'\n');}
else{const xs=[0,...(columns[e.id]||[512,1024]).map(x=>Math.round(x*sx)),m.width],left=xs[col]+4,top=(row?boundary:0)+4,width=xs[col+1]-xs[col]-8,height=(row?m.height-boundary:boundary)-8;await sharp(raw).extract({left,top,width,height}).png().toFile(out);}
const panel=await sharp(out).resize({width:770,height:720,fit:'contain',background:'#777777'}).png().toBuffer();layers.push({input:panel,left:col*800+15,top:row*800+115});
}
const labels=views.map((v,i)=>`<text x="${i%3*800+25}" y="${Math.floor(i/3)*800+100}" font-size="24">${v[1]}</text>`).join('');
const header=Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="2400" height="1710"><rect width="2400" height="1710" fill="#222a30"/><g fill="#e7e1d6" font-family="Arial"><text x="25" y="43" font-size="29">${e.name} / CORRECTED V3 / LIGHTS OFF</text>${labels}</g></svg>`);
const sheet=path.join(v3,'model-sheet.png');await sharp(header).composite(layers).png().toFile(sheet);
const original=path.join(dir,'model-sheet.png'),backup=path.join(dir,'model-sheet.before-repair.png');if(!fs.existsSync(backup))fs.copyFileSync(original,backup);fs.copyFileSync(sheet,original);
const gallery=path.join(dir,'gallery.html');let old=fs.readFileSync(gallery,'utf8');
old=old.replace(/<aside data-consistency-audit[\s\S]*?<\/aside>/,'');
old=old.replace(/<aside data-corrected-v3[\s\S]*?<\/aside>/g,'');
old=old.replace(/<details data-rejected-sheet><summary>[\s\S]*?<\/summary>([\s\S]*?)<\/details>/g,'$1');
old=old.replace(/<h1\b/,`<aside data-corrected-v3 style="padding:16px;background:#293d36;color:#eef5ed">Раक़урсы заменены 29.10: новый детальный набор V3. <a style="color:#edcb96" href="corrected-v3/gallery.html">Открыть шесть видов и основной 3/4</a></aside><h1`);
old=old.replaceAll('Пять художественных ракурсов','Исправленные детальные ракурсы V3').replaceAll('Пять нейтральных ракурсов','Исправленные детальные ракурсы V3').replaceAll('Пять ракурсов','Исправленные ракурсы V3');
old=old.replace(/src="([^"]+)"/g,(match,src)=>path.resolve(dir,src)===path.resolve(root,e.hero)?'src="corrected-v3/hero.png"':match);
old=old.replaceAll('Раक़урсы','Ракурсы');
// The current sheet now has six views. Keep existing motion previews and specifications in place.
fs.writeFileSync(gallery,old);
const manifest={revision:'corrected-v3',date:'2026-10-29',approvedDesign:e.name,inputs:['corrected-v3/hero.png',...views.map(v=>'corrected-v3/'+v[0]+'.png')],contactSheet:'corrected-v3/model-sheet.png',exclude:['model-sheet.before-repair.png','model-inputs.pre-audit.json','consistency-v2/structural-sheet.png','corrected-v3/generation-sheet.png','active animation previews'],priority:'Approved neutral hero defines silhouette and relative proportions; corrected views explain the same building. Do not use old rejected sheets or coarse proxy as an alternative design.',materials:'All lamps and VFX OFF. Orange is diffuse paint. Closed doors and covers.',scope:'Detailed artistic reference, not a dimensioned CAD drawing or a finished 3D mesh.'};fs.writeFileSync(path.join(dir,'model-inputs.json'),JSON.stringify(manifest,null,2));
const readme=path.join(dir,'README.md');let r=fs.readFileSync(readme,'utf8');r=r.replace(/^> \*\*Аудит согласованности 2026-10-29:[\s\S]*?\n\n/,'');if(!r.includes('**Исправленные ракурсы V3'))r='> **Исправленные ракурсы V3 · 2026-10-29:** модельный лист заменён детальными изображениями по выбранному 3/4. [Новый пакет](corrected-v3/gallery.html) · [точные входы](model-inputs.json). Основной 3/4 сохраняет приоритет по пропорциям. Старый лист и грубые каркасы не являются альтернативным дизайном.\n\n'+r;fs.writeFileSync(readme,r);
const nav=`<nav><a href="../gallery.html">Паспорт и отдельная анимация</a><a href="../model-inputs.json">Входы для моделирования</a><a href="${relative(v3,path.join(audit,'gallery.html'))}">Все 11 зданий</a><a href="model-sheet.png">Скачать общий лист</a></nav>`;
fs.writeFileSync(path.join(v3,'gallery.html'),`<!doctype html><html lang="ru"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>${e.name} — исправленные ракурсы V3</title><style>${css}</style><main><h1>${e.name} · исправленные ракурсы V3</h1><p class="badge">29.10.2026 · нейтральные материалы · эффекты выключены</p>${nav}<h2>Утверждённый основной 3/4</h2><img src="hero.png" alt="Основной выбранный вид ${e.name}"><h2>Исправленный модельный лист</h2><img src="model-sheet.png" alt="Шесть исправленных детальных ракурсов ${e.name}"><h2>Виды отдельно</h2><div class="views">${views.map(([id,label])=>`<figure><figcaption>${label}</figcaption><a href="${id}.png"><img src="${id}.png" loading="lazy" alt="${e.name} ${label}"></a></figure>`).join('')}</div><p>Основной 3/4 задаёт выбранный силуэт и соотношения деталей. Все изображения показывают одно состояние покоя; работу механизмов и свет брать из отдельного превью. Это художественные референсы без размерной разметки.</p></main></html>`);
articles.push(`<article id="${e.id}"><h2>${e.name}</h2><p><a href="../${e.modeling}/corrected-v3/gallery.html">Открыть пакет</a> · <a href="../${e.modeling}/corrected-v3/model-sheet.png">Общий лист PNG</a> · <a href="../${e.modeling}/corrected-v3/rear.png">Сзади</a></p><div class="pair"><img src="../${e.modeling}/corrected-v3/hero.png" loading="lazy" alt="Основной вид ${e.name}"><a href="../${e.modeling}/corrected-v3/gallery.html"><img src="../${e.modeling}/corrected-v3/model-sheet.png" loading="lazy" alt="Исправленные ракурсы ${e.name}"></a></div></article>`);
console.log(e.id+' detailed sheet and six views published');
}
const auditGallery=path.join(audit,'gallery.html'),archive=path.join(audit,'gallery-before-repairs.html');if(!fs.existsSync(archive))fs.copyFileSync(auditGallery,archive);
fs.writeFileSync(auditGallery,`<!doctype html><html lang="ru"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Здания людей — исправленные ракурсы V3</title><style>${css}</style><main><h1>Здания людей · исправленные детальные ракурсы</h1><p>Все 11 модельных листов заменены. Основные выбранные 3/4 сохранены; свет и рабочие эффекты выключены. Ниже — сами изображения, отдельные виды и ссылки в модельные пакеты.</p><nav>${entries.map(e=>`<a href="#${e.id}">${e.name}</a>`).join('')}</nav>${articles.sort((a,b)=>a.includes('id="aa"')?-1:b.includes('id="aa"')?1:0).join('')}</main></html>`);
}
main().catch(e=>{console.error(e);process.exitCode=1;});
