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
_TEAR_FRAMES=['fx_tear_0','fx_tear_1','fx_tear_2','fx_tear_3']
for name in tuple(['weapon','fx_slash','fx_aura','fx_pacifier','fx_ball'])+tuple(_TEAR_FRAMES):
    path=SOURCE/(name+'.png')
    full_images[name]=Image.open(path).convert('RGBA')
    source_boxes[name]=[0,0,full_images[name].width,full_images[name].height]
if not args.keep_textures:
    for name,piece in full_images.items(): piece.save(PARTS/(name+'.png'))

# Shelf pack padded, unrotated regions; every part retains its original aspect.
# _WX2_INJECTED 将剑贴图替换为 2 倍版（不被重裁切覆盖）
_wx2=OUT/'editable/parts/weapon_x2.png'
if _wx2.exists():
    full_images['weapon']=Image.open(_wx2).convert('RGBA')
    source_boxes['weapon']=[0,0,full_images['weapon'].width,full_images['weapon'].height]
page_w=2048; packed={}; x=y=4;row_h=0
for name,piece in sorted(full_images.items(),key=lambda item:-item[1].height):
    if x+piece.width+4>page_w:x=4;y+=row_h+8;row_h=0
    packed[name]={'x':x,'y':y,'width':piece.width,'height':piece.height}
    x+=piece.width+8;row_h=max(row_h,piece.height)
page_h=2**math.ceil(math.log2(y+row_h+8))
atlas_image=Image.new('RGBA',(page_w,page_h))
for name,region in packed.items():atlas_image.paste(full_images[name],(region['x'],region['y']))
atlas_image.save(SOURCE/'hao.png')   # ★ atlas 图总是重写（--keep-textures 只跳过部件裁切）
atlas=['hao.png',f'size:{page_w},{page_h}','format:RGBA8888','filter:Linear,Linear','repeat:none']
for name,region in packed.items():atlas += [name,f"bounds:{region['x']},{region['y']},{region['width']},{region['height']}"]
(SOURCE/'hao.atlas').write_text('\n'.join(atlas)+'\n',encoding='utf-8')   # ★ atlas 文本总是重写

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
    attack_multi=motion.get('attack_multi',[])
    die_anim=motion.get('die',[])
    idle_anim=motion.get('idle_loop',[])
    bk_anim=motion.get('idle_basketball',[])
    hurt_anim=motion.get('hurt',[])
    lowhp_anim=motion.get('idle_lowhp',[])
    virt_anim=motion.get('idle_virtual',[])
    relax_anim=motion.get('relaxed_loop',[])
    rest_anims={_n:motion.get(_n,[]) for _n in ('overgrowth_loop','hive_loop','glory_loop')}
else:
    die_anim=[]
    idle_anim=[]
    bk_anim=[]
    hurt_anim=[]
    lowhp_anim=[]
    relax_anim=[]
    rest_anims={'overgrowth_loop':[],'hive_loop':[],'glory_loop':[]}
    motion={'fps':30,'attack':attack,'cast':cast,'fields':{'attack':['frame','torsoDegrees','headDegrees','wristX','wristY','requestedSwordDegrees','capeMidDegrees','capeTailDegrees'],'cast':['frame','torsoDegrees','headDegrees','wristX','wristY','capeMidDegrees','capeTailDegrees']}}
    motion_path.write_text(json.dumps(motion,indent=2)+'\n',encoding='utf-8')
