import bpy, math, os
from mathutils import Vector
from math import sin, cos, pi
ROOT=os.path.dirname(os.path.abspath(__file__))
OUT=os.path.join(ROOT,'Exports')
os.makedirs(OUT,exist_ok=True)
# Original asset is built in its own collection and never linked into the existing gameplay scene.
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
for datablocks in (bpy.data.materials,): 
    for material in list(datablocks):
        if material.users==0: datablocks.remove(material)
def mat(name, color, metallic=0, roughness=.72):
    m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
    b=m.node_tree.nodes.get('Principled BSDF');b.inputs['Base Color'].default_value=(*color,1);b.inputs['Metallic'].default_value=metallic;b.inputs['Roughness'].default_value=roughness
    return m
oak=mat('Warm maple hanger',(.48,.27,.12))
hookmat=mat('Brushed steel hook',(.28,.39,.43),.78,.28)
cloth=mat('Mint cotton shirt',(.24,.66,.55))
shadow=mat('Stitched seam',(.12,.34,.34))
cream=mat('Ivory buttons',(.94,.85,.64),.12,.38)
red=mat('Laundry tag',(.84,.38,.26))
collection=bpy.data.collections.new('Laundry rack prototype • reversible');bpy.context.scene.collection.children.link(collection)
def move(o,name,material):
 o.name=name
 for c in list(o.users_collection):c.objects.unlink(o)
 collection.objects.link(o)
 o.data.materials.append(material)
 return o
def bevel(o,amount=.025,segments=2):
 b=o.modifiers.new('Soft toy-like edges','BEVEL');b.width=amount;b.segments=segments
 o.modifiers.new('Weighted corner normals','WEIGHTED_NORMAL')
def bar(name, a, b, radius, material):
 a=Vector(a);b=Vector(b);d=b-a
 bpy.ops.mesh.primitive_cylinder_add(vertices=10,radius=radius,depth=d.length,location=(a+b)/2)
 o=bpy.context.object;o.rotation_mode='QUATERNION';o.rotation_quaternion=d.to_track_quat('Z','Y');move(o,name,material);bevel(o,.009,2);return o
def path(name, coords, radius, material, cyclic=False):
 cu=bpy.data.curves.new(name,'CURVE');cu.dimensions='3D';cu.resolution_u=12;cu.bevel_depth=radius;cu.bevel_resolution=3
 s=cu.splines.new('BEZIER');s.bezier_points.add(len(coords)-1)
 for bp,co in zip(s.bezier_points,coords):bp.co=co;bp.handle_left_type='AUTO';bp.handle_right_type='AUTO'
 s.use_cyclic_u=cyclic;o=bpy.data.objects.new(name,cu);collection.objects.link(o);o.data.materials.append(material);return o
