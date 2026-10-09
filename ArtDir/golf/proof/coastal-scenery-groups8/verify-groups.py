from pathlib import Path
from collections import defaultdict,Counter
import json,math,hashlib,argparse,numpy as np
p=argparse.ArgumentParser();p.add_argument('--repo',required=True);p.add_argument('--out',required=True);args=p.parse_args();R=Path(args.repo);O=Path(args.out);STAGE=R/'ArtDir/golf'
old=json.loads((O/'original.json').read_text());new=json.loads((O/'candidate.json').read_text());manifest=json.loads((STAGE/'reference-hole12-manifest.json').read_text());S=manifest['runtimeFitYardsPerMeter'];TOL_YARDS=.005;fail=[]
def bounds(points):
 v=np.array(points,dtype=float);return [v.min(0).tolist(),v.max(0).tolist()]
def midpoint(b):return [(a+c)/2 for a,c in zip(*b)]
def half(b):return ((b[1][0]-b[0][0])+(b[1][2]-b[0][2]))*.25
def union(items):return bounds([p for b in items for p in b])
def connected(mesh):
 ids={};w=[]
 for v in mesh['localVertices']:
  key=tuple(round(x*1000) for x in v)
  if key not in ids:ids[key]=len(ids)
  w.append(ids[key])
 parent=list(range(len(ids)))
 def find(i):
  while parent[i]!=i:parent[i]=parent[parent[i]];i=parent[i]
  return i
 for poly in mesh['faces']:
  a=find(w[poly[0]])
  for i in poly[1:]:parent[find(w[i])]=a
 out=defaultdict(list)
 for i,j in enumerate(w):out[find(j)].append(i)
 return list(out.values())
def parts(mesh):
 V=mesh['worldVertices']
 return [{'ids':ii,'bounds':bounds([[V[i][0]*S,V[i][2]*S,V[i][1]*S] for i in ii])} for ii in connected(mesh)]
def standing(parts):
 pp=sorted(parts,key=lambda p:half(p['bounds']),reverse=True);out=[];locations=[]
 for p in pp:
  c=midpoint(p['bounds']);h=half(p['bounds']);into=-1
  for i,(at,width) in enumerate(locations):
   if math.hypot(at[0]-c[0],at[1]-c[2])<min(1.2,.5*max(width,h)):into=i;break
  if into<0:out.append([p]);locations.append(([c[0],c[2]],h))
  else:out[into].append(p)
 return out
def descriptor(mesh,ii,group):
 name=mesh['name'];kind='Tree' if name.startswith('TREE') else 'Bush' if name.startswith(('SHRUB','BUSH')) else 'Rock' if name.startswith('ROCK') else 'Wall'
 bb=[p['bounds'] for p in group];b=union(bb);c=midpoint(b);radius=.5*(min if kind=='Wall' else max)(b[1][0]-b[0][0],b[1][2]-b[0][2]);trunk=0;cb=0;cone=False
 if kind=='Tree':
  widest=max(map(half,bb));cb=float('inf');radius=0
  for p in bb:
   if half(p)<.35*widest:trunk=max(trunk,half(p))
   else:
    cb=min(cb,p[0][1])
    if half(p)>=radius:radius=half(p);c=midpoint(p)
  if trunk<=0 or math.isinf(cb):radius=widest;cb=b[0][1]+.3*(b[1][1]-b[0][1]);trunk=max(.1,min(.35,.1*widest))
  trunk=max(.08,min(.5,trunk));cone=b[1][1]-cb>1.25*2*radius
 elif radius<.25:return None
 return {'source':name,'group':ii,'kind':kind,'X':c[0],'D':c[2],'Base':b[0][1],'Top':b[1][1],'Radius':radius,'TrunkRadius':trunk,'CrownBase':cb,'Cone':cone,'pieceCount':len(group)}
def scan(data):
 rows=[];byMesh={};groupsByMesh={}
 for mesh in data['meshes']:
  ps=parts(mesh);gs=[ps] if mesh['name'].startswith('LIGHTHOUSE') else standing(ps);groupsByMesh[mesh['name']]=gs;rr=[]
  for i,g in enumerate(gs):
   d=descriptor(mesh,i,g)
   if d:rows.append(d);rr.append(d)
  byMesh[mesh['name']]=rr
 return rows,byMesh,groupsByMesh
