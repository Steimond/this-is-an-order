const fs = require('fs');
const path = require('path');
const root = path.resolve(__dirname, '../../docs/concepts/human-factory-directions');
const dir = path.join(root, 'round-2');
const data = JSON.parse(fs.readFileSync(path.join(dir, 'generation.json'), 'utf8'));
const selectionFile = path.join(dir, 'selection.json');
const selected = fs.existsSync(selectionFile) ? JSON.parse(fs.readFileSync(selectionFile,'utf8')) : null;
const descriptions = [
  'Три крупных складки крыши расходятся веером к широкому фасаду.',
  'Крыша провисает седлом между поднятыми краями; массивные боковые опоры.',
  'Три закрытых производственных крыла сходятся в центральном зале.',
  'Низкий выпускной зал и высокая сборочная шахта с наружной кареткой.',
  'Асимметричная оболочка: высокое левое плечо и низкий правый край.',
  'Два смещённых цеха связаны толстым диагональным техническим хребтом.',
  'Секции самого корпуса поднимаются по спирали к центральному ядру.',
  'Четыре сцепленных объёма разной высоты, задняя консоль на диагональной опоре.'
];
const cycles = [
  'Волна света вдоль тёмных полос на складках крыши.',
  'Короткий подъём и возврат одной вентиляционной створки в седловине.',
  'Три группы линз поочерёдно показывают работу крыльев.',
  'Оранжевая каретка совершает короткий вертикальный ход по направляющим.',
  'Последовательность световых ячеек на высоком плече оболочки.',
  'Бегущая волна по диагностической полосе диагонального хребта.',
  'Три группы линз загораются по кругу. Сам корпус неподвижен.',
  'Короткий горизонтальный ход технической плиты над воротами.'
];
const types = ['lights','flap','lights','lift','lights','lights','lights','slide'];
function diagram(i) {
  let body = '';
  if (types[i] === 'lights') {
    const n = i===4 ? 6 : i===5 ? 7 : 3;
    for (let j=0;j<n;j++) body += `<rect class="light anim" x="${45+j*310/n}" y="${i===5?105-j*10:55}" width="${250/n}" height="45" rx="4" fill="#eab253" style="animation-delay:-${j*.35}s"/>`;
  } else if (types[i] === 'lift') {
    body = '<path d="M156 20V145M244 20V145" stroke="#839098" stroke-width="10"/><rect class="lift anim" x="166" y="90" width="68" height="42" rx="5" fill="#bd803d"/>';
  } else if (types[i] === 'flap') {
    body = '<rect x="95" y="35" width="210" height="95" fill="#5d6a70"/><rect class="flap anim" x="95" y="35" width="210" height="95" fill="#cbbb9c" stroke="#819097" stroke-width="5" style="transform-origin:200px 35px"/>';
  } else {
    body = '<path d="M40 45H360M40 119H360" stroke="#839098" stroke-width="8"/><rect class="slide anim" x="70" y="57" width="150" height="50" rx="5" fill="#77858b" stroke="#bd803d" stroke-width="4"/>';
  }
  return `<svg viewBox="0 0 400 165" role="img" aria-label="Схема принципа рабочего цикла"><rect x="15" y="12" width="370" height="140" rx="8" fill="#303b43"/>${body}</svg>`;
}
const states = [['idle','Ожидание'],['production','Производство'],['research','Исследование'],['cancelled','Отмена']];
const variants = data.variants.map((v,i) => ({...v,description:descriptions[i],cycle:cycles[i],type:types[i]}));
for (const v of variants) {
  fs.copyFileSync(v.source, path.join(dir, `${v.id}.png`));
  fs.writeFileSync(path.join(dir, `${v.id}.prompt.md`), v.prompt+'\n');
}
fs.writeFileSync(path.join(dir, 'work-cycles.json'), JSON.stringify({status:'concept-proposals',modelingState:'All lights off, gates closed, mechanisms at rest; no emissive albedo.',states:states.map(x=>x[0]),queue:'One shared unit/research queue; animate only while actual progress advances.',variants:variants.map(({id,name,description,cycle,type})=>({id,name,description,cycle,type}))},null,2));
const css = `*{box-sizing:border-box}body{margin:0;background:#151b20;color:#e5e3dc;font:16px/1.55 system-ui,sans-serif}header,main{max-width:1500px;margin:auto;padding:24px}h1{font-size:32px;margin:0 0 12px}h2{font-size:23px;margin:0 0 12px}a{color:#e6b374}nav{display:flex;gap:18px;flex-wrap:wrap}main{display:grid;grid-template-columns:1fr 1fr;gap:24px}article{padding:18px;background:#222b31;border:1px solid #3e4a51;border-radius:12px}img{width:100%;height:auto;display:block;border-radius:6px}p{margin:12px 0}details{padding:12px;background:#192228;border-radius:7px}summary{cursor:pointer;color:#e6b374}button{background:#303d45;color:#ece8df;border:1px solid #64737b;border-radius:5px;padding:7px 10px;margin:3px;cursor:pointer}button[aria-pressed=true]{border-color:#e6b374;color:#e6b374}svg{display:block;width:100%;max-height:200px;margin-top:10px}.schematic{--speed:2.8s}.schematic[data-state=research]{--speed:4.5s}.anim{animation-duration:var(--speed);animation-iteration-count:infinite;animation-play-state:paused}.light{animation-name:pulse;opacity:.12}.lift{animation-name:lift}.flap{animation-name:flap}.slide{animation-name:slide}.schematic[data-state=production] .anim,.schematic[data-state=research] .anim{animation-play-state:running}.schematic[data-state=idle] .anim{animation:none}.schematic[data-state=idle] .light,.schematic[data-state=cancelled] .light{opacity:.12;animation:none}@keyframes pulse{0%,65%,100%{opacity:.12}22%,42%{opacity:1}}@keyframes lift{0%,100%{transform:translateY(0)}50%{transform:translateY(-62px)}}@keyframes flap{0%,100%{transform:scaleY(1)}50%{transform:scaleY(.62)}}@keyframes slide{0%,100%{transform:translateX(0)}50%{transform:translateX(95px)}}@media(max-width:850px){main{grid-template-columns:1fr}header,main{padding:14px}}@media(prefers-reduced-motion:reduce){.anim{animation:none!important}}`;
const cards = variants.map((v,i)=>`<article id="${v.id}"><h2>${v.name}</h2><a href="${v.id}.png"><img src="${v.id}.png" alt="${v.name}, лампы выключены, ворота закрыты" ${i>1?'loading="lazy"':''}></a><p>${v.description}</p><details><summary>Отдельная схема работы</summary><p>${v.cycle}</p><p>Условная схема, не референс для AI-моделирования. Ожидание — покой; производство и исследование — работа одной очереди; отмена — возврат к покою.</p><div class="schematic" data-state="idle">${states.map(([id,label])=>`<button data-state="${id}" aria-pressed="${id==='idle'}">${label}</button>`).join('')}${diagram(i)}</div></details></article>`).join('\n');
fs.writeFileSync(path.join(dir, 'gallery.html'), `<!doctype html><html lang="ru"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Завод людей — второй проход A–H</title><style>${css}</style><header><h1>Завод людей · второй проход A–H</h1><p>Разнообразие через силуэт и компоновку корпуса. Восемь предложений, выбор пока не сделан.</p><p>Танк, гаубица (при командном модуле), зенитчик; модификации техники. Пятно 3×3. Песочная оболочка, графитовая конструкция, оранжевая краска.</p><p>Все изображения: свет выключен, ворота закрыты, механизмы в покое. Схемы эффектов раскрываются отдельно. Ритм исследования условно замедлен и ещё не утверждён.</p><nav>${variants.map(v=>`<a href="#${v.id}">${v.id.toUpperCase()}</a>`).join('')}<a href="../gallery.html">Первый проход</a><a href="README.md">Описание и ограничения</a></nav></header><main>${cards}</main><script>document.querySelectorAll('button[data-state]').forEach(b=>b.onclick=()=>{const s=b.closest('.schematic');s.dataset.state=b.dataset.state==='cancelled'?'idle':b.dataset.state;s.querySelectorAll('button').forEach(x=>x.setAttribute('aria-pressed',String(x===b)));});</script></html>`);
fs.writeFileSync(path.join(dir, 'README.md'), `# Завод людей — второй проход A–H\n\n2026-10-28. Первый проход оказался слишком однообразным. Здесь изменена архитектура самого корпуса, а не только оборудование крыши. Ни один вариант пока не утверждён.\n\n[Галерея](gallery.html) · [Первый проход](../gallery.html) · [Промпты и происхождение](generation.json) · [Рабочие циклы](work-cycles.json)\n\n${variants.map(v=>`- ${v.name}: ${v.description} Рабочий цикл: ${v.cycle}`).join('\n')}\n\nНазначение и баланс сохраняются: 175 М / 75 П, 45 с, 1000 HP, 3×3; требует оперативный центр. Танк, гаубица при командном модуле, зенитчик, модификации техники. Одна очередь для производства и исследования. Эффекты следуют фактическому прогрессу, не вводят дополнительных способностей и задержек выхода.\n\nНейтральные PNG a.png–h.png: лампы выключены, механизмы в покое, ворота закрыты. Оранжевый — краска; никаких свечения, искр и запечённой эмиссии. Для AI-моделирования передавать отдельный нейтральный PNG, без скриншотов схем. Рабочие эффекты и маски отдельно от albedo.\n\nСхемы SVG/CSS условные: не задают точные размеры, число линз, оси и амплитуды. Для B выбранная решётка требует подвижного верхнего элемента над закрытым техническим объёмом. Для D отдельно выделить каретку и направляющие. Для H движется только встроенная плита, не консоль здания. Перед моделированием выбранного варианта проверить габарит 3×3, высоты, масштаб ворот и путь техники по общей геометрии; концепты не являются размерными чертежами.\n\nПосле выбора — нейтральные ракурсы, единая геометрия, паспорт частей и анимации, запись в DESIGN.md и пересборка PROJECT.html.\n\nПроисхождение: встроенный imagegen, восемь отдельных вызовов. Изображения и точные промпты сохранены рядом: a.png–h.png и a.prompt.md–h.prompt.md.\n`);
const oldGallery = path.join(root,'gallery.html');
let old = fs.readFileSync(oldGallery,'utf8');
if (!old.includes('round-2/gallery.html')) old = old.replace('<h1>', '<p><a href="round-2/gallery.html">Новые варианты — второй проход A–H</a></p><h1>');
fs.writeFileSync(oldGallery,old);
const oldReadme=path.join(root,'README.md');
let readme=fs.readFileSync(oldReadme,'utf8');
if (!readme.includes('round-2/gallery.html')) readme+='\nСледующий проход по запросу о разнообразии: [восемь новых силуэтов A–H](round-2/gallery.html). Первый проход сохранён как история; выбор завода пока не сделан.\n';
fs.writeFileSync(oldReadme,readme);
if(selected){
  const galleryFile=path.join(dir,'gallery.html');
  fs.writeFileSync(galleryFile,fs.readFileSync(galleryFile,'utf8').replace('Восемь предложений, выбор пока не сделан.','Выбран D «Вертикальная сборка». <a href="modeling/gallery.html">Ракурсы и паспорт моделирования</a>.'));
  const readmeFile=path.join(dir,'README.md');
  fs.writeFileSync(readmeFile,fs.readFileSync(readmeFile,'utf8').replace('Ни один вариант пока не утверждён.','Выбран D «Вертикальная сборка» 2026-10-28. [Ракурсы и паспорт моделирования](modeling/gallery.html).'));
}
console.log(`Saved ${variants.length} images, prompts, work cycles and gallery to ${dir}`);
