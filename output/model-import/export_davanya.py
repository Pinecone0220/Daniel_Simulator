import bpy, pathlib, shutil, json
root=pathlib.Path('C:/Users/PC/Desktop/Game_Project/3D/Daniel_Simulator')
dest=root/'Assets/Resources/Characters/DaVanya'
dest.mkdir(parents=True,exist_ok=True)
texdir=dest/'Textures'
texdir.mkdir(exist_ok=True)
source=pathlib.Path('C:/Users/PC/Desktop/Summer Dream_Vanya/Summer Dream_Vanya/tex')
for path in source.glob('*.png'): shutil.copy2(path,texdir/path.name)
arm=next(o for o in bpy.data.objects if o.type=='ARMATURE')
mesh=next(o for o in bpy.data.objects if o.type=='MESH' and len(o.data.vertices)>10000)
# Discard editor-only physics geometry. Export just the character and its armature.
for o in list(bpy.data.objects):
 if o not in (arm,mesh): bpy.data.objects.remove(o,do_unlink=True)
for o in (arm,mesh):
 world=o.matrix_world.copy(); o.parent=None; o.matrix_world=world
 o.hide_set(False); o.hide_viewport=False; o.hide_render=False
for p in arm.pose.bones:
 for c in list(p.constraints): p.constraints.remove(c)
 p.matrix_basis.identity()
arm.animation_data_clear(); mesh.animation_data_clear()
for m in list(mesh.modifiers):
 if m.type!='ARMATURE': mesh.modifiers.remove(m)
mapping={'腰':'Hips','上半身':'Spine','上半身2':'Spine1','上半身3':'Spine2','首':'Neck','頭':'Head','下半身':'LowerBody'}
for side,word in [('L','Left'),('R','Right')]:
 mapping.update({f'腕.{side}':word+'Arm',f'ひじ.{side}':word+'ForeArm',f'手首.{side}':word+'Hand',f'足.{side}':word+'UpLeg',f'ひざ.{side}':word+'Leg',f'足首.{side}':word+'Foot',f'つま先.{side}':word+'ToeBase'})
 for finger,eng in [('親指','Thumb'),('人指','Index'),('中指','Middle'),('薬指','Ring'),('小指','Pinky')]:
  for i,num in enumerate('０１２３'):
   if finger=='親指' and i<3: mapping[f'{finger}{num}.{side}']=word+'Hand'+eng+str(i+1)
   elif finger!='親指' and i>0: mapping[f'{finger}{num}.{side}']=word+'Hand'+eng+str(i)
merge={}
for side in ('L','R'):
 for n in ['腕捩','腕捩1','腕捩2','腕捩3']: merge[f'{n}.{side}']=f'腕.{side}'
 for n in ['手捩','手捩1','手捩2','手捩3']: merge[f'{n}.{side}']=f'ひじ.{side}'
 for n,to in [('足D','足'),('ひざD','ひざ'),('足首D','足首'),('足先EX','つま先')]: merge[f'{n}.{side}']=f'{to}.{side}'
for old,new in merge.items():
 vg=mesh.vertex_groups.get(old)
 if vg:
  target=mesh.vertex_groups.get(new) or mesh.vertex_groups.new(name=new)
  for v in mesh.data.vertices:
   w=next((g.weight for g in v.groups if g.group==vg.index),0)
   if w: target.add([v.index],w,'ADD')
  mesh.vertex_groups.remove(vg)
bpy.context.view_layer.objects.active=arm
bpy.ops.object.mode_set(mode='EDIT')
eb=arm.data.edit_bones
for side in ('L','R'):
 for name,parent in [(f'足.{side}','腰'),(f'ひざ.{side}',f'足.{side}'),(f'足首.{side}',f'ひざ.{side}'),(f'つま先.{side}',f'足首.{side}'),(f'ひじ.{side}',f'腕.{side}'),(f'手首.{side}',f'ひじ.{side}')]:
  eb[name].use_connect=False; eb[name].parent=eb[parent]
# Collapsed helper bones no longer influence any vertex.
for name in merge:
 if name in eb:
  bone=eb[name]
  for child in list(bone.children): child.use_connect=False; child.parent=bone.parent
  eb.remove(bone)
bpy.ops.object.mode_set(mode='OBJECT')
for old,new in mapping.items():
 if old in arm.data.bones: arm.data.bones[old].name=new
 vg=mesh.vertex_groups.get(old)
 if vg: vg.name=new
# Normalize the consolidated deform weights.
bpy.context.view_layer.objects.active=mesh
bpy.ops.object.mode_set(mode='WEIGHT_PAINT')
bpy.ops.object.vertex_group_normalize_all(group_select_mode='ALL',lock_active=False)
bpy.ops.object.mode_set(mode='OBJECT')
for m in mesh.data.materials:
 old=m.node_tree.nodes.get('mmd_base_tex')
 image=old.image if old else None
 diffuse=m.node_tree.nodes.get('mmd_shader').inputs['Diffuse Color'].default_value[:]
 alpha=m.node_tree.nodes.get('mmd_shader').inputs['Alpha'].default_value
 m.node_tree.nodes.clear()
 nodes=m.node_tree.nodes
 p=nodes.new('ShaderNodeBsdfPrincipled'); p.inputs['Base Color'].default_value=(1,1,1,1); p.inputs['Roughness'].default_value=.7
 out=nodes.new('ShaderNodeOutputMaterial'); m.node_tree.links.new(p.outputs['BSDF'],out.inputs['Surface'])
 m.diffuse_color=(1,1,1,alpha)
 if image:
  image.filepath=str(texdir/image.name); image.reload()
  n=nodes.new('ShaderNodeTexImage'); n.image=image
  m.node_tree.links.new(n.outputs['Color'],p.inputs['Base Color'])
  m.node_tree.links.new(n.outputs['Alpha'],p.inputs['Alpha'])
 m.use_backface_culling=False
arm.name='DaVanya_Rig'; mesh.name='DaVanya_Body'
# Blender source faces -Y. A half turn places the imported model on the simulator's +Z heading.
for o in (arm,mesh): o.select_set(True)
bpy.context.view_layer.objects.active=arm
bpy.ops.export_scene.fbx(filepath=str(dest/'DaVanya.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,use_mesh_modifiers=False,path_mode='RELATIVE',use_armature_deform_only=False)
print('EXPORTED_DAVANYA',len(mesh.data.vertices),len(arm.data.bones))