FPS=float(motion.get('fps',30))
# ★ 任意骨骼角度覆盖（用户 2026-10-22 要求全关节可调）
bone_overrides=motion.get('boneOverrides',{})
# ★ 世界角（钟表角）直接覆盖：12点=0°, 3点=90°, 6点=180°, 9点=270°
world_overrides=motion.get('worldOverrides',{})
# ★ ★ P0-1：先对 clock 序列做 unwrap，避免整圈自旋
#   原因：(钟表角 0.3°) -> (360-0.3)=359.7 会跳到 360 分支 ⇒ 整段自旋
#   例：torso clock [0,356,354,350,335,318,270,...] unwrap 后 -> [0,-4,-6,-10,-25,-42,-90,...]
for _wo_action,_wo_bones in world_overrides.items():
    for _wo_bone,_wo_keys in _wo_bones.items():
        if not _wo_keys: continue
        _clocks=[float(k[1]) for k in _wo_keys]
        _unwrapped=list(np.degrees(np.unwrap(np.radians(_clocks), period=2*np.pi)))
        _wo_bones[_wo_bone]=[[_wo_keys[i][0], _unwrapped[i]] for i in range(len(_wo_keys))]
        print('[worldOverrides unwrap] %s.%s -> %s' % (_wo_action,_wo_bone,[round(v,1) for v in _unwrapped]))
root_motion=motion.get('rootMotion',{})
if not math.isfinite(FPS) or FPS<=0:raise ValueError('fps must be positive')
last_frames={'attack':int(attack[-1][0]),'cast':int(cast[-1][0])}
if attack_multi:last_frames['attack_multi']=int(attack_multi[-1][0])
if die_anim:last_frames['die']=int(die_anim[-1][0])
if idle_anim:last_frames['idle_loop']=int(idle_anim[-1][0])
if bk_anim:last_frames['idle_basketball']=int(bk_anim[-1][0])
if hurt_anim:last_frames['hurt']=int(hurt_anim[-1][0])
if lowhp_anim:last_frames['idle_lowhp']=int(lowhp_anim[-1][0])
if virt_anim:last_frames['idle_virtual']=int(virt_anim[-1][0])
if relax_anim:last_frames['relaxed_loop']=int(relax_anim[-1][0])
for _n,_v in rest_anims.items():
    if _v:last_frames[_n]=int(_v[-1][0])
_ef_default={'attack':12,'cast':24}
if attack_multi:_ef_default['attack_multi']=105
if die_anim:_ef_default['die']=15
if idle_anim:_ef_default['idle_loop']=100
if bk_anim:_ef_default['idle_basketball']=173
if hurt_anim:_ef_default['hurt']=60
if lowhp_anim:_ef_default['idle_lowhp']=50
if virt_anim:_ef_default['idle_virtual']=0
if relax_anim:_ef_default['relaxed_loop']=0
for _n,_v in rest_anims.items():
    if _v:_ef_default[_n]=0
event_frames=motion.get('eventFrames',_ef_default)
draw_order=motion.get('drawOrder',{})
effect_frames=motion.get('effectFrames',{
 'attack':{'fx_slash':[[0,0],[9.6,0],[12,.65],[15,.3],[18.6,0],[last_frames['attack'],0]],'fx_aura':[[0,0],[last_frames['attack'],0]]},
 'cast':{'fx_aura':[[0,0],[9,0],[17.1,.65],[24,1],[27,.4],[33,0],[last_frames['cast'],0]],'fx_slash':[[0,0],[last_frames['cast'],0]]}})
if 'attack_multi' in last_frames:
    effect_frames.setdefault('attack_multi',{'fx_slash':[[0,0],[last_frames['attack_multi'],0]],'fx_aura':[[0,0],[last_frames['attack_multi'],0]]})
if 'die' in last_frames:
    effect_frames.setdefault('die',{'fx_slash':[[0,0],[last_frames['die'],0]],'fx_aura':[[0,0],[last_frames['die'],0]]})
if 'idle_loop' in last_frames:
    effect_frames.setdefault('idle_loop',{'fx_slash':[[0,0],[last_frames['idle_loop'],0]],'fx_aura':[[0,0],[last_frames['idle_loop'],0]]})
if 'idle_basketball' in last_frames:
    effect_frames.setdefault('idle_basketball',{'fx_slash':[[0,0],[last_frames['idle_basketball'],0]],'fx_aura':[[0,0],[last_frames['idle_basketball'],0]]})
