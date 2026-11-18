"""Open, hollow raffle jar: low-poly silhouette, dirty pixel glass, Unity-ready maps."""
import bpy, math, os, random, json, sys
from mathutils import Vector

ROOT = os.path.dirname(os.path.abspath(__file__))
os.makedirs(ROOT, exist_ok=True)
random.seed(404)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)

N = 256
scratches = []
for _ in range(42):
    scratches.append((random.randrange(N), random.randrange(N), random.randrange(7, 46), random.uniform(-.22, .22)))
stains = [(random.randrange(N), random.randrange(N), random.uniform(5, 23), random.uniform(7, 31)) for _ in range(19)]
pixels, rough_pixels = [], []
for y in range(N):
    for x in range(N):
        grain = random.uniform(-.045, .045)
        cloud = (math.sin(x*.081 + math.sin(y*.044)*2) + math.sin(y*.111-x*.031))*.022
        stain = 0.0
        for sx, sy, rx, ry in stains:
            dx = min(abs(x-sx), N-abs(x-sx))/rx
            dy = (y-sy)/ry
            stain = max(stain, max(0, 1-dx*dx-dy*dy))
        scratch = 0.0
        for sx, sy, length, slope in scratches:
            t = y-sy
            if 0 <= t < length and abs(x-sx-t*slope) < .65:
                scratch = .45 if (x+y)%5 else .2
        # Dirt is visible in color AND alpha, rather than disappearing on transparent glass.
        value = cloud+grain-stain*.16+scratch*.18
        color = [max(.025, min(.9, c+value)) for c in (.43, .48, .34)]
        alpha = .11 + stain*.27 + scratch*.48 + abs(grain)*1.2
        pixels.extend(color+[min(.75, alpha)])
        r = min(.86, .32+stain*.35+abs(grain)*2+scratch*.3)
        rough_pixels.extend([r, r, r, 1])

def save_image(name, data, colorspace):
    image = bpy.data.images.new(name, width=N, height=N, alpha=True)
    image.colorspace_settings.name = colorspace
    image.pixels = data
    image.filepath_raw = os.path.join(ROOT, name+'.png')
    image.file_format = 'PNG'
    image.save()
    image.pack()
    return image

albedo = save_image('Jar_DirtyGlass_RGBA', pixels, 'sRGB')
roughness = save_image('Jar_DirtyGlass_Roughness', rough_pixels, 'Non-Color')
smoothness_pixels = []
for i in range(0, len(rough_pixels), 4):
    smoothness_pixels.extend([0, 0, 0, 1-rough_pixels[i]])
save_image('Jar_MetallicSmoothness', smoothness_pixels, 'Non-Color')

def glass_material(name, alpha_multiplier, tint=(1, 1, 1), rough=.48):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    mat.surface_render_method = 'DITHERED'
    mat.diffuse_color = (.43*tint[0], .48*tint[1], .34*tint[2], .2*alpha_multiplier)
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    shader = nodes.get('Principled BSDF')
    shader.inputs['Roughness'].default_value = rough
    shader.inputs['Specular IOR Level'].default_value = .45
    # Alpha blending is reproducible in URP; avoid relying on Blender-only glass refraction.
    shader.inputs['Transmission Weight'].default_value = 0
    tex = nodes.new('ShaderNodeTexImage'); tex.image = albedo; tex.interpolation = 'Closest'
    color = nodes.new('ShaderNodeMixRGB'); color.blend_type = 'MULTIPLY'
    color.inputs[0].default_value = 1; color.inputs[2].default_value = (*tint, 1)
    links.new(tex.outputs['Color'], color.inputs[1]); links.new(color.outputs[0], shader.inputs['Base Color'])
    alpha = nodes.new('ShaderNodeMath'); alpha.operation = 'MULTIPLY'; alpha.inputs[1].default_value = alpha_multiplier
    links.new(tex.outputs['Alpha'], alpha.inputs[0]); links.new(alpha.outputs[0], shader.inputs['Alpha'])
    rough_tex = nodes.new('ShaderNodeTexImage'); rough_tex.image = roughness; rough_tex.interpolation = 'Closest'
    links.new(rough_tex.outputs['Color'], shader.inputs['Roughness'])
    return mat

outer = glass_material('Jar_DirtyGlass', 1.0)
inner = glass_material('Jar_InnerGlass', .42)
rim = glass_material('Jar_ThickRim', 3.0, (.72, .76, .68), .38)
base = glass_material('Jar_ThickBase', 2.7, (.75, .79, .72))

