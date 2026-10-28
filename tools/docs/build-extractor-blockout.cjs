/* Selected extractor A: proportional scaffold, not final geometry or game footprint. */
const fs=require('fs'),path=require('path'),{createRequire}=require('module');
const req=createRequire(path.join(path.dirname(process.execPath),'../node_modules/package.json')),sharp=req('sharp');
const dir=path.resolve(__dirname,'../../docs/concepts/resonance-buildings-modeling/plasma-extractor');
const source=fs.readFileSync(path.join(__dirname,'build-reference-blockouts.cjs'),'utf8');
const helpers=source.slice(source.indexOf('const add='),source.indexOf('const specs='));
const renderer=source.slice(source.indexOf('function renderParts('),source.indexOf('async function main('));
const build=new Function(helpers+renderer+`
function ring(name,ri,ro,z0,z1,start=0,end=2*Math.PI,color=light){
 const v=[],f=[],n=48;
 for(let i=0;i<=n;i++){const a=start+(end-start)*i/n;for(const [r,z]of [[ri,z0],[ro,z0],[ro,z1],[ri,z1]])v.push([r*Math.cos(a),r*Math.sin(a),z]);}
 for(let i=0;i<n;i++)for(let j=0;j<4;j++)f.push([i*4+j,i*4+(j+1)%4,(i+1)*4+(j+1)%4,(i+1)*4+j]);
 f.push([3,2,1,0],[n*4,n*4+1,n*4+2,n*4+3]);mesh(name,v,f,color);
}
ring('dark annular foundation',1.05,2.8,0,.45,0,Math.PI*2,dark);
ring('inner intake rim',1.05,1.3,.45,.68,0,Math.PI*2,dark);
for(let i=0;i<4;i++){
 const a=i*Math.PI/2,at=(r,z)=>[r*Math.cos(a),r*Math.sin(a),z];
 ring('large quarter shell '+i,1.5,2.72,.45,.95,a+.17,a+Math.PI/2-.17);
 beam('radial converter '+i,at(1.2,.74),at(2.68,1.04),.62,.52);
 beam('inner process conduit '+i,at(1.12,.45),at(1.68,.88),.12,.12,'#b66bd9');
 beam('outer status strip '+i,at(2.61,1.31),at(2.18,1.22),.08,.035,cyan);
}
return {svg:sheet(parts,'plasma-extractor'),obj:obj(parts),parts:parts.map(p=>p.name),units:'arbitrary proportions; not meters or game cells',limits:'Same-mesh projection scaffold only; not an exact reconstruction of the approved artwork.'};
`);
async function main(){fs.mkdirSync(dir,{recursive:true});const d=build();if(/NaN|Infinity/.test(d.svg+d.obj))throw Error('Invalid geometry');fs.writeFileSync(path.join(dir,'guide.svg'),d.svg);fs.writeFileSync(path.join(dir,'blockout.obj'),d.obj);await sharp(Buffer.from(d.svg)).png().toFile(path.join(dir,'guide.png'));fs.writeFileSync(path.join(dir,'blockout-spec.json'),JSON.stringify({parts:d.parts,units:d.units,limits:d.limits},null,2)+'\n');console.log('Built extractor A guide and OBJ');}
main().catch(e=>{console.error(e);process.exitCode=1});