# Shirt silhouette is broad at sleeves and taper-shaped at its hem.
outline=[(-.34,1.18),(-.58,1.30),(-.76,1.06),(-.55,.86),(-.49,.94),(-.49,.12),(.49,.12),(.49,.94),(.55,.86),(.76,1.06),(.58,1.30),(.34,1.18),(.22,1.33),(-.22,1.33)]
verts=[(x,y,z) for y in (-.075,.075) for x,z in outline];n=len(outline)
faces=[tuple(range(n-1,-1,-1)),tuple(range(n,2*n))]
for i in range(n):faces.append((i,(i+1)%n,(i+1)%n+n,i+n))
me=bpy.data.meshes.new('Tailored short-sleeve shirt mesh');me.from_pydata(verts,[],faces);me.materials.append(cloth);me.update();shirt=bpy.data.objects.new('01 • Mint work shirt',me);collection.objects.link(shirt);bevel(shirt,.035,3)
# Neckline, inner collar, shoulder seam and two sleeves, modelled as flat inset pieces.
def poly_panel(name, points, y, depth, material, bevelsize=.008):
 verts=[(x,yy,z) for yy in (y,y+depth) for x,z in points];n=len(points)
 faces=[tuple(range(n-1,-1,-1)),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
 me=bpy.data.meshes.new(name+' mesh');me.from_pydata(verts,[],faces);me.materials.append(material);me.update();o=bpy.data.objects.new(name,me);collection.objects.link(o);bevel(o,bevelsize,2)
poly_panel('Folded collar • left', [(-.24,1.34),(-.07,1.30),(0,1.16),(-.14,1.22)],-.105,.025,cream)
poly_panel('Folded collar • right',[(.24,1.34),(.07,1.30),(0,1.16),(.14,1.22)],-.105,.025,cream)
poly_panel('Chest pocket',[ (.19,.99),(.39,.99),(.39,.80),(.19,.80)],-.103,.018,shadow)
poly_panel('Laundry name tag',[ (.22,.95),(.35,.95),(.35,.87),(.22,.87)],-.124,.012,red,.004)
for z in (.99,.82,.65,.48):
 bpy.ops.mesh.primitive_uv_sphere_add(segments=10,ring_count=6,radius=.026,location=(.02,-.12,z))
 o=move(bpy.context.object,'Ivory shirt button',cream);o.scale=(1,.42,1)
# Rounded triangular maple hanger and its steel hook.
path('02 • One-piece wooden hanger',[(0,.10,1.48),(-.91,.10,1.44),(0,.10,.86),(.91,.10,1.44)],.045,oak)
# Small inset maple grain bars
for x in (-.65,.53):
    path('Wood grain detail',[(x,.05,1.40),(x+.12,.05,1.36)],.006,cream)
path('03 • Bent steel hanging hook',[(0,.10,1.45),(0,.10,1.66),(.04,.10,1.76),(.16,.10,1.77),(.23,.10,1.71),(.23,.10,1.61),(.18,.10,1.56)],.022,hookmat)
# Logo-free plain cotton folds, stitch marks, hem.
path('Double hem stitching', [(-.43,-.119,.17),(-.2,-.119,.17),(0,-.119,.17),(.2,-.119,.17),(.43,-.119,.17)],.006,cream)
root=bpy.data.objects.new('Hanger & shirt • removable prototype',None);collection.objects.link(root)
for o in list(collection.objects):
 if o is root: continue
 o.parent=root
# Render-only setup in the prototype file makes it easy to inspect without touching Unity.
scene=bpy.context.scene
scene.render.engine='CYCLES';scene.cycles.samples=24
scene.render.resolution_x=760;scene.render.resolution_y=760;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.render.film_transparent=False
world=bpy.data.worlds.new('Soft studio');scene.world=world;world.use_nodes=True
world.node_tree.nodes['Background'].inputs['Color'].default_value=(.31,.40,.40,1);world.node_tree.nodes['Background'].inputs['Strength'].default_value=.65
def area(name,loc,power,size):
 bpy.ops.object.light_add(type='AREA',location=loc);o=bpy.context.object;o.name=name;o.data.energy=power;o.data.shape='DISK';o.data.size=size;o.rotation_euler=(Vector((0,0,.9))-o.location).to_track_quat('-Z','Y').to_euler();collection.objects.link(o);o.users_collection[0].objects.unlink(o)
area('Large softbox',(-2,-4,4),430,3)
area('Front fill',(3,-2,2.5),250,2)
bpy.ops.object.camera_add(location=(2.5,-5,2.55));cam=bpy.context.object;cam.name='Prototype preview camera';cam.rotation_euler=(Vector((0,0,.92))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=2.35;scene.camera=cam;collection.objects.link(cam);cam.users_collection[0].objects.unlink(cam)
scene.render.filepath=os.path.join(OUT,'HangerClothingPreview.png')
bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT,'HangerClothingPrototype.blend'))
# Export the product collection as an FBX for optional Unity import.
bpy.ops.object.select_all(action='DESELECT')
for obj in list(collection.objects):
 if obj.type in {'MESH','CURVE','EMPTY'}: obj.select_set(True)
root.select_set(True);bpy.context.view_layer.objects.active=root
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,'HangerClothingPrototype.fbx'),use_selection=True,apply_scale_options='FBX_SCALE_ALL',object_types={'MESH','EMPTY'},add_leaf_bones=False,bake_anim=False)
