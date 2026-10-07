import bpy, ast, sys, numpy as np
from pathlib import Path
root=Path(sys.argv[sys.argv.index('--')+1]);art=root/'ArtDir/environments/motion-club-3d'
a=ast.parse((art/'tools/build_club_environments.py').read_text());pal=next(ast.literal_eval(n.value) for n in a.body if isinstance(n,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='PAL' for t in n.targets))
b=ast.parse((art/'tools/resort_details.py').read_text());pal.update(next(ast.literal_eval(n.value.args[0]) for n in b.body if isinstance(n,ast.Expr) and isinstance(n.value,ast.Call) and isinstance(n.value.func,ast.Attribute) and n.value.func.attr=='update'))
for room in ['entrance','terrace','locker','loading']:
 path=art/'blender'/('Club_'+room+'.blend');bpy.ops.wm.open_mainfile(filepath=str(path))
 atlas=bpy.data.images['Club_'+room+'_Albedo'];p=np.empty(2048*2048*4,dtype=np.float32);atlas.pixels.foreach_get(p);p=p.reshape((2048,2048,4))
 for index,(name,(h,tile)) in enumerate((item for item in pal.items() if item[1][1] is None)):
  p[1952:2048,index*64:index*64+64,:]=tuple(int(h[i:i+2],16)/255 for i in (0,2,4))+(1,)
 atlas.pixels.foreach_set(p.ravel());atlas.update();atlas.filepath_raw=str(root/'GolfArcade/Unity/ClubEnvironments'/('Club_'+room+'_Albedo.png'));atlas.save();atlas.pack();bpy.ops.wm.save_as_mainfile(filepath=str(path))
 bpy.context.scene.render.filepath=str(art/'review'/(room+'-geometry.png'));bpy.ops.render.render(write_still=True)
