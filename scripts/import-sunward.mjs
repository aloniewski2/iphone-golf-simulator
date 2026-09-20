// Offline GLB -> validated game mesh contract. No runtime GLTF dependency or downloads.
import {readFileSync,writeFileSync,mkdirSync} from 'node:fs';
import {resolve,join} from 'node:path';
const [source,destArg]=process.argv.slice(2);
if(!source) throw Error('Usage: node scripts/import-sunward.mjs workshop.glb [output-directory]');
const destination=resolve(destArg??'GolfArcade/Resources/Sunward');mkdirSync(destination,{recursive:true});
const bytes=readFileSync(source);
if(bytes.readUInt32LE(0)!==0x46546c67||bytes.readUInt32LE(4)!==2)throw Error('Expected GLB v2');
let g,binary;
for(let p=12;p<bytes.length;){const n=bytes.readUInt32LE(p),type=bytes.readUInt32LE(p+4),chunk=bytes.subarray(p+8,p+8+n);if(type===0x4e4f534a)g=JSON.parse(chunk.toString());else if(type===0x004e4942)binary=chunk;p+=8+n;}
if(!g||!binary)throw Error('Missing GLB chunks');
function values(index){
 const a=g.accessors[index],b=g.bufferViews[a.bufferView];
 const width={SCALAR:1,VEC2:2,VEC3:3,VEC4:4,MAT4:16}[a.type];
 const size={5121:1,5123:2,5125:4,5126:4}[a.componentType];
 const read={5121:'readUInt8',5123:'readUInt16LE',5125:'readUInt32LE',5126:'readFloatLE'}[a.componentType];
 if(!b||!width||!size||a.sparse)throw Error('Unsupported accessor');
 return Array.from({length:a.count*width},(_,i)=>{let v=binary[read]((b.byteOffset??0)+(a.byteOffset??0)+Math.floor(i/width)*(b.byteStride??size*width)+(i%width)*size);if(a.normalized&&a.componentType!==5126)v/=(a.componentType===5121?255:65535);return v;});
}
const identity=[1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1];
function mul(a,b){return Array.from({length:16},(_,i)=>[0,1,2,3].reduce((s,k)=>s+a[k*4+i%4]*b[Math.floor(i/4)*4+k],0));}
function matrix(n){if(n.matrix)return n.matrix;const [x,y,z,w]=n.rotation??[0,0,0,1],s=n.scale??[1,1,1],t=n.translation??[0,0,0];return [(1-2*y*y-2*z*z)*s[0],(2*x*y+2*z*w)*s[0],(2*x*z-2*y*w)*s[0],0,(2*x*y-2*z*w)*s[1],(1-2*x*x-2*z*z)*s[1],(2*y*z+2*x*w)*s[1],0,(2*x*z+2*y*w)*s[2],(2*y*z-2*x*w)*s[2],(1-2*x*x-2*y*y)*s[2],0,...t,1];}
const parents=new Map();g.nodes.forEach((n,i)=>(n.children??[]).forEach(c=>parents.set(c,i)));
function world(i){return parents.has(i)?mul(world(parents.get(i)),matrix(g.nodes[i])):matrix(g.nodes[i]);}
const scale=.292608;
function point(m,p){return [0,1,2].map(r=>(m[r]*p[0]+m[4+r]*p[1]+m[8+r]*p[2]+m[12+r])/scale);}
function normal(m,p){ // inverse transpose of upper 3x3, including non-uniform prop scale
 const a=[m[0],m[1],m[2]],b=[m[4],m[5],m[6]],c=[m[8],m[9],m[10]];
 const cross=(x,y)=>[x[1]*y[2]-x[2]*y[1],x[2]*y[0]-x[0]*y[2],x[0]*y[1]-x[1]*y[0]];
 const co=[cross(b,c),cross(c,a),cross(a,b)],det=a.reduce((s,v,i)=>s+v*co[0][i],0);
 if(Math.abs(det)<1e-12)throw Error('Singular node transform');
 const v=[0,1,2].map(r=>(co[0][r]*p[0]+co[1][r]*p[1]+co[2][r]*p[2])/det),l=Math.hypot(...v);
 return v.map(x=>x/l);
}
const materials=(g.materials??[]).map((m,i)=>{
 const p=m.pbrMetallicRoughness??{},out={name:m.name??'material_'+i,color:p.baseColorFactor??[1,1,1,1],roughness:p.roughnessFactor??.8,metalness:p.metallicFactor??0};
 if(p.baseColorTexture){const im=g.images[g.textures[p.baseColorTexture.index].source];if(im.bufferView===undefined)throw Error('Only embedded GLB textures supported');const v=g.bufferViews[im.bufferView],file='SunwardTexture-'+i+(im.mimeType==='image/png'?'.png':'.jpg');writeFileSync(join(destination,file),binary.subarray(v.byteOffset,v.byteOffset+v.byteLength));out.texture=file;}
 return out;
});
function extract(prefix,skinned){
 const grouped=new Map();let bones=[],inverseBinds=[],boneLinks=[];
 for(let ni=0;ni<g.nodes.length;ni++){
  const node=g.nodes[ni];if(!node.name?.startsWith(prefix)||node.mesh===undefined)continue;
  const transform=world(ni);let remap=[];
  if(skinned){const skin=g.skins[node.skin];if(!skin)throw Error('Body missing skin');const ib=values(skin.inverseBindMatrices);
   remap=skin.joints.map((ji,i)=>{const name=g.nodes[ji].name;let index=bones.findIndex(b=>b.name===name);if(index<0){index=bones.length;bones.push({name,parent:-1});const link=Number(name.replace('link_',''));if(!Number.isInteger(link)||link<0||link>=12)throw Error('Unexpected bone '+name);boneLinks.push(link);const m=ib.slice(i*16,i*16+16);m[12]/=scale;m[13]/=scale;m[14]/=scale;inverseBinds.push(...m);}return index;});
  }
  for(const p of g.meshes[node.mesh].primitives){
   if(p.mode!==undefined&&p.mode!==4)throw Error('Expected triangles');
   const positions=values(p.attributes.POSITION),normals=values(p.attributes.NORMAL),uv=p.attributes.TEXCOORD_0===undefined?Array(positions.length/3*2).fill(0):values(p.attributes.TEXCOORD_0);
   const joints=skinned?values(p.attributes.JOINTS_0).map(j=>remap[j]):[],weights=skinned?values(p.attributes.WEIGHTS_0):[];
   const indices=p.indices===undefined?Array.from({length:positions.length/3},(_,i)=>i):values(p.indices);
   const key=p.material??0;let m=grouped.get(key);if(!m){m={positions:[],normals:[],uv:[],joints:[],weights:[],indices:[],material:key};grouped.set(key,m);}const base=m.positions.length/3;
   for(let i=0;i<positions.length;i+=3){m.positions.push(...point(transform,positions.slice(i,i+3)));m.normals.push(...normal(transform,normals.slice(i,i+3)));}
   m.uv.push(...uv);m.joints.push(...joints);m.weights.push(...weights);m.indices.push(...indices.map(i=>i+base));
  }
 }
 const meshes=[...grouped.values()];if(!meshes.length)throw Error('No meshes '+prefix);
 for(const m of meshes){if(m.positions.some(v=>!Number.isFinite(v)))throw Error('Nonfinite mesh');if(m.indices.some(i=>i<0||i>=m.positions.length/3))throw Error('Invalid index');if(skinned)for(let i=0;i<m.weights.length;i+=4){const sum=m.weights.slice(i,i+4).reduce((a,b)=>a+b,0);if(Math.abs(sum-1)>.001)throw Error('Unnormalized weights');}}
 return {version:2,coordinateSpace:'sunward-rig',source:'Higgsfield 3D Jutsu / original Sunward asset source',bones,boneLinks,inverseBinds,materials,meshes};
}
for(const [prefix,name,skinned] of [['body_','SunwardGolfer',true],['prop_tree_','SunwardTree',false],['prop_rock_','SunwardRocks',false]]){
 const asset=extract(prefix,skinned);
 // Props were arranged beside the rig for source review; recenter them for instancing.
 if(!skinned){const ps=asset.meshes.flatMap(m=>m.positions),xs=ps.filter((_,i)=>i%3===0),zs=ps.filter((_,i)=>i%3===2),cx=(Math.min(...xs)+Math.max(...xs))/2,cz=(Math.min(...zs)+Math.max(...zs))/2;for(const m of asset.meshes)for(let i=0;i<m.positions.length;i+=3){m.positions[i]-=cx;m.positions[i+2]-=cz;}}
 writeFileSync(join(destination,name+'.golfmesh'),JSON.stringify(asset));console.log(name+': '+asset.meshes.reduce((n,m)=>n+m.positions.length/3,0)+' vertices; '+asset.meshes.length+' materials');
}
