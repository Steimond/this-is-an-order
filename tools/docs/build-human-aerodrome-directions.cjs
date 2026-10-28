const fs=require('node:fs'),path=require('node:path');
const dir=path.resolve(__dirname,'../../docs/concepts/human-aerodrome-directions');
const data=JSON.parse(fs.readFileSync(path.join(dir,'generation.json'),'utf8'));
const template=fs.readFileSync(path.join(dir,'../human-factory-directions/round-2/gallery.html'),'utf8');
const css=template.match(/<style>([\s\S]*?)<\/style>/)[1];
const states=[['idle','Ожидание'],['production','Производство'],['research','Исследование'],['cancelled','Отмена']];
function diagram(v){
  let body='';
  if(v.type==='lights'){
    for(let j=0;j<5;j++)body+=`<rect class="light anim" x="${48+j*63}" y="${v.id==='g'?111-j*18:60}" width="45" height="36" rx="3" fill="#eab253" style="animation-delay:-${j*.35}s"/>`;
  }else if(v.type==='slide'){
    body='<path d="M40 50H360M40 113H360" stroke="#839098" stroke-width="8"/><rect class="slide anim" x="58" y="61" width="105" height="40" rx="4" fill="#b87938"/>';
  }else{
    for(let j=0;j<(v.id==='e'?2:1);j++){
      const x=v.id==='e'?63+j*147:102,w=v.id==='e'?125:196;
      body+=`<rect x="${x}" y="40" width="${w}" height="84" fill="#596970"/><rect class="flap anim" x="${x}" y="40" width="${w}" height="84" fill="#394047" stroke="#819097" stroke-width="5" style="transform-origin:${x+w/2}px 40px;animation-delay:-${j*1.3}s"/>`;
    }
  }
  return `<svg viewBox="0 0 400 165" role="img" aria-label="Условная схема работы"><rect x="15" y="12" width="370" height="140" rx="8" fill="#303b43"/>${body}</svg>`;
}
for(const v of data.variants){fs.copyFileSync(v.source,path.join(dir,v.id+'.png'));fs.writeFileSync(path.join(dir,v.id+'.prompt.md'),v.prompt+'\n');}
fs.writeFileSync(path.join(dir,'work-cycles.json'),JSON.stringify({status:'unselected concepts',footprintCells:[4,4],modelingState:'All lights OFF, gates and launch hatches CLOSED, moving parts at rest, orange is diffuse paint',workQueue:'One production/research queue; animate only while actual progress advances',states:{idle:'stationary mechanism and no active light sequence',production:'work cycle active',research:'work cycle active for shared research queue',cancelled:'end work cycle, lights off, return mechanism to rest',complete:'end work cycle; launch event separate and does not delay gameplay'},timing:'schematic only; slower research rhythm is a proposal',variants:data.variants.map(({id,name,description,cycle,type})=>({id,name,description,cycle,type}))},null,2));
const cards=data.variants.map((v,i)=>`<article id="${v.id}"><h2>${v.name}</h2><a href="${v.id}.png"><img src="${v.id}.png" alt="${v.name}: нейтральный референс аэродрома" ${i>1?'loading="lazy"':''}></a><p>${v.description}</p><details><summary>Отдельная схема рабочего цикла</summary><p>${v.cycle}</p><p>Схема принципа, не вход для AI-моделирования и не готовый игровой клип.</p><div class="schematic" data-state="idle">${states.map(([id,label])=>`<button data-state="${id}" aria-pressed="${id==='idle'}">${label}</button>`).join('')}${diagram(v)}</div></details></article>`).join('\n');
fs.writeFileSync(path.join(dir,'gallery.html'),`<!doctype html><html lang="ru"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Аэродром людей — A–H</title><style>${css}</style><header><h1>Аэродром людей · восемь направлений A–H</h1><p>Разная архитектура: подкова, складчатая крыша, круглая выпускная палуба, консоль, двойной свод, диагональная площадка, ступени и поперечный мост. Ни один вариант пока не выбран.</p><p>Винтокрыл, перехватчик, транспортник и дрон РЭБ; модификации авиации. Предварительное пятно 4×4, требует оперативный центр.</p><p>Референсы: свет выключен, ворота и люки закрыты. Оранжевый — краска. Рабочий цикл раскрывается отдельно; открытие ангара на выпуск не входит в постоянную анимацию производства.</p><nav>${data.variants.map(v=>`<a href="#${v.id}">${v.id.toUpperCase()}</a>`).join('')}<a href="README.md">Описание</a><a href="../human-factory-directions/round-2/modeling/gallery.html">Утверждённый завод</a></nav></header><main>${cards}</main><script>document.querySelectorAll('button[data-state]').forEach(b=>b.onclick=()=>{const s=b.closest('.schematic');s.dataset.state=b.dataset.state==='cancelled'?'idle':b.dataset.state;s.querySelectorAll('button').forEach(x=>x.setAttribute('aria-pressed',String(x===b)));});</script></html>`);
fs.writeFileSync(path.join(dir,'README.md'),`# Аэродром людей — восемь направлений A–H

2026-10-28. Предложения для выбора. [Галерея](gallery.html) · [Состояния](work-cycles.json) · [Точные промпты и происхождение](generation.json).

${data.variants.map(v=>`- ${v.name}: ${v.description} Работа: ${v.cycle}`).join('\n')}

Назначение по DESIGN.md: винтокрыл, перехватчик, транспортник, дрон РЭБ и модификации авиации. 175 М / 75 П, 45 с, 900 HP, 4×4, требует оперативный центр. Производство и исследования делят одну очередь. Выбор формы не вводит новые площадки ремонта, посадочные ограничения, обзор, радиолокацию, топливо или дополнительные очереди.

PNG a.png–h.png нейтральные: все лампы выключены, линзы тёмные, ворота и люки закрыты, движущиеся элементы в покое. Оранжевый — краска. Никакой эмиссии/блума/искр в базовом цвете. Для AI-моделирования передавать отдельные PNG, не скриншоты активного превью. Маски и эффекты отдельно от albedo.

Схемы SVG/CSS показывают только принцип; не задают точное число линз, амплитуду и ось поворота. Для B/E нужно выделить реальные вентиляционные створки над закрытым техническим объёмом; не раскрывать ангар в каждом рабочем цикле. У D/H движется один сервисный модуль по неподвижной направляющей. Подпись «каретка» у D относится к оранжевому модулю на краю палубы, его масштаб уточнить. У A/C/F/G геометрия может оставаться неподвижной, работу передаёт свет.

Ожидание — покой/рабочие полосы выключены. Работа — цикл следует реальному прогрессу единой очереди. отмена — прекращение цикла и возврат механизма к покою. Завершение/отмена — конец цикла. Возврат к парковке, выпуск самолёта и открытие люков/ворот — отдельные события, не задерживают игровой результат. Условно более медленный ритм исследования в превью — предложение.

Предварительные квадратные основания 4×4 — художественное ограничение, не размерные чертежи. После выбора проверить масштаб четырёх типов авиации, зазоры, путь выпуска, складывание/раздвигание люков и размещение частей внутри пятна на одной общей геометрии. Силуэт и площадка должны читаться как авиационное производство, не наземный завод.

После утверждения: нейтральные модельные ракурсы, общий каркас/OBJ, паспорт движения и материалов, запись в DESIGN.md, пересборка PROJECT.html. Пока выбор в канонический документ не внесён.

Происхождение: встроенный imagegen, восемь отдельных вызовов. Изображения a.png–h.png и точные промпты a.prompt.md–h.prompt.md сохранены в этой папке. Отдельные схемы выполнены в SVG/CSS.
`);
if(fs.existsSync(path.join(dir,'selection.json'))){
  const gallery=path.join(dir,'gallery.html');
  fs.writeFileSync(gallery,fs.readFileSync(gallery,'utf8').replace('Ни один вариант пока не выбран.','Выбран D «Консоль» с сигнальным маячком. <a href="modeling/gallery.html">Утверждённые ракурсы и паспорт</a>.'));
  const readme=path.join(dir,'README.md');
  fs.writeFileSync(readme,fs.readFileSync(readme,'utf8').replace('Предложения для выбора.','История вариантов. Выбран D «Консоль» с вращающимся маячком: [ракурсы и паспорт](modeling/gallery.html).').replace('Пока выбор в канонический документ не внесён.','Выбор D с маячком внесён в DESIGN.md; исторический d.png без маячка не является окончательным референсом.'));
}
console.log('Aerodrome: eight images/prompts, gallery and separate work-cycle previews saved.');
