import * as THREE from 'three';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import { GLTFExporter } from 'three/addons/exporters/GLTFExporter.js';

const stage = document.getElementById('stage');
const scene = new THREE.Scene();
const renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true });
renderer.setPixelRatio(Math.min(devicePixelRatio, 2));
renderer.outputColorSpace = THREE.SRGBColorSpace;
renderer.toneMapping = THREE.ACESFilmicToneMapping;
renderer.toneMappingExposure = 1.15;
renderer.shadowMap.enabled = true;
renderer.shadowMap.type = THREE.PCFShadowMap;
stage.prepend(renderer.domElement);
renderer.domElement.setAttribute('aria-label', 'Three-dimensional Motion Club character');
const camera = new THREE.PerspectiveCamera(32, 1, .02, 50);
const controls = new OrbitControls(camera, renderer.domElement);
controls.enableDamping = true;
controls.dampingFactor = .09;
controls.minDistance = .35;
controls.maxDistance = 8;
controls.maxPolarAngle = Math.PI * .87;
scene.add(new THREE.HemisphereLight(0xfffcf1, 0xc9d2bd, 2.1));
const key = new THREE.DirectionalLight(0xfff0dc, 3.2);
key.position.set(-2.8, 4.5, 3.8);
key.castShadow = true;
key.shadow.mapSize.set(2048, 2048);
key.shadow.camera.left = key.shadow.camera.bottom = -2;
key.shadow.camera.right = key.shadow.camera.top = 2;
key.shadow.camera.near = .1;
key.shadow.camera.far = 10;
key.shadow.bias = -.0005;
key.shadow.normalBias = .012;
key.shadow.radius = 4;
scene.add(key);
const fill = new THREE.DirectionalLight(0xe3efff, 1.4);
fill.position.set(3, 2.4, 2.5); scene.add(fill);
const rim = new THREE.DirectionalLight(0xffffff, 1.6);
rim.position.set(1.5, 3.1, -3); scene.add(rim);
const floor = new THREE.Mesh(new THREE.PlaneGeometry(20,20), new THREE.ShadowMaterial({ color: 0x526348, opacity: .16 }));
floor.rotation.x = -Math.PI / 2; floor.position.y = -.018; floor.receiveShadow = true; scene.add(floor);

