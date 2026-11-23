import bpy,json
from mathutils import Vector
p='C:/Users/PC/Desktop/Game_Project/3D/Daniel_Simulator/output/model-import/inspection.json'
objects=[]
for o in bpy.data.objects:
 d={'name':o.name,'type':o.type,'location':list(o.location),'rotation':list(o.rotation_euler),'scale':list(o.scale),'dimensions':list(o.dimensions),'parent':o.parent.name if o.parent else None}
 if o.type=='MESH': d.update(vertices=len(o.data.vertices),polygons=len(o.data.polygons),materials=[m.name if m else None for m in o.data.materials],groups=[g.name for g in o.vertex_groups],modifiers=[(m.type,m.object.name if m.type=='ARMATURE' and m.object else '') for m in o.modifiers])
 if o.type=='ARMATURE': d['bones']=[{'name':b.name,'parent':b.parent.name if b.parent else None,'head':list(b.head_local),'tail':list(b.tail_local)} for b in o.data.bones]
 objects.append(d)
json.dump({'objects':objects,'images':[(i.name,i.filepath,bool(i.packed_file)) for i in bpy.data.images],'materials':[{'name':m.name,'color':list(m.diffuse_color),'nodes':[(n.type,n.name) for n in m.node_tree.nodes] if m.use_nodes else []} for m in bpy.data.materials]},open(p,'w'),indent=2)
