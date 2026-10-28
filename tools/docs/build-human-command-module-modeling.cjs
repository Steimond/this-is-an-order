const fs=require('node:fs'),path=require('node:path'),{createRequire}=require('node:module');
const req=createRequire(path.join(path.dirname(process.execPath),'../node_modules/package.json')),sharp=req('sharp');
const dir=path.resolve(__dirname,'../../docs/concepts/human-command-module-directions/round-2/modeling');
const source=fs.readFileSync(path.join(__dirname,'build-reference-blockouts.cjs'),'utf8');
const helpers=source.slice(source.indexOf('const add='),source.indexOf('const specs='));
const renderer=source.slice(source.indexOf('function renderParts('),source.indexOf('async function main('));
const build=new Function('degrees',helpers+renderer+`
const sand='#cbb99a',frame='#394047',orange='#b87938',electrode='#42484c',center=[0,0,3.60];
box('square 3x3 foundation',[0,0,.10],[6,6,.20],frame);
cylinder('sealed octagonal sand shell',[0,0,.20],[0,0,1.80],2.64,sand,8);
cylinder('closed octagonal graphite roof',[0,0,1.80],[0,0,1.92],2.51,frame,8);
for(let i=0;i<8;i++){const a=i*Math.PI/4+Math.PI/8,x=Math.cos(a)*2.65,y=Math.sin(a)*2.65;beam('fixed wall buttress '+i,[x*1.07,y*1.07,.24],[x*.91,y*.91,1.65],.23,.28,frame);}
box('closed front door surround',[0,-2.46,.73],[.84,.18,1.07],sand);
box('closed front maintenance door',[0,-2.57,.73],[.61,.04,.84],frame);
box('diffuse door marking',[0,-2.596,1.0],[.44,.012,.055],orange);
for(const sign of [-1,1]){
  box('fixed sand bearing tower '+sign,[sign*2.19,0,2.64],[.38,.76,1.44],sand);
  beam('fixed tower brace '+sign,[sign*2.48,-.44,1.91],[sign*2.19,-.18,3.45],.20,.23,frame);
  cylinder('fixed trunnion bearing '+sign,[sign*1.94,0,3.60],[sign*2.47,0,3.60],.24,frame,20);
}
const radians=degrees*Math.PI/180;
function toWorld(p){const [x,y,z]=p;return [x,y*Math.cos(radians)-z*Math.sin(radians),3.60+y*Math.sin(radians)+z*Math.cos(radians)];}
function annulus(name,inner,outer,thickness,paint){
for(let i=0;i<48;i++){
 const a=i*2*Math.PI/48,b=(i+1)*2*Math.PI/48,v=[];
 for(const z of [-thickness/2,thickness/2])for(const [r,t]of [[inner,a],[outer,a],[outer,b],[inner,b]])v.push(toWorld([r*Math.cos(t),r*Math.sin(t),z]));
 mesh(name+' segment '+i,v,[[0,3,2,1],[4,5,6,7],[0,1,5,4],[1,2,6,5],[2,3,7,6],[3,0,4,7]],paint&&((i>=3&&i<10)||(i>=27&&i<34))?orange:frame);
}}
annulus('TILT carrier',1.84,1.97,.18,false);
annulus('SPIN inner rotor',1.54,1.79,.16,true);
for(let i=0;i<4;i++){
 const a=i*Math.PI/2,points=[];
 for(const z of [-.06,.06])for(const [r,t]of [[1.48,a-.07],[1.55,a-.07],[1.55,a+.07],[1.48,a+.07]])points.push(toWorld([r*Math.cos(t),r*Math.sin(t),z]));
 mesh('SPIN dark electrode '+i,points,[[0,3,2,1],[4,5,6,7],[0,1,5,4],[1,2,6,5],[2,3,7,6],[3,0,4,7]],electrode);
}
const vertices=parts.flatMap(p=>p.vertices);
if(vertices.some(v=>Math.abs(v[0])>3+1e-8||Math.abs(v[1])>3+1e-8))throw Error('Outside 3x3 footprint');
for(let tilt=0;tilt<=45;tilt+=3)for(let phase=0;phase<360;phase+=5)for(let j=0;j<48;j++){
 const a=(phase+j*7.5)*Math.PI/180,t=tilt*Math.PI/180,x=1.79*Math.cos(a),y=1.79*Math.sin(a),z=-.08;
 const p=[x,y*Math.cos(t)-z*Math.sin(t),3.60+y*Math.sin(t)+z*Math.cos(t)];
 if(Math.abs(p[0])>3||Math.abs(p[1])>3||p[2]<=1.92||Math.abs(p[0])>=1.94)throw Error('Rotor envelope clearance failed');
}
return {svg:sheet(parts,'command module A - '+degrees+' deg - effects OFF'),obj:obj(parts)};
`);
async function main(){
fs.mkdirSync(dir,{recursive:true});for(const [angle,suffix]of [[45,''],[0,'-horizontal']]){const d=build(angle);if(/NaN|Infinity/.test(d.svg+d.obj))throw Error('Invalid geometry');fs.writeFileSync(path.join(dir,'guide'+suffix+'.svg'),d.svg);await sharp(Buffer.from(d.svg)).png().toFile(path.join(dir,'guide'+suffix+'.png'));fs.writeFileSync(path.join(dir,'blockout'+suffix+'.obj'),d.obj);}
const gen=JSON.parse(fs.readFileSync(path.join(dir,'sheet-generation.json'),'utf8'));fs.copyFileSync(gen.source,path.join(dir,'model-sheet.png'));fs.writeFileSync(path.join(dir,'model-sheet.prompt.md'),gen.prompt+'\n');
fs.writeFileSync(path.join(dir,'motion-spec.json'),JSON.stringify({variant:'A Orbital frame',footprintCells:[3,3],units:'6 arbitrary horizontal units = 3 cells; not meters',axes:'X right, Y rear, Z up, front -Y',hierarchy:{fixed:['base','shell','roof','two towers','trunnion housings'],tiltPivot:{position:[0,0,3.60],axis:[1,0,0],children:['outer carrier','spin pivot']},spinPivot:{localPosition:[0,0,0],localAxis:[0,0,1],children:['inner rotor','four electrodes','arc attachment points']}},poses:{restTiltDegrees:45,workingTiltDegrees:0,spin:'only at 0 degree tilt, axis then equals world Z'},sequence:['hold inclined rest, spin stopped, arcs OFF','tilt carrier and rotor together to horizontal, spin stopped, arcs OFF','hold carrier horizontal, spin inner rotor, intermittent arcs inside aperture','arcs OFF, decelerate rotor','when stopped return carrier to rest'],arcs:{state:'separate visual effect only',appearance:'short intermittent white-amber electric arcs between inner-rim contact points, confined to aperture',excluded:'no portal, filled energy disc, weapon beam or gameplay damage',albedo:'no emissive baked lightning; separate effect/material'},cancelled:'arcs OFF, decelerate rotor then return tilt to rest; no resumable pause',idle:'inclined rest; no spin or arcs',activation:'not yet tied to a gameplay queue; building unlocks technology and has no production/research queue',timing:'cycle lengths, rates, easing and trigger unapproved',validation:'sampled tilt 0..45 by 3 degrees and spin 0..355 by 5 degrees; rotor remains within 3x3, above sealed roof and inside tower inner faces; final detailed bearings and clamps still require collision check',proportions:{pivotHeight:3.60,carrierRadii:[1.84,1.97],rotorRadii:[1.54,1.79],ringRadialGap:.05,closedRoofHeight:1.92},limitations:'coarse guide, not final mesh, rig or effect'},null,2));
fs.writeFileSync(path.join(dir,'model-inputs.json'),JSON.stringify({approved:'Command module A, tilt carrier + independent inner rotor',inputs:['hero-neutral.png','model-sheet.png'],geometryReferences:['guide.png','guide-horizontal.png','blockout.obj','blockout-horizontal.obj'],exclude:['../a.png (historical single-ring bearing concept)','active effect preview and screenshots'],neutral:'all lights and electricity OFF; orange is diffuse paint; closed doors; dark electrodes',authority:'hero-neutral for surface character; guides of one geometry for projections and exact mechanical hierarchy; artistic sheet has perspective errors',separateGroups:['fixed building/towers','tilting outer carrier','spinning inner rotor/electrodes','electrical VFX']},null,2));
fs.writeFileSync(path.join(dir,'README.md'),`# Командный модуль A «Орбитальная рама» — моделирование

Выбран автором 2026-10-28. Правка автора: кольцо ложится параллельно земле, вращается в горизонтальной плоскости, внутри проскакивают электрические разряды. Самостоятельное здание, рабочее пятно 3×3, без стыковки со штабом.

[Основной нейтральный вид](hero-neutral.png) · [шесть художественных панелей](model-sheet.png) · [общая геометрия в покое](guide.png) · [горизонтальная поза](guide-horizontal.png) · [OBJ покоя](blockout.obj) · [OBJ горизонтальной позы](blockout-horizontal.obj) · [паспорт движения](motion-spec.json) · [галерея и отдельное превью](gallery.html).

## Принятая конструкция

Низкий восьмигранный песочный корпус с закрытой крышей/дверью и графитовыми опорами. Две боковые стойки держат наклоняемую внешнюю обойму. Внутри неё отдельный ротор-кольцо с оранжевыми окрашенными дугами и четырьмя тёмными контактами. Обойма поворачивается на горизонтальной оси между стойками; ротор вращается вокруг собственной нормали в кольцевом подшипнике. Это один двухчастный кольцевой узел, не две отдельные машины.

Исходное ../a.png уточнено: нельзя приварить вращающийся ротор прямо к двум неподвижным боковым осям. Для AI-моделирования использовать только hero-neutral.png и model-sheet.png, список в model-inputs.json. Все лампы и разряды выключены, контакты тёмные, оранжевые дуги — краска. Эффекты/маски отдельно от albedo; активное превью и его скриншоты не являются входом для текстурирования.

Художественный лист включает пять ракурсов и горизонтальную позу, но FRONT/RIGHT/REAR имеют перспективные ошибки, TOP может иначе показать наклон. Не строить разные модели по каждой панели. Направления, положение шарниров и позы брать из guide.png и guide-horizontal.png одной геометрии, материалы и фаски — из hero-neutral.png. Скрытые детали остаются предложением.

## Риг и последовательность

Неподвижны корпус, кровля, стойки и корпуса боковых подшипников. Pivot наклона на линии боковых опор: сначала обойма и вложенный ротор вместе переводятся из наклонного покоя в горизонталь. Затем обойма остаётся горизонтальной, только внутренний ротор вращается вокруг мировой вертикали. В риге: fixed → tiltPivot → spinPivot → rotor/electrodes. Для возврата сначала погасить разряды, затормозить ротор, затем поднять обойму. Не вращать стойки и не выполнять два несовместимых поворота одним объектом.

Внутри пустого проёма кольца — короткие нерегулярные электрические дуги между контактами внутренней кромки. Рабочее предложение цвета — бело-янтарный, не утверждённый финальный оттенок. Это отдельный VFX: не заполнять отверстие сплошным диском, не рисовать портал, не направлять разряд наружу. Оружие, урон, новая энергия и способности не добавляются. При отмене разряды выключаются, ротор останавливается и рама возвращается в покой. Темп, easing, плотность дуг и возврат ещё настраиваются.

Командный модуль открывает технологии; своей очереди пока нет. Автором принят вид активной последовательности, точный игровой триггер запуска и длительность активности ещё не выбраны. Не показывать фиктивное производство/исследование и не менять очередь штаба. Превью — демонстрация последовательности по кнопке.

## Геометрия и пределы

Z вверх, X вправо, Y назад; фасад -Y. Шесть условных единиц основания = три клетки, не метры. Для каркаса предложены наклон покоя 45°, рабочий 0°, центр [0,0,3.60], радиусы обоймы 1.84–1.97 и ротора 1.54–1.79. Это пропорции, не утверждённые игровые размеры. На сетке углов проверены наклон и полный поворот ротора: пятно 3×3, зазор над кровлей, проход внутри стоек. Детальные зажимы, подшипники и электрические контакты проверять после детализации на всём ходе. Оба OBJ — позы одной сетки, не разные ассеты.

Роль и исходные числа сохраняются: доступ к III уровню, требует завод или аэродром; 150 М / 150 П, 55 с, 350 HP; окончательный габарит 3×3 ещё требует игровой проверки. Основной вид и художественные ракурсы созданы встроенным imagegen, точные промпты рядом. Каркас — tools/docs/build-human-command-module-modeling.cjs. Детальная игровая сетка, UV, риг, материалы и VFX в Unity ещё не созданы.
`);
writeGallery();console.log('Command module A: rest/horizontal guides, 2 OBJ poses, two-axis hierarchy and sampled clearance validation complete.');
}
function writeGallery(){fs.writeFileSync(path.join(dir,'gallery.html'),`<!doctype html><html lang="ru"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Командный модуль A — моделирование</title><style>body{margin:24px;background:#191e21;color:#ece8df;font:18px/1.5 system-ui}main{max-width:1600px;margin:auto}img{display:block;width:100%;height:auto;border-radius:10px}a{color:#eab16a}p{color:#c3c9ca}button{font:inherit;padding:8px 14px;background:#39464e;color:#eee;border:1px solid #65717a;border-radius:6px;cursor:pointer;margin:4px}button[aria-pressed=true]{background:#945e27}.preview{max-width:750px;margin:auto}.ring-tilt{transform-origin:260px 150px;animation:tilt 10s ease-in-out infinite;animation-play-state:paused}.rotor{transform-origin:260px 150px;animation:spin 10s linear infinite;animation-play-state:paused}.arcs{animation:arcs 10s steps(1) infinite;animation-play-state:paused;opacity:0}.preview[data-state=work] .anim{animation-play-state:running}.preview[data-state=idle] .anim{animation:none}.preview[data-state=idle] .ring-tilt{transform:scaleY(.35)}.preview[data-state=cancelled] .arcs{visibility:hidden}@keyframes tilt{0%,12%,95%,100%{transform:scaleY(.35)}28%,82%{transform:scaleY(1)}}@keyframes spin{0%,28%{transform:rotate(0)}75%,100%{transform:rotate(720deg)}}@keyframes arcs{0%,33%,41%,49%,57%,65%,73%,80%,100%{opacity:0}35%,44%,52%,60%,68%,76%{opacity:1}}svg{display:block;width:100%}@media(prefers-reduced-motion:reduce){.anim{animation:none!important}}</style><main><h1>Командный модуль · A «Орбитальная рама»</h1><p>Выбран с последовательностью: кольцо ложится горизонтально → внутренний ротор вращается → внутри проскакивают электрические дуги. Самостоятельное здание, рабочее пятно 3×3.</p><p><a href="README.md">Паспорт</a> · <a href="motion-spec.json">Движение и эффект</a> · <a href="model-inputs.json">Входы для AI</a> · <a href="blockout.obj">OBJ покоя</a> · <a href="blockout-horizontal.obj">OBJ горизонтальной позы</a> · <a href="../gallery.html">История A–H</a></p><h2>Основной нейтральный вид</h2><p>Внешняя наклонная обойма и внутренний ротор разделены кольцевым подшипником. Все эффекты выключены.</p><img src="hero-neutral.png" alt="Принятый модуль A, эффектов нет"><h2>Художественные ракурсы и горизонтальная поза</h2><p>Лист содержит перспективные неточности. Строгие проекции и положение частей — ниже.</p><img src="model-sheet.png" alt="Ракурсы и горизонтальная поза без света и электричества"><h2>Одна геометрия: наклонный покой</h2><img src="guide.png" alt="Каркас в покое"><h2>Та же геометрия: горизонтальная поза</h2><img src="guide-horizontal.png" alt="Каркас в горизонтальной позе, без эффектов"><h2>Отдельное превью последовательности</h2><p>Условный вид сверху: изменение масштаба эллипса иллюстрирует наклон, это не точная 3D-анимация. Внешняя обойма ложится горизонтально; внутри неё вращается ротор с оранжевыми метками. Дуги — отдельный эффект внутри проёма. Не передавать превью на текстурирование.</p><section class="preview" data-state="idle"><div><button data-state="idle" aria-pressed="true">Покой</button><button data-state="work" aria-pressed="false">Показать цикл</button><button data-state="cancelled" aria-pressed="false">Отмена</button></div><svg viewBox="0 0 520 310"><rect x="20" y="20" width="480" height="270" rx="16" fill="#303b43"/><path d="M100 150H130M390 150H420" stroke="#cbb99a" stroke-width="25"/><g class="ring-tilt anim"><circle cx="260" cy="150" r="130" fill="none" stroke="#74818a" stroke-width="12"/><g class="rotor anim"><circle cx="260" cy="150" r="111" fill="none" stroke="#424b51" stroke-width="20"/><path d="M260 39A111 111 0 0 1 356 94M260 261A111 111 0 0 1 164 206" fill="none" stroke="#b87938" stroke-width="20"/><g class="arcs anim" stroke="#ffdc9b" stroke-width="3" fill="none"><path d="M156 150L191 127L218 156L244 139L277 167L306 142L364 150"/><path d="M260 47L245 89L270 121L250 165L276 193L260 253"/></g></g></g></svg></section><p>Отмена гасит дуги и запускает возврат к покою. Для возврата сначала выключаются дуги и останавливается ротор, затем обойма наклоняется обратно. Точный игровой триггер активности и тайминги ещё не выбраны: своей очереди у модуля нет.</p></main><script>document.querySelectorAll('button[data-state]').forEach(b=>b.onclick=()=>{const s=b.closest('section');s.dataset.state=b.dataset.state==='cancelled'?'idle':b.dataset.state;s.querySelectorAll('button').forEach(x=>x.setAttribute('aria-pressed',String(x===b)));});</script></html>`);}
main().catch(e=>{console.error(e);process.exitCode=1;});