# A closed cross-section: outside up to the lip, then down the inside to the floor.
# The mouth has no cap. Thickness is actual geometry, not a transparent solid cylinder.
profile = [(.23, .006), (.265, .024), (.29, .075), (.35, .18), (.396, .35),
           (.397, .49), (.36, .62), (.292, .735), (.269, .776),
           (.285, .791), (.289, .82), (.274, .842), (.252, .834),
           (.25, .811), (.254, .784), (.273, .735), (.34, .615),
           (.376, .486), (.375, .35), (.33, .18), (.27, .077), (.245, .043)]
segments = 24
verts, faces, mats, uv_faces = [], [], [], []
for ring, (radius, z) in enumerate(profile):
    for s in range(segments):
        a = math.tau*s/segments
        wobble = 1+.012*math.sin(a*3+.4)+.009*math.cos(a*5+z*4)
        verts.append((math.cos(a)*radius*wobble, math.sin(a)*radius*wobble,
                      z + .0025*math.sin(a*4+.3)*min(1, z/.05)))
for j in range(len(profile)-1):
    for i in range(segments):
        nxt = (i+1)%segments
        faces.append((j*segments+i, j*segments+nxt, (j+1)*segments+nxt, (j+1)*segments+i))
        mats.append(2 if 8 <= j <= 13 else 3 if j < 2 or j >= 20 else 1 if j >= 14 else 0)
        u0, u1 = i/segments, (i+1)/segments
        v0, v1 = profile[j][1]/.842, profile[j+1][1]/.842
        uv_faces.append(((u0, v0), (u1, v0), (u1, v1), (u0, v1)))
# Closed bottom, outside winding down and inside winding up.
faces.append(tuple(reversed(range(segments)))); mats.append(3)
uv_faces.append(tuple((.5+verts[i][0], .5+verts[i][1]) for i in reversed(range(segments))))
last = (len(profile)-1)*segments
faces.append(tuple(last+i for i in range(segments))); mats.append(3)
uv_faces.append(tuple((.5+verts[last+i][0], .5+verts[last+i][1]) for i in range(segments)))
mesh = bpy.data.meshes.new('SkillJarMesh')
mesh.from_pydata(verts, [], faces); mesh.update()
jar = bpy.data.objects.new('SkillJar', mesh); bpy.context.collection.objects.link(jar)
for mat in (outer, inner, rim, base): mesh.materials.append(mat)
uv = mesh.uv_layers.new(name='UVMap')
for poly, material_index, coords in zip(mesh.polygons, mats, uv_faces):
    poly.material_index = material_index
    poly.use_smooth = False
    for loop_index, coordinate in zip(poly.loop_indices, coords): uv.data[loop_index].uv = coordinate

# Two faint raised bands characteristic of old onggi, kept transparent rather than metal hoops.
def band(z, radius, name):
    bpy.ops.mesh.primitive_torus_add(major_segments=24, minor_segments=4,
        location=(0, 0, z), major_radius=radius, minor_radius=.006)
    obj = bpy.context.object; obj.name = name; obj.data.materials.append(rim)
    return obj
band1 = band(.19, .354, 'LowerThrownBand')
band2 = band(.625, .357, 'ShoulderThrownBand')
bpy.ops.object.select_all(action='DESELECT')
for obj in (jar, band1, band2): obj.select_set(True)
bpy.context.view_layer.objects.active = jar
bpy.ops.object.join()
jar['purpose'] = 'Transparent skill raffle jar; mouth open; base at floor; Z-up Blender / Y-up Unity.'
jar['texture_style'] = '256px nearest-filtered dirty glass, scratches, mottled alpha; no bloom or refraction dependency.'

bpy.ops.export_scene.fbx(filepath=os.path.join(ROOT, 'SkillJar.fbx'), use_selection=True,
    object_types={'MESH'}, axis_forward='-Z', axis_up='Y', apply_unit_scale=True,
    apply_scale_options='FBX_SCALE_UNITS', bake_anim=False, add_leaf_bones=False,
    path_mode='RELATIVE')

# Export a GLB using simple PBR maps (complex alpha multiply is kept in the .blend source).
# Unity's authored URP materials use the separate RGBA texture and correct material alpha.
bpy.ops.export_scene.gltf(filepath=os.path.join(ROOT, 'SkillJar.glb'), export_format='GLB', use_selection=True)

