const fs=require('node:fs'),path=require('node:path'),{createRequire}=require('node:module');
const sharp=createRequire(path.join(path.dirname(process.execPath),'../node_modules/package.json'))('sharp');
const dir=path.resolve(__dirname,'../../docs/concepts/human-interceptor-directions');
async function main(){
 const variants=JSON.parse(fs.readFileSync(path.join(dir,'results.json'),'utf8'));
 const layers=[];
 for(let i=0;i<variants.length;i++){
  const v=variants[i];fs.copyFileSync(v.source,path.join(dir,v.id+'.png'));fs.writeFileSync(path.join(dir,v.id+'.prompt.md'),v.prompt+'\n');
  layers.push({input:await sharp(path.join(dir,v.id+'.png')).resize(768,512,{fit:'contain',background:'#222b32'}).png().toBuffer(),left:i%4*768,top:Math.floor(i/4)*570+58});
  layers.push({input:Buffer.from(`<svg width="768" height="58"><text x="20" y="39" fill="#efba78" font-family="Arial" font-size="30">${v.name}</text></svg>`),left:i%4*768,top:Math.floor(i/4)*570});
 }
 await sharp({create:{width:3072,height:1140,channels:3,background:'#222b32'}}).composite(layers).png().toFile(path.join(dir,'overview.png'));
 fs.writeFileSync(path.join(dir,'generation.json'),JSON.stringify({date:'2026-10-30',tool:'built-in imagegen',status:'eight generated appearance proposals; no selection',variants},null,2)+'\n');
 const note='Восемь вариантов внешности, выбор ещё не сделан. Роль: воздушный контроль, атака только по воздуху; аэродром. Сохраняется исследуемый форсаж: +25% скорости на 4 с, откат 25 с, без иммунитета. Вид оружия и число двигателей не утверждают новый баланс, тип снаряда или режимы. Нет превращения в наземного бойца. Это художественный поиск, не проверенная аэродинамика.';
 fs.writeFileSync(path.join(dir,'gallery.html'),`<!doctype html><html lang="ru"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Перехватчик людей · A–H</title><style>body{margin:0;background:#182027;color:#e5e8eb;font:17px/1.6 system-ui}header,main{max-width:1600px;margin:auto;padding:24px}a{color:#efba78}nav{display:flex;gap:16px;flex-wrap:wrap}img{width:100%;height:auto;display:block}article{background:#25313a;padding:20px;border-radius:14px;margin:30px 0}h2{color:#efba78}</style><header><h1>Перехватчик людей · восемь направлений</h1><p>${note}</p><nav>${variants.map(v=>`<a href="#${v.id}">${v.name}</a>`).join('')}<a href="overview.png">Общее сравнение</a><a href="README.md">Паспорт</a></nav><a href="overview.png"><img src="overview.png" alt="Сравнение A–H"></a></header><main>${variants.map(v=>`<article id="${v.id}"><h2>${v.name}</h2><p>${v.description}</p><a href="${v.id}.png"><img loading="lazy" src="${v.id}.png" alt="${v.name}"></a><p><a href="${v.id}.prompt.md">Промпт</a></p></article>`).join('')}</main></html>`);
 fs.writeFileSync(path.join(dir,'README.md'),'# Перехватчик людей · A–H\n\n'+note+'\n\nСоздано 2026-10-30 встроенным imagegen. Полные промпты и пути исходников: [generation.json](generation.json).\n\n'+variants.map(v=>`- **${v.name}** — ${v.description} [Изображение](${v.id}.png), [промпт](${v.id}.prompt.md).`).join('\n')+'\n\nПосле выбора: единые нейтральные ракурсы → согласование объёма/двигателей/оружия → UV и независимые зоны цвета игрока → риг, полёт/повороты/остановка по правилам игры/наведение/атака/форсаж/выход из форсажа/смерть и переходы → Unity. Панели владельца должны читаться на корпусе и гондолах с игровой камеры при выключенных огнях; оранжевые детали концептов пока не являются готовой маской. Посадка и трансформация не добавлены как новые игровые режимы. Это изображения, не готовая 3D-модель.\n');
 console.log('Built eight interceptor proposals, overview, gallery and provenance.');
}
main().catch(e=>{console.error(e);process.exitCode=1;});
