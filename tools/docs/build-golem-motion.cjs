/* One-degree-of-freedom sweep check for the proportion blockout, not a finished rig. */
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const out=path.resolve(__dirname,'../../docs/concepts/resonance-final/geometry-guides');
const pivot={y:-.175,z:1.775},rear={y:2.65,z:2.4},radius=Math.hypot(rear.y-pivot.y,rear.z-pivot.z),alpha=Math.atan2(rear.z-pivot.z,rear.y-pivot.y);
const frames=[];let minClearance=Infinity;
for(let i=0;i<=1000;i++){
 const t=i/1000,a=alpha+Math.PI*t,y=pivot.y+radius*Math.cos(a),z=pivot.z+radius*Math.sin(a);
 assert(Math.abs(Math.hypot(y-pivot.y,z-pivot.z)-radius)<1e-10);
 // Vertical center panel: full height 2.3 and depth .2. Hull bounding box ends at +/-2.4, top 1.36.
 if(y-.1<=2.4&&y+.1>=-2.4){const clearance=z-1.15-1.36;assert(clearance>0,'Shield intersects hull');minClearance=Math.min(minClearance,clearance);}
 // A small front instrument is conservatively included in the envelope.
 if(y-.1<-2.3-1e-6&&y+.1>-2.9+1e-6)assert(z-1.15>1.155,'Shield intersects short front instrument');
 frames.push({t,y,z,angleDegrees:(a-alpha)*180/Math.PI,wingOpening:t*90});
}
assert(Math.abs(frames.at(-1).y+3)<1e-10);assert(Math.abs(frames.at(-1).z-1.15)<1e-10);
const color={bg:'#172129',light:'#d4dde2',muted:'#a9bcc7',cyan:'#57d7e4'};
const sx=y=>320+y*70,sz=z=>400-z*70;
function pose(f,x){let s=`<g transform="translate(${x},0)"><rect x="${sx(-2.4)}" y="${sz(1.36)}" width="336" height="${70*1.36}" rx="14" fill="#62737e"/><rect x="${sx(-2.9)}" y="${sz(1.155)}" width="42" height="24" fill="#344752"/>`;
 s+=`<path d="M${sx(rear.y)},${sz(rear.z)} A${radius*70},${radius*70} 0 1 0 ${sx(-3)},${sz(1.15)}" fill="none" stroke="#355967" stroke-width="2" stroke-dasharray="8 6"/>`;
 s+=`<line x1="${sx(pivot.y)}" y1="${sz(pivot.z)}" x2="${sx(f.y)}" y2="${sz(f.z)}" stroke="${color.cyan}" stroke-width="12"/><rect x="${sx(f.y)-7}" y="${sz(f.z+1.15)}" width="14" height="161" rx="3" fill="${color.light}"/><circle cx="${sx(pivot.y)}" cy="${sz(pivot.z)}" r="14" fill="${color.cyan}"/><text x="${sx(pivot.y)}" y="450" text-anchor="middle" fill="${color.muted}" font-size="18">Ось остаётся на месте</text></g>`;return s;}
const svg=`<svg xmlns="http://www.w3.org/2000/svg" width="1800" height="600"><rect width="1800" height="600" fill="${color.bg}"/><g font-family="Arial"><text x="35" y="48" font-size="30" fill="${color.light}">ГОЛЕМ / перенос щита за одно непрерывное движение</text><text x="35" y="88" font-size="19" fill="${color.muted}">Одна ведущая ось в середине корпуса • постоянная длина парных рычагов • зависимая ориентация плиты</text>${[0,.5,1].map((t,i)=>pose(frames[Math.round(t*1000)],i*590)).join('')}<text x="300" y="510" text-anchor="middle" fill="${color.light}" font-size="24">Походное состояние</text><text x="890" y="510" text-anchor="middle" fill="${color.light}" font-size="24">Перенос через верх</text><text x="1480" y="510" text-anchor="middle" fill="${color.light}" font-size="24">Передняя оборона</text><text x="35" y="565" fill="${color.muted}" font-size="18">Проверка 1001 положения: плита проходит над корпусом. Сложенные боковые секции и полный риг требуют отдельной объёмной проверки.</text></g></svg>`;
fs.writeFileSync(path.join(out,'golem-shield-motion.svg'),svg);
fs.writeFileSync(path.join(out,'golem-shield-motion.json'),JSON.stringify({pivot,radius,sweepDegrees:180,minCenterPanelHullClearance:minClearance,positions:frames,limits:'Only center shield plate against hull and short front instrument; excludes folded wing panels, detailed linkages and final render mesh.'},null,2));
console.log(JSON.stringify({positionsChecked:frames.length,constantArmLength:radius,centerPanelClearance:minClearance,finalCenter:frames.at(-1),limits:'center-panel proxy only'}));
