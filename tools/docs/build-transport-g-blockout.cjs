/* Original G proportion scaffold, one shared carrier for both states. */
const fs=require('fs'),path=require('path'),{createRequire}=require('module');
const req=createRequire(path.join(path.dirname(process.execPath),'../node_modules/package.json')),sharp=req('sharp');
const dir=path.resolve(__dirname,'../../docs/concepts/resonance-transport-reboot/modeling');fs.mkdirSync(dir,{recursive:true});
const source=fs.readFileSync(path.join(__dirname,'build-reference-blockouts.cjs'),'utf8');
const helpers=source.slice(source.indexOf('const add='),source.indexOf('const specs='));
const render=source.slice(source.indexOf('function renderParts('),source.indexOf('async function main(')).replace('v[2]===1?[0,-1,.0001]:v','Math.abs(v[2])===1?[0,-v[2],.0001]:v').replace('up=v[2]===1?[0,1,0]','up=Math.abs(v[2])===1?[0,1,0]');
const build=new Function(helpers+render+`
function loft(name,levels,color=light){const vertices=[];for(const [z,w,d]of levels){const x=w/2,y=d/2,c=Math.min(w,d)*.17;for(const [xx,yy]of [[-x+c,-y],[x-c,-y],[x,-y+c],[x,y-c],[x-c,y],[-x+c,y],[-x,y-c],[-x,-y+c]])vertices.push([xx,yy,z]);}const faces=[Array.from({length:8},(_,i)=>7-i),Array.from({length:8},(_,i)=>vertices.length-8+i)];for(let k=0;k<levels.length-1;k++)for(let i=0;i<8;i++)faces.push([k*8+i,k*8+(i+1)%8,(k+1)*8+(i+1)%8,(k+1)*8+i]);mesh(name,vertices,faces,color);}
loft('CARRIER core',[[3.04,1.48,1.15],[4.65,1.48,1.18],[6.15,1.02,.94],[6.32,.86,.78]]);
box('CARRIER front graphite spine',[0,-.607,4.58],[.23,.05,2.8],dark);
box('CARRIER front status',[0,-.639,4.62],[.035,.025,.64],cyan);
box('CARRIER rear service spine',[0,.606,4.5],[.3,.05,2.35],dark);
loft('CARRIER docking collar',[[3,1.48,1.16],[3.08,1.48,1.16]],dark);
box('CARRIER recessed docking plate',[0,0,3.015],[.95,.73,.025],dark);
for(const side of [-1,1]){
 const xz=[[side*.68,4.69],[side*2.14,4.18],[side*2.14,3.66],[side*.84,3.66]],v=[];
 for(const y of [-.56,.56])for(const [x,z]of xz)v.push([x,y,z]);
 mesh('CARRIER shoulder engine '+side,v,[[3,2,1,0],[4,5,6,7],[0,1,5,4],[1,2,6,5],[2,3,7,6],[3,0,4,7]]);
 box('CARRIER engine underside '+side,[side*1.43,0,3.645],[1.25,.94,.09],dark);
 box('CARRIER engine emitter '+side,[side*1.43,0,3.591],[1.02,.7,.025],cyan);
 box('CARRIER hidden latch recess '+side,[side*.44,0,2.997],[.15,.4,.04],dark);
}
loft('CAPSULE shell',[[.16,.92,.86],[.48,1.36,1.12],[2.96,1.48,1.16]]);
box('CAPSULE front spine',[0,-.592,1.64],[.23,.035,2.34],dark);
box('CAPSULE front status',[0,-.619,1.11],[.035,.022,.66],cyan);
const assembled=parts,carrier=parts.filter(p=>p.name.startsWith('CARRIER')),capsule=parts.filter(p=>p.name.startsWith('CAPSULE'));
const center=[0,0,3.24],scale=67;
function guide(ps,state){const shifted=ps.map(p=>({...p,vertices:p.vertices.map(v=>sub(v,center))}));const panels=[['FRONT',[0,-1,0],300,390],['SIDE',[1,0,0],900,390],['REAR',[0,1,0],1500,390],['TOP',[0,0,1],300,1020],['BOTTOM',[0,0,-1],900,1020],['THREE QUARTER',[1,-1,.6],1500,1020]];return '<svg xmlns="http://www.w3.org/2000/svg" width="1800" height="1400"><rect width="1800" height="1400" fill="#e5e9eb"/><text x="35" y="45" font-family="Arial" font-size="29" fill="#25333e">TRANSPORT G / '+state+'</text><text x="35" y="80" font-family="Arial" font-size="18" fill="#536570">One shared carrier mesh | fixed scale and height datum | proportion scaffold, not final surfaces</text>'+panels.map(([name,v,x,y])=>renderParts(shifted,v,x,y,scale)+'<text x="'+x+'" y="'+(y<700?690:1330)+'" text-anchor="middle" font-family="Arial" font-size="23" fill="#25333e">'+name+'</text>').join('')+'</svg>';}
return {assembled:guide(assembled,'CAPSULE ATTACHED'),carrier:guide(carrier,'CAPSULE REMOVED'),assembledObj:obj(assembled),carrierObj:obj(carrier),capsuleObj:obj(capsule),carrierMesh:carrier};
`);
const r=build();
for(const [name,svg]of [['attached',r.assembled],['empty',r.carrier]]){fs.writeFileSync(path.join(dir,name+'-guide.svg'),svg);}
fs.writeFileSync(path.join(dir,'attached.obj'),r.assembledObj);fs.writeFileSync(path.join(dir,'empty.obj'),r.carrierObj);fs.writeFileSync(path.join(dir,'capsule.obj'),r.capsuleObj);
fs.writeFileSync(path.join(dir,'proportions.json'),JSON.stringify({units:'arbitrary, not gameplay size',axes:{front:'-Y',up:'+Z',symmetry:'X=0'},assembledHeight:6.16,totalWidth:4.28,carrierHeight:3.36,capsuleHeight:2.8,dockingPlaneZ:3,engineCount:2,hiddenLatchCount:2,commonScale:67,commonCenter:[0,0,3.24],carrierIdentity:'identical vertices, faces and parts in attached.obj and empty.obj',capsule:'closed and opaque; bottom-release; no windows',limits:'Geometrically consistent scaffold; rear details and docking interface are modeling proposals, not an exact reconstruction of the selected hero.'},null,2));
Promise.all([sharp(Buffer.from(r.assembled)).png().toFile(path.join(dir,'attached-guide.png')),sharp(Buffer.from(r.carrier)).png().toFile(path.join(dir,'empty-guide.png'))]).then(()=>console.log('Built G attached/empty guides at identical scale, shared carrier and separate capsule OBJ.'));