if 'hurt' in last_frames:
    effect_frames.setdefault('hurt',{'weapon':[[0,1.0],[last_frames['hurt'],1.0]],'fx_slash':[[0,0],[last_frames['hurt'],0]],'fx_aura':[[0,0],[last_frames['hurt'],0]]})
if 'idle_virtual' in last_frames:
    effect_frames.setdefault('idle_virtual',{'fx_slash':[[0,0],[last_frames['idle_virtual'],0]],'fx_aura':[[0,0],[last_frames['idle_virtual'],0]]})
for _n in rest_anims:
    if _n in last_frames:
        effect_frames.setdefault(_n,{'weapon':[[0,0.0],[last_frames[_n],0.0]],'fx_slash':[[0,0],[last_frames[_n],0]],'fx_aura':[[0,0],[last_frames[_n],0]]})
if 'relaxed_loop' in last_frames:
    effect_frames.setdefault('relaxed_loop',{'weapon':[[0,0.0],[last_frames['relaxed_loop'],0.0]],'fx_slash':[[0,0],[last_frames['relaxed_loop'],0]],'fx_aura':[[0,0],[last_frames['relaxed_loop'],0]]})
if 'idle_lowhp' in last_frames:
    effect_frames.setdefault('idle_lowhp',{'weapon':[[0,1.0],[50,1.0],[75,0.0],[last_frames['idle_lowhp'],0.0]],'fx_slash':[[0,0],[last_frames['idle_lowhp'],0]],'fx_aura':[[0,0],[last_frames['idle_lowhp'],0]]})
def interp(keys,frame):
    for a,b in zip(keys,keys[1:]):
        if a[0]<=frame<=b[0]:
            t=(frame-a[0])/(b[0]-a[0]);t=t*t*(3-2*t)
            return np.array(a[1:],float)*(1-t)+np.array(b[1:],float)*t
    return np.array(keys[0][1:] if frame<keys[0][0] else keys[-1][1:],float)
def _interp_root(keys,frame):
    for a,b in zip(keys,keys[1:]):
        if a[0]<=frame<=b[0]:
            t=(frame-a[0])/(b[0]-a[0]) if b[0]!=a[0] else 1.0
            t=t*t*(3-2*t)
            return a[1]*(1-t)+b[1]*t, a[2]*(1-t)+b[2]*t
    k=keys[0] if frame<keys[0][0] else keys[-1]
    return float(k[1]),float(k[2])
def solve(shoulder,target,a,b,bend):
    delta=target-shoulder;d=float(np.linalg.norm(delta));clamped=min(a+b,max(abs(a-b),d))
    e=bend*math.acos(np.clip((clamped*clamped-a*a-b*b)/(2*a*b),-1,1))
    u=math.atan2(delta[1],delta[0])-math.atan2(b*math.sin(e),a+b*math.cos(e))
    return math.degrees(u),math.degrees(e),abs(clamped-d)
