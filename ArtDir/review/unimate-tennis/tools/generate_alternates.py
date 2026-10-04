"""UniMate inference on V5's own rest skeleton, using upstream canonicalization and conditioning.
No existing gameplay motion is used as generation input.
"""
import os,sys,json,time,gc
from pathlib import Path
U=Path('/Users/adnanyonathan/Documents/Codex/Tools/UniMate');sys.path.insert(0,str(U));os.chdir(U)
O=Path(__file__).resolve().parent.parent/'alternate-prompts'
os.environ['MPLCONFIGDIR']=str(U/'.cache/matplotlib')
os.environ['PYTORCH_ENABLE_MPS_FALLBACK']='1'
import numpy as np,torch
from scipy.spatial.transform import Rotation
from Quaternions import Quaternions
from Animation import positions_global,rotations_global
from data_process.utils.motion_features import process_tpose,build_topology_cond
from unimate.configs.schema import MainConfig
from unimate.dataset.transforms import apply_normalization,build_parent_features
from unimate.dataset.mixture.collate import mixture_batch_collate
from unimate.models.factory import create_model,create_transport
from unimate.models.flow.transport import Sampler
from unimate.inference.sample import _load_checkpoint
from unimate.inference.generate import ClassifierFreeSampleModel
from unimate.utils.motion_utils import recover_unimate_anim_from_rot
from transformers import T5Tokenizer,T5EncoderModel

# torchdiffeq defaults to float64 solver bookkeeping, unsupported by Apple MPS.
# Keep the upstream adaptive Dopri5 algorithm/tolerances, with float32 time state.
import unimate.models.flow.integrators as flow_integrators
_original_odeint=flow_integrators.odeint
def _mac_odeint(*args,**kwargs):
    kwargs['options']={**kwargs.get('options',{}),'dtype':torch.float32}
    return _original_odeint(*args,**kwargs)
flow_integrators.odeint=_mac_odeint
torch.set_num_threads(6)
raw=json.loads((O/'skeleton-rest.json').read_text());bones=raw['bones']; names=[b['name'] for b in bones]
parents=np.array([names.index(b['parent']) if b['parent'] else -1 for b in bones]);world=np.array([b['world_rest'] for b in bones])
local=np.array([np.linalg.inv(world[p])@world[j] if p>=0 else world[j] for j,p in enumerate(parents)])
q=Rotation.from_matrix(local[:,:3,:3]).as_quat()[:,[3,0,1,2]];pos=local[:,:3,3].copy()
axis=Quaternions.from_euler(np.array([[-np.pi/2,0,0]]));q[0]=(axis*Quaternions(q[0:1])).qs[0];pos[0]=(axis*pos[0:1])[0]
tpos={'names':np.array(names),'parents':parents,'rest_local_pos':pos,'rest_local_rot':q,'fps':np.array(30)}
canon,offsets,scale,ground,parents,names,fps,bfs,face,body=process_tpose(tpos,face_joints={'r_hip':{'raw':'UpperLeg.R'},'l_hip':{'raw':'UpperLeg.L'}})
def clean(n):
 side='Left ' if n.endswith('.L') else 'Right ' if n.endswith('.R') else ''
 core=n.split('.')[0]
 label={'Hips':'Hips','Spine':'Spine','Chest':'Spine','Neck':'Neck','Head':'Head','Shoulder':'Shoulder','UpperArm':'Upper Arm','LowerArm':'Forearm','Hand':'Hand','UpperLeg':'Upper Leg','LowerLeg':'Lower Leg','Foot':'Foot','Toes':'Toe'}.get(core)
 if label is None:label=core.rstrip('12')+' Finger'
 return side+label
c=build_topology_cond('HeroV5',parents,offsets,names,[clean(n) for n in names],positions_global(canon)[0],canon.rotations.qs[0],rotations_global(canon).qs[0],face_joint_idxs=face,scale_factor=scale)
np.save(O/'cond.npy',{'HeroV5':c});N=len(names);T=60
config=MainConfig.from_json(U/'outputs/unimate_uniml3d_f60_v2/config.json')
stats=np.load(U/'outputs/unimate_uniml3d_f60_v2/dataset_stats.npy',allow_pickle=True).item()['mixamo']
mean=np.tile(stats['mean_local'],(N,1));std=np.tile(stats['std_local'],(N,1));mean[0]=stats['mean_root'];std[0]=stats['std_root']
pad=np.zeros((N,9));pad[:,:6]=Quaternions.id(1).rotation_matrix(cont6d=True)[0]
tpos12=apply_normalization(np.concatenate([c['tpos_first_frame'],pad],axis=-1),mean,std)
prompts={
 '02_forehand':'A person swings a tennis racket with their right hand to hit a tennis ball.',
 '03_backhand':'A person hits a tennis ball with a two handed backhand swing.'}
