const fs = require('node:fs');
const path = require('node:path');
const {createRequire} = require('node:module');
const runtimeRequire = createRequire(path.join(path.dirname(process.execPath), '../node_modules/package.json'));
const sharp = runtimeRequire('sharp');
const dir = path.resolve(__dirname, '../../docs/concepts/human-worker-directions');
const data = JSON.parse(fs.readFileSync(path.join(dir, 'generation.json'), 'utf8'));
const notes = {
 a:'Отдельная поворотная сенсорная головка; боковая рука и пустой кузов.',
 b:'Низкая оптика в носу, рука над центральной осью, задний закрытый блок.',
 c:'Высокая портальная рама; в полученном варианте инструмент свисает над кузовом. Компактную походную укладку ещё предстоит согласовать.',
 d:'Асимметричный аппаратный блок и вынесенная вперёд рабочая рука. Проверить габарит манипулятора после выбора.',
 e:'Открытая центральная ванна; в полученном варианте рука приподнята. Походную укладку ещё предстоит согласовать.',
 f:'Продольная телескопическая стрела с передним инструментом; проверить вынос и зазоры.',
 g:'Боковая ниша со сложенной рукой, закрытый аппаратный объём.',
 h:'Видимая подвеска и центральная рука; в полученном варианте рука поднята, а грузовая площадка имеет борта. Походную укладку ещё предстоит согласовать.'
};
const common = 'Все восемь — предложения внешности, выбор автора ещё не сделан. Четырёхколёсный беспилотный инженерный дрон; добыча, строительство, ремонт и контактная атака следуют DESIGN.md. Размер кузова, число инструментов и форма руки не меняют скорость добычи, вместимость, урон или правила ремонта.';
const production = 'После выбора: согласовать инструмент для добычи/стройки/ремонта/контактной атаки; разделить шасси, колёса L/R, подвеску, основание руки, плечо, локоть, запястье и инструмент, оптику при наличии поворота. Оси, пределы и зазоры проверить на одной геометрии. Подготовить простой, движение, добычу, возврат/разгрузку, стройку, ремонт, атаку и гибель с событиями и прерыванием по игре. Свечение, пыль, искры и контактные эффекты отдельно. Предлагаемые зоны владельца — парные верхние панели крыльев и боковые панели оборудования/кузова, с задней зоной для чтения сзади; точную раскладку закрепить после выбора. Оранжевые акценты и оптика не являются автоматически цветом игрока. Модель, маски, риг и клипы ещё не созданы; проверка Unity не выполнена.';
const css = 'body{margin:0;background:#182027;color:#e4e6e7;font:16px/1.55 system-ui}header,main{max-width:1440px;margin:auto;padding:24px}h1{font-size:32px}a{color:#efba78}nav{display:flex;gap:16px;flex-wrap:wrap}.grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:24px}article{background:#25313a;padding:18px;border-radius:12px}img{width:100%;height:auto;display:block;border-radius:6px}h2{margin:0 0 12px}details{margin-top:12px}summary{cursor:pointer;color:#efba78}@media(max-width:750px){.grid{grid-template-columns:1fr}}';
async function build(){
 const cells=[];
 for(let i=0;i<data.variants.length;i++){
  const v=data.variants[i];
  fs.copyFileSync(v.source,path.join(dir,v.id+'.png'));
  fs.writeFileSync(path.join(dir,v.id+'.prompt.md'),v.prompt+'\n');
  const tile=await sharp(path.join(dir,v.id+'.png')).resize(640,427).png().toBuffer();
  cells.push({input:tile,left:(i%2)*640,top:Math.floor(i/2)*475+48});
  const title=Buffer.from(`<svg width="640" height="48"><rect width="640" height="48" fill="#182027"/><text x="20" y="33" fill="#efba78" font-family="Arial" font-size="24">${v.name}</text></svg>`);
  cells.push({input:title,left:(i%2)*640,top:Math.floor(i/2)*475});
 }
 await sharp({create:{width:1280,height:1900,channels:3,background:'#182027'}}).composite(cells).png().toFile(path.join(dir,'overview.png'));
 const cards=data.variants.map(v=>`<article id="${v.id}"><h2>${v.name}</h2><a href="${v.id}.png"><img src="${v.id}.png" alt="${v.name} — рабочий-дрон, вариант для выбора" loading="lazy"></a><p>${v.description}</p><p>${notes[v.id]}</p><details><summary>Подготовка после выбора</summary><p>${production}</p></details></article>`).join('\n');
 fs.writeFileSync(path.join(dir,'gallery.html'),`<!doctype html><html lang="ru"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Рабочий-дрон людей · A–H</title><style>${css}</style><header><h1>Рабочий-дрон людей · A–H</h1><p>29 октября 2026 · первый юнит в последовательном проходе людей</p><p>${common}</p><p>Песочная броня, графитовая конструкция, приглушённая оранжевая краска. Свет и оптика выключены. Неподвижные концепты без рабочих эффектов; это ещё не согласованные модельные входы. Нажмите изображение, чтобы открыть оригинал.</p><nav>${data.variants.map(v=>`<a href="#${v.id}">${v.id.toUpperCase()}</a>`).join('')}<a href="overview.png">Все варианты рядом</a><a href="README.md">Паспорт этапа</a><a href="../../PROJECT.html#art">Дизайн</a></nav></header><main class="grid">${cards}</main></html>`);
 fs.writeFileSync(path.join(dir,'README.md'),`# Рабочий-дрон людей — варианты A–H\n\n2026-10-29. [Галерея](gallery.html) · [Общий лист](overview.png) · [Точные промпты и происхождение](generation.json).\n\n${common}\n\n${data.variants.map(v=>`- **${v.name}**: ${v.description} ${notes[v.id]}`).join('\n')}\n\n## Подготовка после выбора\n\n${production}\n\nВсе PNG без свечения и рабочих эффектов. У C/E/H рука приподнята: это фактическое положение концепта, не утверждённая походная поза. Показанный материал не подтверждает готовность нейтрального входа для генерации 3D. Масштаб, скрытые стороны, число колёс и соединения проверить на выбранном едином объёме; игровой footprint и технические бюджеты не назначены этим проходом.\n\nИгровые правила: [DESIGN.md](../../DESIGN.md#rules). Производственные требования: [BLENDER_ASSET_RULES.md](../../BLENDER_ASSET_RULES.md). После выбора — нейтральный основной вид, согласованные отдельные ракурсы, паспорт частей и действий; затем модель по общему процессу.\n\nПроисхождение: встроенный imagegen, восемь отдельных вызовов. Исходные изображения скопированы без изменения. Общий лист собран из оригиналов с подписями; это сравнение, не вход для 3D. Промпты сохранены в a.prompt.md–h.prompt.md и generation.json.\n`);
 console.log('Saved 8 concepts, exact prompts, gallery, stage passport and comparison sheet.');
}
build().catch(e=>{console.error(e);process.exitCode=1;});
