"""Copy the generated Spine data into the mod's PCK asset namespace."""
from pathlib import Path
import argparse, json, shutil

HERE=Path(__file__).resolve().parent
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--authoring',type=Path,default=HERE)
parser.add_argument('--assets',type=Path,default=HERE.parents[2]/'MySts2Mod/MySts2Mod/animations/coding_farmer')
args=parser.parse_args()
authoring=args.authoring.resolve();target=args.assets.resolve();target.mkdir(parents=True,exist_ok=True)
source=authoring/'source'
data=json.loads((source/'hao.spine-json').read_text(encoding='utf-8'))
def neutral(duration):
    return {'bones':{b['name']:{'rotate':[{'time':0,'value':0},{'time':duration,'value':0}]} for b in data['bones']},
            'slots':{s['name']:{'alpha':[{'time':0,'value':0},{'time':duration,'value':0}]} for s in data['slots'] if s['name'].startswith('fx_')}}
# 只为**缺失**的动画生成 neutral 占位；已有真实数据的（如 die）不再被覆盖
for _n,_d in (('idle_loop',1.0),('hurt',.18),('die',1.5)):
    if _n not in data['animations']:
        data['animations'][_n]=neutral(_d)
(target/'hao.spjson').write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
for name in ('hao.atlas','hao.png'):shutil.copy2(source/name,target/name)
prefix='res://MySts2Mod/animations/coding_farmer'
wrapper={'source_path':prefix+'/hao.atlas','atlas_data':(source/'hao.atlas').read_text(encoding='utf-8'),
         'normal_texture_prefix':'','specular_texture_prefix':''}
(target/'hao.spatlas').write_text(json.dumps(wrapper,indent=2)+'\n',encoding='utf-8')
# ★ 动画混合时长（借鉴原版「重击瞬间爆发」的通用做法）
_mixes=[('idle_loop','attack',0.02),('idle_loop','attack_multi',0.02),('idle_loop','cast',0.05),
        ('idle_loop','hurt',0.03),
        ('hurt','idle_loop',0.10),('hurt','die',0.10),('idle_loop','die',0.10),
        ('attack','idle_loop',0.06),('attack_multi','idle_loop',0.08),('cast','idle_loop',0.06),
        ('die','idle_loop',0.10),
        # ★ 2026-10-23 低血量待机（审批报告 P1-11）
        #   idle_virtual = 虚弱捂腹（常驻）；idle_lowhp = 扔剑淋雨（6s 特殊）
        ('idle_loop','idle_virtual',0.28),('idle_virtual','idle_loop',0.28),
        ('idle_virtual','attack',0.06),('idle_virtual','attack_multi',0.06),
        ('idle_virtual','cast',0.08),('idle_virtual','hurt',0.06),
        ('attack','idle_virtual',0.10),('attack_multi','idle_virtual',0.10),
        ('cast','idle_virtual',0.12),('hurt','idle_virtual',0.12),
        ('idle_virtual','idle_lowhp',0.15),('idle_lowhp','idle_virtual',0.28),
        ('idle_lowhp','attack',0.08),('idle_lowhp','attack_multi',0.08),
        ('idle_lowhp','cast',0.08),('idle_lowhp','hurt',0.08),
        ('attack','idle_lowhp',0.10),('cast','idle_lowhp',0.10),('hurt','idle_lowhp',0.10),
        ('idle_lowhp','die',0.10),('idle_virtual','die',0.10)]
_sub=''.join('[sub_resource type="SpineAnimationMix" id="mix_%d"]\nfrom = "%s"\nto = "%s"\nmix = %s\n\n' % (i,a,b,m) for i,(a,b,m) in enumerate(_mixes))
_refs=', '.join('SubResource("mix_%d")' % i for i in range(len(_mixes)))
(target/'hao.tres').write_text(f'''[gd_resource type="SpineSkeletonDataResource" load_steps={2+len(_mixes)+1} format=3]

[ext_resource type="SpineAtlasResource" path="{prefix}/hao.spatlas" id="1_atlas"]
[ext_resource type="SpineSkeletonFileResource" path="{prefix}/hao.spjson" id="2_skeleton"]

{_sub}[resource]
atlas_res = ExtResource("1_atlas")
skeleton_file_res = ExtResource("2_skeleton")
default_mix = 0.05
animation_mixes = [{_refs}]
''',encoding='utf-8')
print('Deployed character Spine resources: '+str(target))
