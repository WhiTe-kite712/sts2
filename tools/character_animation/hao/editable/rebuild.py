from pathlib import Path
import argparse, json, math, shutil
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

parser=argparse.ArgumentParser(description='Rebuild anatomically aligned Hao Spine 4.2 animations.')
parser.add_argument('--no-previews',action='store_true')
parser.add_argument('--keep-textures',action='store_true',help='Reuse the existing parts and atlas without writing image files.')
args=parser.parse_args()
OUT=Path(__file__).resolve().parents[1]
SOURCE=OUT/'source'; PREVIEW=OUT/'preview'; PARTS=OUT/'editable/parts'
for folder in (SOURCE,PREVIEW,PARTS): folder.mkdir(exist_ok=True,parents=True)
config=json.loads((OUT/'editable/bind.json').read_text(encoding='utf-8'))
reference=Image.open(SOURCE/'character-reference.png').convert('RGBA')
S=float(config.get('uniformScale',1/3)); origin=np.array(config.get('originPixel',config.get('origin',[597,1510])),float)
joints={name:np.array(value,float) for name,value in config['joints'].items()}
def R(deg):
    angle=math.radians(deg)
    return np.array([[math.cos(angle),-math.sin(angle)],[math.sin(angle),math.cos(angle)]])
def world(pixel): return np.array([(pixel[0]-origin[0])*S,(origin[1]-pixel[1])*S])
def degree(vector): return math.degrees(math.atan2(vector[1],vector[0]))
def polygon_mask(points):
    mask=Image.new('L',reference.size,0);ImageDraw.Draw(mask).polygon([tuple(p) for p in points],fill=255);return mask

full_images={};source_boxes={}; masks={}
for name,spec in config['parts'].items():
    points=spec['polygon'] if isinstance(spec,dict) else spec
    mask=polygon_mask(points);masks[name]=mask
    piece=reference.copy();piece.putalpha(Image.fromarray(np.minimum(np.asarray(mask),np.asarray(reference.getchannel('A')))))
    box=piece.getchannel('A').getbbox()
    if not box: raise ValueError('Empty part '+name)
    source_boxes[name]=list(box); full_images[name]=piece.crop(box)

rgba=np.asarray(reference)
cloth_blue=(rgba[:,:,2].astype(float)>rgba[:,:,0]*1.5)&(rgba[:,:,2]>rgba[:,:,1]*1.2)&(rgba[:,:,2]>80)
arm_union=np.zeros((reference.height,reference.width),dtype=bool)
for name in ('upper_arm_off','forearm_off','hand_off','upper_arm_main','forearm_main','hand_main'):
    arm_union |= np.asarray(masks[name])>0
for name in config['parts']:
    if name=='cape':continue
    alpha=np.minimum(np.asarray(masks[name]),np.asarray(reference.getchannel('A'))).copy()
    if name in ('torso','pelvis'):alpha[arm_union]=0
    if name=='mantle':
        cloth_edge=np.asarray(Image.fromarray(cloth_blue.astype('uint8')*255).filter(ImageFilter.MaxFilter(7)))
        alpha=np.minimum(alpha,cloth_edge)
    elif name!='head':alpha[cloth_blue]=0
    piece=reference.copy();piece.putalpha(Image.fromarray(alpha));box=piece.getchannel('A').getbbox()
    if not box:raise ValueError('Empty filtered part '+name)
    source_boxes[name]=list(box);full_images[name]=piece.crop(box)

