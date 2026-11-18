"""A reusable low-poly napa cabbage ingredient, in meters."""
import bpy, math, os, random
from mathutils import Vector

root=os.path.dirname(os.path.abspath(__file__))
os.makedirs(root,exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
random.seed(7)

def material(name,color):
    m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
    p=m.node_tree.nodes.get('Principled BSDF')
    p.inputs['Base Color'].default_value=(*color,1)
    p.inputs['Roughness'].default_value=.83
    return m

palette=[material('Cabbage_Stem',(.78,.79,.57)),
         material('Cabbage_PaleLeaf',(.54,.64,.24)),
         material('Cabbage_Leaf',(.24,.40,.07)),
         material('Cabbage_DarkLeaf',(.095,.23,.035)),
         material('Cabbage_Inner',(.70,.74,.36))]
parts=[]
for layer,count in [(0,10),(1,7),(2,4)]:
    for n in range(count):
        angle=math.tau*n/count+layer*.37
        outward=Vector((math.cos(angle),0,math.sin(angle)))
        across=Vector((-math.sin(angle),0,math.cos(angle)))
        length=.50-layer*.055+random.uniform(-.02,.02)
        vertices=[];faces=[];slots=[]
        rows,columns=10,6
        for j in range(rows+1):
            t=j/rows
            y=-.24+length*t
            radius=(.018+.082*math.sin(math.pi*t*.82))*(1-layer*.25)
            # Broad, rippled outer tips, and tightly nested pale inner leaves.
            width=(.007+.078*math.sin(math.pi*t)**.7)*(1-layer*.20)
            center=Vector((0,y,.125))+outward*radius
            for i in range(columns+1):
                s=2*i/columns-1
                rib_fold=.016*s*s*math.sin(math.pi*t)
                edge_ripple=.008*math.sin(t*24+n*1.7)*abs(s)**2*t
                p=center+across*(s*width)+outward*(rib_fold+edge_ripple)
                vertices.append(tuple(p))
        for j in range(rows):
            for i in range(columns):
                a=j*(columns+1)+i
                faces.append((a,a+1,a+columns+2,a+columns+1))
                if j<3: slot=0
                elif layer==2: slot=4
                elif j<5: slot=1
                else: slot=2 if (n+j+i)%4 else 3
                slots.append(slot)
        mesh=bpy.data.meshes.new('LeafMesh');mesh.from_pydata(vertices,[],faces);mesh.update()
        leaf=bpy.data.objects.new('Leaf',mesh);bpy.context.collection.objects.link(leaf)
        for m in palette: mesh.materials.append(m)
        for face,slot in zip(mesh.polygons,slots): face.material_index=slot
        bpy.context.view_layer.objects.active=leaf;leaf.select_set(True)
        shell=leaf.modifiers.new('Leaf thickness','SOLIDIFY');shell.thickness=.0015
        bpy.ops.object.modifier_apply(modifier=shell.name)
        leaf.select_set(False);parts.append(leaf)
        # Raised ivory rib down each leaf, wider at the base.
        rib_verts=[];rib_faces=[]
        for j in range(9):
            t=j/10
            radius=(.018+.082*math.sin(math.pi*t*.82))*(1-layer*.25)+.0015
            center=Vector((0,-.24+length*t,.125))+outward*radius
            width=(.011*(1-t)+.001)*(1-layer*.2)
            rib_verts.extend([tuple(center-across*width),tuple(center+across*width)])
        for j in range(8): rib_faces.append((2*j,2*j+1,2*j+3,2*j+2))
        rm=bpy.data.meshes.new('RibMesh');rm.from_pydata(rib_verts,[],rib_faces);rm.update()
        rib=bpy.data.objects.new('IvoryRib',rm);bpy.context.collection.objects.link(rib)
        rm.materials.append(palette[0]);parts.append(rib)

bpy.ops.mesh.primitive_uv_sphere_add(segments=12,ring_count=6,location=(0,-.17,.125))
core=bpy.context.object;core.scale=(.035,.075,.04)
bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
core.data.materials.append(palette[0]);parts.append(core)
bpy.ops.object.select_all(action='DESELECT')
for part in parts: part.select_set(True)
bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join()
cabbage=bpy.context.object;cabbage.name='NapaCabbage'
# Put the lowest point at ground level, with origin centered for spawning.
lowest=min(v.co.z for v in cabbage.data.vertices)
for v in cabbage.data.vertices: v.co.z-=lowest
bpy.context.scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
bpy.ops.export_scene.fbx(filepath=os.path.join(root,'NapaCabbage.fbx'),use_selection=True,
    object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,
    apply_scale_options='FBX_SCALE_UNITS',bake_anim=False,add_leaf_bones=False)

def aim(obj,target): obj.rotation_euler=(Vector(target)-obj.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(.7,-.9,.65))
cam=bpy.context.object;aim(cam,(0,0,.11));cam.data.type='ORTHO';cam.data.ortho_scale=.72
scene=bpy.context.scene;scene.camera=cam
for loc,energy,size in [((-.5,-.4,1.5),65,1),((.8,.5,.7),40,.8)]:
    data=bpy.data.lights.new('PreviewLight','AREA');data.energy=energy;data.size=size
    light=bpy.data.objects.new('PreviewLight',data);bpy.context.collection.objects.link(light)
    light.location=loc;aim(light,(0,0,.1))
scene.world.color=(.18,.18,.18)
scene.render.engine='CYCLES';scene.cycles.samples=24
scene.render.resolution_x=1000;scene.render.resolution_y=800;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.render.filepath=os.path.join(root,'Preview.png')
scene.render.film_transparent=True
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.shading.type='MATERIAL'
            area.spaces.active.region_3d.view_perspective='CAMERA'
            area.spaces.active.overlay.show_overlays=False
bpy.ops.object.select_all(action='DESELECT');cabbage.select_set(True)
bpy.context.view_layer.objects.active=cabbage
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(root,'NapaCabbage.blend'))
bpy.ops.render.render(write_still=True)
print('CABBAGE_READY',len(cabbage.data.vertices),'vertices')