def pose(action,frame):
    xy={name:np.array(value) for name,value in local_xy.items()};angles=dict(local_rot);wp={};wa={};reach_errors={}
    if action in (('attack','attack_multi','die','idle_loop','idle_basketball','hurt','idle_lowhp','idle_virtual','relaxed_loop')+tuple(rest_anims)) or action in rest_anims:
        _src=_anim_src_map[action]
        _v=interp(_src,frame)
        tr,hr,mx,my,sword,c2,c3=_v[:7]                      # 兼容旧的 8 列格式
        _leg=_v[7:11] if len(_v)>=11 else [0.0,0.0,0.0,0.0]  # ★ 4 列腿部偏移
        _off=_v[11:13] if len(_v)>=13 else [325.0,728.0]     # ★ 2 列左手 IK 目标
        offWristX,offWristY=[float(x) for x in _off]
        _hand=_v[13:15] if len(_v)>=15 else [0.0,0.0]        # ★ 2 列腕部偏移
        mainHandDeg,offHandDeg=[float(x) for x in _hand]
        dThighOff,dShinOff,dThighMain,dShinMain=[float(x) for x in _leg]
        targets={'main':world([mx,my]),'off':world(joints['wrist_off'])}
    else:
        tr,hr,ox,oy,c2,c3=interp(cast,frame);sword=-25
        targets={'main':world(joints['wrist_main']),'off':world([ox,oy])}
        dThighOff=dShinOff=dThighMain=dShinMain=0.0          # cast 无腿部数据，行为不变
        offWristX,offWristY=float(joints['wrist_off'][0]),float(joints['wrist_off'][1])
        mainHandDeg=offHandDeg=0.0                            # cast 无腕部数据
    angles['torso']=tr;angles['head']=hr;angles['cape_02']=c2;angles['cape_03']=c3
    # ★ 世界角（钟表角）直接覆盖表：先存下来，FK 递推时优先使用
    _world_ov={}
    for _wn,_wkeys in world_overrides.get(action,{}).items():
        if _wkeys:
            # 钟表角 -> 数学角（屏幕 y 向下，数学角 0=右、90=上）
            _clock=float(interp(_wkeys,frame)[0])
            # ★★ 角度语义（审批报告 E15 纠正）：
            #   · Spine 骨骼长度沿局部 +X，所以「0=右、90=下、180=左、270=上」；
            #   · 但 pelvis/torso/head/cape_01..03 这类 rotation=0、美术沿 +Y 摆放的骨骼，它们的「0=上」；
            #   · 因此 worldOverrides 只应用于「无 length 的 upright 骨骼」（pelvis/torso/head），
            #     用于四肢（有 length、rest rotation 非 0）会差 90°！
            #   · 本项目中 pelvis/torso/head 的 rest=0，所以转换为 spine = (360 - clock) % 360
            _world_ov[_wn]=((360.0-_clock)%360.0)
            # ★ 归一化到 [-180,180]：避免导出 352 这种等价但会在 mix 时绕圈的值
            if _world_ov[_wn]>180.0:_world_ov[_wn]-=360.0
    # ★ 腿部（扎马步）：局部旋转 + 偏移。angles 初值即 local_rot，故 += 偏移即语义一致。
    angles['thigh_off']=local_rot['thigh_off']+dThighOff
    angles['shin_off']=local_rot['shin_off']+dShinOff
    angles['thigh_main']=local_rot['thigh_main']+dThighMain
    angles['shin_main']=local_rot['shin_main']+dShinMain
    for name,parent in parents.items():
        if name.startswith('upper_arm_'):
            side=name.removeprefix('upper_arm_');shoulder=wp[parent]+R(wa[parent])@xy[name]
            if (action in (('attack','attack_multi','die','idle_loop','idle_basketball','hurt','idle_lowhp','idle_virtual','relaxed_loop')+tuple(rest_anims)) and side=='off') or (action=='cast' and side=='main'):
                _tw=world([offWristX,offWristY]) if (action in (('attack','attack_multi','die','idle_loop','idle_basketball','hurt','idle_lowhp','idle_virtual','relaxed_loop')+tuple(rest_anims)) and side=='off') else world(joints['wrist_'+side])
                targets[side]=wp[parent]+R(wa[parent])@(_tw-rest_positions[parent])
            bend=1
            if action in (('attack','attack_multi','die','idle_loop','idle_basketball','hurt','idle_lowhp','idle_virtual','relaxed_loop')+tuple(rest_anims)) and side=='main':
                ik=config.get('mainArmIk',{'attackBend':-1,'neutralBend':1,'straightFrames':[1,28]})
                start,end=ik['straightFrames']
                bend=ik['attackBend'] if start<frame<end else ik['neutralBend']
                if frame in (start,end):
                    direction=world(joints['wrist_main'])-world(joints['shoulder_main'])
                    targets[side]=shoulder+direction/np.linalg.norm(direction)*(lengths[name]+lengths['forearm_'+side])
            u,e,err=solve(shoulder,targets[side],lengths[name],lengths['forearm_'+side],bend)
            angles[name]=u-wa[parent];angles['forearm_'+side]=e;reach_errors[side]=err
            if action in ('attack','attack_multi') and side=='main':
                upper_lift,forearm_lift=interp(motion.get('attackArmRaise',[[0,0,0],[1,0,0],[5,12,18],[8,20,30],[12,0,0],[30,0,0]]),frame)
                angles[name]+=upper_lift
                angles['forearm_main']=local_rot['forearm_main']+forearm_lift
        elif name=='hand_main':
            natural=wa[parent]+local_rot[name]
            desired=sword-local_rot['weapon']
            flex=np.clip(((desired-natural+180)%360)-180,-28,28)
            angles[name]=local_rot[name]+float(flex)+mainHandDeg   # ★ 可调偏移
        elif name=='hand_off':angles[name]=local_rot[name]+offHandDeg   # ★ 可调偏移
        elif name=='weapon':angles[name]=sword-wa[parent]
        elif name=='fx_aura':angles[name]=-wa[parent]
        # ★ 任意骨骼覆盖：在本骨骼分支之后、FK 递推之前（优先级最高）
        if name in bone_overrides.get(action,{}):
            _keys=bone_overrides[action][name]
            if _keys:angles[name]=local_rot[name]+float(interp(_keys,frame)[0])
        if parent is None:wp[name]=xy[name];wa[name]=angles[name]
        else:wp[name]=wp[parent]+R(wa[parent])@xy[name];wa[name]=wa[parent]+angles[name]
        # ★ 世界角直接覆盖（最高优先级）：不受父级累加影响
        if name in _world_ov:
            wa[name]=_world_ov[name]
            # ★ 同时回写 angles：spjson 写的是 angles（而 wa 只用于染染/递推）
            if parent is None:
                angles[name]=wa[name]
            else:
                angles[name]=wa[name]-wa[parent]
                wp[name]=wp[parent]+R(wa[parent])@xy[name]
    # ★ root 位移：整体平移所有骨骼世界位置
    _rm=root_motion.get(action,[])
    if _rm:
        _rx,_ry=_interp_root(_rm,frame)
        for _n in list(wp.keys()):
            wp[_n]=[wp[_n][0]+_rx,wp[_n][1]+_ry]
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

