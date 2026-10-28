/* Retire the invented manual pause state in human production/research references. */
const fs=require('node:fs'),path=require('node:path');
const root=path.resolve(__dirname,'../..');
const replacements=[
 ['На паузе разряды выключены и позы удерживаются.','При отмене разряды выключаются, ротор останавливается и рама возвращается в покой.'],
 ['Пауза гасит дуги и удерживает позы.','Отмена гасит дуги и запускает возврат к покою.'],
 ['Пауза гасит разряды и удерживает позы.','Отмена гасит разряды и запускает возврат к покою.'],
 ['На паузе каретка удерживает позу.','При отмене каретка возвращается к парковке.'],
 ['Пауза выключает свет и останавливает движение.','Отмена выключает свет и возвращает механизм в покой.'],
 ['Пауза удерживает текущую позу.','Отмена запускает возврат к покою.'],
 ['паузу — остановку','отмену — возврат к покою'],
 ['пауза — остановка','отмена — возврат к покою'],
 ['пауза — удержание текущей позы','отмена — возврат к покою'],
 ['пауза — последовательность прекращает движение','отмена — рабочая последовательность прекращается'],
 ['при паузе свет выключен, фаза вращения удерживается','при отмене свет выключается, вращение прекращается'],
 ['Покой и пауза отличаются остановкой рабочего движения.','Покой и отмена не показывают рабочего движения; при отмене механизм возвращается к парковке.'],
 ['Простой и пауза не показывают продолжающуюся последовательность.','В простое и после отмены рабочая последовательность выключена.'],
 ['пауза не продолжает рабочую последовательность','отмена прекращает рабочую последовательность'],
 ['при паузе нет ложного цикла прогресса','после отмены нет ложного цикла прогресса'],
 ['при паузе цикл останавливается','при отмене цикл прекращается и механизм возвращается в покой'],
 ['ожидание/работа/пауза','ожидание/работа/отмена'],
 ['ожидание, производство, исследование, паузу','ожидание, производство, исследование, отмену'],
 ['длительность остановок и возврат','длительность остановок механизма и возврат'],
 ['пауза, отмена, завершение и обесточивание описываются по применимым игровым правилам','отмена, завершение и вынужденная остановка из-за обесточивания описываются по применимым игровым правилам; ручной паузы производства и исследования нет'],
 ['Заказ/начало/пауза/отмена/готовность/потеря','Заказ/начало/отмена/готовность/потеря'],
 ["paused:'hold current carriage pose; no ongoing work light sequence'","cancelled:'end work cycle and return carriage to park; no saved resumable order'"],
 ["paused:'hold mechanism pose and stop active light sequence'","cancelled:'end work cycle, lights off, return mechanism to rest'"],
 ["paused:'off; rotation held'","cancelled:'off; rotation stops, no resumable pause'"],
 ["paused:'hold current pose'","cancelled:'end cycle and return to park'"],
 ["pause:'arcs OFF, hold tilt/spin pose; blending tune later'","cancelled:'arcs OFF, decelerate rotor then return tilt to rest; no resumable pause'"],
 ["paused:'stop active sequence; distinct from ongoing work'","cancelled:'end active sequence and return to idle'"],
 ["paused:'stop work motion'","cancelled:'end work motion and return to park'"],
 ["paused:'stop research sequence'","cancelled:'end research sequence'"],
 ['hold current carriage pose; no ongoing work light sequence','end work cycle and return carriage to park'],
 ['hold mechanism pose and stop active light sequence','end work cycle, lights off, return mechanism to rest'],
 ['arcs OFF, hold tilt/spin pose; blending tune later','arcs OFF, decelerate rotor then return tilt to rest'],
 ['off; rotation held','off; rotation stops'],
 ['hold current pose','end cycle and return to park'],
 ['stop active sequence; distinct from ongoing work','end active sequence and return to idle'],
 ['stop work motion','end work motion and return to park'],
 ['stop research sequence','end research sequence'],
];
function rewrite(text){
 let s=text;
 for(const [a,b]of replacements)s=s.split(a).join(b);
 // CSS animation-play-state:paused is an implementation default, not a game command.
 s=s.replace(/data-state="paused"/g,'data-state="cancelled"').replace(/data-state=paused/g,'data-state=cancelled');
 s=s.replace(/\['paused','Пауза'\]/g,"['cancelled','Отмена']");
 s=s.replace(/\bpaused:/g,'cancelled:').replace(/\bpause:/g,'cancelled:');
 s=s.replace(/"paused"\s*:/g,'"cancelled":').replace(/"pause"\s*:/g,'"cancelled":').replace(/"paused"(?=\s*[,\]])/g,'"cancelled"');
 s=s.replace(/>Пауза<\/button>/g,'>Отмена</button>').replace(/(data-state="cancelled"[^>]*>)Остановить<\/button>/g,'$1Отмена</button>');
 // The terminal cancellation button resets demo animations; it never holds their phase.
 s=s.replace(/s\.dataset\.state=b\.dataset\.state(?!===)/g,"s.dataset.state=b.dataset.state==='cancelled'?'idle':b.dataset.state");
 s=s.replace(/пауза — удержание механизма и прекращение активной последовательности/gi,'отмена — прекращение цикла и возврат механизма к покою');
 s=s.replace(/Пауза — остановка в текущем положении\./g,'Отмена — прекращение цикла и возврат к покою.');
 s=s.replace(/Пауза: свет выключен, фаза вращения удерживается\./g,'Отмена: свет выключен, вращение прекращается.');
 return s;
}
const files=[];
function walk(dir){for(const e of fs.readdirSync(dir,{withFileTypes:true})){const p=path.join(dir,e.name);if(e.isDirectory())walk(p);else if(/\.(md|json|html)$/.test(e.name)&&!e.name.endsWith('.prompt.md')&&!/generation\.json$/.test(e.name))files.push(p);}}
for(const e of fs.readdirSync(path.join(root,'docs/concepts'),{withFileTypes:true}))if(e.isDirectory()&&e.name.startsWith('human-'))walk(path.join(root,'docs/concepts',e.name));
for(const e of fs.readdirSync(path.join(root,'tools/docs')))if(/^build-human.*\.cjs$/.test(e))files.push(path.join(root,'tools/docs',e));
files.push(path.join(root,'docs/DESIGN.md'));
let count=0;for(const p of files){const before=fs.readFileSync(p,'utf8');let after=rewrite(before);if(p.endsWith('DESIGN.md')){
after=after.replace('Версия 0.22 · 2026-10-28 · выбран командный модуль людей A «Орбитальная рама»: наклон в горизонталь, вращение кольца и электрические дуги.','Версия 0.23 · 2026-10-28 · ручной паузы производства и исследований нет: только отмена.');
const anchor='#### Утрата требований: единое правило для обеих рас';
if(!after.includes('**Управление производством и исследованиями — решение автора 2026-10-28.**'))after=after.replace(anchor,'**Управление производством и исследованиями — решение автора 2026-10-28.** Поставленный в работу заказ нельзя вручную приостановить и затем продолжить: доступна только отмена по обычным правилам возврата ресурсов. После отмены рабочая анимация/свет/VFX прекращаются, механизм возвращается в покой; визуальный переход не задерживает игровой результат. Отмена не сохраняет прогресс для возобновления. Вынужденная остановка Резонанса при потере питания остаётся отдельным правилом сети, не командой игрока.\n\n'+anchor);
}
if(after!==before){fs.writeFileSync(p,after);count++;}}
console.log(`Updated ${count} human reference, generator and canonical files; exact image prompts preserved.`);
