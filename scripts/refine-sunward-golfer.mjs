// Offline, deterministic mesh authoring. Reference: reference-0.png / reference-1.png
// from the Sunward Higgsfield ledger. This is model construction, not AI mesh extraction.
// Retains the imported 12-bone bind contract; never changes gameplay joint positions.
import {readFileSync, writeFileSync} from 'node:fs';
import {fileURLToPath} from 'node:url';
const root=fileURLToPath(new URL('../',import.meta.url));
const path=root+'GolfArcade/Resources/Sunward/SunwardGolfer.golfmesh';
const asset=JSON.parse(readFileSync(path));
const rest=JSON.parse(readFileSync(root+'scripts/sunward-rest-pose.json'));
const add=(a,b)=>a.map((v,i)=>v+b[i]), sub=(a,b)=>a.map((v,i)=>v-b[i]);
const mul=(a,s)=>a.map(v=>v*s), dot=(a,b)=>a.reduce((s,v,i)=>s+v*b[i],0);
const unit=a=>mul(a,1/(Math.hypot(...a)||1));
const cross=(a,b)=>[a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]];
const mix=(a,b,t)=>add(mul(a,1-t),mul(b,t));
const smooth=t=>{t=Math.max(0,Math.min(1,t));return t*t*(3-2*t)};
// Cubic profiles eliminate the old eight-ring shoulder/waist silhouettes.
function profile(keys,t) {
 let i=0;while(i<keys.length-2 && t>keys[i+1][0])i++;
 const a=keys[Math.max(0,i-1)],b=keys[i],c=keys[i+1],d=keys[Math.min(keys.length-1,i+2)];
 const u=Math.max(0,Math.min(1,(t-b[0])/(c[0]-b[0])));
 return [1,2].map(k=>.5*((2*b[k])+(-a[k]+c[k])*u+(2*a[k]-5*b[k]+4*c[k]-d[k])*u*u+(-a[k]+3*b[k]-3*c[k]+d[k])*u*u*u));
}
const meshes=[];
function tube(name,centers,radii,weights,material,columns=36) {
 const p=[],uv=[],joints=[],ws=[],indices=[];
 for(let r=0;r<centers.length;r++) {
  const axis=unit(sub(centers[Math.min(r+1,centers.length-1)],centers[Math.max(0,r-1)]));
  const u=unit(sub([1,0,0],mul(axis,axis[0]))),v=unit(cross(axis,u));
  for(let c=0;c<columns;c++) {
   const angle=c/columns*Math.PI*2;
   p.push(...add(centers[r],add(mul(u,Math.cos(angle)*radii[r][0]),mul(v,Math.sin(angle)*radii[r][1]))));
   uv.push(c/columns,r/(centers.length-1));
   const w=Object.entries(weights[r]).filter(([,v])=>v>0);
   joints.push(...Array.from({length:4},(_,i)=>w[i]?Number(w[i][0]):0));
   ws.push(...Array.from({length:4},(_,i)=>w[i]?.[1]??0));
   if(r) {const a=(r-1)*columns+c,b=(r-1)*columns+(c+1)%columns;indices.push(a,b,a+columns,b,b+columns,a+columns)}
  }
 }
 for(let c=1;c<columns-1;c++) {indices.push(0,c+1,c);const b=(centers.length-1)*columns;indices.push(b,b+c,b+c+1)}
 const normals=Array(p.length).fill(0);
 for(let i=0;i<indices.length;i+=3) {
  const [a,b,c]=indices.slice(i,i+3).map(v=>v*3);
  const n=cross(sub(p.slice(b,b+3),p.slice(a,a+3)),sub(p.slice(c,c+3),p.slice(a,a+3)));
  for(const j of [a,b,c])for(let k=0;k<3;k++)normals[j+k]+=n[k];
 }
 for(let i=0;i<normals.length;i+=3)normals.splice(i,3,...unit(normals.slice(i,i+3)));
 meshes.push({name,positions:p,normals,uv,joints,weights:ws,indices,material:asset.materials.findIndex(m=>m.name===material)});
}
function garment(name,a,b,keys,bone,material,steps=40) {
 const lo=keys[0][0],hi=keys.at(-1)[0];
 const ts=Array.from({length:steps},(_,i)=>lo+(hi-lo)*i/(steps-1));
 tube(name,ts.map(t=>mix(a,b,t)),ts.map(t=>profile(keys,t)),ts.map(()=>({[bone]:1})),material);
}
garment('tailored-polo-v4',rest.root,rest.neck,
 [[-.06,.50,.78],[0,.54,.82],[.06,.55,.82],[.24,.55,.80],[.48,.58,.85],[.70,.57,.91],[.84,.51,.90],[.94,.34,.65],[1,.22,.28],[1.035,.215,.26]],0,'shirt',48);
garment('trouser-hip',add(rest.root,[0,-.35,0]),add(rest.root,[0,.1,0]),
 [[0,.26,.48],[.25,.43,.69],[.7,.49,.77],[1,.47,.74]],10,'trousers');
for(const [side,upper,lower,hip,knee] of [['left',2,3,6,7],['right',4,5,8,9]]) {
 const shoulder=rest[side+'Shoulder'],elbow=rest[side+'Elbow'],wrist=rest[side+'Wrist'];
 const centers=[],radii=[],weights=[];
 for(let i=0;i<=40;i++) {
  const t=i/40,blend=smooth((t-.37)/.26);
  centers.push(t<=.5?mix(shoulder,elbow,t*2):mix(elbow,wrist,(t-.5)*2));
  const r=.27*(1-t)+.155*t+.026*Math.sin(t*Math.PI);radii.push([r,r]);weights.push({[upper]:1-blend,[lower]:blend});
 }
 tube(side+'-anatomical-arm',centers,radii,weights,'skin');
 garment(side+'-rounded-sleeve',shoulder,elbow,
  [[-.27,.015,.015],[-.20,.18,.18],[-.10,.31,.31],[0,.36,.36],[.16,.39,.39],[.36,.375,.375],[.53,.33,.33],[.61,.315,.315]],upper,'shirt');
 garment(side+'-sewn-cuff',shoulder,elbow,[[.53,.335,.335],[.55,.335,.335],[.625,.317,.317]],upper,'cuff',8);
 const top=rest[side+'Hip'],mid=rest[side+'Knee'],end=rest[side+'Ankle'];
 const pc=[],pr=[],pw=[];
 for(let i=0;i<=40;i++) {
  const t=i/40,blend=smooth((t-.34)/.32);
  pc.push(t<=.5?mix(top,mid,t*2):mix(mid,end,(t-.5)*2));
  const r=.345*(1-t)+.235*t+.025*Math.sin(t*Math.PI);pr.push([r*.98,r]);pw.push({[hip]:1-blend,[knee]:blend});
 }
 tube(side+'-tailored-leg',pc,pr,pw,'trousers');
}
garment('neck',add(rest.neck,[0,-.08,0]),add(rest.neck,[0,.4,0]),[[0,.22,.24],[1,.20,.215]],1,'skin',12);
asset.meshes=meshes;
asset.source='Sunward integration v4: original reference-matched mesh authoring; refine-sunward-golfer.mjs; retained 3D Jutsu bind data';
writeFileSync(path,JSON.stringify(asset));
console.log(JSON.stringify({vertices:meshes.reduce((s,m)=>s+m.positions.length/3,0),triangles:meshes.reduce((s,m)=>s+m.indices.length/3,0),materials:asset.materials.map(m=>m.name)}));