# Preview-only context: existing red/green mat, several kimchi-colored pieces inside,
# checker board behind the glass. None of these go into the exported game model.
def plain(name, color, rough=.7):
    mat = bpy.data.materials.new(name); mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = (*color, 1); bsdf.inputs['Roughness'].default_value = rough
    return mat

red = plain('Preview_KimchiRed', (.55, .055, .018))
leaf = plain('Preview_KimchiLeaf', (.37, .20, .035))
for n in range(7):
    a = n*2.4
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=.09,
        location=(math.cos(a)*.14, math.sin(a)*.14, .11+(n//4)*.12))
    obj = bpy.context.object; obj.name = 'PREVIEW_KimchiOnly'
    obj.scale = (1.05, .65, 1.25); obj.rotation_euler = (.2, .3, a)
    obj.data.materials.append(red); obj.data.materials.append(leaf)
    for p in obj.data.polygons: p.material_index = 1 if p.index%5 == 0 else 0

bpy.ops.mesh.primitive_plane_add(size=200, location=(0, 0, -.012))
floor = bpy.context.object; floor.name = 'PREVIEW_Floor'
floor.data.materials.append(plain('Preview_DarkConcrete', (.07, .079, .066), .93))

# A worn kitchen tile board provides a readable transparency reference behind the jar.
board = plain('Preview_TileBoard', (.22, .26, .18))
nodes = board.node_tree.nodes; links = board.node_tree.links
checker = nodes.new('ShaderNodeTexChecker'); checker.inputs['Scale'].default_value = 4
checker.inputs['Color1'].default_value = (.25, .28, .20, 1)
checker.inputs['Color2'].default_value = (.09, .12, .085, 1)
links.new(checker.outputs['Color'], nodes.get('Principled BSDF').inputs['Base Color'])
bpy.ops.mesh.primitive_cube_add(size=1, location=(0, .75, .60))
back = bpy.context.object; back.name = 'PREVIEW_TransparencyBoard'; back.scale = (2.6, .025, 1.3)
back.data.materials.append(board)

def aim(obj, target): obj.rotation_euler = (Vector(target)-obj.location).to_track_quat('-Z', 'Y').to_euler()
bpy.ops.object.camera_add(location=(1.35, -2.35, 1.5))
camera = bpy.context.object; aim(camera, (0, 0, .41))
camera.data.type = 'ORTHO'; camera.data.ortho_scale = 1.35
bpy.context.scene.camera = camera
for name, position, power, size, color in [
    ('Key', (-1.5, -2, 2.7), 140, 2.0, (1, .91, .75)),
    ('Rim', (1.5, .8, 1.7), 180, 1.5, (.72, .86, 1)),
    ('Fill', (.3, -1, .7), 18, 1.2, (.8, 1, .72))]:
    data = bpy.data.lights.new(name, 'AREA'); data.energy = power; data.shape = 'DISK'; data.size = size; data.color = color
    light = bpy.data.objects.new(name, data); bpy.context.collection.objects.link(light)
    light.location = position; aim(light, (0, 0, .4))

scene = bpy.context.scene
scene.render.engine = 'CYCLES'; scene.cycles.samples = 32
scene.cycles.use_denoising = True
scene.cycles.transparent_max_bounces = 16
scene.world.color = (.11, .12, .10)
scene.render.resolution_x = 1000; scene.render.resolution_y = 1000; scene.render.resolution_percentage = 100
scene.view_settings.view_transform = 'AgX'
scene.render.image_settings.file_format = 'PNG'; scene.render.filepath = os.path.join(ROOT, 'Preview.png')
bpy.ops.object.select_all(action='DESELECT'); jar.select_set(True); bpy.context.view_layer.objects.active = jar
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type == 'VIEW_3D':
            area.spaces.active.shading.type = 'MATERIAL'
            area.spaces.active.region_3d.view_perspective = 'CAMERA'
            area.spaces.active.overlay.show_overlays = False
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT, 'SkillJar.blend'))
stats = {'name': 'SkillJar', 'vertices': len(jar.data.vertices),
         'triangles': sum(len(p.vertices)-2 for p in jar.data.polygons),
         'height_m': .8445, 'texture_size': 256, 'materials': [m.name for m in jar.data.materials],
         'preview_only_objects_excluded_from_exports': True}
with open(os.path.join(ROOT, 'model-info.json'), 'w') as f: json.dump(stats, f, indent=2)
print('SKILL_JAR_READY', json.dumps(stats), flush=True)
if '--skip-render' not in sys.argv:
    bpy.ops.render.render(write_still=True)
