// Offline conversion of the verified CC0 Standard glTF. No runtime downloads or dependency.
// Usage: node scripts/import-golfer.mjs <Standard-pack-directory>
import {readFileSync, writeFileSync, mkdirSync, copyFileSync} from 'node:fs';
import {join, resolve} from 'node:path';
const pack = process.argv[2];
// Version 2 preserves authored PBR materials and embedded texture references.
// The existing Standard-pack directory interface and v1 mesh remain unchanged.
if (pack?.toLowerCase().endsWith('.glb')) {
  await import('./import-sunward.mjs');
  process.exit(0);
}
if (!pack) throw new Error('Supply the extracted official Standard pack directory');
const source = join(pack, 'Base Characters/Godot - UE');
const g = JSON.parse(readFileSync(join(source, 'Superhero_Male_FullBody.gltf')));
const binary = readFileSync(join(source, g.buffers[0].uri));
function values(index) {
  const a=g.accessors[index], b=g.bufferViews[a.bufferView];
  const width={SCALAR:1,VEC2:2,VEC3:3,VEC4:4,MAT4:16}[a.type];
  const size={5121:1,5123:2,5125:4,5126:4}[a.componentType];
  if (!size || !width || a.sparse) throw new Error('Unsupported accessor');
  const read={5121:'readUInt8',5123:'readUInt16LE',5125:'readUInt32LE',5126:'readFloatLE'}[a.componentType];
  const result=[];
  for(let i=0;i<a.count;i++) for(let c=0;c<width;c++) {
    let value=binary[read]((b.byteOffset||0)+(a.byteOffset||0)+i*(b.byteStride||size*width)+c*size);
    if(a.normalized && a.componentType!==5126) value/=a.componentType===5121?255:65535;
    result.push(value);
  }
  return result;
}
const skin=g.skins[0];
const meshes=g.meshes.flatMap((m)=>m.primitives.map(p=>({
  positions:values(p.attributes.POSITION),normals:values(p.attributes.NORMAL),
  uv:values(p.attributes.TEXCOORD_0),joints:values(p.attributes.JOINTS_0),
  weights:values(p.attributes.WEIGHTS_0),indices:values(p.indices),material:p.material
})));
const parent=new Map();
g.nodes.forEach((n,i)=>(n.children||[]).forEach(c=>parent.set(c,i)));
const bones=skin.joints.map(i=>({name:g.nodes[i].name,parent:skin.joints.indexOf(parent.get(i))}));
const asset={version:1,source:'Quaternius Universal Base Characters Standard / CC0',bones,
  inverseBinds:values(skin.inverseBindMatrices),meshes};
const destination=resolve('GolfArcade/Resources/Golfer');
mkdirSync(destination,{recursive:true});
writeFileSync(join(destination,'ResortGolfer.golfmesh'),JSON.stringify(asset));
copyFileSync(join(source,'T_Eye_Brown.png'),join(destination,'GolferEyes.png'));
copyFileSync(join(pack,'License_Standard.txt'),join(destination,'Quaternius-LICENSE.txt'));
console.log(`Imported ${bones.length} bones, ${meshes.reduce((n,m)=>n+m.positions.length/3,0)} vertices`);