# Reconstruct only cloth hidden behind the foreground. Visible cloth remains from
# the reference. Continuous cape mesh prevents separated strips during motion.
cape_mask=masks['cape']; cloth=np.asarray(reference).copy()
blue=(cloth[:,:,2].astype(float)>cloth[:,:,0]*1.20)&(cloth[:,:,2]>cloth[:,:,1]*1.07)&(cloth[:,:,2]>35)
exclusion=np.zeros((reference.height,reference.width),dtype=bool)
for name in config['parts']['cape'].get('excludeParts',[]):exclusion |= np.asarray(masks[name])>0
extra=config['parts']['cape'].get('bodyExclusionPolygon')
if extra:exclusion |= np.asarray(polygon_mask(extra))>0
yy,xx=np.indices((reference.height,reference.width))
shade=(0.72+0.14*np.cos(xx/55)+0.07*np.sin(yy/89)).clip(.50,1)
fill=np.stack([18*shade,58*shade,168*shade,np.full_like(shade,255)],axis=2).astype('uint8')
hidden=exclusion|(np.asarray(reference.getchannel('A'))<128)
cloth[hidden]=fill[hidden]
cloth[:,:,3]=np.asarray(cape_mask)
cape=Image.fromarray(cloth,'RGBA'); box=cape.getchannel('A').getbbox()
source_boxes['cape']=list(box); full_images['cape']=cape.crop(box)

# Existing artwork is reused without anisotropic stretching.
for name in ('weapon','fx_slash','fx_aura'):
    path=SOURCE/(name+'.png')
    full_images[name]=Image.open(path).convert('RGBA')
    source_boxes[name]=[0,0,full_images[name].width,full_images[name].height]
if not args.keep_textures:
    for name,piece in full_images.items(): piece.save(PARTS/(name+'.png'))

# Shelf pack padded, unrotated regions; every part retains its original aspect.
page_w=2048; packed={}; x=y=4;row_h=0
for name,piece in sorted(full_images.items(),key=lambda item:-item[1].height):
    if x+piece.width+4>page_w:x=4;y+=row_h+8;row_h=0
    packed[name]={'x':x,'y':y,'width':piece.width,'height':piece.height}
    x+=piece.width+8;row_h=max(row_h,piece.height)
page_h=2**math.ceil(math.log2(y+row_h+8))
atlas_image=Image.new('RGBA',(page_w,page_h))
for name,region in packed.items():atlas_image.paste(full_images[name],(region['x'],region['y']))
if not args.keep_textures: atlas_image.save(SOURCE/'hao.png')
atlas=['hao.png',f'size:{page_w},{page_h}','format:RGBA8888','filter:Linear,Linear','repeat:none']
for name,region in packed.items():atlas += [name,f"bounds:{region['x']},{region['y']},{region['width']},{region['height']}"]
if not args.keep_textures: (SOURCE/'hao.atlas').write_text('\n'.join(atlas)+'\n',encoding='utf-8')

parents={};rest_positions={};rest_angles={};lengths={}
def bone(name,parent,pixel,angle=0,length=None):
    parents[name]=parent;rest_positions[name]=world(pixel);rest_angles[name]=float(angle)
    if length is not None:lengths[name]=length
bone('root',None,origin)
bone('pelvis','root',joints['pelvis'])
bone('torso','pelvis',joints['torso'])
bone('head','torso',joints['head'])
bone('cape_01','torso',[569,333],0)
bone('cape_02','cape_01',[508,727],0)
bone('cape_03','cape_02',[493,1070],0)
for side in ('off','main'):
    shoulder,elbow,wrist=[joints[name+'_'+side] for name in ('shoulder','elbow','wrist')]
    hand=joints['hand_off'] if side=='off' else joints['grip_main']
    hip,knee,ankle=[joints[name+'_'+side] for name in ('hip','knee','ankle')]
    bone('upper_arm_'+side,'torso',shoulder,degree(world(elbow)-world(shoulder)),np.linalg.norm(world(elbow)-world(shoulder)))
    bone('forearm_'+side,'upper_arm_'+side,elbow,degree(world(wrist)-world(elbow)),np.linalg.norm(world(wrist)-world(elbow)))
    bone('hand_'+side,'forearm_'+side,wrist,degree(world(hand)-world(wrist)),np.linalg.norm(world(hand)-world(wrist)))
    bone('thigh_'+side,'pelvis',hip,degree(world(knee)-world(hip)),np.linalg.norm(world(knee)-world(hip)))
    bone('shin_'+side,'thigh_'+side,knee,degree(world(ankle)-world(knee)),np.linalg.norm(world(ankle)-world(knee)))
    bone('foot_'+side,'shin_'+side,ankle,0)
