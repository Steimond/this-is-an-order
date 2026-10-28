const fs=require('node:fs'),path=require('node:path');
const dir=path.resolve(__dirname,'../../docs/concepts/human-command-module-directions');
const data=JSON.parse(fs.readFileSync(path.join(dir,'generation.json'),'utf8'));
const template=fs.readFileSync(path.join(dir,'../human-aerodrome-directions/gallery.html'),'utf8');
const css=template.match(/<style>([\s\S]*?)<\/style>/)[1];
function diagram(v){
let body='';
if(v.type==='lights'){for(let j=0;j<3;j++)body+=`<rect class="light anim" x="${65+j*95}" y="${v.id==='g'?100-j*25:54}" width="72" height="40" rx="4" fill="#eab253" style="animation-delay:-${j*.65}s"/>`;}
else if(v.type==='slide')body='<path d="M40 50H360M40 115H360" stroke="#839098" stroke-width="8"/><rect class="slide anim" x="65" y="62" width="105" height="40" fill="#b87938"/>';
else body='<rect x="95" y="35" width="210" height="95" fill="#596970"/><rect class="flap anim" x="95" y="35" width="210" height="95" fill="#394047" stroke="#819097" stroke-width="5" style="transform-origin:200px 35px"/>';
return `<svg viewBox="0 0 400 165"><rect x="15" y="12" width="370" height="140" rx="8" fill="#303b43"/>${body}</svg>`;
}
for(const v of data.variants){fs.copyFileSync(v.source,path.join(dir,v.id+'.png'));fs.writeFileSync(path.join(dir,v.id+'.prompt.md'),v.prompt+'\n');}
const cards=data.variants.map((v,i)=>`<article id="${v.id}"><h2>${v.name}</h2><a href="${v.id}.png"><img src="${v.id}.png" alt="${v.name}, нейтральный референс" ${i>1?'loading="lazy"':''}></a><p>${v.description}</p><details><summary>Предложение индикации готовности</summary><p>${v.effect}</p><p>Художественная идея. Модуль открывает технологии, собственной очереди производства или исследований у него нет. Схема не задаёт новую механику.</p><div class="schematic" data-state="idle"><button data-state="idle" aria-pressed="true">Нейтрально</button><button data-state="production" aria-pressed="false">Пример эффекта</button><button data-state="cancelled" aria-pressed="false">Отмена</button>${diagram(v)}</div></details></article>`).join('\n');
fs.writeFileSync(path.join(dir,'gallery.html'),`<!doctype html><html lang="ru"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Командный модуль людей — A–H</title><style>${css}</style><header><h1>Командный модуль людей · A–H</h1><p>Пристройка 2×2 рядом со штабом, открывает третий уровень технологий. 150 М / 150 П, 55 с, 350 HP; требует завод или аэродром. Не занимает очередь штаба. Выбор внешности пока не сделан.</p><p>Восемь разных силуэтов с боковым стыковочным интерфейсом. Все лампы выключены, люки закрыты. Свет и движение — отдельные предложения индикации, не собственная производственная очередь.</p><nav>${data.variants.map(v=>`<a href="#${v.id}">${v.id.toUpperCase()}</a>`).join('')}<a href="README.md">Описание</a></nav></header><main>${cards}</main><script>document.querySelectorAll('button[data-state]').forEach(b=>b.onclick=()=>{const s=b.closest('.schematic');s.dataset.state=b.dataset.state==='cancelled'?'idle':b.dataset.state;s.querySelectorAll('button').forEach(x=>x.setAttribute('aria-pressed',String(x===b)));});</script></html>`);
fs.writeFileSync(path.join(dir,'README.md'),`# Командный модуль людей — A–H

2026-10-28. [Галерея](gallery.html) · [Точные промпты](generation.json). Выбор пока не сделан.

${data.variants.map(v=>`- ${v.name}: ${v.description} Предложение эффекта: ${v.effect}`).join('\n')}

Роль: пристройка рядом со штабом, третий уровень технологий, не занимает очередь штаба. 150 М / 150 П, 55 с, 350 HP, 2×2; требует завод или аэродром. Свою очередь производства или исследований концепты не вводят. Декоративный диагностический цикл/индикация готовности — предложение; привязка и темп ещё не утверждены. Не показывать фиктивное исследование и не добавлять связь/обзор/сеть.

Все PNG нейтральные: свет выключен, линзы тёмно-серые, оранжевый — краска, люки закрыты, механизмы неподвижны. Схемы эффектов и их скриншоты не передавать на AI-моделирование/текстурирование. Эмиссия отдельно от albedo.

Пятно 2×2 предварительное, стыковочный интерфейс входит в габарит. После выбора проверить на единой геометрии размер и положение относительно утверждённого штаба, совпадение сопряжения, доступ рабочих и подвижные детали. Мелкие швы, точное число линз и скрытые стороны не являются размерными требованиями. У E генерация дала две заметные верхние панели, число каналов уточнять после выбора. У F створку выделить из крупной решётки над закрытым техническим объёмом.

После утверждения: нейтральные ракурсы, общий каркас и OBJ, паспорт частей/эффектов, запись в DESIGN.md, обновление PROJECT.html.

Изображения a.png–h.png и промпты a.prompt.md–h.prompt.md сохранены здесь. Режим — встроенный imagegen, восемь отдельных вызовов. Схемы — SVG/CSS.
`);
console.log('Command module: eight neutral concepts, exact prompts and separate effect proposals saved.');
