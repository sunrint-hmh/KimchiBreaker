"""Restore and export the approved tall green version, independent of rough variants."""
import bpy, os, math, random, json
root=os.path.dirname(os.path.abspath(__file__))
out=os.path.join(root,'Approved')
os.makedirs(out,exist_ok=True)
random.seed(41)
obj=bpy.data.objects['KimjangMat']
assert .4 < max(v.co.z for v in obj.data.vertices) < .49, 'Source is not the approved high-wall mesh'
for name,base in [('Vinyl_Red',(.52,.055,.035)),
                  ('Vinyl_NeonGreen',(.12,.95,.008)),
                  ('Binding_Red',(.38,.035,.025))]:
    mat=bpy.data.materials[name]
    image=bpy.data.images.new(name+'_Approved',width=256,height=256)
    pixels=[]
    for y in range(256):
        for x in range(256):
            noise=random.uniform(-.035,.035)
            weave=(.019 if x%4==0 else 0)+(.015 if y%4==0 else 0)
            folds=.032*math.sin(x*.13+math.sin(y*.047)*2)
            crease=.055*math.exp(-((x-126-math.sin(y*.035)*3)/1.9)**2)
            crease+=.04*math.exp(-((y-127-math.sin(x*.03)*4)/2.1)**2)
            value=noise+weave+folds-crease
            pixels.extend([max(.01,min(1,c+value)) for c in base]+[1])
    image.pixels=pixels
    image.filepath_raw=os.path.join(out,name+'_Albedo.png')
    image.file_format='PNG';image.save()
    for node in mat.node_tree.nodes:
        if node.type=='TEX_IMAGE': node.image=image
    image.pack()
    mat.diffuse_color=(*base,1)
    shader=mat.node_tree.nodes.get('Principled BSDF')
    shader.inputs['Roughness'].default_value=.48
    shader.inputs['Specular IOR Level'].default_value=.27
bpy.ops.object.select_all(action='DESELECT')
obj.select_set(True);bpy.context.view_layer.objects.active=obj
bpy.ops.export_scene.fbx(filepath=os.path.join(out,'KimjangMat.fbx'),use_selection=True,
    object_types={'MESH'},axis_forward='-Z',axis_up='Y',global_scale=1,
    apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_anim=False,
    path_mode='RELATIVE',embed_textures=False,add_leaf_bones=False)
bpy.ops.export_scene.gltf(filepath=os.path.join(out,'KimjangMat.glb'),export_format='GLB',use_selection=True)
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.shading.type='MATERIAL'
            area.spaces.active.overlay.show_overlays=False
            area.spaces.active.region_3d.view_perspective='CAMERA'
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out,'KimjangMat.blend'))
stats={'approved_variant':'tall green, original subtle fabric',
       'vertices':len(obj.data.vertices),'triangles':sum(len(p.vertices)-2 for p in obj.data.polygons),
       'height_m':round(max(v.co.z for v in obj.data.vertices)-min(v.co.z for v in obj.data.vertices),3),
       'materials':[m.name for m in obj.data.materials]}
print('APPROVED_EXPORT',json.dumps(stats))