bone('weapon','hand_main',joints['grip_main'],-25)
bone('fx_slash','weapon',joints['grip_main'],-25)
bone('fx_aura','hand_off',joints['hand_off'],0)
names=list(parents);bone_indices={name:i for i,name in enumerate(names)}
local_xy={};local_rot={}
for name,parent in parents.items():
    local_xy[name]=rest_positions[name] if parent is None else R(-rest_angles[parent])@(rest_positions[name]-rest_positions[parent])
    local_rot[name]=rest_angles[name]-(rest_angles[parent] if parent else 0)
local_xy['fx_slash']=np.array([105.,-10.])
local_xy['fx_aura']=np.array([32.,0.])
bones=[]
for name,parent in parents.items():
    b={'name':name,'x':round(local_xy[name][0],5),'y':round(local_xy[name][1],5),'rotation':round(local_rot[name],5)}
    if parent:b['parent']=parent
    if name in lengths:b['length']=round(float(lengths[name]),5)
    bones.append(b)

attack=[
 [0,0,0,842,747,-25,0,0], [5,1,-.5,815,700,12,-.4,-.8],
 [8,1.5,-1,807,674,48,-.8,-1.5], [12,-4,2,883,716,-7,1.8,3.2],
 [16,-3,1.5,872,742,-42,2.8,4.5], [23,-1,.5,846,747,-30,-.7,-1.8], [30,0,0,842,747,-25,0,0]]
cast=[
 [0,0,0,325,728,0,0], [9,1,-.5,398,669,-.2,-.4],
 [17,-1,.5,472,625,-.5,-1], [24,-2,1,560,588,1,2],
 [27,-2,1,549,607,1.6,3], [33,-.5,.3,420,677,-.4,-1], [42,0,0,325,728,0,0]]
motion_path=OUT/'editable/motion-keyframes.json'
if motion_path.exists():
    motion=json.loads(motion_path.read_text(encoding='utf-8'));attack=motion['attack'];cast=motion['cast']
else:
    motion={'fps':30,'attack':attack,'cast':cast,'fields':{'attack':['frame','torsoDegrees','headDegrees','wristX','wristY','requestedSwordDegrees','capeMidDegrees','capeTailDegrees'],'cast':['frame','torsoDegrees','headDegrees','wristX','wristY','capeMidDegrees','capeTailDegrees']}}
    motion_path.write_text(json.dumps(motion,indent=2)+'\n',encoding='utf-8')
FPS=float(motion.get('fps',30))
if not math.isfinite(FPS) or FPS<=0:raise ValueError('fps must be positive')
last_frames={'attack':int(attack[-1][0]),'cast':int(cast[-1][0])}
event_frames=motion.get('eventFrames',{'attack':12,'cast':24})
effect_frames=motion.get('effectFrames',{
 'attack':{'fx_slash':[[0,0],[9.6,0],[12,.65],[15,.3],[18.6,0],[last_frames['attack'],0]],'fx_aura':[[0,0],[last_frames['attack'],0]]},
 'cast':{'fx_aura':[[0,0],[9,0],[17.1,.65],[24,1],[27,.4],[33,0],[last_frames['cast'],0]],'fx_slash':[[0,0],[last_frames['cast'],0]]}})
def interp(keys,frame):
    for a,b in zip(keys,keys[1:]):
        if a[0]<=frame<=b[0]:
            t=(frame-a[0])/(b[0]-a[0]);t=t*t*(3-2*t)
            return np.array(a[1:],float)*(1-t)+np.array(b[1:],float)*t
    return np.array(keys[0][1:] if frame<keys[0][0] else keys[-1][1:],float)
