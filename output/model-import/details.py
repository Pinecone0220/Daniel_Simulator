import bpy,json,collections
arm=next(o for o in bpy.data.objects if o.type=='ARMATURE')
mesh=next(o for o in bpy.data.objects if o.type=='MESH' and len(o.data.vertices)>10000)
weights=collections.Counter()
for v in mesh.data.vertices:
 for g in v.groups: weights[mesh.vertex_groups[g.group].name]+=g.weight
mats=[]
for m in mesh.data.materials:
 ns=m.node_tree.nodes
 mats.append({'name':m.name,'image':[(n.name,n.image.name if n.image else None) for n in ns if n.type=='TEX_IMAGE'],'inputs':[(i.name,str(i.default_value)) for n in ns if n.name=='mmd_shader' for i in n.inputs]})
json.dump({'weights':weights,'mats':mats,'constraints':[(b.name,[(c.type,c.name) for c in b.constraints]) for b in arm.pose.bones if b.constraints]},open('C:/Users/PC/Desktop/Game_Project/3D/Daniel_Simulator/output/model-import/details.json','w'),ensure_ascii=False,indent=2)
