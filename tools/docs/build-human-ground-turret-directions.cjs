const fs=require('node:fs'),path=require('node:path');
const dir=path.resolve(__dirname,'../../docs/concepts/human-ground-turret-directions');
const data=JSON.parse(fs.readFileSync(path.join(dir,'generation.json'),'utf8'));
const template=fs.readFileSync(path.join(dir,'../human-command-module-directions/round-2/gallery.html'),'utf8');
const css=template.match(/<style>([\s\S]*?)<\/style>/)[1]+`.aim{transform-origin:200px 85px;animation:aim 4s ease-in-out infinite;animation-play-state:paused}.recoil{animation:recoil .9s ease-out infinite;animation-play-state:paused}.schematic[data-state=production] .aim,.schematic[data-state=production] .recoil{animation-play-state:running}.schematic[data-state=idle] .aim,.schematic[data-state=idle] .recoil{animation:none}@keyframes aim{0%,100%{transform:rotate(-20deg)}50%{transform:rotate(20deg)}}@keyframes recoil{0%,55%,100%{transform:translateX(0)}62%{transform:translateX(-12px)}}@media(prefers-reduced-motion:reduce){.aim,.recoil{animation:none!important}}`;
for(const v of data.variants){fs.copyFileSync(v.source,path.join(dir,v.id+'.png'));fs.writeFileSync(path.join(dir,v.id+'.prompt.md'),v.prompt+'\n');}
const cards=data.variants.map((v,i)=>`<article id="${v.id}"><h2>${v.name}</h2><a href="${v.id}.png"><img src="${v.id}.png" alt="${v.name}, целая неподвижная турель без эффектов" ${i>1?'loading="lazy"':''}></a><p>${v.description}</p><details><summary>Наведение и стрельба — отдельно</summary><p>${v.motion}</p><p>Условная общая схема поворота и отдачи, не точный риг выбранной модели. Без цели нет постоянной производственной анимации.</p><div class="schematic" data-state="idle"><button data-state="idle" aria-pressed="true">Покой</button><button data-state="production" aria-pressed="false">Пример наведения и отдачи</button><svg viewBox="0 0 400 170"><rect x="30" y="20" width="340" height="130" rx="8" fill="#303b43"/><circle cx="200" cy="85" r="52" fill="#596970"/><g class="aim"><rect x="155" y="56" width="90" height="58" rx="5" fill="#cbb99a"/><rect class="recoil" x="230" y="75" width="90" height="20" fill="#839097"/><rect x="161" y="60" width="20" height="50" fill="#b87938"/></g></svg></div></details></article>`).join('\n');
fs.writeFileSync(path.join(dir,'gallery.html'),`<!doctype html><html lang="ru"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Наземная турель людей — A–H</title><style>${css}</style><header><h1>Наземная турель людей · A–H</h1><p>Самостоятельная защита только от наземных целей. Заменяет гарнизонный опорный пост; бойцы внутри не нужны. ПВО будет отдельным зданием.</p><p>Предварительное пятно 2×2. Число и вид стволов — предложения внешности; урон, дальность и темп огня пока не утверждены. Все изображения без стрельбы, свечения и дыма.</p><nav>${data.variants.map(v=>`<a href="#${v.id}">${v.id.toUpperCase()}</a>`).join('')}<a href="README.md">Описание</a></nav></header><main>${cards}</main><script>document.querySelectorAll('button[data-state]').forEach(b=>b.onclick=()=>{const s=b.closest('.schematic');s.dataset.state=b.dataset.state;s.querySelectorAll('button').forEach(x=>x.setAttribute('aria-pressed',String(x===b)));});</script></html>`);
fs.writeFileSync(path.join(dir,'README.md'),`# Наземная турель людей — восемь направлений

2026-10-28. [Галерея](gallery.html) · [Точные промпты](generation.json). Ни один вариант пока не выбран.

${data.variants.map(v=>`- ${v.name}: ${v.description} Движение: ${v.motion}`).join('\n')}

Роль: собственное оружие только по наземным целям, гарнизон отсутствует. ПВО — отдельная постройка. 100 М / 0 П, 25 с, 600 HP, 2×2 и казарма пока перенесены от старого опорного поста как исходные числа для проверки, не новый утверждённый баланс. Оружие, урон, темп и дальность открыты (BQ-31). Число стволов не задаёт количество целей или отдельных очередей атаки.

Все PNG нейтральные: целая турель в покое, лампы выключены, без вспышек, выстрелов, трассеров, дыма и свечения. Оранжевый — краска. Для AI-моделирования использовать PNG, не скриншоты анимационных схем. Вспышки/дым/VFX отдельны от albedo.

Основание неподвижно, поворотная платформа и оружейная люлька разделены; отдачу отделять от наведения. У D вращающийся блок стволов требует отдельной оси; точное число стволов уточнить после выбора. У B изображение не гарантирует свободный круговой поворот внутри борта: до моделирования согласовать вращающийся защитный воротник вместе с установкой либо безопасную геометрию выреза, не фиксировать ограниченный сектор стрельбы автоматически. Для всех проверить полный поворот, наклон, откат, коллизии соседних зданий, габарит основания и радиус выноса стволов. Картинки не доказывают, что ствол целиком помещается в строительном пятне 2×2; эти пределы нужно отдельно согласовать на общей геометрии.

Схема под каждым вариантом демонстрирует общий принцип наведения и отдачи, не точную кинематику или боевой темп. Турель не производит юнитов и не исследует; не нужна ложная анимация производственной занятости. Реальные поворот/выстрелы должны следовать цели и игровым событиям.

После выбора: нейтральные модельные ракурсы, общий каркас/OBJ, паспорт частей и осей, DESIGN.md и PROJECT.html. Затем завершить визуал ПВО. После общего дизайна зданий — строительство людей и разрушение обеих рас. Выбор шейдера/отдельных моделей/сочетания отложен.

Происхождение: встроенный imagegen, восемь отдельных финальных изображений. PNG и точные промпты a.prompt.md–h.prompt.md сохранены рядом. Отдельные схемы — SVG/CSS.
`);
for(const file of ['gallery.html','README.md']){
 const p=path.join(dir,file);let text=fs.readFileSync(p,'utf8');
 text=text.replaceAll('A–H','A–G').replace('восемь направлений','история направлений').replace('восемь отдельных финальных изображений','семь финальных изображений; H не завершён, поскольку автор выбрал C');
 if(fs.existsSync(path.join(dir,'selection.json'))){
  if(file==='gallery.html')text=text.replace('<h1>Наземная турель людей · A–G</h1>','<h1>Наземная турель людей · история A–G</h1><p><strong>Выбран C «Боковой щит» за строгие пропорции.</strong> <a href="modeling/gallery.html">Модельные ракурсы и паспорт</a>. Поиск завершён после выбора C; H отменён.</p>');
  else text=text.replace('Ни один вариант пока не выбран.','Выбран C «Боковой щит» за строгие пропорции. [Ракурсы и паспорт](modeling/gallery.html). H отменён после выбора C.');
 }
 fs.writeFileSync(p,text);
}
console.log('Ground turret: '+data.variants.length+' neutral concepts, exact prompts and separate aiming/recoil diagrams saved.');