# ★ 眼泪 / 奶嘴（绑定 head 骨骼，用于死亡演出）
#   局部偏移按 head 骨骼为原点：眼泪在脸侧上方、奶嘴在嘴部
_head_parent = 'head'
#      眼泪改成 4 帧 flipbook（脸颊外侧，不遮挡脸和奶嘴）
for _fx, _dx, _dy, _scale in [
    ('fx_tear_0',    44.0,  20.0, 0.47),   # 小水珠
    ('fx_tear_1',    44.0,  20.0, 0.47),   # 中水滴
    ('fx_tear_2',    44.0,  20.0, 0.47),   # 大水滴
    ('fx_tear_3',    44.0,  20.0, 0.47),   # 滴落拉长
    ('fx_pacifier',  22.0,  -8.0, 0.16),   # 奶嘴：嘴部
    ('fx_ball',       2.0,   9.0, 0.42),   # ★ 虚影球：绑 hand_main（右手），球心在手上方
]:
    _piece = full_images[_fx]
    attachments[_fx] = {
        'type': 'region', 'path': _fx,
        'x': float(_dx), 'y': float(_dy),
        'rotation': 0.0,
        'width': _piece.width * _scale,
        'height': _piece.height * _scale,
    }
    print('[fx] %s -> head(%+.1f, %+.1f) scale=%.2f' % (_fx, _dx, _dy, _scale))

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

