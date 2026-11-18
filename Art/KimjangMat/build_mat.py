"""Procedural game prop: red/neon-green vinyl kimjang mat, meters."""
import bpy, math, random, os, json
from mathutils import Vector

ROOT = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(ROOT, 'Export')
os.makedirs(OUT, exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
random.seed(41)

def fabric(name, base):
    size = 128
    img = bpy.data.images.new(name + '_Albedo', width=size, height=size)
    # Broad discoloration and sparse directional scuffs, not uniform fine noise.
    patch_rng = random.Random(931)
    patches = [[patch_rng.uniform(-.075,.035) for _ in range(16)] for _ in range(16)]
    scuffs = [(patch_rng.randrange(size), patch_rng.randrange(size),
               patch_rng.randrange(6,25), patch_rng.choice([-1,1])) for _ in range(32)]
    pixels = []
    for y in range(size):
        for x in range(size):
            noise = random.uniform(-.025, .025)
            weave = (-.035 if x % 4 == 0 else .009) + (-.025 if y % 4 == 0 else .007)
            patch = patches[y//8][x//8]
            fold = abs(x - 64 - math.sin(y*.08)*2)
            fold2 = abs(y - 62 - math.sin(x*.065)*3)
            crease = -.11 if min(fold,fold2)<1.2 else (.07 if min(fold,fold2)<2.6 else 0)
            scratch = 0
            for sx,sy,length,slope in scuffs:
                dx=x-sx
                if 0 <= dx < length and abs(y-sy-slope*dx*.28)<.65:
                    scratch += .09
            value = noise + weave + patch + crease + min(scratch,.22)
            # Worn fibers desaturate toward a dusty yellow instead of whitening the whole prop.
            stain = max(0,-patch-.025)
            color = [max(.008,min(1,c+value)) for c in base]
            color[0] = min(1,color[0]+stain*.15)
            pixels.extend(color + [1])
    img.pixels = pixels
    img.filepath_raw = os.path.join(OUT, name + '_Albedo.png')
    img.file_format = 'PNG'
    img.save()
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*base, 1)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    bsdf = nodes.get('Principled BSDF')
    bsdf.inputs['Roughness'].default_value = .82
    bsdf.inputs['Specular IOR Level'].default_value = .16
    tex = nodes.new('ShaderNodeTexImage')
    tex.image = img
    tex.interpolation = 'Closest'
    mat.node_tree.links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])
    bump = nodes.new('ShaderNodeBump')
    bump.inputs['Strength'].default_value = .16
    bump.inputs['Distance'].default_value = .009
    mat.node_tree.links.new(tex.outputs['Color'], bump.inputs['Height'])
    mat.node_tree.links.new(bump.outputs['Normal'], bsdf.inputs['Normal'])
    # Bake the simple height interpretation into a tangent-space normal image,
    # so game exports can carry rough detail without Blender-only shader nodes.
    normals=[]
    for y in range(size):
        for x in range(size):
            def h(px,py):
                idx=((py%size)*size+(px%size))*4
                return sum(pixels[idx:idx+3])/3
            dx=(h(x+1,y)-h(x-1,y))*.75
            dy=(h(x,y+1)-h(x,y-1))*.75
            n=Vector((-dx,-dy,1)).normalized()
            normals.extend([n.x*.5+.5,n.y*.5+.5,n.z*.5+.5,1])
    normal_img=bpy.data.images.new(name+'_Normal',width=size,height=size)
    normal_img.colorspace_settings.name='Non-Color'
    normal_img.pixels=normals
    normal_img.filepath_raw=os.path.join(OUT,name+'_Normal.png')
    normal_img.file_format='PNG';normal_img.save()
    normal_tex=nodes.new('ShaderNodeTexImage');normal_tex.image=normal_img
    normal_tex.interpolation='Closest'
    normal_node=nodes.new('ShaderNodeNormalMap')
    mat.node_tree.links.new(normal_tex.outputs['Color'],normal_node.inputs['Color'])
    mat.node_tree.links.new(normal_node.outputs['Normal'],bsdf.inputs['Normal'])
    return mat

red = fabric('Vinyl_Red', (.43, .045, .025))
green = fabric('Vinyl_NeonGreen', (.10, .85, .008))
edge = fabric('Binding_Red', (.38, .035, .025))
verts, faces, uv_faces, material_ids = [], [], [], []
N = 96

def radial(a):
    return 1 + .009*math.sin(5*a+.4) + .005*math.sin(13*a)

def wrinkle(a):
    return .024*math.sin(11*a+.5) + .014*math.sin(23*a) + .008*math.sin(37*a)

# One continuous floor, inner wall, rolled binding, outer wall and underside.
# Keep the floor shape; raise the flexible wall from 26 cm to about 45 cm.
profile = [(.0,.025,0),(.22,.026,0),(.55,.025,0),(.95,.025,0),
           (1.25,.027,0),(1.44,.032,0),(1.50,.051,0),
           (1.525,.105,0),(1.55,.19,0),(1.58,.255,0),
           (1.596,.26,2),(1.612,.248,2),(1.602,.212,2),
           (1.585,.175,1),(1.565,.095,1),(1.545,.032,1),
           (1.50,.013,1),(.95,.013,1),(.22,.013,1),(.0,.013,1)]
profile = [(r, .032 + (z-.032)*1.85 if z>.032 else z, m) for r,z,m in profile]

