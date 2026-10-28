/* Supply depot F: square-footprint proportion scaffold, not detailed game mesh. */
const fs=require('node:fs'),path=require('node:path'),{createRequire}=require('node:module');
const req=createRequire(path.join(path.dirname(process.execPath),'../node_modules/package.json')),sharp=req('sharp');
const dir=path.resolve(__dirname,'../../docs/concepts/human-supply-directions/protected/modeling');
const source=fs.readFileSync(path.join(__dirname,'build-reference-blockouts.cjs'),'utf8');
const helpers=source.slice(source.indexOf('const add='),source.indexOf('const specs='));
const renderer=source.slice(source.indexOf('function renderParts('),source.indexOf('async function main('));
const build=new Function(helpers+renderer+`
const sand='#c9b99b',frame='#394047',orange='#b97938',amber='#efb149';
box('square foundation',[0,0,.10],[4,4,.20],frame);
box('sealed insulated store',[0,0,1.40],[2.95,2.95,2.40],sand);
for(const x of [-1.75,1.75])for(const y of [-1.75,1.75]){
box('foot '+x+' '+y,[x,y,.26],[.50,.50,.32],frame);
box('post '+x+' '+y,[x,y,1.62],[.31,.31,2.72],frame);
if(y<0)box('amber marker '+x,[x,y-.163,2.72],[.07,.025,.15],amber);
}
for(const side of [-1,1]){
box('upper x beam '+side,[0,side*1.75,2.85],[3.50,.23,.24],frame);
box('lower x rail '+side,[0,side*1.75,.30],[3.50,.23,.22],frame);
box('upper y beam '+side,[side*1.75,0,2.85],[.23,3.50,.24],frame);
box('lower y rail '+side,[side*1.75,0,.30],[.23,3.50,.22],frame);
box('closed sliding leaf '+side,[side*.65,-1.49,1.40],[1.28,.06,1.95],sand);
}
box('front threshold',[0,-1.61,.33],[2.7,.16,.10],orange);
box('right ventilation',[1.49,.55,1.37],[.06,.68,1.05],frame);
box('roof climate unit',[.20,1.0,2.76],[.85,.42,.32],orange);
box('climate grille',[.20,.781,2.76],[.62,.025,.21],frame);
beam('roof conduit',[.65,1.0,2.70],[1.12,1.0,2.70],.07,.07,frame);
const vertices=parts.flatMap(p=>p.vertices);
const bounds=[0,1,2].map(i=>[Math.min(...vertices.map(v=>v[i])),Math.max(...vertices.map(v=>v[i]))]);
if(Math.abs((bounds[0][1]-bounds[0][0])-(bounds[1][1]-bounds[1][0]))>1e-9)throw Error('Non-square');
if(vertices.some(v=>Math.abs(v[0])>2+1e-9||Math.abs(v[1])>2+1e-9))throw Error('Footprint overhang');
return {svg:sheet(parts,'human supply F'),obj:obj(parts),spec:{bounds,footprintCells:[2,2],units:'4 arbitrary horizontal units = 2 game cells; not meters',parts:parts.map(p=>p.name),limits:'Square proportional scaffold only; final shell surfaces follow selected f.png.'}};
`);
async function main(){fs.mkdirSync(dir,{recursive:true});const d=build();if(/NaN|Infinity/.test(d.svg+d.obj))throw Error('Invalid geometry');fs.writeFileSync(path.join(dir,'guide.svg'),d.svg);fs.writeFileSync(path.join(dir,'blockout.obj'),d.obj);await sharp(Buffer.from(d.svg)).png().toFile(path.join(dir,'guide.png'));fs.writeFileSync(path.join(dir,'blockout-spec.json'),JSON.stringify(d.spec,null,2)+'\n');console.log('Supply F: square 2x2 scaffold, no footprint overhang; five shared-mesh views and OBJ written.');}
main().catch(e=>{console.error(e);process.exitCode=1;});

