"""Reusable worn kitchen gloves: low-poly meshes + portable 256px point-filter maps.
Blender +Y points toward fingertips, +Z is the back of the hand. No rig dependency.
"""
import bpy, math, os, random, json
from mathutils import Vector

ROOT = os.path.dirname(os.path.abspath(__file__))
os.makedirs(ROOT, exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
random.seed(7304)
N = 256
scuffs = [(random.randrange(N), random.randrange(N), random.randrange(3, 15), random.randrange(3, 20)) for _ in range(27)]
albedo, rough, packed, heights = [], [], [], []
for y in range(N):
    for x in range(N):
        grain = random.uniform(-.025, .025)
        cloud = math.sin(x*.053 + math.sin(y*.04)*2)*.022 + math.sin(y*.092-x*.03)*.018
        wear = max([max(0, 1-((x-cx)/rx)**2-((y-cy)/ry)**2) for cx,cy,rx,ry in scuffs]+[0])
        palm = x >= 128 and y < 128
        cuff = y >= 128
        grip = palm and ((x+y)%10 < 2 or (x-y)%10 < 2)
        rib = cuff and y%12 < 2
        crease = (not cuff) and abs((y + 3*math.sin(x*.08))%27 - 2) < 1
        value = cloud+grain+wear*.055-(.035 if grip else 0)-(.045 if crease else 0)-(.03 if rib else 0)
        base = (.60,.145,.19) if not cuff else (.67,.195,.235)
        # Rubber, not fabric: molded grip and subdued scuffs, no woven cross-grain everywhere.
        color = [max(.015,min(.92,c+value)) for c in base]
        if palm and y > 90 and wear > .45:
            color = [color[0]*.80,color[1]*.62,color[2]*.51]  # faint old seasoning stains
        albedo.extend(color+[1])
        r = min(.88,max(.56,.70 + abs(grain)*2 + wear*.09 - (.06 if grip else 0)))
        rough.extend([r,r,r,1]); packed.extend([0,0,0,1-r])
        heights.append((.22 if grip else 0)+(.12 if rib else 0)-(.12 if crease else 0)+grain*.4)

def image(name, pixels, colorspace):
    result = bpy.data.images.new(name, width=N,height=N,alpha=True)
    result.colorspace_settings.name=colorspace; result.pixels=pixels
    result.filepath_raw=os.path.join(ROOT,name+'.png');result.file_format='PNG';result.save();result.pack()
    return result

color_map=image('Glove_WornPink_Albedo',albedo,'sRGB')
rough_map=image('Glove_WornPink_Roughness',rough,'Non-Color')
image('Glove_WornPink_MetallicSmoothness',packed,'Non-Color')
normal=[]
for y in range(N):
    for x in range(N):
        dx=(heights[y*N+(x+1)%N]-heights[y*N+(x-1)%N])*.35
        dy=(heights[((y+1)%N)*N+x]-heights[((y-1)%N)*N+x])*.35
        v=Vector((-dx,-dy,1)).normalized();normal.extend([v.x*.5+.5,v.y*.5+.5,v.z*.5+.5,1])
normal_map=image('Glove_WornPink_Normal',normal,'Non-Color')
sleeve_pixels=[]
for y in range(N):
    for x in range(N):
        v=random.uniform(-.018,.018)+math.sin(y*.11+x*.025)*.013
        if (x-y)%9==0:v-=.018
        sleeve_pixels.extend([max(.015,c+v) for c in (.16,.17,.135)]+[1])
sleeve_map=image('Sleeve_WornGrey_Albedo',sleeve_pixels,'sRGB')
sleeve_material=bpy.data.materials.new('Sleeve_WornGrey');sleeve_material.use_nodes=True
sleeve_shader=sleeve_material.node_tree.nodes.get('Principled BSDF')
sleeve_shader.inputs['Roughness'].default_value=.86;sleeve_shader.inputs['Specular IOR Level'].default_value=.2
sleeve_tex=sleeve_material.node_tree.nodes.new('ShaderNodeTexImage');sleeve_tex.image=sleeve_map;sleeve_tex.interpolation='Closest'
sleeve_material.node_tree.links.new(sleeve_tex.outputs['Color'],sleeve_shader.inputs['Base Color'])
material=bpy.data.materials.new('Glove_WornPink');material.use_nodes=True
nodes=material.node_tree.nodes;links=material.node_tree.links
bsdf=nodes.get('Principled BSDF');bsdf.inputs['Roughness'].default_value=.72
bsdf.inputs['Metallic'].default_value=0;bsdf.inputs['Specular IOR Level'].default_value=.25
for source,channel in [(color_map,'Base Color'),(rough_map,'Roughness')]:
    tex=nodes.new('ShaderNodeTexImage');tex.image=source;tex.interpolation='Closest';links.new(tex.outputs['Color'],bsdf.inputs[channel])
tex=nodes.new('ShaderNodeTexImage');tex.image=normal_map;tex.interpolation='Closest'
normal_node=nodes.new('ShaderNodeNormalMap');normal_node.inputs['Strength'].default_value=.45
links.new(tex.outputs['Color'],normal_node.inputs['Color']);links.new(normal_node.outputs['Normal'],bsdf.inputs['Normal'])

def glove(name, mirror):
    vertices=[];faces=[];uv_faces=[];smooth=[]
    def tube(centers,radii,section=10,zone='hand',open_start=False,cap_end=True):
        base=len(vertices)
        for j,(center,(rx,rz)) in enumerate(zip(centers,radii)):
            tangent=Vector(centers[min(len(centers)-1,j+1)])-Vector(centers[max(0,j-1)])
            tangent.normalize()
            across=Vector((1,0,0));across=(across-tangent*across.dot(tangent)).normalized()
            depth=across.cross(tangent).normalized()
            for i in range(section):
                a=math.tau*i/section
                # Dorsal and palm sections are broad, with softened faceted side walls.
                z=math.sin(a)
                if zone=='palm': z=math.copysign(abs(z)**.8,z)
                point=Vector(center)+across*(rx*math.cos(a))+depth*(rz*z)
                vertices.append((mirror*point.x,point.y,point.z))
        def uv(u,v,palm_side):
            if zone=='cuff':return (.02+.96*u,.53+.45*v)
            return ((.52 if palm_side else .02)+.46*u,.02+.46*v)
        for j in range(len(centers)-1):
            for i in range(section):
                a=base+j*section+i;b=base+j*section+(i+1)%section
                face=(a,b,b+section,a+section)
                palm_side=math.sin(math.tau*(i+.5)/section)<0
                coords=[uv(i/section,j/(len(centers)-1),palm_side),uv((i+1)/section,j/(len(centers)-1),palm_side),
                    uv((i+1)/section,(j+1)/(len(centers)-1),palm_side),uv(i/section,(j+1)/(len(centers)-1),palm_side)]
                if mirror<0:face=face[::-1];coords=coords[::-1]
                faces.append(face);uv_faces.append(coords);smooth.append(zone in ('finger','palm'))
        if not open_start:
            face=tuple(base+i for i in reversed(range(section)))
            if mirror<0:face=face[::-1]
            faces.append(face);uv_faces.append([(.2,.2)]*section);smooth.append(False)
        if cap_end:
            face=tuple(base+(len(centers)-1)*section+i for i in range(section))
            if mirror<0:face=face[::-1]
            faces.append(face);uv_faces.append([(.22,.43)]*section);smooth.append(zone=='finger')
    # Long household cuff, widening toward the forearm. Small molded folds break the silhouette.
    ys=[-.285,-.274,-.25,-.20,-.145,-.095,-.045,0]
    radii=[(.076,.051),(.078,.052),(.072,.048),(.069,.045),(.061,.041),(.055,.035),(.047,.027),(.047,.024)]
    tube([(0,y,0) for y in ys],radii,12,'cuff',True,False)
    # Rolled rim and a short dark inner return make the opening feel thick, not paper-thin.
    tube([(0,-.285,0),(0,-.29,0),(0,-.289,0),(0,-.265,0)],
         [(.076,.051),(.079,.053),(.072,.046),(.069,.044)],12,'cuff',True,False)
    tube([(0,0,0),(0,.02,0),(.001,.045,.001),(.002,.075,.003),(.002,.098,.002),(.001,.12,0),(0,.137,-.002)],
         [(.047,.024),(.055,.026),(.063,.028),(.071,.028),(.072,.027),(.069,.024),(.062,.020)],14,'palm')
    for x,start,length,width,splay in [(-.047,.124,.110,.017,-.013),(-.014,.131,.131,.0175,-.002),
                                      (.021,.126,.120,.016,.008),(.051,.115,.089,.014,.022)]:
        centers=[];radii=[]
        for t,r in [(0,1),(.15,1.02),(.34,.94),(.52,.99),(.70,.90),(.86,.87),(.94,.76),(.99,.50),(1.015,.15),(1.02,.005)]:
            centers.append((x+splay*t,start+length*t,-.002-.010*t*t+.002*math.sin(t*math.pi)))
            radii.append((width*r,width*.82*r))
        tube(centers,radii,10,'finger')
    tube([(-.045,.032,-.009),(-.065,.047,-.004),(-.083,.062,0),(-.104,.083,.002),
          (-.121,.104,.001),(-.131,.122,-.003),(-.134,.131,-.005),(-.135,.134,-.006)],
         [(.029,.025),(.027,.024),(.023,.021),(.020,.018),(.018,.016),(.014,.013),(.008,.008),(.001,.001)],10,'finger')
    mesh=bpy.data.meshes.new(name+'Mesh');mesh.from_pydata(vertices,[],faces);mesh.update()
    obj=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(obj);mesh.materials.append(material)
    layer=mesh.uv_layers.new(name='UVMap')
    for poly,coords,is_smooth in zip(mesh.polygons,uv_faces,smooth):
        poly.use_smooth=is_smooth
        for loop,uv in zip(poly.loop_indices,coords):layer.data[loop].uv=uv
    # Normalize outward face winding consistently on both mirrored hands.
    bpy.context.view_layer.objects.active=obj;obj.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.normals_make_consistent(inside=False);bpy.ops.object.mode_set(mode='OBJECT')
    # Join finger webs, thumb mound and wrist into one continuous rubber surface.
    # Transfer the existing UV atlas back; colors/maps are deliberately unchanged.
    source=obj.copy();source.data=obj.data.copy();source.name=name+'_UVTransfer';bpy.context.collection.objects.link(source)
    source.hide_render=True
    obj.data.remesh_voxel_size=.0028
    obj.data.remesh_voxel_adaptivity=0
    bpy.context.view_layer.objects.active=obj
    bpy.ops.object.voxel_remesh()
    relax=obj.modifiers.new('Soft molded rubber','SMOOTH');relax.factor=.42;relax.iterations=3
    bpy.ops.object.modifier_apply(modifier=relax.name)
    reduce=obj.modifiers.new('Retro silhouette budget','DECIMATE');reduce.ratio=.08;reduce.use_collapse_triangulate=True
    bpy.ops.object.modifier_apply(modifier=reduce.name)
    if not obj.data.uv_layers:obj.data.uv_layers.new(name='UVMap')
    transfer=obj.modifiers.new('Preserve authored rubber atlas','DATA_TRANSFER');transfer.object=source
    transfer.use_loop_data=True;transfer.data_types_loops={'UV'};transfer.loop_mapping='POLYINTERP_NEAREST'
    bpy.ops.object.modifier_apply(modifier=transfer.name)
    for poly in obj.data.polygons:poly.use_smooth=True
    bpy.data.objects.remove(source,do_unlink=True)
    obj.select_set(False);return obj

left=glove('RubberGlove_Left',-1);right=glove('RubberGlove_Right',1)
for hand in (left,right):
    # A clothed forearm fills the cuff opening and continues below the first-person frame.
    verts=[];faces=[]
    for y,rx,rz in [(-.25,.063,.038),(-.31,.066,.042),(-.49,.071,.047),(-.76,.077,.052)]:
        for i in range(12):
            a=math.tau*i/12;verts.append((rx*math.cos(a),y,rz*math.sin(a)))
    for j in range(3):
        for i in range(12):faces.append((j*12+i,j*12+(i+1)%12,(j+1)*12+(i+1)%12,(j+1)*12+i))
    mesh=bpy.data.meshes.new('SleeveMesh');mesh.from_pydata(verts,[],faces);mesh.update();mesh.materials.append(sleeve_material)
    sleeve=bpy.data.objects.new('WornSleeve',mesh);bpy.context.collection.objects.link(sleeve)
    uv=mesh.uv_layers.new(name='UVMap')
    for poly in mesh.polygons:
        for loop in poly.loop_indices:
            idx=mesh.loops[loop].vertex_index;uv.data[loop].uv=((idx%12)/12,(idx//12)/3)
    bpy.ops.object.select_all(action='DESELECT');hand.select_set(True);sleeve.select_set(True)
    bpy.context.view_layer.objects.active=hand;bpy.ops.object.join()
for obj in (left,right):
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
    bpy.ops.export_scene.fbx(filepath=os.path.join(ROOT,obj.name+'.fbx'),use_selection=True,object_types={'MESH'},
        axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',
        bake_anim=False,add_leaf_bones=False,path_mode='RELATIVE',embed_textures=False)
left.location.x=-.19;right.location.x=.19
def aim(obj,target):obj.rotation_euler=(Vector(target)-obj.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(.75,-1.35,1.25));camera=bpy.context.object;aim(camera,(0,-.22,0))
camera.data.type='ORTHO';camera.data.ortho_scale=1.28
scene=bpy.context.scene;scene.camera=camera
for location,energy,size in [((-.55,-.3,1.3),75,1.2),((.65,.6,.7),45,.8)]:
    data=bpy.data.lights.new('PreviewSoftbox','AREA');data.energy=energy;data.size=size
    light=bpy.data.objects.new('PreviewSoftbox',data);bpy.context.collection.objects.link(light);light.location=location;aim(light,(0,0,0))
scene.world.color=(.1,.115,.085);scene.render.engine='CYCLES';scene.cycles.samples=24
scene.view_settings.view_transform='AgX'
scene.render.resolution_x=1200;scene.render.resolution_y=900;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.render.filepath=os.path.join(ROOT,'Preview.png')
scene.render.film_transparent=False
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.shading.type='MATERIAL';area.spaces.active.region_3d.view_perspective='CAMERA'
            area.spaces.active.overlay.show_overlays=False
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT,'RubberGloves.blend'))
info={'style':'faceted kitchen rubber gloves, worn red-pink; no fabric weave, no chrome',
      'texture_size':256,'filter':'Point / Closest','metallic':0,'roughness_range':[.56,.88],
      'native_axes':'+Y fingertips, +Z dorsal','triangles_per_hand':sum(len(p.vertices)-2 for p in right.data.polygons),
      'hands':['RubberGlove_Left','RubberGlove_Right'],'rig':'rigid hand pivot; separate left/right swing motion'}
with open(os.path.join(ROOT,'model-info.json'),'w',encoding='utf-8') as f:json.dump(info,f,indent=2)
bpy.ops.render.render(write_still=True)
print('RUBBER_GLOVES_READY',info)
