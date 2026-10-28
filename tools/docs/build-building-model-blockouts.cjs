/* Seven selected building massing guides; arbitrary units, not game footprints. */
const fs=require('fs'),path=require('path'),{createRequire}=require('module');
const req=createRequire(path.join(path.dirname(process.execPath),'../node_modules/package.json')),sharp=req('sharp');
const dir=path.resolve(__dirname,'../../docs/concepts/resonance-buildings-modeling');fs.mkdirSync(dir,{recursive:true});
const source=fs.readFileSync(path.join(__dirname,'build-reference-blockouts.cjs'),'utf8');
const helpers=source.slice(source.indexOf('const add='),source.indexOf('const specs='));
const renderer=source.slice(source.indexOf('function renderParts('),source.indexOf('async function main('));
const build=new Function('id',helpers+renderer+`
function base(w,d){box('foundation',[0,0,.16],[w,d,.32],dark);for(const x of [-1,1])for(const y of [-1,1])box('corner anchor '+x+' '+y,[x*(w/2-.28),y*(d/2-.28),.38],[.56,.56,.5]);}
if(id==='computer'){
 base(4,3.4);for(let i=-1;i<=1;i++){box('dry compute cassette '+i,[i*.8,0,1.85],[.57,2.15,2.9],dark);box('cassette solid front '+i,[i*.8,-1.095,1.85],[.48,.08,2.6]);box('cassette status '+i,[i*.8,-1.14,1.7],[.04,.02,.45],cyan);}
 for(const x of [-1,1]){beam('end cheek '+x,[x*1.64,-.7,.5],[x*1.64,.45,3.45],.52,.58);box('end cheek foot '+x,[x*1.64,-.7,.65],[.63,1.1,.65]);}
 box('common top bridge',[0,.57,3.48],[3.72,.5,.4]);
}
if(id==='relay'){
 base(2.1,2.1);box('mast core',[0,0,1.2],[.48,.5,1.7],dark);cylinder('contained central chamber',[0,-.04,.7],[0,-.04,2.63],.25,dark);for(const [x,h]of [[-.44,4.7],[.44,3.1]]){box('unequal antenna blade '+x,[x,0,.55+h/2],[.28,.48,h]);box('blade front status '+x,[x,-.251,.55+h*.58],[.035,.02,h*.48],cyan);}
}
if(id==='firmware'){
 base(4.6,4.2);cylinder('solid programming drum',[0,-1.45,1.64],[0,1.45,1.64],1.1,dark,32);
 for(const y of [-1.46,1.46])cylinder('end ring '+y,[0,y-.08,1.64],[0,y+.08,1.64],1.14,light,24);
 for(const y of [-.75,0,.75])cylinder('solid indexed band '+y,[0,y-.04,1.64],[0,y+.04,1.64],1.11,light,28);
 for(const x of [-1,1])box('bridge support '+x,[x*1.68,.6,1.6],[.45,.65,2.55]);
 box('single programming head bridge',[0,.6,3.01],[3.8,.7,.46]);box('read write head',[0,.2,2.77],[.62,.72,.48],dark);box('head status',[0,-.17,2.83],[.35,.025,.05],cyan);
}
if(id==='matrix'){
 base(6.5,3.5);for(const x of [-2,0,2])cylinder('sample pedestal '+x,[x,0,.35],[x,0,.61],.63,dark,16);
 box('cube sample',[-2,0,1.35],[1.08,1.08,1.4]);ellipsoid('sphere sample',[0,0,1.33],[.7,.7,.7]);mesh('pyramid sample',[[1.3,-.7,.64],[2.7,-.7,.64],[2.7,.7,.64],[1.3,.7,.64],[2,0,2.08]],[[3,2,1,0],[0,1,4],[1,2,4],[2,3,4],[3,0,4]],light);
 for(const x of [-1,1]){beam('canopy side frame '+x,[x*3,.7,.4],[x*3,.8,2.86],.43,.48);}
 box('long rear canopy beam',[0,.82,2.86],[6.35,.42,.4]);box('lower front frame',[0,-1.1,.62],[5.9,.18,.22],dark);
}
if(id==='nest'){
 cylinder('hex foundation',[0,0,0],[0,0,.35],2.9,dark,6);cylinder('central research hub',[0,0,.35],[0,0,1.52],.72,dark,24);cylinder('hub cap',[0,0,1.5],[0,0,1.68],.86,light,24);
 for(let i=0;i<3;i++){const a=Math.PI/2+i*2*Math.PI/3,at=(r,z)=>[Math.cos(a)*r,Math.sin(a)*r,z];beam('claw lower '+i,at(2.3,.32),at(2.1,2.5),.65,.58);beam('claw middle '+i,at(2.1,2.5),at(1.03,4.5),.58,.48);beam('claw tip '+i,at(1.03,4.5),at(.35,5.05),.39,.38);box('claw anchor '+i,at(2.3,.37),[.8,.8,.6]);beam('hub radial connector '+i,at(.5,.8),at(2.1,.8),.2,.25,dark);}
}
if(id==='deep-core'){
 for(const x of [-1,1])for(const y of [-1,1]){box('corner tower '+x+' '+y,[x*2.25,y*2.25,.65],[1.15,1.15,1.3]);cylinder('corner cap '+x+' '+y,[x*2.25,y*2.25,1.3],[x*2.25,y*2.25,1.4],.36,dark,16);cylinder('descending column '+x+' '+y,[x*1.5,y*1.5,-1.38],[x*1.5,y*1.5,.84],.24,dark,16);}
 for(const side of [-1,1]){box('rim rail X '+side,[side*2.36,0,.65],[.65,3.6,.65]);box('rim rail Y '+side,[0,side*2.36,.65],[3.6,.65,.65]);}
 box('recessed floor',[0,0,-1.45],[3.9,3.9,.18],dark);
}
if(id==='reservoir'){
 base(4.3,5);const a=[0,-1.6,1.03],b=[0,1.45,2.6];cylinder('single inclined vessel',a,b,.95,dark,32);const axis=norm(sub(b,a));cylinder('low front collar',sub(a,mul(axis,.1)),add(a,mul(axis,.13)),1.02,light,24);cylinder('high rear collar',sub(b,mul(axis,.1)),add(b,mul(axis,.15)),1.02,light,24);
 for(const x of [-1,1])beam('inclined support '+x,[x*1.4,1.3,.3],[x*.8,1.3,2.5],.28,.3);cylinder('small side buffer',[1.55,.5,.4],[1.55,.5,1.4],.27,dark,16);
}
return {svg:sheet(parts,id),obj:obj(parts),parts:parts.map(p=>p.name),bounds:[0,1,2].map(i=>[Math.min(...parts.flatMap(p=>p.vertices.map(v=>v[i]))),Math.max(...parts.flatMap(p=>p.vertices.map(v=>v[i])))])};
`);
async function main(){const specs={};for(const id of ['computer','relay','firmware','matrix','nest','deep-core','reservoir']){const d=build(id);fs.mkdirSync(path.join(dir,id),{recursive:true});fs.writeFileSync(path.join(dir,id,'guide.svg'),d.svg);fs.writeFileSync(path.join(dir,id,'blockout.obj'),d.obj);await sharp(Buffer.from(d.svg)).png().toFile(path.join(dir,id,'guide.png'));if(/NaN|Infinity/.test(d.svg+d.obj))throw Error('Non-finite '+id);specs[id]={bounds:d.bounds,parts:d.parts,units:'arbitrary proportions, not game footprint',limits:'Massing scaffold, not exact artistic reconstruction'};}fs.writeFileSync(path.join(dir,'blockout-specs.json'),JSON.stringify(specs,null,2));console.log('Built seven single-mesh building projection guides and OBJ.');}
main().catch(e=>{console.error(e);process.exitCode=1});