(O/'prompts.json').write_text(json.dumps(prompts,indent=2))
# Same frozen T5 encoder and masked mean pooling as the upstream conditioner.
print('Loading text encoder',flush=True)
tokenizer=T5Tokenizer.from_pretrained(U/'models/flan-t5-base',local_files_only=True)
encoder=T5EncoderModel.from_pretrained(U/'models/flan-t5-base',local_files_only=True).eval()
def encode(texts):
 inp=tokenizer(texts,padding=True,return_tensors='pt')
 with torch.inference_mode():h=encoder(**inp).last_hidden_state
 return [(row[mask.bool()].numpy()) for row,mask in zip(h,inp['attention_mask'])]
all_text=c['clean_joint_names']+list(prompts.values());emb=encode(all_text)
joint_emb=np.stack([x.mean(0) for x in emb[:N]])
caption_embs=emb[N:];del encoder;gc.collect()
print('Loading UniMate EMA',flush=True)
model=create_model(config.dataset,config.model);_load_checkpoint(model,str(U/'outputs/unimate_uniml3d_f60_v2/checkpoints/checkpoint_step_100000.pt'),config)
device='mps' if torch.backends.mps.is_available() else 'cpu';model=model.to(device).eval();guided=ClassifierFreeSampleModel(model,3.)
sampler=Sampler(create_transport(training_config=config.training));sample_fn=sampler.sample_ode(sampling_method='dopri5',num_steps=50)
(O/'motions').mkdir(exist_ok=True);report={'device':device,'source_sha256':raw['source_sha256'],'joints':N,'frames':T,'native_fps':30,'clean_joint_names':dict(zip(names,c['clean_joint_names'])),'clips':{}}
for i,(name,prompt) in enumerate(prompts.items()):
 out=O/'motions'/f'{name}.npy'
 if out.exists():features=np.load(out);seconds=0
 else:
  batch={'motion':np.zeros((T,N,12)), 'max_joints':config.dataset.max_joints,'motion_length':T,'max_motion_length':T,'parents':parents,'edge_indexs':c['edge_indexs'],'tpos_first_frame':tpos12,'tpos_first_frame_parents':build_parent_features(tpos12,parents)['tpos_first_frame_parents'],'offsets':c['tpos_offsets'],'joint_graph_dist':c['joint_graph_dists'],'joint_relations':c['joint_relations'],'joint_depths':c['joint_depths'],'spectral_feats':c['spectral_feats'],'joint_names_emb':joint_emb,'start_idx':0,'mean':mean,'std':std,'caption_emb':caption_embs[i].mean(0),'caption_tokens':caption_embs[i],'caption':prompt,'object_type':'HeroV5'}
  _,condition=mixture_batch_collate([batch]);condition={k:v.to(device) if torch.is_tensor(v) else v for k,v in condition.items()}
  torch.manual_seed(1730+i);noise=torch.randn((1,config.dataset.max_joints,12,T),device=device)
  print('GENERATING',name,device,flush=True);start=time.time()
  with torch.inference_mode():generated=sample_fn(noise,guided,cond=condition)[-1]
  features=generated[0,:N].cpu().numpy().transpose(2,0,1)*std[None]+mean[None];seconds=time.time()-start
  assert np.isfinite(features).all();np.save(out,features)
 # Exact upstream FK decoder, with rest-frame conjugation used by animate_motion.
 anim=recover_unimate_anim_from_rot(features,parents,c['tpos_offsets']);bind=Quaternions(c['tpos_global_rotations'])
 basis=(-bind)[None]*anim.rotations*bind[None]
 rootloc=(-bind[0:1])*(anim.positions[:,0]-c['tpos_offsets'][0])/scale
 # Quaternion continuity for Blender's interpolation, no swing curve edits.
 rots=basis.qs.copy()
 for f in range(1,T):
  flip=np.sum(rots[f]*rots[f-1],axis=-1)<0;rots[f,flip]*=-1
 data={'names':list(names),'fps':30,'rotation_quaternion':rots.tolist(),'hips_location':rootloc.tolist(),'prompt':prompt,'seed':1730+i}
 (O/'motions'/f'{name}.json').write_text(json.dumps(data))
 report['clips'][name]={'seconds':seconds,'prompt':prompt,'seed':1730+i,'finite':True}
 (O/'generation-report.json').write_text(json.dumps(report,indent=2));print('SAVED',name,round(seconds,1),flush=True)
print('ALL_MOTIONS_DONE',flush=True)