const hero = new THREE.Group();
hero.name = 'MotionClub_RepairedHero_Idle';
hero.rotation.y = Math.PI;
scene.add(hero);
const state = { body: 'Boy', hair: 'Swept', hat: 'Visor', size: .5, hairHex: '593722', skinHex: 'E3AA7A' };
let parts = [], materialInstances = [], atlas, atlasSource, kitMask, skinMask, refs, center, height, showFace = false;
const linear = c => c <= .04045 ? c / 12.92 : Math.pow((c + .055) / 1.055, 2.4);
const srgb = c => { c=Math.max(0,Math.min(1,c));return Math.round(255*(c<=.0031308?c*12.92:1.055*Math.pow(c,1/2.4)-.055)); };
const hexRGB = hex => [0,2,4].map(i=>parseInt(hex.slice(i,i+2),16)/255);
const toLinear = new Float32Array(Array.from({length:256},(_,i)=>linear(i/255)));
const texLoader = new THREE.TextureLoader();
async function texture(name) {
  const t = await texLoader.loadAsync('./assets/'+name);
  t.colorSpace = THREE.SRGBColorSpace; t.flipY = false;
  t.anisotropy = Math.min(8,renderer.capabilities.getMaxAnisotropy());
  return t;
}
function imagePixels(image) {
  const c=document.createElement('canvas');c.width=c.height=1024;
  const ctx=c.getContext('2d',{willReadFrequently:true});ctx.drawImage(image,0,0,1024,1024);
  return ctx.getImageData(0,0,1024,1024).data;
}
function tintAtlas() {
  const out = new Uint8ClampedArray(atlasSource);
  const targets=[null,null,hexRGB(state.hairHex).map(linear),hexRGB(state.skinHex).map(linear)];
  const reference=[refs.shirt,refs.shorts,refs.hair,refs.skin];
  for(let i=0;i<out.length;i+=4) {
    const w=[kitMask[i]/255,kitMask[i+1]/255,kitMask[i+2]/255,skinMask[i]/255];
    if(!w[2]&&!w[3]) continue;
    const c=[toLinear[out[i]],toLinear[out[i+1]],toLinear[out[i+2]]];
    const lum=c[0]*.2126+c[1]*.7152+c[2]*.0722;
    for(let k=2;k<4;k++) if(w[k]) {
      const l=k===3?reference[3]+(lum-reference[3])*.45:lum;
      const shade=Math.min(1.35,Math.max(.25,l/Math.max(reference[k],.01)));
      for(let j=0;j<3;j++) c[j]+=(targets[k][j]*shade-c[j])*w[k];
    }
    for(let j=0;j<3;j++) out[i+j]=srgb(c[j]);
  }
  const canvas=atlas.image, ctx=canvas.getContext('2d');
  ctx.putImageData(new ImageData(out,1024,1024),0,0);atlas.needsUpdate=true;
}
function material(info, part) {
  const c=info.color;
  const mat=new THREE.MeshStandardMaterial({
    name:info.name, color:new THREE.Color().setRGB(c[0],c[1],c[2],THREE.SRGBColorSpace),
    roughness:Math.max(.7,1-info.smoothness), metalness:0,
    side:part.name.startsWith('Hair_') ? THREE.FrontSide : THREE.DoubleSide,
    transparent:info.transparent, opacity:info.transparent?c[3]:1,
    depthWrite:!info.transparent
  });
  if(info.texture==='atlas') { mat.map=atlas;mat.color.set(0xffffff); }
  else if(info.texture==='iris') { mat.map=iris;mat.color.set(0xffffff);mat.roughness=.38; }
  materialInstances.push({mat, name:info.name});
  return mat;
}
let iris;
function updateColors() {
  const hair=hexRGB(state.hairHex), skin=hexRGB(state.skinHex);
  for(const {mat,name} of materialInstances) {
    let c;
    if(name.startsWith('Hero_01_HairTuft') || name.startsWith('Hero side hair')) c=hair;
    else if(name.startsWith('Hero neck skin')) c=skin;
    else if(name.startsWith('Hero lid skin')) c=skin.map((v,i)=>v*[.97,.9,.86][i]);
    else if(name.includes('CoveredFoundation')) c=hair.map(v=>v*.8);
    if(c) mat.color.setRGB(...c,THREE.SRGBColorSpace);
  }
  tintAtlas();
}
function visible(part) {
  if(part.hat && (part.hat==='Worn' ? state.hat==='None' : part.hat!==state.hat)) return false;
  if(part.hair && part.hair!==state.hair) return false;
  if(part.body && part.body!==state.body) return false;
  return true;
}
function updateParts() { for(const {mesh,part} of parts) mesh.visible=visible(part); }
function updateSize() {
  for(const {mesh,base,slim,broad} of parts) {
    if(!slim) continue;
    const pos=mesh.geometry.attributes.position, delta=state.size<.5?slim:broad, weight=Math.abs(state.size-.5)*2;
    for(let j=0;j<base.length;j++) pos.array[j]=base[j]+delta[j]*weight;
    pos.needsUpdate=true;mesh.geometry.computeBoundingSphere();
  }
  document.getElementById('size-value').textContent=Math.round(state.size*100)+'%';
}
function aim(view='threequarter') {
  if(!center) return;
  const target = showFace ? new THREE.Vector3(center.x,height*.87,center.z) : center;
  const distance=showFace?height*1.05:height*2.35;
  const yaw={front:0, side:Math.PI/2, back:Math.PI,threequarter:.36}[view];
  camera.position.copy(target).add(new THREE.Vector3(Math.sin(yaw)*distance,showFace?.025*height:.10*height,Math.cos(yaw)*distance));
  controls.target.copy(target); controls.update();
  document.querySelectorAll('[data-view]').forEach(b=>b.setAttribute('aria-pressed',String(b.dataset.view===view)));
}
function swatches(id,colors,key) {
  const parent=document.getElementById(id);
  for(const [label,hex] of colors) {
    const b=document.createElement('button');b.className='swatch';b.style.background='#'+hex;
    b.title=label;b.setAttribute('aria-label',label);b.setAttribute('aria-pressed',String(state[key]===hex));
    b.onclick=()=>{state[key]=hex;parent.querySelectorAll('button').forEach(x=>x.setAttribute('aria-pressed',String(x===b)));updateColors();};
    parent.append(b);
  }
}
swatches('hair-colors',[['Black','211C1A'],['Brown','593722'],['Auburn','A54D2B'],['Blond','D8B365'],['Silver','BCC0C5']], 'hairHex');
swatches('skin-colors',[['Fair','FFEBDA'],['Light','F2C9A5'],['Current peach','E3AA7A'],['Brown','B7744A'],['Deep','74432C']], 'skinHex');
for(const id of ['body','hair','hat']) document.getElementById(id).onchange=e=>{state[id]=e.target.value;updateParts();};
document.getElementById('size').oninput=e=>{state.size=Number(e.target.value)/100;updateSize();};
document.querySelectorAll('[data-view]').forEach(b=>b.onclick=()=>aim(b.dataset.view));
document.getElementById('detail').onclick=e=>{showFace=!showFace;e.currentTarget.setAttribute('aria-pressed',String(showFace));e.currentTarget.textContent=showFace?'Full body':'Face';aim('front');};
document.getElementById('download').onclick=async()=>{
  const status=document.getElementById('status');status.textContent='Preparing model…';
  try {
    const data=await new GLTFExporter().parseAsync(hero,{binary:true,onlyVisible:true});
    const response=await fetch('/export',{method:'POST',headers:{'Content-Type':'model/gltf-binary'},body:data});
    if(!response.ok) throw new Error('Local save failed. Start the viewer with server.py.');
    const {url}=await response.json();
    status.replaceChildren(document.createTextNode('Exported current look and idle pose. '));
    const a=document.createElement('a');a.href=url;a.download='MotionClub_RepairedHero.glb';a.textContent='Open GLB';status.append(a);
  } catch(e) { status.textContent='Export failed: '+e.message; }
};