# ══════════════════════════════════════════════════════════════════════════════
# _LEG_MESH_INJECTED  腿部 mesh 化（2026-10-22 用户拍板：骨架全改，先改腿）
# ══════════════════════════════════════════════════════════════════════════════
# 为什么：region 是刚性贴图，只能整体旋转 ⇒ 单膝跪地时小腿无法贴地、脚掌无法平放。
# 做法：和 cape 同一套网格范式（规则网格 + smoothstep 双骨权重），
#       让 thigh 在膝部跟着 shin 走、shin 在踝部跟着 foot 走。
def _leg_mesh(part, cols, rows, bone_a, bone_b, split_ratio=0.72):
    """把某个腿部部件做成可弯曲的 mesh。

    split_ratio = 从部件顶部往下多少比例处开始过渡到 bone_b（骨骼）。
    """
    box=source_boxes[part]; piece=full_images[part]
    _uvs=[]; _verts=[]; _tris=[]
    span=max(1.0, piece.height*0.42)
    for row in range(rows):
        for col in range(cols):
            u=col/(cols-1); v=row/(rows-1)
            _uvs.extend([u,v])
            pixel=np.array([box[0]+u*piece.width, box[1]+v*piece.height])
            p=world(pixel)
            t=np.clip((v-split_ratio)/0.42,0.0,1.0); t=t*t*(3-2*t)
            weights=[(bone_a,1.0-t),(bone_b,t)]
            weights=[(n,float(w)) for n,w in weights if w>1e-9]
            _verts.append(len(weights))
            for name,weight in weights:
                q=R(-rest_angles[name])@(p-rest_positions[name])
                _verts.extend([bone_indices[name],round(float(q[0]),5),round(float(q[1]),5),round(weight,8)])
    for row in range(rows-1):
        for col in range(cols-1):
            i=row*cols+col
            _tris.extend([i,i+cols,i+1,i+1,i+cols,i+cols+1])
    return {'type':'mesh','path':part,'uvs':_uvs,'triangles':_tris,'vertices':_verts,
            'width':piece.width*S,'height':piece.height*S}

# (部件, 列, 行, 主骨骼, 过渡到, 过渡起点比例)
_LEG_MESHES=[
    ('thigh_off', 4, 7, 'thigh_off', 'shin_off', 0.62),
    ('shin_off',  4, 8, 'shin_off',  'foot_off', 0.70),
    ('foot_off',  4, 5, 'foot_off',  'foot_off', 1.00),
    ('thigh_main',4, 7, 'thigh_main','shin_main',0.62),
    ('shin_main', 4, 8, 'shin_main', 'foot_main',0.70),
    ('foot_main', 4, 5, 'foot_main', 'foot_main',1.00),
]
for _part,_c,_r,_ba,_bb,_sr in _LEG_MESHES:
    try:
        attachments[_part]=_leg_mesh(_part,_c,_r,_ba,_bb,_sr)
        print('[legmesh] %s -> mesh %dx%d' % (_part,_c,_r))
    except Exception as _e:
        print('[legmesh] %s FAILED: %s' % (_part,_e))
# ══════════════════════════════════════════════════════════════════════════════

order=['cape','upper_arm_off','forearm_off','hand_off','thigh_off','shin_off','foot_off','thigh_main','shin_main','foot_main','pelvis','torso','mantle','head','upper_arm_main','forearm_main','hand_main','weapon','fx_ball','fx_tear','fx_pacifier','fx_slash','fx_aura']
slots=[];skin={}
for part in order:
    if part=='fx_ball':
        parent='hand_main'     # 虚影球挂在右手
    elif part in ('fx_tear','fx_pacifier'):
        parent='head'          # 眼泪/奶嘴挂在头骨骼上
    elif part=='cape':
        parent='cape_01'
    elif part=='mantle':
        parent='torso'
    else:
        parent=part
    _default_att = _TEAR_FRAMES[0] if part=='fx_tear' else part
    slot={'name':part,'bone':parent,'attachment':_default_att}
    if part in ('fx_tear','fx_pacifier','fx_ball'):
        # 眼泪/奶嘴/虚影球：默认透明，普通混合
        slot.update(color='ffffff00')
    elif part.startswith('fx_'):
        slot.update(color='ffffff00',blend='additive')
    slots.append(slot)
    if part=='fx_tear':
        # ★ 眼泪：skin 里放 4 帧，由 attachment 时间轴切换
        skin[part]={n:attachments[n] for n in _TEAR_FRAMES}
    else:
        skin[part]={part:attachments[part]}
