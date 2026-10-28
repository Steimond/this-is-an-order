/* Workshop F: eight-sector proportion guide and independent light masks. */
const fs=require('node:fs'),path=require('node:path'),{createRequire}=require('node:module');
const req=createRequire(path.join(path.dirname(process.execPath),'../node_modules/package.json')),sharp=req('sharp');
const dir=path.resolve(__dirname,'../../docs/concepts/human-workshop-directions/modeling');
const source=fs.readFileSync(path.join(__dirname,'build-reference-blockouts.cjs'),'utf8');
const helpers=source.slice(source.indexOf('const add='),source.indexOf('const specs='));
const renderer=source.slice(source.indexOf('function renderParts('),source.indexOf('async function main('));
const build=new Function(helpers+renderer+`
const sand='#cbb99a',frame='#394047',orange='#b87938',lit='#41474c',dim='#41474c';
const count=8,start=-Math.PI/2-Math.PI/8;
function point(i,r,z){const a=start+i*Math.PI/4;return[r*Math.cos(a),r*Math.sin(a),z];}
function shell(name,r0,z0,r1,z1,color){
const v=[];for(let i=0;i<count;i++)v.push(point(i,r0,z0));for(let i=0;i<count;i++)v.push(point(i,r1,z1));
const faces=[Array.from({length:count},(_,i)=>count-1-i),Array.from({length:count},(_,i)=>count+i)];
for(let i=0;i<count;i++)faces.push([i,(i+1)%count,(i+1)%count+count,i+count]);
mesh(name,v,faces,color);
}
box('square 3x3 foundation',[0,0,.10],[6,6,.20],frame);
shell('lower faceted shell',2.75,.20,2.65,1.10,sand);
shell('light band shell',2.65,1.10,2.25,1.85,sand);
shell('shallow eight face roof',2.25,1.85,.70,2.40,sand);
shell('closed central cap',.76,2.40,.72,2.49,frame);
function panel(i,t0,t1,h0,h1,offset,color,name){
const verts=[];for(const [t,h]of [[t0,h0],[t1,h0],[t1,h1],[t0,h1]]){
const r=2.65+(.0-.40)*h+offset,z=1.10+.75*h;
verts.push(add(mul(point(i,r,z),1-t),mul(point(i+1,r,z),t)));
}mesh(name,verts,[[0,1,2,3]],color);
}
for(let i=0;i<count;i++){
panel(i,.10,.90,.10,.90,.02,frame,'fixed light frame '+i);
panel(i,.17,.83,.22,.78,.04,[1,2].includes(i)?lit:dim,'EMISSIVE light sector '+i);
beam('fixed radial rib '+i,point(i,2.77,.24),point(i,2.27,1.87),.12,.13,frame);
beam('fixed roof rib '+i,point(i,2.27,1.87),point(i,.73,2.42),.095,.095,frame);
}
box('closed front personnel hatch',[0,-2.54,.62],[.63,.07,.84],frame);
for(const x of [-2.32,2.32])for(const y of [-1.52,1.52])box('orange service unit '+x+' '+y,[x,y,.38],[.55,.55,.36],orange);
const v=parts.flatMap(p=>p.vertices);if(v.some(p=>Math.abs(p[0])>3+1e-8||Math.abs(p[1])>3+1e-8))throw Error('Outside footprint');
return {svg:sheet(parts,'human workshop F'),obj:obj(parts),spec:{footprintCells:[3,3],units:'6 arbitrary horizontal units = 3 game cells; not meters',sectorCount:8,sectorOrder:'0 front, increments toward building right around circumference',emissiveMasks:Array.from({length:8},(_,i)=>'light_sector_'+i),fixed:'all geometry; no mechanical motion rig',work:'two adjacent panels at a time, sequence moves around band',idle:'diagnostic sequence inactive; background lamps may stay steady',cancelled:'end active sequence and return to idle',complete:'end sequence; optional brief uniform confirmation with no gameplay delay',timing:'not approved; gallery timing is schematic only',limits:'Eight-sector shared scaffold, not exact shell reconstruction. Facet/panel count in artistic sheets must be checked against single geometry.'}};
`);
async function main(){fs.mkdirSync(dir,{recursive:true});const d=build();if(/NaN|Infinity/.test(d.svg+d.obj))throw Error('Invalid geometry');fs.writeFileSync(path.join(dir,'guide.svg'),d.svg);fs.writeFileSync(path.join(dir,'blockout.obj'),d.obj);await sharp(Buffer.from(d.svg)).png().toFile(path.join(dir,'guide.png'));fs.writeFileSync(path.join(dir,'light-spec.json'),JSON.stringify(d.spec,null,2)+'\n');console.log('Workshop F: 8 independent light sectors, fixed geometry, all parts within 3x3 foundation.');}
main().catch(e=>{console.error(e);process.exitCode=1;});