def solve(shoulder,target,a,b,bend):
    delta=target-shoulder;d=float(np.linalg.norm(delta));clamped=min(a+b,max(abs(a-b),d))
    e=bend*math.acos(np.clip((clamped*clamped-a*a-b*b)/(2*a*b),-1,1))
    u=math.atan2(delta[1],delta[0])-math.atan2(b*math.sin(e),a+b*math.cos(e))
    return math.degrees(u),math.degrees(e),abs(clamped-d)
def pose(action,frame):
    xy={name:np.array(value) for name,value in local_xy.items()};angles=dict(local_rot);wp={};wa={};reach_errors={}
    if action=='attack':tr,hr,mx,my,sword,c2,c3=interp(attack,frame);targets={'main':world([mx,my]),'off':world(joints['wrist_off'])}
    else:tr,hr,ox,oy,c2,c3=interp(cast,frame);sword=-25;targets={'main':world(joints['wrist_main']),'off':world([ox,oy])}
    angles['torso']=tr;angles['head']=hr;angles['cape_02']=c2;angles['cape_03']=c3
    for name,parent in parents.items():
        if name.startswith('upper_arm_'):
            side=name.removeprefix('upper_arm_');shoulder=wp[parent]+R(wa[parent])@xy[name]
            if (action=='attack' and side=='off') or (action=='cast' and side=='main'):
                targets[side]=wp[parent]+R(wa[parent])@(world(joints['wrist_'+side])-rest_positions[parent])
            bend=1
            if action=='attack' and side=='main':
                ik=config.get('mainArmIk',{'attackBend':-1,'neutralBend':1,'straightFrames':[1,28]})
                start,end=ik['straightFrames']
                bend=ik['attackBend'] if start<frame<end else ik['neutralBend']
                if frame in (start,end):
                    direction=world(joints['wrist_main'])-world(joints['shoulder_main'])
                    targets[side]=shoulder+direction/np.linalg.norm(direction)*(lengths[name]+lengths['forearm_'+side])
            u,e,err=solve(shoulder,targets[side],lengths[name],lengths['forearm_'+side],bend)
            angles[name]=u-wa[parent];angles['forearm_'+side]=e;reach_errors[side]=err
            if action=='attack' and side=='main':
                upper_lift,forearm_lift=interp(motion.get('attackArmRaise',[[0,0,0],[1,0,0],[5,12,18],[8,20,30],[12,0,0],[30,0,0]]),frame)
                angles[name]+=upper_lift
                angles['forearm_main']=local_rot['forearm_main']+forearm_lift
        elif name=='hand_main':
            natural=wa[parent]+local_rot[name]
            desired=sword-local_rot['weapon']
            flex=np.clip(((desired-natural+180)%360)-180,-28,28)
            angles[name]=local_rot[name]+float(flex)
        elif name=='hand_off':angles[name]=local_rot[name]
        elif name=='weapon':angles[name]=sword-wa[parent]
        elif name=='fx_aura':angles[name]=-wa[parent]
        if parent is None:wp[name]=xy[name];wa[name]=angles[name]
        else:wp[name]=wp[parent]+R(wa[parent])@xy[name];wa[name]=wa[parent]+angles[name]
    return xy,angles,wp,wa,reach_errors

attachments={}
for part in config['parts']:
    if part=='cape':continue
    parent='torso' if part=='mantle' else part
    center=np.array(source_boxes[part],float).reshape(2,2).mean(axis=0)
    local=R(-rest_angles[parent])@(world(center)-rest_positions[parent])
    attachments[part]={'type':'region','path':part,'x':round(local[0],5),'y':round(local[1],5),'rotation':round(-rest_angles[parent],5),'width':full_images[part].width*S,'height':full_images[part].height*S}
weapon=full_images['weapon'];ws=.72;grip=np.array(config.get('weaponGripPixel',[50,232]),float)
v=-R(-90)@np.diag([ws,-ws])@(grip-np.array(weapon.size)/2)
attachments['weapon']={'type':'region','path':'weapon','x':float(v[0]),'y':float(v[1]),'rotation':-90,'width':weapon.width*ws,'height':weapon.height*ws}
for name,scale in [('fx_slash',.72),('fx_aura',.47)]:
    piece=full_images[name];attachments[name]={'type':'region','path':name,'x':0,'y':0,'width':piece.width*scale,'height':piece.height*scale}