orig,originalByMesh,originalGroups=scan(old);cand,candidateByMesh,candidateGroups=scan(new)
expectedCounts={'Tree':43,'Bush':74,'Rock':43,'Wall':1};countOld=dict(Counter(x['kind'] for x in orig));countNew=dict(Counter(x['kind'] for x in cand))
if countOld!=expectedCounts:fail.append({'reason':'original CPU replay mismatch','actual':countOld})
if countNew!=expectedCounts:fail.append({'reason':'candidate CPU replay mismatch','actual':countNew})
oldMeshes={x['name']:x for x in old['meshes']};newMeshes={x['name']:x for x in new['meshes']};rigidBySource=defaultdict(list)
for entry in manifest['rigidScenery']:rigidBySource[entry['object']].append(entry)
rows=[];usedRigid=defaultdict(set);usedCandidate=set()
for source in ['LIGHTHOUSE','ROCKS','SHRUBS','TREES']:
 mesh=oldMeshes[source]
 for gi,group in enumerate(originalGroups[source]):
  oldRecord=descriptor(mesh,gi,group)
  if oldRecord is None:continue
  indices=sorted(i for p in group for i in p['ids']);V=np.array([mesh['worldVertices'][i] for i in indices]);pivot=(V.min(0)+V.max(0))/2
  manifestGi,rig=min(enumerate(rigidBySource[source]),key=lambda entry:np.linalg.norm(pivot-np.array(entry[1]['pivotBefore'])))
  if manifestGi in usedRigid[source]:fail.append({'reason':'duplicate rigid identity assignment','source':source,'originalIdentity':gi,'manifestIdentity':manifestGi})
  usedRigid[source].add(manifestGi);dy=rig['translationY']
  frozen=source in ('SHRUBS','TREES');name=('SHRUB_REFERENCE_' if source=='SHRUBS' else 'TREE_REFERENCE_')+f'{manifestGi:03}' if frozen else source
  if name not in newMeshes:fail.append({'reason':'missing frozen identity','source':source,'identity':gi,'candidate':name});continue
  candidates=candidateByMesh[name]
  if frozen and len(candidates)!=1:fail.append({'reason':'phantom within frozen identity','source':source,'identity':gi,'candidateCount':len(candidates)})
  nr=candidates[0] if frozen else min(candidates,key=lambda d:math.hypot(d['X']-oldRecord['X'],d['D']-(oldRecord['D']+dy*S)),default=None)
  if nr is None:fail.append({'reason':'missing unsplit record','source':source,'identity':gi});continue
  key=(nr['source'],nr['group'])
  if key in usedCandidate:fail.append({'reason':'duplicate candidate identity match','key':key})
  usedCandidate.add(key)
  expected={k:oldRecord[k] for k in ['X','D','Base','Top','Radius','TrunkRadius','CrownBase','Cone','pieceCount']};expected['D']+=dy*S
  deltas={k:abs(nr[k]-expected[k]) for k in ['X','D','Base','Top','Radius','TrunkRadius','CrownBase']}
  pivotError=float(np.max(np.abs(pivot-np.array(rig['pivotBefore']))))
  geomError=None;vertexCounts=None;topology=None
  if frozen:
   expectV=V.copy();expectV[:,1]+=dy;actualV=np.array(newMeshes[name]['worldVertices']);vertexCounts=[len(expectV),len(actualV)]
   dist=np.sqrt(np.sum((expectV[:,None,:]-actualV[None,:,:])**2,axis=2));geomError=float(max(dist.min(0).max(),dist.min(1).max()))
   member=set(indices);polygons=[p for p in mesh['faces'] if p[0] in member]
   topology={'expectedPolygonCount':len(polygons),'actualPolygonCount':len(newMeshes[name]['faces']),'expectedTriangleCount':sum(len(p)-2 for p in polygons),'actualTriangleCount':sum(len(p)-2 for p in newMeshes[name]['faces'])}
  record={'source':source,'identity':gi,'frozenManifestIdentity':manifestGi,'candidateSource':name,'original':oldRecord,'expectedRigidTranslationSourceYMetres':dy,'expected':expected,'candidate':nr,'maximumDescriptorDeltaYards':max(deltas.values()),'descriptorDeltasYards':deltas,'manifestPivotErrorMetres':pivotError,'geometryHausdorffErrorMetres':geomError,'vertexCounts':vertexCounts,'topology':topology}
  rows.append(record)
  bad=max(deltas.values())>TOL_YARDS or nr['Cone']!=expected['Cone'] or nr['pieceCount']!=expected['pieceCount'] or pivotError>.0001
  if frozen:bad=bad or geomError>.0001 or vertexCounts[0]!=vertexCounts[1] or topology['expectedPolygonCount']!=topology['actualPolygonCount'] or topology['expectedTriangleCount']!=topology['actualTriangleCount']
  if bad:fail.append({'reason':'identity geometry or descriptor mismatch',**record})
oldMarkers={x['name']:x for x in old['markers']};newMarkers={x['name']:x for x in new['markers']};markerErrors={name:max(abs(a-b) for a,b in zip(rec['position'],newMarkers[name]['position'])) for name,rec in oldMarkers.items()}
if any(v>.00005 for v in markerErrors.values()) or set(oldMarkers)!=set(newMarkers):fail.append({'reason':'marker anchor mismatch','markerErrorsMeters':markerErrors})
if old['sha256']!=manifest['inputSha256'] or new['sha256']!=manifest['fbxSha256']:fail.append({'reason':'input or candidate hash differs from manifest'})
report={'gate':'PASS' if not fail else 'FAIL','scope':'Independent source FBX import + local-space1mm positional weld + ObstacleScan Standing/TreeOf CPU replay; Unity final oracle pending','originalSha256':old['sha256'],'candidateSha256':new['sha256'],'yardScale':S,'counts':{'original':len(orig),'candidate':len(cand)},'countsByKind':{'original':countOld,'candidate':countNew},'identityCount':len(rows),'uniqueCandidateMatches':len(usedCandidate),'identityMatchRule':'Match original physical group bbox to manifest original rigid pivot before warp, then exact named frozen filter. Ordinal sorting of equal-width shrubs is not a stable physical identity.','sourceReferenceMeshCount':len(new['meshes']),'markerMaximumErrorsMetres':markerErrors,'maximumDescriptorDeltaYards':max(x['maximumDescriptorDeltaYards'] for x in rows),'maximumFrozenGroupGeometryErrorMetres':max(x['geometryHausdorffErrorMetres'] or 0 for x in rows),'maximumManifestPivotErrorMetres':max(x['manifestPivotErrorMetres'] for x in rows),'changedBeyond5mmYards':fail,'allIdentityMatches':rows}
(O/'group-preservation-report.json').write_text(json.dumps(report,indent=2)+'\n')
(O/'original-obstacles.json').write_text(json.dumps(orig,indent=2)+'\n');(O/'candidate-obstacles.json').write_text(json.dumps(cand,indent=2)+'\n')
print(json.dumps({k:v for k,v in report.items() if k not in ('allIdentityMatches','changedBeyond5mmYards')},indent=2))
print('FAILURES',len(fail))
for f in fail[:8]:print(json.dumps(f))
