// Static CC0 rock conversion. Does not run or modify either golfer/tree importer.
import {readFileSync,writeFileSync,mkdirSync,copyFileSync} from 'node:fs';
import {join,basename} from 'node:path';
import {execFileSync} from 'node:child_process';
const [input,output]=process.argv.slice(2);
if(!input||!output)throw Error('Usage: node scripts/import-shore-rocks.mjs source destination');
mkdirSync(output,{recursive:true});
for(let variant=1;variant<=3;variant++) {
 const g=JSON.parse(readFileSync(join(input,`Rock_Medium_${variant}.gltf`)));
 const safe=n=>{if(n!==basename(n))throw Error('External path rejected');return join(input,n);};
 const buffers=g.buffers.map(b=>readFileSync(safe(b.uri)));
 function values(index) {
  const a=g.accessors[index],v=g.bufferViews[a.bufferView],b=buffers[v.buffer];
  const width={SCALAR:1,VEC2:2,VEC3:3,VEC4:4}[a.type],size={5121:1,5123:2,5125:4,5126:4}[a.componentType];
  const read={5121:'readUInt8',5123:'readUInt16LE',5125:'readUInt32LE',5126:'readFloatLE'}[a.componentType];
  if(!width||!size||a.sparse||a.normalized)throw Error('Unsupported accessor');
  return Array.from({length:a.count*width},(_,i)=>{
   const offset=(v.byteOffset??0)+(a.byteOffset??0)+Math.floor(i/width)*(v.byteStride??size*width)+(i%width)*size;
   if(offset+size>(v.byteOffset??0)+v.byteLength)throw Error('Accessor overflow');return b[read](offset);
  });
 }
 const materials=g.materials.map(m=>{
  const p=m.pbrMetallicRoughness??{},texture=g.images[g.textures[p.baseColorTexture.index].source].uri;
  copyFileSync(safe(texture),join(output,texture));
  return {name:'Shore stone / '+m.name,color:[.86,.90,.82,1],roughness:.94,metalness:0,texture,doubleSided:false};
 });
 const meshes=[];
 for(const ni of g.scenes[g.scene??0].nodes) {
  const n=g.nodes[ni];
  if(n.matrix||n.rotation||n.scale||n.translation||n.children||n.skin!==undefined)throw Error('Only untransformed static nodes supported');
  for(const p of g.meshes[n.mesh].primitives) {
   if(p.mode!==undefined&&p.mode!==4)throw Error('Expected triangles');
   meshes.push({positions:values(p.attributes.POSITION),normals:values(p.attributes.NORMAL),uv:values(p.attributes.TEXCOORD_0),
    indices:values(p.indices),material:p.material,joints:[],weights:[]});
  }
 }
 const bounds=[Infinity,Infinity,Infinity,-Infinity,-Infinity,-Infinity];
 for(const m of meshes)for(let i=0;i<m.positions.length;i++) {const c=i%3;bounds[c]=Math.min(bounds[c],m.positions[i]);bounds[c+3]=Math.max(bounds[c+3],m.positions[i]);}
 const cx=(bounds[0]+bounds[3])/2,cz=(bounds[2]+bounds[5])/2;
 let radius=0;
 for(const m of meshes)for(let i=0;i<m.positions.length;i+=3)radius=Math.max(radius,Math.hypot(m.positions[i]-cx,m.positions[i+2]-cz));
 for(const m of meshes)for(let i=0;i<m.positions.length;i+=3) {
  m.positions[i]=(m.positions[i]-cx)/radius;m.positions[i+1]=(m.positions[i+1]-bounds[1])/radius;m.positions[i+2]=(m.positions[i+2]-cz)/radius;
 }
 for(const m of meshes)if(m.positions.some(x=>!Number.isFinite(x))||m.indices.some(i=>i<0||i>=m.positions.length/3))throw Error('Invalid mesh');
 writeFileSync(join(output,`NatureShoreRock${variant}.golfmesh`),JSON.stringify({version:2,coordinateSpace:'sunward-rig',
  source:'Quaternius Stylized Nature MegaKit Standard / CC0 1.0',bones:[],boneLinks:[],inverseBinds:[],materials,meshes}));
 console.log(`NatureShoreRock${variant}: ${meshes.reduce((s,m)=>s+m.indices.length/3,0)} triangles, 1-yard radius`);
}
execFileSync('/usr/bin/sips',['-Z','1024',join(output,'Rocks_Diffuse.png')]);