for j,(radius,z,_) in enumerate(profile):
    for i in range(N):
        a = math.tau*i/N
        is_floor = j <= 5
        w = 0 if radius == 0 else wrinkle(a)
        if is_floor:
            dz = .0025 * math.sin(a*12+radius*13) * min(radius,1)
            # Two shallow fold ridges, kept much lower than clickable ingredients.
            xx,yy = radius*math.cos(a),radius*math.sin(a)
            dz += .009*math.exp(-(xx/.035)**2)*min(radius,1)
            dz += .006*math.exp(-(yy/.035)**2)*min(radius,1)
            dz += .018*math.exp(-((xx+.45*yy-.48)/.04)**2)*min(radius,1)
            dz += .013*math.exp(-((yy-.33*xx+.52)/.05)**2)*min(radius,1)
            dz += .006*math.sin(xx*19+yy*8)*math.sin(yy*14)*min(radius,1)
        elif j >= 16:
            dz = 0
        else:
            dz = w * (z/.454)
        rr = radius*radial(a)
        if 6 < j < 16:
            rr += .020*math.sin(19*a + j*.65)
        verts.append((rr*math.cos(a),rr*math.sin(a),z+dz))

for j in range(len(profile)-1):
    for i in range(N):
        k=(i+1)%N
        face=(j*N+i,j*N+k,(j+1)*N+k,(j+1)*N+i)
        # Floor up, interior wall inward, exterior wall outward.
        face=tuple(reversed(face))
        faces.append(face)
        material_ids.append(profile[j+1][2])
        if j<6 or j>=16:
            uv_faces.append([(verts[v][0]/3.3+.5,verts[v][1]/3.3+.5) for v in face])
        else:
            v0=profile[j][1]/.454
            v1=profile[j+1][1]/.454
            uv_faces.append([(i/N,v0),((i+1)/N,v0),((i+1)/N,v1),(i/N,v1)][::-1])

# The two axis centers have coincident ring vertices: weld for a watertight prop.
mesh=bpy.data.meshes.new('KimjangMatMesh')
mesh.from_pydata(verts,[],faces)
mesh.update()
obj=bpy.data.objects.new('KimjangMat',mesh)
bpy.context.collection.objects.link(obj)
for m in (red,green,edge): mesh.materials.append(m)
uv=mesh.uv_layers.new(name='UVMap')
for p,coords,mi in zip(mesh.polygons,uv_faces,material_ids):
    p.material_index=mi
    p.use_smooth=False
    for loop_index,coord in zip(p.loop_indices,coords): uv.data[loop_index].uv=coord
bpy.context.view_layer.objects.active=obj
obj.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.remove_doubles(threshold=.0001)
bpy.ops.mesh.normals_make_consistent(inside=False)
bpy.ops.object.mode_set(mode='OBJECT')

# Export only the game mesh; preview equipment is excluded.
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,'KimjangMat.fbx'),use_selection=True,
    object_types={'MESH'},axis_forward='-Z',axis_up='Y',global_scale=1,
    apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_anim=False,
    path_mode='COPY',embed_textures=False,add_leaf_bones=False)
bpy.ops.export_scene.gltf(filepath=os.path.join(OUT,'KimjangMat.glb'),
    export_format='GLB',use_selection=True)
triangles=sum(len(p.vertices)-2 for p in obj.data.polygons)
stats={'name':'KimjangMat','units':'meters',
       'diameter_m':round(max(v.co.x for v in mesh.vertices)-min(v.co.x for v in mesh.vertices),3),
       'height_m':round(max(v.co.z for v in mesh.vertices)-min(v.co.z for v in mesh.vertices),3),
       'triangles':triangles,'materials':3,'albedo_resolution':'128x128 each',
       'pivot':'center of ground plane','style':'faceted low-poly, point-filter vinyl textures'}
with open(os.path.join(ROOT,'model-info.json'),'w') as f: json.dump(stats,f,indent=2)

# Neutral preview stage; exported files never include these objects.
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.005))
ground=bpy.context.object
ground.name='PREVIEW_Ground'
gm=bpy.data.materials.new('PREVIEW_Slate')
gm.diffuse_color=(.055,.065,.067,1)
ground.data.materials.append(gm)

def aim(o,at): o.rotation_euler=(Vector(at)-o.location).to_track_quat('-Z','Y').to_euler()
for name,loc,power,size,color in [('Key',(-3,-4,6),850,4,(1,.87,.73)),
    ('Fill',(4,-1,3),480,4,(.7,.82,1)),('Rim',(0,4,4),1000,3,(1,.9,.8))]:
    data=bpy.data.lights.new('PREVIEW_'+name,'AREA')
    data.energy=power;data.shape='DISK';data.size=size;data.color=color
    light=bpy.data.objects.new(data.name,data)
    bpy.context.collection.objects.link(light);light.location=loc;aim(light,(0,0,0))
bpy.ops.object.camera_add(location=(3.8,-4.8,4.0))
cam=bpy.context.object
cam.name='PREVIEW_Camera';cam.data.type='ORTHO';cam.data.ortho_scale=4.5
aim(cam,(0,0,.03))
scene=bpy.context.scene;scene.camera=cam
scene.render.engine='CYCLES';scene.cycles.samples=32
scene.render.resolution_x=1200;scene.render.resolution_y=900;scene.render.resolution_percentage=100
scene.world.color=(.17,.17,.17)
scene.view_settings.view_transform='AgX'
scene.render.image_settings.file_format='PNG'
scene.render.filepath=os.path.join(ROOT,'Preview_Rough.png')
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type == 'VIEW_3D':
            area.spaces.active.shading.type = 'MATERIAL'
bpy.ops.object.select_all(action='DESELECT')
obj.select_set(True)
bpy.context.view_layer.objects.active = obj
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT,'KimjangMat_Rough.blend'))
bpy.ops.render.render(write_still=True)
print('KIMJANG_MAT_COMPLETE',json.dumps(stats))
