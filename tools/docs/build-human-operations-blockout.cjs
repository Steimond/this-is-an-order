/* Operations G: fixed wedge geometry, three processing ribs, three unlit strips. */
const fs=require('node:fs'),path=require('node:path'),{createRequire}=require('node:module');
const req=createRequire(path.join(path.dirname(process.execPath),'../node_modules/package.json')),sharp=req('sharp');
const dir=path.resolve(__dirname,'../../docs/concepts/human-operations-directions/modeling');
const source=fs.readFileSync(path.join(__dirname,'build-reference-blockouts.cjs'),'utf8');
const helpers=source.slice(source.indexOf('const add='),source.indexOf('const specs='));
const renderer=source.slice(source.indexOf('function renderParts('),source.indexOf('async function main('));
const build=new Function(helpers+renderer+`
const sand='#cbb99a',frame='#394047',orange='#b87938',lensColor='#42484c';
box('square 3x3 foundation',[0,0,.10],[6,6,.20],frame);
const yz=[[-2.65,.20],[-2.50,1.10],[.65,2.25],[2.50,2.25],[2.65,.20]];
const v=[];for(const x of [-2.60,2.60])for(const [y,z]of yz)v.push([x,y,z]);
const n=yz.length,f=[Array.from({length:n},(_,i)=>n-1-i),Array.from({length:n},(_,i)=>n+i)];
for(let i=0;i<n;i++)if(i!==1)f.push([i,(i+1)%n,(i+1)%n+n,i+n]);
mesh('sealed wedge shell',v,f,sand);
function roof(y){return 1.10+(y+2.50)*1.15/3.15;}
const roofEdges=[-2.60,-2.10,-.70,.70,2.10,2.60];
for(let i=0;i<roofEdges.length-1;i++)mesh('fixed roof band '+i,[[roofEdges[i],-2.5,1.1],[roofEdges[i+1],-2.5,1.1],[roofEdges[i+1],.65,2.25],[roofEdges[i],.65,2.25]],[[0,1,2,3]],sand);
function roofPanel(name,x,width,y0,y1,zoff,color){mesh(name,[[x-width/2,y0,roof(y0)+zoff],[x+width/2,y0,roof(y0)+zoff],[x+width/2,y1,roof(y1)+zoff],[x-width/2,y1,roof(y1)+zoff]],[[0,1,2,3]],color);}
for(const [i,x]of [-1.4,0,1.4].entries()){
box('fixed processor rib '+i,[x,1.65,2.71],[.62,1.25,.92],frame);
box('sand rib cap '+i,[x,1.65,3.20],[.67,1.30,.08],sand);
box('orange rib front mark '+i,[x,1.014,2.71],[.12,.025,.68],orange);
roofPanel('fixed strip frame '+i,x,.84,-2.10,.25,.025,frame);
roofPanel('NEUTRAL emissive-mask strip '+i,x,.64,-2.00,.15,.045,lensColor);
}
for(const x of [-2.68,2.68])for(const y of [-2.68,2.68])box('corner buttress '+x+' '+y,[x,y,.32],[.55,.55,.24],frame);
box('closed front-right door surround',[1.72,-2.62,.63],[.86,.12,.94],orange);
box('closed personnel door',[1.72,-2.69,.63],[.60,.04,.74],frame);
const verts=parts.flatMap(p=>p.vertices);
if(verts.some(p=>Math.abs(p[0])>3+1e-8||Math.abs(p[1])>3+1e-8))throw Error('Outside footprint');
return {svg:sheet(parts,'human operations G'),obj:obj(parts),spec:{footprintCells:[3,3],units:'6 arbitrary horizontal units = 3 cells, not meters',processorRibs:3,lightStrips:3,geometryMotion:'none; all ribs and strips fixed',modelingState:'all emission OFF, neutral dark lenses, no baked working pattern',emissiveMasks:['research_strip_0','research_strip_1','research_strip_2'],work:'brightness waves shifted in phase across three strips',idle:'research sequence off',cancelled:'end research sequence',complete:'end sequence, no delay to gameplay',timing:'not approved; preview schematic only',limits:'Shared proportion blockout; shell facets, vents and hidden surfaces follow chosen g.png, final mesh not created.'}};
`);
async function main(){fs.mkdirSync(dir,{recursive:true});const d=build();if(/NaN|Infinity/.test(d.svg+d.obj))throw Error('Invalid geometry');fs.writeFileSync(path.join(dir,'guide.svg'),d.svg);fs.writeFileSync(path.join(dir,'blockout.obj'),d.obj);await sharp(Buffer.from(d.svg)).png().toFile(path.join(dir,'guide.png'));fs.writeFileSync(path.join(dir,'light-spec.json'),JSON.stringify(d.spec,null,2)+'\n');console.log('Operations G: 3 fixed ribs and 3 independent dark strips, all parts within 3x3 foundation.');}
main().catch(e=>{console.error(e);process.exitCode=1;});

