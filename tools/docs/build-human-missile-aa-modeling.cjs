const fs=require('node:fs'),path=require('node:path'),{createRequire}=require('node:module');
const req=createRequire(path.join(path.dirname(process.execPath),'../node_modules/package.json')),sharp=req('sharp');
const dir=path.resolve(__dirname,'../../docs/concepts/human-missile-aa-directions/modeling');
const source=fs.readFileSync(path.join(__dirname,'build-reference-blockouts.cjs'),'utf8');
const helpers=source.slice(source.indexOf('const add='),source.indexOf('const specs='));
const renderer=source.slice(source.indexOf('function renderParts('),source.indexOf('async function main('));
const build=new Function(helpers+renderer+`
const sand='#cbb99a',frame='#394047',orange='#b87938',pivot=[0,0,1.65],angle=55*Math.PI/180;
box('FIXED square 2x2 foundation',[0,0,.10],[4,4,.20],frame);
box('FIXED low sand equipment base',[0,0,.43],[3.55,3.55,.46],sand);
for(const x of [-1.72,1.72])for(const y of [-1.72,1.72])box('FIXED restrained corner clamp '+x+' '+y,[x,y,.37],[.36,.36,.34],frame);
const fixed=parts.length;
cylinder('YAW azimuth bearing',[0,0,.66],[0,0,.85],1.10,frame,32);
box('YAW lower U cradle',[0,0,.96],[2.35,.75,.22],sand);
for(const sign of [-1,1]){
box('YAW sand side cheek '+sign,[sign*.98,0,1.30],[.27,.68,.68],sand);
cylinder('YAW fixed elevation bearing '+sign,[sign*.76,0,1.65],[sign*1.18,0,1.65],.22,frame,20);
}
function world(p){return [p[0],p[1]*Math.sin(angle)-p[2]*Math.cos(angle),1.65+p[1]*Math.cos(angle)+p[2]*Math.sin(angle)];}
cylinder('PITCH selector bearing housing',world([0,0,-.40]),world([0,0,.15]),.48,frame,28);
cylinder('INDEX rotating selector hub',world([0,0,-.28]),world([0,0,.25]),.37,frame,24);
function localBox(name,c,size,color){const start=parts.length;box(name,c,size,color);parts[start].vertices=parts[start].vertices.map(world);}
for(let i=0;i<3;i++){
const a=(90+i*120)*Math.PI/180,x=.54*Math.cos(a),y=.54*Math.sin(a);
localBox('INDEX sand closed container '+i,[x,y,1.00],[.43,.43,2.50],sand);
for(const t of [-.16,.80,2.17])localBox('INDEX graphite restraint '+i+' '+t,[x,y,t],[.46,.46,.075],frame);
localBox('INDEX CLOSED separate launch cap '+i,[x,y,2.28],[.44,.44,.05],frame);
localBox('INDEX diffuse orange marker '+i,[x+.219,y,1.36],[.012,.23,.24],orange);
}
const baseVerts=parts.slice(0,fixed).flatMap(p=>p.vertices),upper=parts.slice(fixed).flatMap(p=>p.vertices);
if(baseVerts.some(p=>Math.abs(p[0])>2||Math.abs(p[1])>2))throw Error('Fixed base outside 2x2');
const neutralRadius=Math.max(...upper.map(p=>Math.hypot(p[0],p[1])));
// Sample the container envelope through elevation and selector phases, without asserting it fits the base.
let sweep=0,minZ=Infinity;
for(let elev=30;elev<=75;elev+=5)for(let phase=0;phase<360;phase+=10)for(let i=0;i<3;i++)for(const dx of [-.215,.215])for(const dy of [-.215,.215])for(const t of [-.25,2.31]){
const a=(90+i*120)*Math.PI/180,theta=phase*Math.PI/180,px=.54*Math.cos(a)+dx,py=.54*Math.sin(a)+dy;
const x=px*Math.cos(theta)-py*Math.sin(theta),radial=px*Math.sin(theta)+py*Math.cos(theta),e=elev*Math.PI/180;
const y=radial*Math.sin(e)-t*Math.cos(e),z=1.65+radial*Math.cos(e)+t*Math.sin(e);
sweep=Math.max(sweep,Math.hypot(x,y));minZ=Math.min(minZ,z);
}
if(minZ<=.66)throw Error('Container envelope intersects fixed base');
return {svg:sheet(parts,'human missile AA F - closed containers'),obj:obj(parts),neutralRadius,sweep,minZ};
`);
async function main(){fs.mkdirSync(dir,{recursive:true});const d=build();if(/NaN|Infinity/.test(d.svg+d.obj))throw Error('Invalid geometry');fs.writeFileSync(path.join(dir,'guide.svg'),d.svg);await sharp(Buffer.from(d.svg)).png().toFile(path.join(dir,'guide.png'));fs.writeFileSync(path.join(dir,'blockout.obj'),d.obj);
const gen=JSON.parse(fs.readFileSync(path.join(dir,'generation.json'),'utf8'));fs.copyFileSync(gen.source,path.join(dir,'model-sheet.png'));fs.writeFileSync(path.join(dir,'model-sheet.prompt.md'),gen.prompt+'\n');
fs.writeFileSync(path.join(dir,'motion-spec.json'),JSON.stringify({variant:'F Container drum',approvedDate:'2026-10-29',constructionFootprintCells:[2,2],units:'4 arbitrary horizontal units = 2 cells; not meters',axes:'X right, Y rear, Z up, front -Y',hierarchy:{fixed:['foundation','lower equipment base'],yaw:{axis:[0,0,1],children:['U cradle','pitch pivot']},pitch:{pivot:[0,0,1.65],axis:[-1,0,0],children:['selector bearing housing','index pivot']},index:{localAxis:'longitudinal container axis; world [0,-cos(elevation),sin(elevation)]',children:['selector hub','3 containers','3 closed launch caps','orange markers']}},neutralElevationDegrees:55,selectorStepDegrees:120,containerCount:3,sequence:'aim at actual aerial target; optional visual selector indexing; separate launch cap event and rocket/VFX; do not impose unapproved firing delay',idle:'no endless rotation or production cycle',targetLayer:'air only',neutralModelingState:'all caps closed, lights OFF, no launch flame/smoke/glow, no exposed missile',balance:{metal:100,plasma:25,buildSeconds:25,health:500,range:8,dps:18,requirement:'workshop',finiteAmmo:false,manualReload:false,multiTargetSalvo:'not introduced',missileFlightParameters:'not chosen'},validation:{fixedBaseFits2x2:true,neutralUpperRadius:d.neutralRadius,sampledSweepRadius:d.sweep,sampledContainerMinHeight:d.minZ,sampledElevation:'30..75 degrees by 5',sampledIndex:'0..350 degrees by 10',limits:'upper container envelope above base checked; U-cradle/bearings/caps exact clearances and adjacent building placement still require final mesh validation; do not claim all moving parts fit 2x2'},limitations:'shared proportion guide only; final mesh UV rig materials and launch VFX not created'},null,2));
fs.writeFileSync(path.join(dir,'model-inputs.json'),JSON.stringify({approved:'Missile AA F, 2026-10-29',inputs:['../f.png','model-sheet.png'],geometryReferences:['guide.png','blockout.obj'],neutral:'all lights OFF, caps CLOSED, machinery at rest, no firing/launch effects; orange is diffuse paint',authority:'f.png for exact silhouette/proportions, shared geometry for axes and occlusion; artistic sheet has TOP pose/direction errors',separateGroups:['fixed base','yaw U-cradle','pitch selector housing','index rotor with 3 containers','3 cap panels','launch VFX'],exclude:['active selector preview screenshots']},null,2));
fs.writeFileSync(path.join(dir,'README.md'),`# Ракетная ПВО F «Барабан контейнеров» — моделирование

Выбрана автором 2026-10-29. [Основной вид F](../f.png) · [пять нейтральных художественных ракурсов](model-sheet.png) · [строгие проекции одной геометрии](guide.png) · [OBJ](blockout.obj) · [паспорт осей](motion-spec.json) · [галерея и отдельная схема барабана](gallery.html).

## Принятый вид

Низкое квадратное основание, круглый азимутальный погон, U-образная люлька с боковыми шарнирами и наклонённый барабан. Три длинных тонких закрытых прямоугольных ракетных контейнера расположены треугольником вокруг общей продольной оси. На барабане оранжевая индексная метка; песочные оболочки, графитовая механика, сдержанная толщина опор. Это ПВО только по воздушным целям, без пушечного вооружения.

Сохраняются 100 М / 25 П, 25 с, 500 HP, предварительное основание 2×2, мастерская, 18 урона/с и дальность 8. Три контейнера не задают конечный боезапас, ручную перезарядку, залп по нескольким целям или новую способность. Скорость ракеты, урон/интервал отдельного пуска, наведение и потеря цели уточняются отдельно, в рамках общих правил летящих атак.

## Входы и материалы

Для AI-моделирования — ../f.png и model-sheet.png, список в model-inputs.json. Контейнеры и люки закрыты, все механизмы в покое, свет выключен. Нет огня, дыма, вспышек, свечения и ракеты в полёте. Оранжевый — краска. VFX старта/полёта и эмиссия отдельно от albedo; активные превью не передавать в генератор базового цвета.

Художественный лист не размерный чертёж: TOP показывает иную позу/направление кассет и может скрыть третий контейнер. Строгие направления, расположение трёх кассет, осей и невидимость поверхностей сверять по guide.png/OBJ одной геометрии; поверхности и пропорции — по f.png. Задние мелкие панели пока предложены.

## Три оси

Неподвижны основание и нижний корпус. Вокруг вертикали поворачивается U-люлька. На боковых горизонтальных шарнирах наклоняется узел барабана. Внутри него отдельный ротор вместе с тремя контейнерами поворачивается вокруг общей продольной оси контейнеров. Корпус подшипника не вращается вместе с индексным ротором; боковые опоры не крутятся вокруг продольной оси. Иерархия fixed → yaw → pitch → index → containers/caps.

Предложение для внешней анимации: короткий пошаговый поворот на 120° выбирает следующий контейнер; это не непрерывное пушечное вращение. Движение наведения следует реальной цели, крышка и стартовый эффект — событию пуска. Точный момент индексирования и способ открытия/закрытия крышек ещё не утверждены; визуальная операция не добавляет неоговорённую задержку выстрела. В ожидании нет бесконечного производственного цикла.

## Габариты

X вправо, Y назад, Z вверх, фасад -Y. Четыре условных единицы = две клетки, не метры. Нейтральный наклон каркаса 55° — пропорция, не финальный игровой угол. Проверено неподвижное основание внутри 2×2 и контейнерный габарит над основанием на сетке углов наклона/индексирования. Радиусы нейтрального и подвижного габаритов записаны в motion-spec.json. Не считать все движущиеся части гарантированно вписанными в 2×2. Детальную люльку, оси, кольца, крышки, полный поворот и соседние здания проверить после детализации.

Основной вид и художественный лист — встроенный imagegen, точный промпт листа рядом. Каркас — tools/docs/build-human-missile-aa-modeling.cjs. Детальная игровая сетка, UV, риг, материалы и запуск в Unity ещё не созданы. Выбором F завершён общий внешний дизайн всех 11 зданий людей; строительство людей и повреждения/разрушение обеих рас остаются отдельным этапом предложений, не утверждённой реализацией.
`);
fs.writeFileSync(path.join(dir,'gallery.html'),`<!doctype html><html lang="ru"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>ПВО F — моделирование</title><style>body{margin:24px;background:#191e21;color:#ece8df;font:18px/1.5 system-ui}main{max-width:1600px;margin:auto}img{display:block;width:100%;height:auto;border-radius:10px}a{color:#eab16a}p{color:#c3c9ca}button{font:inherit;padding:8px 14px;margin:4px;background:#39464e;color:#eee;border:1px solid #65717a;border-radius:6px;cursor:pointer}.preview{max-width:580px;margin:auto}.index{transform-origin:200px 130px;animation:index 6s ease-in-out infinite;animation-play-state:paused}.preview[data-state=work] .index{animation-play-state:running}.preview[data-state=idle] .index{animation:none}@keyframes index{0%,12%{transform:rotate(0)}25%,45%{transform:rotate(120deg)}58%,78%{transform:rotate(240deg)}91%,100%{transform:rotate(360deg)}}svg{display:block;width:100%}@media(prefers-reduced-motion:reduce){.index{animation:none!important}}</style><main><h1>Ракетная ПВО · F «Барабан контейнеров»</h1><p>Утверждена 29 октября 2026. Три длинных закрытых контейнера на индексируемом барабане, U-люлька и низкое основание. Строгие пропорции, только воздушные цели.</p><p><a href="README.md">Паспорт моделирования</a> · <a href="motion-spec.json">Оси и части</a> · <a href="model-inputs.json">Входы для AI</a> · <a href="blockout.obj">OBJ</a> · <a href="../gallery.html">История A–H</a></p><h2>Утверждённый основной вид</h2><img src="../f.png" alt="ПВО F: свет выключен, крышки закрыты"><h2>Пять художественных ракурсов</h2><p>TOP имеет ошибку направления/позы; не переносить её в отдельную геометрию. Строгие проекции — ниже.</p><img src="model-sheet.png" alt="Пять нейтральных ракурсов F"><h2>Проекции одной геометрии</h2><img src="guide.png" alt="Каркас F с тремя контейнерами"><h2>Отдельная схема индексирования</h2><p>Вид вдоль продольной оси контейнеров. Три закрытых квадратных крышки поворачиваются вместе с ротором, наружный подшипник неподвижен. Темп условный; это не готовый клип или ограничение боезапаса.</p><section class="preview" data-state="idle"><button data-state="idle">Покой</button><button data-state="work">Пример индексирования</button><svg viewBox="0 0 400 260"><circle cx="200" cy="130" r="100" fill="#303b43" stroke="#74818a" stroke-width="12"/><g class="index"><circle cx="200" cy="130" r="43" fill="#394047" stroke="#b87938" stroke-width="8"/><rect x="175" y="33" width="50" height="50" fill="#cbb99a" stroke="#394047" stroke-width="7"/><rect x="111" y="145" width="50" height="50" fill="#cbb99a" stroke="#394047" stroke-width="7"/><rect x="239" y="145" width="50" height="50" fill="#cbb99a" stroke="#394047" stroke-width="7"/></g></svg></section><p>Наведение и старт — по реальным игровым событиям. В модельные референсы не включены ракета, дым, огонь или свечение. Точные крышки, темп пуска и полёт ещё требуют отдельного решения.</p><p>Внешний вид всех 11 зданий людей теперь выбран. <a href="../../building-lifecycle/gallery.html">Следующий этап: предложения стройки, повреждений и разрушения</a>.</p></main><script>document.querySelectorAll('button[data-state]').forEach(b=>b.onclick=()=>b.closest('section').dataset.state=b.dataset.state);</script></html>`);
console.log('Missile AA F: 3 containers, 3 mechanical axes, fixed base 2x2; sampled envelope above base; neutral references saved.');}
main().catch(e=>{console.error(e);process.exitCode=1;});