try {
  const [manifest,bin,source,mk,sk,eye,ref]=await Promise.all([
    fetch('./assets/HeroMenu.json').then(r=>r.json()), fetch('./assets/HeroMenu.bin').then(r=>r.arrayBuffer()),
    texture('HeroV4_Atlas.png'),texture('HeroV4_KitMask.png'),texture('HeroV4_SkinMask.png'),texture('HeroV4_Iris.png'),
    fetch('./assets/HeroV4_KitRef.json').then(r=>r.json())
  ]);
  iris=eye; refs=ref; atlasSource=imagePixels(source.image);kitMask=imagePixels(mk.image);skinMask=imagePixels(sk.image);
  const canvas=document.createElement('canvas');canvas.width=canvas.height=1024;
  atlas=new THREE.CanvasTexture(canvas);atlas.colorSpace=THREE.SRGBColorSpace;atlas.flipY=false;atlas.anisotropy=8;
  const mats=new Map(manifest.materials.map(m=>[m.name,m]));
  for(const part of manifest.parts) {
    const f32=(off,components)=>new Float32Array(bin,off,part.vertexCount*components);
    const base=f32(part.positionOffset,3), g=new THREE.BufferGeometry();
    g.setAttribute('position',new THREE.BufferAttribute(new Float32Array(base),3));
    g.setAttribute('normal',new THREE.BufferAttribute(f32(part.normalOffset,3),3));
    g.setAttribute('uv',new THREE.BufferAttribute(f32(part.uvOffset,2),2));
    const index=new Uint32Array(part.submeshes.reduce((n,s)=>n+s.indexCount,0));let off=0;
    part.submeshes.forEach((sub,i)=>{index.set(new Uint32Array(bin,sub.indexOffset,sub.indexCount),off);g.addGroup(off,sub.indexCount,i);off+=sub.indexCount;});
    g.setIndex(new THREE.BufferAttribute(index,1));
    const mesh=new THREE.Mesh(g,part.submeshes.map(sub=>material(mats.get(sub.material),part)));
    mesh.name=part.name;mesh.castShadow=true;mesh.receiveShadow=false;hero.add(mesh);
    parts.push({part,mesh,base,slim:part.slimOffset?f32(part.slimOffset,3):null,broad:part.broadOffset?f32(part.broadOffset,3):null});
  }
  updateParts();updateSize();updateColors();hero.updateMatrixWorld(true);
  const box=new THREE.Box3();for(const {mesh} of parts) if(mesh.visible) box.expandByObject(mesh);
  height=box.max.y-box.min.y;center=new THREE.Vector3((box.max.x+box.min.x)/2,height*.53,0);
  aim();document.getElementById('loading').hidden=true;
  document.getElementById('status').textContent='Ready · rotate to inspect';
  window.addEventListener('resize',resize);resize();
} catch(e) { document.getElementById('loading').textContent='Could not load character: '+e.message;console.error(e); }
function resize(){ const w=stage.clientWidth,h=stage.clientHeight;renderer.setSize(w,h);camera.aspect=w/h;camera.updateProjectionMatrix(); }
renderer.setAnimationLoop(()=>{controls.update();renderer.render(scene,camera);});
