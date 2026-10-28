/* Human factory R2-D: shared proportion mesh and one vertical carriage. */
const fs=require('node:fs'),path=require('node:path'),{createRequire}=require('node:module');
const req=createRequire(path.join(path.dirname(process.execPath),'../node_modules/package.json')),sharp=req('sharp');
const dir=path.resolve(__dirname,'../../docs/concepts/human-factory-directions/round-2/modeling');
const source=fs.readFileSync(path.join(__dirname,'build-reference-blockouts.cjs'),'utf8');
const helpers=source.slice(source.indexOf('const add='),source.indexOf('const specs='));
const renderer=source.slice(source.indexOf('function renderParts('),source.indexOf('async function main('));
const build=new Function(helpers+renderer+`
const sand='#cbb99a',frame='#394047',orange='#b87938',glass='#42484c';
box('square 3x3 foundation',[0,0,.10],[6,6,.20],frame);
function hall(name,x0,x1,y0,y1,z0,z1,color){
const p=[[x0,z0],[x1,z0],[x1,z1-.32],[x1-.28,z1],[x0+.28,z1],[x0,z1-.32]],v=[];
for(const y of [y0,y1])for(const [x,z]of p)v.push([x,y,z]);
const n=p.length,f=[Array.from({length:n},(_,i)=>n-1-i),Array.from({length:n},(_,i)=>n+i)];
for(let i=0;i<n;i++)f.push([i,(i+1)%n,(i+1)%n+n,i+n]);mesh(name,v,f,color);
}
hall('low enclosed vehicle hall',-1.40,2.78,-2.65,2.55,.20,2.45,sand);
hall('intermediate rear assembly hall',-.95,1.95,.35,2.55,2.15,3.35,sand);
hall('rear stepped technical block',-1.15,.55,1.0,2.50,3.20,4.15,sand);
const tower=[[-2.72,-1.65,.20],[-1.33,-1.65,.20],[-2.72,2.55,.20],[-1.33,2.55,.20],[-2.72,-1.65,5.35],[-1.33,-1.65,5.35],[-2.72,2.55,5.85],[-1.33,2.55,5.85]];
mesh('fixed tall assembly shaft',tower,[[0,2,3,1],[4,5,7,6],[0,1,5,4],[2,6,7,3],[0,4,6,2],[1,3,7,5]],sand);
for(const x of [-2.53,-1.52])box('fixed vertical guide '+x,[x,-1.74,2.95],[.14,.18,4.90],frame);
box('sealed track backing',[-2.025,-1.68,2.95],[.82,.07,4.90],frame);
box('MOVING carriage orange housing',[-2.025,-1.97,3.20],[.82,.45,.78],orange);
for(const x of [-2.40,-1.65])box('MOVING carriage dark clamp '+x,[x,-2.21,3.20],[.10,.06,.88],frame);
for(const z of [2.84,3.56])box('MOVING carriage horizontal clamp '+z,[-2.025,-2.21,z],[.92,.06,.10],frame);
box('closed gate graphite surround',[.67,-2.70,1.25],[3.65,.14,2.02],frame);
box('closed gate face',[.67,-2.79,1.25],[3.21,.04,1.67],frame);
box('diffuse orange gate paint',[.67,-2.817,1.19],[3.10,.012,.09],orange);
for(const x of [-1.17,2.53]){
beam('front diagonal buttress '+x,[x,-2.84,.24],[x,-2.66,1.99],.27,.25,frame);
box('orange front jamb mark '+x,[x,-2.75,2.20],[.23,.05,.18],orange);
}
for(const x of [-.95,.65,2.20])box('fixed hall roof rib '+x,[x,-.05,2.49],[.13,4.54,.10],frame);
for(const x of [-.35,1.65])box('unlit gate diagnostic lens '+x,[x,-2.729,2.22],[.52,.03,.09],glass);
for(const x of [-2.77,2.78])for(const y of [-2.72,2.72])box('foundation foot '+x+' '+y,[x,y,.30],[.40,.42,.20],frame);
const vertices=parts.flatMap(p=>p.vertices);
if(vertices.some(v=>Math.abs(v[0])>3+1e-8||Math.abs(v[1])>3+1e-8))throw Error('Outside 3x3 footprint');
const low=1.15,high=4.65,halfHeight=.44;
if(low-halfHeight<.50||high+halfHeight>5.40)throw Error('Carriage crosses rail end stops');
return {svg:sheet(parts,'human factory D - vertical assembly'),obj:obj(parts),spec:{variant:'R2-D Vertical assembly',footprintCells:[3,3],units:'6 arbitrary units = 3 cells; not meters',axes:'X right, Y rear, Z up; front is -Y',modelingState:'all lights off, all gates closed, carriage parked mid-stroke',movingGroup:'MOVING carriage housing and clamps; one rigid group',fixedGroups:['tower shell','track backing','two vertical guides','hall shells','roof ribs','buttresses','gate frame'],pivot:{position:[-2.025,-1.97,3.20],axis:[0,0,1],motion:'translation only'},proposedTravel:{centerMin:low,centerMax:high,neutralCenter:3.20,units:'blockout units; tune final mesh'},carriageCount:1,railCount:2,clearance:'guide end stops validated; carriage outside tower skin, left of vehicle hall and gate path',states:{idle:'stationary parked carriage; working lights off',production:'one carriage reciprocates vertically while shared queue advances',research:'same carriage work while shared research queue advances',cancelled:'end work cycle and return carriage to park; no saved resumable order',complete:'end work loop without delaying gameplay; return to park can be visual blend',cancel:'end loop without invented production delay'},timing:'duration, easing and return blend not approved; separate gallery schematic only',materials:'matte sand shell; graphite steel; muted orange diffuse paint; dark gray unlit lenses; emission separate from albedo',limitations:'coarse shared proportion mesh; rear details proposed; detailed asset, rig and Unity animation not created'}};
`);
async function main(){fs.mkdirSync(dir,{recursive:true});const d=build();if(/NaN|Infinity/.test(d.svg+d.obj))throw Error('Invalid geometry');fs.writeFileSync(path.join(dir,'guide.svg'),d.svg);fs.writeFileSync(path.join(dir,'blockout.obj'),d.obj);await sharp(Buffer.from(d.svg)).png().toFile(path.join(dir,'guide.png'));fs.writeFileSync(path.join(dir,'motion-spec.json'),JSON.stringify(d.spec,null,2)+'\n');console.log('Factory D: 3x3 footprint validated, one carriage group, two guides, bounded vertical travel.');}
main().catch(e=>{console.error(e);process.exitCode=1;});