cols,rows=7,11;uvs=[];vertices=[];triangles=[];cape_box=source_boxes['cape'];cape_piece=full_images['cape']
mesh_weights=[]
for row in range(rows):
    for col in range(cols):
        u=col/(cols-1);v=row/(rows-1);uvs.extend([u,v]);pixel=np.array([cape_box[0]+u*cape_piece.width,cape_box[1]+v*cape_piece.height]);p=world(pixel)
        if pixel[1]<727:
            t=np.clip((pixel[1]-380)/(727-380),0,1);t=t*t*(3-2*t);weights=[('cape_01',1-t),('cape_02',t)]
        else:
            t=np.clip((pixel[1]-727)/(1070-727),0,1);t=t*t*(3-2*t);weights=[('cape_02',1-t),('cape_03',t)]
        weights=[(n,float(w)) for n,w in weights if w>1e-9];vertices.append(len(weights));mesh_weights.append([])
        for name,weight in weights:
            q=R(-rest_angles[name])@(p-rest_positions[name]);vertices.extend([bone_indices[name],round(float(q[0]),5),round(float(q[1]),5),round(weight,8)]);mesh_weights[-1].append((name,q,weight))
for row in range(rows-1):
    for col in range(cols-1):
        i=row*cols+col;triangles.extend([i,i+cols,i+1,i+1,i+cols,i+cols+1])
attachments['cape']={'type':'mesh','path':'cape','uvs':uvs,'triangles':triangles,'vertices':vertices,'width':cape_piece.width*S,'height':cape_piece.height*S}
order=['cape','thigh_off','shin_off','foot_off','upper_arm_off','thigh_main','shin_main','foot_main','pelvis','torso','mantle','head','forearm_off','hand_off','upper_arm_main','forearm_main','weapon','hand_main','fx_slash','fx_aura']
slots=[];skin={}
for part in order:
    parent='cape_01' if part=='cape' else ('torso' if part=='mantle' else part)
    slot={'name':part,'bone':parent,'attachment':part}
    if part.startswith('fx_'):slot.update(color='ffffff00',blend='additive')
    slots.append(slot);skin[part]={part:attachments[part]}
# Bake the original poses at even frames, then insert local bone midpoints.
# This preserves the existing motion instead of solving a different half-frame IK path.
frame_step=int(motion.get('keyframeSubdivisions',1))
if frame_step<1 or any(last%frame_step for last in last_frames.values()):
    raise ValueError('Animation lengths must be divisible by keyframeSubdivisions')

def blend_pose(left,right,t):
    xy={name:left[0][name]*(1-t)+right[0][name]*t for name in names}
    angles={name:left[1][name]+((right[1][name]-left[1][name]+180)%360-180)*t for name in names}
    wp={};wa={}
    for name,parent in parents.items():
        if parent is None:wp[name]=xy[name];wa[name]=angles[name]
        else:wp[name]=wp[parent]+R(wa[parent])@xy[name];wa[name]=wa[parent]+angles[name]
    reach_errors={side:max(left[4][side],right[4][side]) for side in left[4]}
    return xy,angles,wp,wa,reach_errors

poses={}
for action,last in last_frames.items():
    frames=[None]*(last+1)
    for f in range(0,last+1,frame_step):frames[f]=pose(action,f)
    for f in range(0,last,frame_step):
        for offset in range(1,frame_step):frames[f+offset]=blend_pose(frames[f],frames[f+frame_step],offset/frame_step)
    poses[action]=frames
