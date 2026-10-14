from pathlib import Path
import json, subprocess
root=Path('.').resolve();work=root/'work/online-lobby/sim-native';work.mkdir(exist_ok=True)
spec=json.loads(subprocess.check_output(['/usr/bin/ruby','-ryaml','-rjson','-e','puts JSON.generate(YAML.load_file(ARGV[0]))',str(root/'project.yml')]));spec['name']='GolfArcadeLobbyProof'
for name,target in spec['targets'].items():
 for src in target['sources']:
  if 'path' in src:src['path']=str(root/src['path'])
 for key in ['INFOPLIST_FILE','CODE_SIGN_ENTITLEMENTS','SWIFT_OBJC_BRIDGING_HEADER']:
  if key in target.get('settings',{}).get('base',{}):target['settings']['base'][key]=str(root/target['settings']['base'][key])
spec['targets']['GolfArcade']['sources'][0]['excludes'].append('Unity/SportsRuntime.mm')
bridge=(root/'GolfArcade/Unity/SportsRuntime.mm').read_text().replace(' && !TARGET_OS_SIMULATOR','')
bridge=bridge.replace('#import "SportsRuntime.h"', '#import "'+str(root/'GolfArcade/Unity/SportsRuntime.h')+'"')
(work/'SportsRuntimeSimulator.mm').write_text(bridge)
spec['targets']['GolfArcade']['sources'].append({'path':str(work/'SportsRuntimeSimulator.mm')})
export=root/'work/online-lobby/UnityClone/Builds/LobbySimulator'
spec['projectReferences']={'Unity':{'path':str(export/'Unity-iPhone.xcodeproj')}}
spec['targets']['GolfArcade']['sources'].append({'path':str(export/'Data'),'type':'folder','buildPhase':'resources'})
spec['targets']['GolfArcade']['dependencies']=[{'target':'Unity/UnityFramework','embed':True}]
spec['targets']['GolfArcade']['settings']['base']['ENABLE_BITCODE']=False
spec['targets']['GolfArcade']['settings']['base']['OTHER_LDFLAGS']=['$(inherited)','-ObjC']
(work/'project.json').write_text(json.dumps(spec,indent=2))
