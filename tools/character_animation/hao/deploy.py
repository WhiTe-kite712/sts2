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
data['animations'].update(idle_loop=neutral(1.0),hurt=neutral(.18),die=neutral(1.5))
# Native rest sites select one of these names by act. Each character uses its
# own SpineSprite, so hiding the weapon here cannot affect the combat instance.
rest=neutral(1.0)
rest['bones']['torso']['rotate']=[{'time':0,'value':0},{'time':.5,'value':.6},{'time':1,'value':0}]
rest['bones']['head']['rotate']=[{'time':0,'value':0},{'time':.5,'value':-.3},{'time':1,'value':0}]
rest['slots']['weapon']={'alpha':[{'time':0,'value':0},{'time':1,'value':0}]}
for name in ('overgrowth_loop','hive_loop','glory_loop'):
    data['animations'][name]=json.loads(json.dumps(rest))

(target/'hao.spjson').write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
for name in ('hao.atlas','hao.png'):shutil.copy2(source/name,target/name)
prefix='res://CodeFarmer/animations/coding_farmer'
wrapper={'source_path':prefix+'/hao.atlas','atlas_data':(source/'hao.atlas').read_text(encoding='utf-8'),
         'normal_texture_prefix':'','specular_texture_prefix':''}
(target/'hao.spatlas').write_text(json.dumps(wrapper,indent=2)+'\n',encoding='utf-8')
(target/'hao.tres').write_text(f'''[gd_resource type="SpineSkeletonDataResource" load_steps=5 format=3]

[ext_resource type="SpineAtlasResource" path="{prefix}/hao.spatlas" id="1_atlas"]
[ext_resource type="SpineSkeletonFileResource" path="{prefix}/hao.spjson" id="2_skeleton"]

[sub_resource type="SpineAnimationMix" id="attack_in"]
from = "idle_loop"
to = "attack"
mix = 0.04

[sub_resource type="SpineAnimationMix" id="attack_out"]
from = "attack"
to = "idle_loop"
mix = 0.08

[resource]
animation_mixes = [SubResource("attack_in"), SubResource("attack_out")]
atlas_res = ExtResource("1_atlas")
skeleton_file_res = ExtResource("2_skeleton")
default_mix = 0.10
''',encoding='utf-8')
print('Deployed character Spine resources: '+str(target))
