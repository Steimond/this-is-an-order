/* Shared proportion renderer from the unit guides; no final game geometry. */
const fs=require('fs'),path=require('path'),{createRequire}=require('module');
const req=createRequire(path.join(path.dirname(process.execPath),'../node_modules/package.json')),sharp=req('sharp');
const dir=path.resolve(__dirname,'../../docs/concepts/resonance-transport-directions');
const source=fs.readFileSync(path.join(__dirname,'build-reference-blockouts.cjs'),'utf8');
const helpers=source.slice(source.indexOf('const add='),source.indexOf('const specs='));
const render=source.slice(source.indexOf('function renderParts('),source.indexOf('async function main('));
const build=new Function(helpers+render+`
  box('constant-width detachable container',[0,0,1.2],[2.1,6,1.3]);
  wing('central fuselage',[[-.26,-2],[.26,-2],[.7,-.7],[.7,.7],[.38,2],[-.38,2],[-.7,.7],[-.7,-.7]],2.2,.7);
  box('central graphite spine',[0,0,2.59],[.45,2.7,.09],dark);
  for(const x of [-1,1])for(const y of [-1,1]){
    beam('arm '+x+' '+y,[x*.6,y*.8,2.2],[x*3.15,y*1.7,2.2],.48,.4);
    cylinder('closed repulsor '+x+' '+y,[x*3.4,y*1.7,1.9],[x*3.4,y*1.7,2.5],.9,light,12);
    cylinder('lower collar '+x+' '+y,[x*3.4,y*1.7,1.77],[x*3.4,y*1.7,1.91],.73,dark,12);
    cylinder('downward emitter '+x+' '+y,[x*3.4,y*1.7,1.73],[x*3.4,y*1.7,1.78],.59,cyan,12);
  }
  for(const y of [-1,1])box('container end cyan marker '+y,[0,y*3.006,1.45],[.7,.025,.07],cyan);
  return {svg:sheet(parts,'transport-c4'),obj:obj(parts)};
`);
const result=build();
fs.writeFileSync(path.join(dir,'c4-geometry-guide.svg'),result.svg);
fs.writeFileSync(path.join(dir,'c4-geometry-guide.obj'),result.obj);
fs.writeFileSync(path.join(dir,'c4-proportions.json'),JSON.stringify({units:'arbitrary proportions, not gameplay size',fuselage:{length:4,width:1.4},container:{length:6,width:2.1,height:1.3,center:[0,0,1.2],frontOverhang:1,rearOverhang:1,constantWidth:true},propulsion:{type:'closed repulsor concept',count:4},mount:'flush underside, no exposed suspension',limits:'Shared blockout proves only its own proportions; artistic views still require checking.'},null,2));
sharp(Buffer.from(result.svg)).png().toFile(path.join(dir,'c4-geometry-guide.png')).then(()=>console.log('Built one-mesh C4 guide: equal overhangs, constant width, 4 repulsors.'));