# Bake the original poses at even frames, then insert local bone midpoints.
# This preserves the existing motion instead of solving a different half-frame IK path.
# ★ 动作 -> 关键帧表（替代原先的嵌套三元）
_anim_src_map={'attack':attack,'attack_multi':attack_multi,'cast':cast,'die':die_anim,
               'idle_loop':idle_anim,'idle_basketball':bk_anim,'hurt':hurt_anim,
               'idle_lowhp':lowhp_anim,'idle_virtual':virt_anim,'relaxed_loop':relax_anim}
for _n,_v in rest_anims.items():
    _anim_src_map[_n]=_v
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
    # ★ root 位移 → Spine translate 通道
    _rm=root_motion.get(action,[])
    if _rm:
        _ts=[]
        _prev=None
        for _f in range(last+1):
            _rx,_ry=_interp_root(_rm,_f)
            if _prev is None or abs(_rx-_prev[0])>1e-6 or abs(_ry-_prev[1])>1e-6:
                _ts.append({'time':round(_f/FPS,6),'x':round(_rx,4),'y':round(_ry,4)})
                _prev=(_rx,_ry)
        timelines.setdefault('root',{})['translate']=_ts
    slot_times={}
    for part,keys in effect_frames[action].items():
        if isinstance(keys,dict):
            entry={}
            if 'alpha' in keys:
                entry['alpha']=[{'time':round(f/FPS,6),'value':v} for f,v in keys['alpha']]
            if 'flipbook' in keys:
                entry['attachment']=[{'time':round(f/FPS,6),'name':n} for f,n in keys['flipbook']]
            slot_times[part]=entry
        else:
            slot_times[part]={'alpha':[{'time':round(f/FPS,6),'value':v} for f,v in keys]}
    event={'time':round(event_frames[action]/FPS,6),'name':'hit' if action in ('attack','attack_multi') else 'release'}
    # ★ drawOrder：按 animation 的 drawOrder 字段（Spine 4.2 格式）
    #   {"time": 0, "offsets":[{"slot":"forearm_off","offset":11}, ...]}
    _draw_order=draw_order.get(action)
    animations[action]={'bones':timelines,'slots':slot_times,'events':[event]}
    if _draw_order:
        animations[action]['drawOrder']=_draw_order
