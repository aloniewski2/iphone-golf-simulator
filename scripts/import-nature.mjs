// Offline conversion of the selected CC0 Quaternius static glTF models.
// Deliberately separate from the golfer importer; no rig assets are touched.
import {readFileSync,writeFileSync,mkdirSync,copyFileSync} from 'node:fs';
import {resolve,join,basename} from 'node:path';
import {execFileSync} from 'node:child_process';
const [input,output]=process.argv.slice(2);
if(!input||!output)throw Error('Usage: node scripts/import-nature.mjs source-glTF-directory destination');
// Clip triangles rather than squash their vertices: retain the lower sculpted
// trunk, bark UVs and branches, while removing tips built for the old leaf cards.
// The cut plane lies inside the opaque lower crown; source glTF stays untouched.
function trimUpperBranches(mesh,height) {
 const result={...mesh,positions:[],normals:[],uv:[],colors:mesh.colors?[]:null,indices:[]};
 const vertex=i=>({p:mesh.positions.slice(i*3,i*3+3),n:mesh.normals.slice(i*3,i*3+3),
  uv:mesh.uv.slice(i*2,i*2+2),c:mesh.colors?.slice(i*4,i*4+4)});
 const interpolate=(a,b)=>{
  const t=(height-a.p[1])/(b.p[1]-a.p[1]);
  const blend=(x,y)=>x.map((v,i)=>v+(y[i]-v)*t);
  return {p:blend(a.p,b.p),n:blend(a.n,b.n),uv:blend(a.uv,b.uv),c:a.c?blend(a.c,b.c):null};
 };
 for(let i=0;i<mesh.indices.length;i+=3) {
  const triangle=mesh.indices.slice(i,i+3).map(vertex),polygon=[];
  for(let j=0;j<3;j++) {
   const a=triangle[j],b=triangle[(j+1)%3],inside=a.p[1]<=height;
   if(inside)polygon.push(a);
   if(inside!==(b.p[1]<=height))polygon.push(interpolate(a,b));
  }
  if(polygon.length<3)continue;
  const base=result.positions.length/3;
  for(const v of polygon) {
   const length=Math.hypot(...v.n);
   if(length<1e-8)throw Error('Invalid clipped normal');
   result.positions.push(...v.p);result.normals.push(...v.n.map(x=>x/length));
   result.uv.push(...v.uv);if(result.colors)result.colors.push(...v.c);
  }
  for(let j=1;j<polygon.length-1;j++)result.indices.push(base,base+j,base+j+1);
 }
 return result;
}
mkdirSync(output,{recursive:true});
for(let variant=1;variant<=5;variant++) {
 const g=JSON.parse(readFileSync(join(input,`CommonTree_${variant}.gltf`)));
 const safe=n=>{if(n!==basename(n))throw Error('External path rejected');return join(input,n);};
 const buffers=g.buffers.map(b=>readFileSync(safe(b.uri)));
 function values(index){
  const a=g.accessors[index],v=g.bufferViews[a.bufferView],b=buffers[v.buffer];
  const width={SCALAR:1,VEC2:2,VEC3:3,VEC4:4}[a.type];
  const size={5121:1,5123:2,5125:4,5126:4}[a.componentType];
  const read={5121:'readUInt8',5123:'readUInt16LE',5125:'readUInt32LE',5126:'readFloatLE'}[a.componentType];
  if(!width||!size||a.sparse)throw Error('Unsupported accessor');
  return Array.from({length:a.count*width},(_,i)=>{const offset=(v.byteOffset??0)+(a.byteOffset??0)+Math.floor(i/width)*(v.byteStride??size*width)+(i%width)*size;
   if(offset+size>(v.byteOffset??0)+v.byteLength)throw Error('Accessor overflow');
   let x=b[read](offset);if(a.normalized&&a.componentType!==5126)x/=a.componentType===5121?255:65535;return x;});
 }
 const materials=g.materials.map(m=>{
  const p=m.pbrMetallicRoughness??{};
  const texture=g.images[g.textures[p.baseColorTexture.index].source].uri;
  copyFileSync(safe(texture),join(output,texture));
  return {name:m.name,color:p.baseColorFactor??[1,1,1,1],roughness:.92,metalness:0,texture,
   alphaCutoff:m.alphaMode==='MASK' ? m.alphaCutoff??.5 : null,doubleSided:m.doubleSided??false};
 });
 let meshes=[];
 for(const ni of g.scenes[g.scene??0].nodes) {
  const n=g.nodes[ni];
  if(n.matrix||n.rotation||n.scale||n.translation||n.children||n.skin!==undefined)throw Error('Only untransformed static source nodes supported');
  for(const p of g.meshes[n.mesh].primitives){
   if(p.mode!==undefined&&p.mode!==4)throw Error('Expected triangles');
   meshes.push({positions:values(p.attributes.POSITION),normals:values(p.attributes.NORMAL),uv:values(p.attributes.TEXCOORD_0),
    colors:p.attributes.COLOR_0===undefined?null:values(p.attributes.COLOR_0),indices:values(p.indices),material:p.material,joints:[],weights:[]});
  }
 }
 // Consistent reserved footprint: 8-yard trees, centered on their trunk base.
 const bounds=[Infinity,Infinity,Infinity,-Infinity,-Infinity,-Infinity];
 for(const m of meshes)for(let i=0;i<m.positions.length;i++) {const c=i%3;bounds[c]=Math.min(bounds[c],m.positions[i]);bounds[c+3]=Math.max(bounds[c+3],m.positions[i]);}
 const scale=8/(bounds[4]-bounds[1]);
 for(const m of meshes){
  for(let i=0;i<m.positions.length;i+=3){m.positions[i]*=scale;m.positions[i+1]=(m.positions[i+1]-bounds[1])*scale;m.positions[i+2]*=scale;}
  if(m.positions.some(x=>!Number.isFinite(x))||m.indices.some(i=>i<0||i>=m.positions.length/3))throw Error('Invalid geometry');
 }
 let radius=0;
 for(const m of meshes)for(let i=0;i<m.positions.length;i+=3)radius=Math.max(radius,Math.hypot(m.positions[i],m.positions[i+2]));
 const breadth=3.6/radius;
 for(const m of meshes)for(let i=0;i<m.positions.length;i+=3){
  m.positions[i]*=breadth;m.positions[i+2]*=breadth;
  const n=[m.normals[i]/breadth,m.normals[i+1],m.normals[i+2]/breadth],l=Math.hypot(...n);
  for(let c=0;c<3;c++)m.normals[i+c]=n[c]/l;
 }
 // The reference has sculpted rounded foliage, not sparse alpha-card leaves.
 // Keep the modeled branching structure, but author connected crown tiers
 // instead of retaining the source's sparse leaf-card silhouette.
 const leaf=meshes.find(m=>materials[m.material].name.startsWith('Leaves'));
 const crown={positions:[],normals:[],uv:[],colors:[],indices:[],material:materials.length,joints:[],weights:[]};
 const foliageColors=[[.36,.49,.13,1],[.31,.46,.15,1],[.40,.53,.16,1]];
 materials.push({name:'sculpted-canopy',color:foliageColors[variant%3],roughness:.92,metalness:0});
 // Three overlapping lower lobes support a taller tapered leader. No detached
 // leaf balls or alpha cards; variation is deterministic and stays in the
 // existing reserved footprint after normalization.
 const masses=Array.from({length:3},(_,k)=>{
  const angle=k*2*Math.PI/3+variant*.7;
  return {center:[Math.cos(angle)*1.12,4.55+.32*Math.sin(k+variant),Math.sin(angle)*1.12],
   r:[2.05,1.95+.22*((variant+k)%3),1.95],pear:true};
 });
 masses.push({center:[.45*Math.sin(variant),6.45+.2*(variant%3),.3*Math.cos(variant)],
  r:[1.85+.15*(variant%3),2.3+.3*(variant%3),1.85],pear:true});
 masses.forEach(({center,r,pear},k)=>{
  const base=crown.positions.length/3,rows=12,cols=16;
  function point(phi,theta) {
   const d=[Math.sin(phi)*Math.cos(theta),Math.cos(phi),Math.sin(phi)*Math.sin(theta)];
   const ripple=1+.035*Math.sin(theta*3+k)*Math.sin(phi)**2+.018*Math.cos(theta*5-phi*4+k);
   const taper=pear ? .9-.19*Math.cos(phi) : 1;
   return d.map((x,c)=>center[c]+x*r[c]*ripple*(c===1?1:taper));
  }
  for(let row=0;row<=rows;row++)for(let col=0;col<=cols;col++){
   const phi=row*Math.PI/rows,theta=col*2*Math.PI/cols;
   const d=[Math.sin(phi)*Math.cos(theta),Math.cos(phi),Math.sin(phi)*Math.sin(theta)];
   const p=point(phi,theta);
   const t=point(phi,theta+.001).map((x,c)=>x-point(phi,theta-.001)[c]);
   const v=point(phi+.001,theta).map((x,c)=>x-point(phi-.001,theta)[c]);
   let n=[t[1]*v[2]-t[2]*v[1],t[2]*v[0]-t[0]*v[2],t[0]*v[1]-t[1]*v[0]];
   if(Math.hypot(...n)<1e-8)n=d.map((x,c)=>x/r[c]);
   const l=Math.hypot(...n);crown.positions.push(...p);crown.normals.push(...n.map(x=>x/l));
   crown.uv.push(col/cols,row/rows);
   const shade=.70+.30*(d[1]+1)/2;crown.colors.push(shade*.97,shade,shade*.94,1);
   if(row<rows&&col<cols){const a=base+row*(cols+1)+col,b=a+cols+1;crown.indices.push(a,a+1,b,a+1,b+1,b);}
  }
 });
 meshes=meshes.filter(m=>m!==leaf).map(m=>trimUpperBranches(m,3.7));meshes.push(crown);
 // Refit the finished crown envelope, not merely the input cards.
 let finalRadius=0,finalHeight=0;
 for(const m of meshes)for(let i=0;i<m.positions.length;i+=3){finalRadius=Math.max(finalRadius,Math.hypot(m.positions[i],m.positions[i+2]));finalHeight=Math.max(finalHeight,m.positions[i+1]);}
 const sx=3.6/finalRadius,sy=8/finalHeight;
 for(const m of meshes)for(let i=0;i<m.positions.length;i+=3){m.positions[i]*=sx;m.positions[i+1]*=sy;m.positions[i+2]*=sx;
  const n=[m.normals[i]/sx,m.normals[i+1]/sy,m.normals[i+2]/sx],l=Math.hypot(...n);for(let c=0;c<3;c++)m.normals[i+c]=n[c]/l;}
 const result={version:2,coordinateSpace:'sunward-rig',source:'Quaternius Stylized Nature MegaKit Standard / CC0 1.0',bones:[],boneLinks:[],inverseBinds:[],materials,meshes};
 writeFileSync(join(output,`NatureTree${variant}.golfmesh`),JSON.stringify(result));
 console.log(`NatureTree${variant}: ${meshes.reduce((s,m)=>s+m.indices.length/3,0)} triangles, 8 yards tall`);
}
// Keep the original 2K source; the bundled bark atlas is a mobile-sized 1K copy.
execFileSync('/usr/bin/sips',['-Z','1024',join(output,'Bark_NormalTree.png')]);
