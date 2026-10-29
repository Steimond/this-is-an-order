/* Consistent structural cameras rendered from one geometry per building. No AI view synthesis. */
const fs=require('node:fs'),path=require('node:path'),vm=require('node:vm'),crypto=require('node:crypto'),{createRequire}=require('node:module');
const req=createRequire(path.join(path.dirname(process.execPath),'../node_modules/package.json')),sharp=req('sharp');
const root=path.resolve(__dirname,'../../docs/concepts'),audit=path.join(root,'human-buildings-consistency-audit');
const entries=JSON.parse(fs.readFileSync(path.join(audit,'inputs.json'),'utf8')).entries;
const scripts={supply:'supply-blockout',extractor:'extractor-blockout',barracks:'barracks-blockout',workshop:'workshop-blockout',operations:'operations-blockout',factory:'factory-blockout',aerodrome:'aerodrome-modeling',command:'command-module-modeling',ground:'ground-turret-modeling',aa:'missile-aa-modeling'};
const common=fs.readFileSync(path.join(__dirname,'build-reference-blockouts.cjs'),'utf8');
const helpers=common.slice(common.indexOf('const add='),common.indexOf('const specs='));
function hq(){return new Function(helpers+`
const sand='#cbb99a',frame='#394047',orange='#b87938',glass='#39464e';
box('HQ fixed square base',[0,0,.12],[8,8,.24],frame);
function profile(name,x0,x1,y0,y1,low,high,color){const p=[[x0,.24],[x1,.24],[x1,low],[x1-.38,high],[x0+.38,high],[x0,low]],v=[];for(const y of [y0,y1])for(const [x,z]of p)v.push([x,y,z]);const n=p.length,f=[Array.from({length:n},(_,i)=>n-1-i),Array.from({length:n},(_,i)=>n+i)];for(let i=0;i<n;i++)f.push([i,(i+1)%n,(i+1)%n+n,i+n]);mesh(name,v,f,color);}
profile('left sloped armored wing',-3.65,-1.12,-3.50,3.38,1.1,1.90,sand);
profile('right sloped armored wing',1.12,3.65,-3.50,3.38,1.1,1.90,sand);
profile('central wedge command spine',-1.12,1.12,-3.50,3.38,1.70,2.60,sand);
box('closed main gate frame',[0,-3.57,.99],[1.96,.18,1.48],frame);
for(const x of [-.43,.43])box('closed gate leaf '+x,[x,-3.68,.99],[.82,.04,1.26],frame);
for(const x of [-1.02,1.02])box('orange gate cheek '+x,[x,-3.55,1.54],[.24,.08,.88],orange);
for(const x of [-.94,.94])box('dark side command glazing '+x,[x,-.38,2.34],[.035,3.35,.30],glass);
box('dark front command glazing',[0,-2.06,2.33],[1.74,.025,.30],glass);
for(const x of [-3.70,-1.17,1.17,3.70])for(const y of [-3.38,3.30])box('fixed wing corner '+x+' '+y,[x,y,.62],[.18,.25,.76],frame);
for(const x of [-2.96,-2.54,-2.12]){box('left front closed service panel '+x,[x,-3.54,.72],[.32,.07,.76],frame);}
for(const x of [-2.50,2.50])box('wing roof vent '+x,[x,-.45,1.92],[.80,.72,.045],frame);
cylinder('short roof sensor',[0,-.15,2.60],[0,-.15,2.98],.16,frame,16);
for(const [x,y,height]of [[.48,1.62,3.60],[.72,1.94,3.90]])cylinder('rear antenna '+x,[x,y,2.60],[x,y,height],.035,frame,12);
return {parts};`)().parts;}
function loadParts(id){if(id==='hq')return hq();const file=path.join(__dirname,'build-human-'+scripts[id]+'.cjs');let code=fs.readFileSync(file,'utf8').replace(/main\(\)\.catch\([\s\S]*$/,'');code=code.replaceAll('return {svg:','return {parts,svg:');code+='\nmodule.exports=build;';const mod={exports:{}};vm.runInNewContext(code,{require,process,__dirname:__dirname,module:mod,console},{filename:file,timeout:2000});return mod.exports(id==='command'?45:undefined).parts;}
const sub=(a,b)=>a.map((x,i)=>x-b[i]),dot=(a,b)=>a.reduce((s,x,i)=>s+x*b[i],0),cross=(a,b)=>[a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]],norm=a=>a.map(x=>x/Math.hypot(...a));
const views=[['front','FRONT / -Y',[0,-1,0]],['right','RIGHT / +X',[1,0,0]],['rear','REAR / +Y',[0,1,0]],['top','TOP / +Z',[0,0,1]],['left','LEFT / -X',[-1,0,0]],['front-three-quarter','FRONT 3/4',[1,-1,.75]],['rear-three-quarter','REAR 3/4',[-1,1,.75]]];
function basis(view){const v=norm(view);if(view[2]===1&&view[0]===0&&view[1]===0)return {v,r:[1,0,0],u:[0,1,0]};const r=norm(cross([0,0,1],v));return {v,r,u:norm(cross(v,r))};}
function project(p,b,c,scale,W,H){const q=sub(p,c);return [W/2+dot(q,b.r)*scale,H/2-dot(q,b.u)*scale,dot(q,b.v)];}
function raster(parts,b,c,scale,W=900,H=680){const pixels=new Uint8Array(W*H*3),depth=new Float64Array(W*H);depth.fill(-Infinity);for(let i=0;i<W*H;i++){pixels[i*3]=231;pixels[i*3+1]=234;pixels[i*3+2]=236;}const edges=[];
for(const p of parts)for(const f of p.faces){if(f.length<3)continue;const points=f.map(i=>p.vertices[i]),n=norm(cross(sub(points[1],points[0]),sub(points[2],points[0])));if(!n.every(Number.isFinite))continue;const brightness=.65+.35*Math.abs(dot(n,norm([-.4,-.6,1]))),rgb=p.color.replace('#','').match(/../g).map(x=>Math.round(parseInt(x,16)*brightness));const pp=points.map(q=>project(q,b,c,scale,W,H));
for(let k=1;k<pp.length-1;k++){const [a,d,e]=[pp[0],pp[k],pp[k+1]],den=(d[1]-e[1])*(a[0]-e[0])+(e[0]-d[0])*(a[1]-e[1]);if(Math.abs(den)<1e-8)continue;const minX=Math.max(0,Math.floor(Math.min(a[0],d[0],e[0]))),maxX=Math.min(W-1,Math.ceil(Math.max(a[0],d[0],e[0]))),minY=Math.max(0,Math.floor(Math.min(a[1],d[1],e[1]))),maxY=Math.min(H-1,Math.ceil(Math.max(a[1],d[1],e[1])));
for(let y=minY;y<=maxY;y++)for(let x=minX;x<=maxX;x++){const xx=x+.5,yy=y+.5,s=((d[1]-e[1])*(xx-e[0])+(e[0]-d[0])*(yy-e[1]))/den,t=((e[1]-a[1])*(xx-e[0])+(a[0]-e[0])*(yy-e[1]))/den,w=1-s-t;if(s< -1e-8||t< -1e-8||w< -1e-8)continue;const z=s*a[2]+t*d[2]+w*e[2],i=y*W+x;if(z>=depth[i]){depth[i]=z;pixels.set(rgb,i*3);}}}
for(let k=0;k<pp.length;k++)edges.push([pp[k],pp[(k+1)%pp.length]]);}
for(const [a,b]of edges){const steps=Math.ceil(Math.hypot(a[0]-b[0],a[1]-b[1]));for(let k=0;k<=steps;k++){const t=k/Math.max(1,steps),x=Math.round(a[0]+t*(b[0]-a[0])),y=Math.round(a[1]+t*(b[1]-a[1])),z=a[2]+t*(b[2]-a[2]);if(x<0||x>=W||y<0||y>=H)continue;const i=y*W+x;if(z>=depth[i]-.012)pixels.set([70,80,87],i*3);}}
return sharp(Buffer.from(pixels),{raw:{width:W,height:H,channels:3}}).png().toBuffer();}
function cube(name,c,size,color){const vertices=[];for(const z of [-1,1])for(const y of [-1,1])for(const x of [-1,1])vertices.push([c[0]+x*size[0]/2,c[1]+y*size[1]/2,c[2]+z*size[2]/2]);return {name,vertices,faces:[[0,1,3,2],[4,6,7,5],[0,4,5,1],[2,3,7,6],[0,2,6,4],[1,5,7,3]],color};}
function obj(parts){let offset=1,s='# Shared structural geometry v2; not final mesh or dimensions\n';for(const p of parts){s+='o '+p.name.replaceAll(' ','_')+'\n';for(const v of p.vertices)s+='v '+v.join(' ')+'\n';for(const f of p.faces)s+='f '+f.map(i=>i+offset).join(' ')+'\n';offset+=p.vertices.length;}return s;}
async function main(){for(const e of entries){let parts=loadParts(e.id);parts=parts.map(p=>({...p,color:/amber|purple|plasma|window|lens|emissive/i.test(p.name)?'#41494f':p.color}));
if(e.id==='supply'){parts=parts.filter(p=>!p.name.startsWith('closed sliding leaf'));parts.push(cube('single closed front panel',[0,-1.52,1.40],[2.58,.065,1.95],'#cbb99a'));}
const verts=parts.flatMap(p=>p.vertices),bounds=[0,1,2].map(i=>[Math.min(...verts.map(p=>p[i])),Math.max(...verts.map(p=>p[i]))]),center=bounds.map(([a,b])=>(a+b)/2);let scale=Infinity;
for(const [, ,v]of views){const b=basis(v),x=verts.map(p=>dot(sub(p,center),b.r)),y=verts.map(p=>dot(sub(p,center),b.u));scale=Math.min(scale,800/(Math.max(...x)-Math.min(...x)),560/(Math.max(...y)-Math.min(...y)));}
const out=path.join(root,e.modeling,'consistency-v2');fs.mkdirSync(out,{recursive:true});fs.writeFileSync(path.join(out,'geometry.obj'),obj(parts));fs.writeFileSync(path.join(out,'geometry.json'),JSON.stringify({id:e.id,status:'shared structural proxy; approved hero still defines detailed appearance',axes:'X right Y rear Z up; front -Y',cameraScale:scale,bounds,parts},null,2));
const layers=[];for(let i=0;i<views.length;i++){const [id,label,v]=views[i],png=await raster(parts,basis(v),center,scale);await fs.promises.writeFile(path.join(out,id+'.png'),png);const x=i%3*600,y=Math.floor(i/3)*490;layers.push({input:await sharp(png).resize(600,453).png().toBuffer(),left:x,top:y+40});layers.push({input:Buffer.from(`<svg width="600" height="40"><text x="300" y="28" text-anchor="middle" font-family="Arial" font-size="20" fill="#26343e">${label}</text></svg>`),left:x,top:y});}
layers.push({input:Buffer.from('<svg width="1200" height="460"><text x="45" y="90" font-family="Arial" font-size="26" fill="#26343e">ONE GEOMETRY / ONE POSE / ONE SCALE</text><text x="45" y="140" font-family="Arial" font-size="21" fill="#536570">Structural proxy: directions, counts and occlusion.</text><text x="45" y="180" font-family="Arial" font-size="21" fill="#536570">Detailed surfaces and proportions: use approved hero.</text><text x="45" y="220" font-family="Arial" font-size="21" fill="#536570">Not a finished asset or a recovered unseen design.</text></svg>'),left:600,top:980});
await sharp({create:{width:1800,height:1470,channels:3,background:'#e7eaec'}}).composite(layers).png().toFile(path.join(out,'structural-sheet.png'));
fs.writeFileSync(path.join(out,'validation.json'),JSON.stringify({geometrySha256:crypto.createHash('sha256').update(JSON.stringify(parts)).digest('hex'),viewCount:7,allViewsSameGeometry:true,allViewsSamePose:true,allViewsSameOrthographicScale:true,cameraDirections:views.map(([id,label,v])=>({id,label,direction:v})),notVerified:'exact equivalence of proxy proportions/detail to image; unseen details are proposals'},null,2));console.log(e.id+' / '+parts.length+' parts / 7 coherent structural views');}}
main().catch(e=>{console.error(e);process.exitCode=1;});
