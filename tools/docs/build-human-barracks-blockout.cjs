/* Barracks F2: proportion scaffold and collision-free proposed carriage travel. */
const fs=require('node:fs'),path=require('node:path'),{createRequire}=require('node:module');
const req=createRequire(path.join(path.dirname(process.execPath),'../node_modules/package.json')),sharp=req('sharp');
const dir=path.resolve(__dirname,'../../docs/concepts/human-barracks-directions/work-cycles/modeling');
const source=fs.readFileSync(path.join(__dirname,'build-reference-blockouts.cjs'),'utf8');
const helpers=source.slice(source.indexOf('const add='),source.indexOf('const specs='));
const renderer=source.slice(source.indexOf('function renderParts('),source.indexOf('async function main('));
const build=new Function(helpers+renderer+`
const sand='#cbb99a',frame='#394047',orange='#ba7b3b',amber='#ecb045';
box('square foundation',[0,0,.10],[6,6,.20],frame);
for(const x of [-2.08,2.08]){
box('lodging wing '+x,[x,0,.89],[1.58,5.75,1.38],sand);
for(const y of [-2.65,0,2.65])box('wing rib '+x+' '+y,[x,y,.94],[1.65,.12,1.50],frame);
box('slit windows '+x,[x+(x>0?.80:-.80),0,1.15],[.035,3.2,.10],frame);
}
box('rear connector',[0,2.08,1.02],[2.60,1.58,1.64],sand);
box('rear central module',[0,2.08,1.46],[1.05,1.58,2.52],sand);
box('rear closed door',[0,1.27,.75],[.56,.05,1.02],frame);
for(const x of [-.90,.90])box('front wall '+x,[x,-2.78,.57],[.70,.18,.74],sand);
box('closed gate',[0,-2.79,.60],[1.12,.20,.80],frame);
box('gate amber',[0,-2.91,1.05],[.60,.025,.06],amber);
box('right roof mechanism base',[2.08,0,1.73],[1.40,4.10,.22],frame);
for(const x of [1.46,2.70])box('fixed guard rail '+x,[x,0,1.96],[.09,4.10,.36],frame);
for(const x of [1.78,2.38])cylinder('fixed slide rail '+x,[x,-1.97,1.95],[x,1.97,1.95],.045,frame,12);
for(const y of [-2.0,2.0])box('fixed end stop '+y,[2.08,y,1.96],[1.40,.12,.36],frame);
for(const y of [-.8,.8]){
box('MOVING carriage '+y,[2.08,y,2.05],[.82,.58,.32],orange);
box('MOVING carriage cap '+y,[2.08,y,2.23],[.68,.46,.06],sand);
cylinder('actuator '+y,[2.08,y<0?-1.9:1.9,1.98],[2.08,y,1.98],.06,light,12);
}
const v=parts.flatMap(p=>p.vertices);
if(v.some(q=>Math.abs(q[0])>3+1e-8||Math.abs(q[1])>3+1e-8))throw Error('Outside 3x3');
const ranges=[[-1.10,-.50],[.50,1.10]],halfLength=.29;
if(ranges[0][1]+halfLength>=ranges[1][0]-halfLength)throw Error('Swept carriage collision');
for(const r of ranges)if(r[0]-halfLength< -1.94||r[1]+halfLength>1.94)throw Error('End stop collision');
return {svg:sheet(parts,'human barracks F2'),obj:obj(parts),spec:{units:'6 arbitrary horizontal units = 3 game cells; not meters',footprintCells:[3,3],carriageCount:2,axis:'local Y, front to rear',carriageCenterX:2.08,carriageCenterZ:2.05,carriageSize:[.82,.58,.32],travelCenterRanges:ranges,minimumSweptGap:ranges[1][0]-ranges[0][1]-.58,rangeStatus:'proposed blockout ranges, not final dimensions',states:{idle:'parked; background lamps may stay steady',work:'alternating forward and return strokes while real order progresses',cancelled:'end work motion and return to park',complete:'stop work cycle; optional short return to park, no production delay',cancel:'stop work cycle; visual return must not change gameplay'},limits:'One shared schematic mesh; shell details and courtyard facade follow approved f2.png. Motion ranges checked for no carriage overlap or end stop collision.'}};
`);
async function main(){fs.mkdirSync(dir,{recursive:true});const d=build();if(/NaN|Infinity/.test(d.svg+d.obj))throw Error('Invalid geometry');fs.writeFileSync(path.join(dir,'guide.svg'),d.svg);fs.writeFileSync(path.join(dir,'blockout.obj'),d.obj);await sharp(Buffer.from(d.svg)).png().toFile(path.join(dir,'guide.png'));fs.writeFileSync(path.join(dir,'motion-spec.json'),JSON.stringify(d.spec,null,2)+'\n');console.log('Barracks F2: 3x3 shared scaffold, two carriages, disjoint swept ranges and no end-stop collision.');}
main().catch(e=>{console.error(e);process.exitCode=1;});

