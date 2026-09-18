import {readFileSync} from 'node:fs';
import {createHash} from 'node:crypto';
const root=new URL('../GolfArcade/Resources/Golfer/',import.meta.url);
const expected={
 'ResortGolfer.golfmesh':'db638225173261f8cdf64d4a5bbbd32a05ccafb971499aea077e2c1013dc4218',
 'GolferEyes.png':'d08e3356a83211bc6ca21fe3a8e39f4b5c1a3b8f85457fc2c0fb57be09935025',
 'Quaternius-LICENSE.txt':'0f4beaf0fe360a7732e58bbe3dbf60a2422367fbea60cb9ea4add968f383268e'
};
for(const [file,hash] of Object.entries(expected)) {
 const actual=createHash('sha256').update(readFileSync(new URL(file,root))).digest('hex');
 if(actual!==hash) throw new Error(`Asset changed without provenance review: ${file}`);
}
const asset=JSON.parse(readFileSync(new URL('ResortGolfer.golfmesh',root),'utf8'));
if(asset.version!==1 || asset.bones.length!==65 || asset.inverseBinds.length!==65*16) throw new Error('Invalid rig');
for(const mesh of asset.meshes) {
 const count=mesh.positions.length/3;
 if(mesh.normals.length!==count*3 || mesh.joints.length!==count*4 || mesh.weights.length!==count*4) throw new Error('Invalid mesh lengths');
 if(mesh.indices.some(i=>i<0||i>=count) || mesh.joints.some(i=>i<0||i>=65)) throw new Error('Invalid skin index');
 for(let i=0;i<count;i++) {
  const sum=mesh.weights.slice(i*4,i*4+4).reduce((a,b)=>a+b,0);
  if(Math.abs(sum-1)>0.001) throw new Error(`Unnormalized skin weights ${i}`);
 }
}
console.log('PASS: free-pack provenance hashes, 65-bone mesh and normalized skin weights');