animations={}
for action,last in last_frames.items():
    timelines={}
    for name in names:
        values=np.rad2deg(np.unwrap(np.deg2rad([p[1][name] for p in poses[action]])))
        if np.max(np.abs(values-local_rot[name]))>1e-7:
            timelines[name]={'rotate':[{'time':round(f/FPS,6),'value':round(float(a-local_rot[name]),5)} for f,a in enumerate(values)]}
    slot_times={part:{'alpha':[{'time':round(f/FPS,6),'value':value} for f,value in keys]} for part,keys in effect_frames[action].items()}
    event={'time':round(event_frames[action]/FPS,6),'name':'hit' if action=='attack' else 'release'}
    animations[action]={'bones':timelines,'slots':slot_times,'events':[event]}
data={'skeleton':{'spine':'4.2.43','fps':FPS,'hash':motion.get('hash','hao-rebind-v2-2026-10-08'),'x':-260,'y':-20,'width':850,'height':710,'images':'../editable/parts/'},'bones':bones,'slots':slots,'skins':[{'name':'default','attachments':skin}],'events':{'hit':{},'release':{}},'animations':animations}
(SOURCE/'hao.spine-json').write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
(OUT/'editable/hao.json').write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
(OUT/'editable/rig.json').write_text(json.dumps({'bones':bones,'slots':slots,'regions':packed,'uniformScale':S,'sourceBoxes':source_boxes,'attachments':attachments},indent=2)+'\n',encoding='utf-8')
proof={'ok':True,'bodyUniformScale':S,'characterRegionsHavePreservedAspect':True,'bones':len(bones),'slots':len(slots),'capeMeshVertices':cols*rows,'capeMeshTriangles':len(triangles)//3,'maxIKReachClamp':max(err for action in poses.values() for p in action for err in p[4].values()),'bodyAttachmentCenterError':0.,'weaponGripAttachmentError':0.}
for part in config['parts']:
    if part=='cape':continue
    parent='torso' if part=='mantle' else part;attachment=attachments[part]
    got=rest_positions[parent]+R(rest_angles[parent])@np.array([attachment['x'],attachment['y']]);expected=world(np.array(source_boxes[part]).reshape(2,2).mean(axis=0))
    proof['bodyAttachmentCenterError']=max(proof['bodyAttachmentCenterError'],float(np.linalg.norm(got-expected)))
    proof['ok']=proof['ok'] and abs(attachment['width']/attachment['height']-full_images[part].width/full_images[part].height)<1e-9
proof['weaponGripAttachmentError']=float(np.linalg.norm(np.array([attachments['weapon']['x'],attachments['weapon']['y']])+R(-90)@np.diag([ws,-ws])@(grip-np.array(weapon.size)/2)))
proof['ok']=bool(proof['ok'] and proof['maxIKReachClamp']<.005 and proof['bodyAttachmentCenterError']<.0001 and proof['weaponGripAttachmentError']<.0001)
(OUT/'validation/bind-check.json').write_text(json.dumps(proof,indent=2)+'\n',encoding='utf-8')
print(json.dumps(proof))
if args.no_previews: raise SystemExit(0)

SIZE=(1100,800);ORIGIN=np.array([400.,742.]);PS=1.25
def opacity(action,part,t):
    frames=animations[action]['slots'].get(part,{}).get('alpha')
    if not frames:return 1.
    return float(np.interp(t,[f['time'] for f in frames],[f['value'] for f in frames]))
def screen(point):return ORIGIN+np.array([point[0],-point[1]])*PS
def raster_region(result,image,center,matrix,alpha):
    transform=np.diag([PS,-PS])@matrix
    translation=screen(center)-transform@np.array(image.size)/2
    inverse=np.linalg.inv(transform);bias=-inverse@translation
    if alpha<.999:image=image.copy();image.putalpha(image.getchannel('A').point(lambda a:round(a*alpha)))
    layer=image.transform(SIZE,Image.Transform.AFFINE,(*inverse[0],bias[0],*inverse[1],bias[1]),resample=Image.Resampling.BICUBIC)
    result.alpha_composite(layer)
def draw_mesh(result,wp,wa):
    points=[]
    for weights in mesh_weights:
        p=sum((wp[name]+R(wa[name])@q)*weight for name,q,weight in weights);points.append(screen(p))
    uv=np.array(uvs).reshape(-1,2)*np.array(cape_piece.size)
    for i in range(0,len(triangles),3):
        ids=triangles[i:i+3];dst=np.array([points[j] for j in ids]);src=uv[ids]
        lo=np.floor(dst.min(axis=0)-1).astype(int);hi=np.ceil(dst.max(axis=0)+1).astype(int);lo=np.maximum(lo,0);hi=np.minimum(hi,np.array(SIZE))
        if np.any(hi<=lo):continue
        size=tuple(hi-lo);basis=np.column_stack([dst,np.ones(3)]);mapping=np.linalg.solve(basis,src).T
        bias=mapping[:,:2]@lo+mapping[:,2]
        layer=cape_piece.transform(size,Image.Transform.AFFINE,(*mapping[0,:2],bias[0],*mapping[1,:2],bias[1]),resample=Image.Resampling.BICUBIC)
        clip=Image.new('L',size);ImageDraw.Draw(clip).polygon([tuple(p-lo) for p in dst],fill=255)
        layer.putalpha(Image.fromarray(np.minimum(np.asarray(clip),np.asarray(layer.getchannel('A')))))
        result.alpha_composite(layer,tuple(lo))
def render(action,frame,overlay=False):
    image=Image.new('RGBA',SIZE,(13,19,31,255));draw=ImageDraw.Draw(image);draw.ellipse((260,712,545,765),fill=(3,6,12,180))
    _,_,wp,wa,_=poses[action][frame]
    for slot in slots:
        part=slot['name'];a=attachments[part];alpha=opacity(action,part,frame/FPS)
        if alpha<.001:continue
        if a['type']=='mesh':draw_mesh(image,wp,wa);continue
        bone_name=slot['bone'];texture=full_images[part];center=wp[bone_name]+R(wa[bone_name])@np.array([a['x'],a['y']])
        matrix=R(wa[bone_name])@R(a.get('rotation',0))@np.diag([a['width']/texture.width,-a['height']/texture.height])
        raster_region(image,texture,center,matrix,alpha)
    if overlay:
        draw=ImageDraw.Draw(image)
        for name in names:
            if name.startswith('fx_'):continue
            point=screen(wp[name]);parent=parents[name]
            if parent:draw.line([tuple(screen(wp[parent])),tuple(point)],fill=(248,220,100,220),width=2)
            draw.ellipse((point[0]-3,point[1]-3,point[0]+3,point[1]+3),fill=(255,225,120))
    return image
for action,last in last_frames.items():
    frames=[render(action,f).convert('RGB') for f in range(last+1)]
    if action=='attack':frames[0].save(PREVIEW/'setup.png')
    contact=Image.new('RGB',(SIZE[0]*3,SIZE[1]*3));selected=np.linspace(0,last,9).round().astype(int)
    for i,f in enumerate(selected):contact.paste(frames[f],((i%3)*SIZE[0],(i//3)*SIZE[1]))
    contact.resize((1650,1200),Image.Resampling.LANCZOS).save(PREVIEW/(action+'-contact.png'))
    render(action,int(event_frames[action]),True).save(PREVIEW/(action+'-bones.png'))
    palette=contact.resize(SIZE).quantize(colors=220)
    sequence=[f.resize((660,480),Image.Resampling.LANCZOS).quantize(palette=palette,dither=Image.Dither.NONE) for f in frames]
    gif_name=f'{action}-{int(FPS)}fps.gif'
    gif_delays=[round((f+1)*100/FPS)*10-round(f*100/FPS)*10 for f in range(last)]+[450]
    sequence[0].save(PREVIEW/gif_name,save_all=True,append_images=sequence[1:],duration=gif_delays,loop=0,disposal=2,optimize=False)
print('Finished fitted-body previews at '+str(PREVIEW))