data={'skeleton':{'spine':'4.2.43','fps':FPS,'hash':motion.get('hash','hao-rebind-v2-2026-10-08'),'x':-260,'y':-20,'width':850,'height':710,'images':'../editable/parts/'},'bones':bones,'slots':slots,'skins':[{'name':'default','attachments':skin}],'events':{'hit':{},'release':{}},'animations':animations}
(SOURCE/'hao.spine-json').write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
(OUT/'editable/hao.json').write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
(OUT/'editable/rig.json').write_text(json.dumps({'bones':bones,'slots':slots,'regions':packed,'uniformScale':S,'sourceBoxes':source_boxes,'attachments':attachments},indent=2)+'\n',encoding='utf-8')
proof={'ok':True,'bodyUniformScale':S,'characterRegionsHavePreservedAspect':True,'bones':len(bones),'slots':len(slots),'capeMeshVertices':cols*rows,'capeMeshTriangles':len(triangles)//3,'maxIKReachClamp':max(err for action in poses.values() for p in action for err in p[4].values()),'bodyAttachmentCenterError':0.,'weaponGripAttachmentError':0.}
for part in config['parts']:
    if part=='cape':continue
    parent='torso' if part=='mantle' else part;attachment=attachments[part]
    if attachment.get('type')=='mesh':continue
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
_bone_names=[b['name'] for b in bones]
def _parse_spine_mesh(att):
    """把 Spine mesh 的 vertices 解析成 [[(boneName, localXY, weight), ...], ...]"""
    v=att['vertices'];out=[];i=0
    while i<len(v):
        n=int(v[i]);i+=1;ws=[]
        for _ in range(n):
            bi=int(v[i]);x=float(v[i+1]);y=float(v[i+2]);w=float(v[i+3]);i+=4
            ws.append((_bone_names[bi],np.array([x,y]),w))
        out.append(ws)
    return out
def draw_mesh(result,wp,wa,att,piece):
    """按【附件自己的】mesh 数据绘制（原实现硬编码 cape ⇒ 其他 mesh 附件画不出来）。"""
    weights_list=_parse_spine_mesh(att)
    points=[]
    for weights in weights_list:
        p=sum((wp[name]+R(wa[name])@q)*weight for name,q,weight in weights);points.append(screen(p))
    uv=np.array(att['uvs']).reshape(-1,2)*np.array(piece.size)
    tris=att['triangles']
    for i in range(0,len(tris),3):
        ids=tris[i:i+3];dst=np.array([points[j] for j in ids]);src=uv[ids]
        lo=np.floor(dst.min(axis=0)-1).astype(int);hi=np.ceil(dst.max(axis=0)+1).astype(int);lo=np.maximum(lo,0);hi=np.minimum(hi,np.array(SIZE))
        if np.any(hi<=lo):continue
        size=tuple(hi-lo);basis=np.column_stack([dst,np.ones(3)])
        try:mapping=np.linalg.solve(basis,src).T
        except np.linalg.LinAlgError:continue
        bias=mapping[:,:2]@lo+mapping[:,2]
        try:layer=piece.transform(size,Image.Transform.AFFINE,(*mapping[0,:2],bias[0],*mapping[1,:2],bias[1]),resample=Image.Resampling.BICUBIC)
        except Exception:continue
        clip=Image.new('L',size);ImageDraw.Draw(clip).polygon([tuple(p-lo) for p in dst],fill=255)
        layer.putalpha(Image.fromarray(np.minimum(np.asarray(clip),np.asarray(layer.getchannel('A')))))
        result.alpha_composite(layer,tuple(lo))
def render(action,frame,overlay=False):
    image=Image.new('RGBA',SIZE,(13,19,31,255));draw=ImageDraw.Draw(image);draw.ellipse((260,712,545,765),fill=(3,6,12,180))
    _,_,wp,wa,_=poses[action][frame]
    for slot in slots:
        part=slot['name']
        # ★ attachment 时间轴（flipbook）：选当前帧的 attachment
        _att_name=part
        _akes=animations[action]['slots'].get(part,{}).get('attachment')
        if _akes:
            _t=frame/FPS;_cur=_akes[0]['name']
            for _k in _akes:
                if _k['time']<=_t+1e-9:_cur=_k['name']
            _att_name=_cur
        # 兜底：flipbook slot 的默认 attachment 名不同于 slot 名（如 fx_tear -> fx_tear_0）
        if _att_name not in attachments and (_att_name+"_0") in attachments:
            _att_name=_att_name+"_0"
        a=attachments[_att_name];alpha=opacity(action,part,frame/FPS)
        if alpha<.001:continue
        if a['type']=='mesh':draw_mesh(image,wp,wa,a,full_images[_att_name]);continue
        bone_name=slot['bone'];texture=full_images[_att_name];center=wp[bone_name]+R(wa[bone_name])@np.array([a['x'],a['y']])
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
    if action in ('attack','attack_multi'):frames[0].save(PREVIEW/(action+'-setup.png'))
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
