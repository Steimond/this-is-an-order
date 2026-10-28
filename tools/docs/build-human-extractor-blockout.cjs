/* Human extractor E: shared proportion geometry, not a detailed game asset. */
const fs=require('node:fs'),path=require('node:path'),{createRequire}=require('node:module');
const req=createRequire(path.join(path.dirname(process.execPath),'../node_modules/package.json')),sharp=req('sharp');
const dir=path.resolve(__dirname,'../../docs/concepts/human-plasma-extractor-directions/enclosed/modeling');
const source=fs.readFileSync(path.join(__dirname,'build-reference-blockouts.cjs'),'utf8');
const helpers=source.slice(source.indexOf('const add='),source.indexOf('const specs='));
const renderer=source.slice(source.indexOf('function renderParts('),source.indexOf('async function main('));
const build=new Function(helpers+renderer+`
const sand='#cbb99a',frame='#394047',orange='#b87838',purple='#995ad5';
box('continuous sealed foundation',[0,0,.12],[4.8,6.0,.24],frame);
const steps=[[-1.8,1.30],[0,2.12],[1.8,3.02]];
for(let i=0;i<steps.length;i++){
const [y,top]=steps[i];
box('closed stage '+i,[0,y,(top+.24)/2],[3.65,1.8,top-.24],sand);
for(const x of [-1.84,1.84])box('edge frame '+i+' '+x,[x,y-.85,(top+.24)/2],[.13,.16,top-.24],frame);
box('roof grille '+i,[-.42,y,top+.025],[.85,.60,.05],frame);
box('closed roof hatch '+i,[.62,y,top+.025],[.55,.52,.05],orange);
}
box('closed worker transfer hatch',[0,-2.72,.67],[.95,.06,.74],orange);
box('front right lower process housing',[2.04,-1.30,.86],[.46,.74,1.08],sand);
box('rear right upper process housing',[2.04,1.72,2.36],[.46,.64,.72],sand);
const a=[2.04,-1.30,1.18],b=[2.04,1.72,2.36];
cylinder('single purple diagonal pipe',a,b,.18,purple,24);
const axis=norm(sub(b,a));
for(const t of [0,.25,.5,.75,1]){
const c=add(a,mul(sub(b,a),t));
cylinder('pipe collar '+t,add(c,mul(axis,-.055)),add(c,mul(axis,.055)),.245,frame,24);
}
beam('pipe protective rail',[2.23,-1.30,1.18],[2.23,1.72,2.36],.06,.06,frame);
box('rear right process window',[1.87,2.10,2.75],[.065,.80,.32],purple);
const limits=parts.flatMap(p=>p.vertices);
if(limits.some(v=>Math.abs(v[0])>2.4+1e-8||Math.abs(v[1])>3+1e-8))throw Error('Outside foundation');
return {svg:sheet(parts,'human extractor E'),obj:obj(parts),spec:{units:'arbitrary relative units; NOT meters or fixed game cells',foundation:[4.8,6],roofHeights:steps.map(s=>s[1]),pipe:{side:'right',start:a,end:b,flow:'low front toward high rear'},limits:'Proportion scaffold. Final sand shells, trims, ports and window shape follow approved e.png. Deposit entirely concealed under continuous foundation.'}};
`);
async function main(){fs.mkdirSync(dir,{recursive:true});const d=build();if(/NaN|Infinity/.test(d.svg+d.obj))throw Error('Invalid geometry');fs.writeFileSync(path.join(dir,'guide.svg'),d.svg);fs.writeFileSync(path.join(dir,'blockout.obj'),d.obj);await sharp(Buffer.from(d.svg)).png().toFile(path.join(dir,'guide.png'));fs.writeFileSync(path.join(dir,'blockout-spec.json'),JSON.stringify(d.spec,null,2)+'\n');console.log('Extractor E: three stages, one right diagonal pipe, all parts within sealed foundation. Five views and OBJ written.');}
main().catch(e=>{console.error(e);process.exitCode=1;});

