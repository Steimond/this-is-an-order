const fs=require('node:fs'),path=require('node:path'),{createRequire}=require('node:module');
const req=createRequire(path.join(path.dirname(process.execPath),'../node_modules/package.json')),sharp=req('sharp');
const dir=path.resolve(__dirname,'../../docs/concepts/human-aerodrome-directions/modeling');
const source=fs.readFileSync(path.join(__dirname,'build-reference-blockouts.cjs'),'utf8');
const helpers=source.slice(source.indexOf('const add='),source.indexOf('const specs='));
const renderer=source.slice(source.indexOf('function renderParts('),source.indexOf('async function main('));
const build=new Function(helpers+renderer+`
const sand='#cbb99a',frame='#394047',orange='#b87938',glass='#42484c';
box('square 4x4 foundation',[0,0,.10],[8,8,.20],frame);
box('sealed lower hangar',[0,0,1.15],[6.10,6.60,1.90],sand);
box('closed gate surround',[.15,-3.36,1.15],[5.35,.16,1.72],frame);
box('closed gate face',[.15,-3.46,1.13],[4.94,.04,1.42],frame);
box('diffuse orange gate paint',[.15,-3.49,1.13],[4.84,.015,.08],orange);
box('fixed raised deck sand rim',[0,0,2.50],[7.46,7.46,.30],sand);
box('empty graphite deck surface',[.05,-.03,2.665],[6.92,6.92,.03],frame);
for(const x of [-2.88,2.88])box('diffuse orange approach stripe '+x,[x,-.3,2.69],[.055,5.90,.012],orange);
for(let i=0;i<16;i++){
  const a=i*2*Math.PI/16,b=(i+.55)*2*Math.PI/16;
  beam('diffuse painted landing circle '+i,[1.13*Math.cos(a),-.30+1.13*Math.sin(a),2.69],[1.13*Math.cos(b),-.30+1.13*Math.sin(b),2.69],.025,.035,orange);
}
for(const x of [-3.44,3.44]){
  beam('fixed diagonal deck support '+x,[x,-3.58,.25],[x,-1.23,2.36],.44,.47,frame);
  box('sand foot '+x,[x,-3.55,.30],[.62,.64,.20],sand);
}
box('rear left enclosed service block',[-2.68,2.59,1.83],[1.65,2.00,3.26],sand);
box('service block dark roof',[-2.68,2.59,3.49],[1.48,1.84,.08],frame);
for(const y of [-2.95,-1.48,0,1.48,2.95])box('right deck edge bumper '+y,[3.58,y,2.80],[.22,.50,.22],sand);
for(const x of [-1.26,.15,1.56,2.97])box('rear deck bumper '+x,[x,3.58,2.80],[.45,.22,.22],sand);
for(const x of [-3.03,-2.64])box('fixed carriage rail '+x,[x,-.28,2.73],[.06,4.38,.06],frame);
box('MOVING horizontal service carriage',[-2.835,.35,2.95],[.57,1.04,.40],orange);
box('carriage graphite center',[-2.835,.35,3.16],[.46,.44,.025],frame);
cylinder('FIXED beacon pedestal',[-2.68,2.59,3.53],[-2.68,2.59,3.63],.26,frame,20);
cylinder('FIXED beacon dark unlit cover',[-2.68,2.59,3.63],[-2.68,2.59,4.02],.20,glass,20);
box('ROTATING internal reflector represented as separate part',[-2.68,2.59,3.82],[.16,.08,.22],frame);
cylinder('FIXED beacon top cap',[-2.68,2.59,4.02],[-2.68,2.59,4.07],.21,frame,20);
const verts=parts.flatMap(p=>p.vertices);
if(verts.some(v=>Math.abs(v[0])>4+1e-8||Math.abs(v[1])>4+1e-8))throw Error('Outside 4x4 footprint');
if(-1.45-.52< -2.47||.90+.52>1.91)throw Error('Carriage exceeds clear rail segment');
return {svg:sheet(parts,'human aerodrome D - beacon OFF'),obj:obj(parts),spec:{variant:'D Console with rotating signal beacon',footprintCells:[4,4],units:'8 arbitrary horizontal units = 4 cells, not meters',axes:'X right, Y rear, Z up; front -Y',modelingState:'all emission OFF, dark gray beacon cover, gate CLOSED, carriage at rest',beacon:{count:1,position:[-2.68,2.59,3.82],axis:[0,0,1],motion:'internal reflector rotates around vertical axis; pedestal and protective cover remain fixed',activeLight:'amber rotating sweep; separate emissive mask/material or light effect, no bright pattern baked in albedo',idle:'off and stopped',cancelled:'off; rotation stops, no resumable pause',production:'rotate and emit while actual queue progress advances',research:'same active-work indication for shared research queue',complete:'stop and switch off; no delay to gameplay',timing:'speed, sweep width, intensity and fade unapproved; preview schematic only'},carriage:{count:1,group:'orange housing and attached graphite center move together',axis:[0,1,0],neutralCenter:[-2.835,.35,2.95],proposedCenterYRange:[-1.45,.90],motion:'short horizontal service loop retained from D concept, secondary to beacon; never crosses flight deck',cancelled:'end cycle and return to park',clearance:'end-stop range validated on coarse rail, refine detailed clamps and bearings'},fixed:['base','lower hangar','closed gate at rest','deck','diagonal supports','service block','rails','beacon pedestal and cover'],queue:'One shared aircraft-production/research queue; no independent simultaneous jobs',launch:'separate future event, no rule requiring landing or docking, no delay introduced',limitations:'coarse shared proportion guide; hidden shell details proposed; final mesh UV materials rig and Unity effect not created'}};
`);
async function main(){
fs.mkdirSync(dir,{recursive:true});const d=build();if(/NaN|Infinity/.test(d.svg+d.obj))throw Error('Invalid geometry');
fs.writeFileSync(path.join(dir,'guide.svg'),d.svg);fs.writeFileSync(path.join(dir,'blockout.obj'),d.obj);await sharp(Buffer.from(d.svg)).png().toFile(path.join(dir,'guide.png'));fs.writeFileSync(path.join(dir,'motion-spec.json'),JSON.stringify(d.spec,null,2)+'\n');
const gen=JSON.parse(fs.readFileSync(path.join(dir,'sheet-generation.json'),'utf8'));fs.copyFileSync(gen.source,path.join(dir,'model-sheet.png'));fs.writeFileSync(path.join(dir,'model-sheet.prompt.md'),gen.prompt+'\n');
fs.writeFileSync(path.join(dir,'model-inputs.json'),JSON.stringify({approved:'D Console with rooftop beacon',inputs:['hero-neutral.png','model-sheet.png'],proportionReferences:['guide.png','blockout.obj'],exclude:['../d.png (historical design without beacon)','active effect previews and their screenshots'],neutral:'all lights OFF, dark gray cover, gate CLOSED, carriage at rest, no emissive albedo',authority:'hero-neutral for approved architecture and surfaces, shared geometry for strict projection/occlusion; sheet is artistic and may contain perspective errors',separateParts:['fixed pedestal','fixed cover','rotating internal reflector','emissive channel','horizontal carriage group','fixed rails']},null,2));
fs.writeFileSync(path.join(dir,'README.md'),`# Аэродром D «Консоль» с сигнальным маячком — моделирование

Выбран автором 2026-10-28 с правкой: добавить вращающуюся рабочую мигалку на крышу заднего левого блока. [Утверждённый нейтральный вид](hero-neutral.png) · [пять художественных ракурсов](model-sheet.png) · [строгие проекции одной геометрии](guide.png) · [OBJ](blockout.obj) · [паспорт движения](motion-spec.json) · [галерея](gallery.html).

## Принятый вид

Поднятая прямоугольная лётная палуба над низким закрытым ангаром, две массивные диагональные боковые опоры, задний левый технический блок. Одна оранжевая сервисная каретка на горизонтальной направляющей у левого края палубы. Один небольшой сигнальный маячок на крыше заднего блока в отмеченной автором области. Песочная оболочка, графитовая конструкция, приглушённая оранжевая краска, квадрат 4×4.

## Входы для AI-моделирования

Использовать только hero-neutral.png и model-sheet.png; список в model-inputs.json. Исторический ../d.png не содержит маячка. Все лампы и маячок выключены; колпак тёмно-серый, каретка в покое, ворота закрыты. Оранжевая разметка/каретка — краска. Albedo без свечения, световых лучей, активных узоров и искр. Превью работы и его скриншоты не передавать в генератор текстур. Свет/маски задаются отдельно.

Ракурсы художественные, не размерные чертежи: фронт слегка приподнят, боковой/задний виды могут показывать лишние поверхности и видимость каретки. Не переносить ошибки в разные геометрии. Строгие направления и размещение масс сверять по guide.png/OBJ одной сетки, детальные оболочки и опоры — по hero-neutral.png. Задняя деталировка остаётся предложением.

## Части и рабочий цикл

Маячок: неподвижное основание и защитный колпак; внутри один отдельный вращающийся отражатель вокруг вертикальной оси. Янтарный световой сектор синхронен повороту отражателя. Не вращать весь колпак и не зашивать светлое пятно в текстуру. Для RTS допустимо реализовать световую волну материалом, сохраняя тот же видимый принцип вращения. Точная техника эффекта, яркость и скорость ещё настраиваются.

Главный признак работы — вращающийся маячок. Ожидание: свет выключен, вращение остановлено. Производство и исследование: маячок вращается и светит, пока фактический прогресс общей очереди идёт. Отмена: свет выключен, вращение прекращается. Завершение/отмена: рабочий эффект прекращается. Одновременно две независимые очереди не изображать.

Каретка D сохраняется как вторичный механический цикл: короткий ход вдоль левого края палубы, корпус и направляющие неподвижны. При отмене каретка возвращается к парковке. Возврат к парковке и будущий выпуск авиации не задерживают игровой результат. Каретка не перевозит самолёты по открытой палубе; новых посадочных, ремонтных или топливных правил не добавлено.

Координаты: Z вверх, X вправо, Y назад, фасад -Y. Восемь условных единиц = четыре клетки, не метры. На грубом каркасе проверены все вершины внутри 4×4 и диапазон каретки относительно концов направляющих. Детальные зазоры, колпак/отражатель, масштаб четырёх типов авиации и путь выпуска проверить после детализации. Высоты и длительности не утверждены как численные параметры игры.

Назначение и баланс: винтокрыл, перехватчик, транспортник, дрон РЭБ и исследования авиации; 175 М / 75 П, 45 с, 900 HP, 4×4, требует оперативный центр. Площадка и маячок не дают обзора, обнаружения или бонусов.

Основной вид с маячком и художественные ракурсы — встроенный imagegen; точные промпты рядом. Каркас и OBJ — tools/docs/build-human-aerodrome-modeling.cjs. Детальная игровая сетка, UV, материалы, риг и анимации в Unity ещё не созданы.
`);
fs.writeFileSync(path.join(dir,'gallery.html'),`<!doctype html><html lang="ru"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Аэродром D — моделирование</title><style>body{margin:24px;background:#191e21;color:#ece8df;font:18px/1.5 system-ui}main{max-width:1600px;margin:auto}img{display:block;width:100%;height:auto;border-radius:10px}a{color:#eab16a}p{color:#c3c9ca}button{font:inherit;padding:8px 14px;background:#39464e;color:#eee;border:1px solid #65717a;border-radius:6px;cursor:pointer;margin:4px}button[aria-pressed=true]{background:#945e27}.preview{max-width:700px;margin:auto}.rotor{transform-origin:170px 105px;animation:turn 2.8s linear infinite;animation-play-state:paused}.shuttle{animation:slide 4s ease-in-out infinite;animation-play-state:paused}.preview[data-state=production] .rotor,.preview[data-state=research] .rotor,.preview[data-state=production] .shuttle,.preview[data-state=research] .shuttle{animation-play-state:running}.preview[data-state=idle] .rotor,.preview[data-state=idle] .shuttle{animation:none}.preview[data-state=idle] .sweep,.preview[data-state=cancelled] .sweep{display:none}@keyframes turn{to{transform:rotate(360deg)}}@keyframes slide{50%{transform:translateX(120px)}}svg{display:block;width:100%}@media(prefers-reduced-motion:reduce){.rotor,.shuttle{animation:none!important}}</style><main><h1>Аэродром · D «Консоль»</h1><p>Утверждён с вращающимся сигнальным маячком на крыше заднего блока. Поднятая палуба и сервисная каретка сохранены.</p><p><a href="README.md">Паспорт моделирования</a> · <a href="model-inputs.json">Входы для AI</a> · <a href="motion-spec.json">Движение и свет</a> · <a href="blockout.obj">OBJ</a> · <a href="../gallery.html">История A–H</a></p><h2>Утверждённый нейтральный вид</h2><p>Маячок выключен. Тёмный колпак и отражатель моделировать отдельно от светового эффекта.</p><img src="hero-neutral.png" alt="Аэродром D с выключенным маячком"><h2>Пять нейтральных художественных ракурсов</h2><img src="model-sheet.png" alt="Пять ракурсов, лампы выключены, ворота закрыты"><h2>Строгие проекции одной геометрии</h2><p>Грубый каркас задаёт расположение масс и частей. Подробные оболочки смотреть на основном виде.</p><img src="guide.png" alt="Общий каркас 4×4 с одним маячком и одной кареткой"><h2>Отдельное превью рабочего эффекта</h2><p>Вид маячка сверху, увеличено. Янтарный сектор вращается вместе с внутренним отражателем; колпак и основание неподвижны. Это условная схема, не вход для текстурирования.</p><section class="preview" data-state="idle"><div><button data-state="idle" aria-pressed="true">Ожидание</button><button data-state="production" aria-pressed="false">Производство</button><button data-state="research" aria-pressed="false">Исследование</button><button data-state="cancelled" aria-pressed="false">Отмена</button></div><svg viewBox="0 0 500 300"><rect x="15" y="15" width="470" height="270" rx="12" fill="#303b43"/><circle cx="170" cy="105" r="65" fill="#42484c" stroke="#849198" stroke-width="10"/><g class="rotor"><path class="sweep" d="M170 105L260 46A108 108 0 0 1 276 119Z" fill="#eab253" opacity=".65"/><path d="M143 105H194" stroke="#a8b0b4" stroke-width="12"/><path class="sweep" d="M170 105H210" stroke="#f5be58" stroke-width="12"/></g><text x="285" y="95" fill="#c3c9ca" font-size="16">Маячок: вид сверху</text><text x="285" y="121" fill="#c3c9ca" font-size="14">Колпак неподвижен</text><path d="M65 220H420M65 258H420" stroke="#839098" stroke-width="7"/><rect class="shuttle" x="105" y="225" width="100" height="28" rx="4" fill="#b87938"/></svg></section><p>Работа соответствует прогрессу общей очереди. Отмена выключает свет и возвращает механизм в покой. Точные скорость, яркость и переходы ещё настраиваются; выпуск авиации — отдельное событие без добавленной задержки.</p></main><script>document.querySelectorAll('button[data-state]').forEach(b=>b.onclick=()=>{const s=b.closest('section');s.dataset.state=b.dataset.state==='cancelled'?'idle':b.dataset.state;s.querySelectorAll('button').forEach(x=>x.setAttribute('aria-pressed',String(x===b)));});</script></html>`);
console.log('Aerodrome D: all vertices within 4x4, one beacon and carriage, neutral inputs and separate motion preview saved.');
}
main().catch(e=>{console.error(e);process.exitCode=1;});
