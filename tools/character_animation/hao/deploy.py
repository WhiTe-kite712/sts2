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
(target/'hao.spjson').write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
for name in ('hao.atlas','hao.png'):shutil.copy2(source/name,target/name)
prefix='res://MySts2Mod/animations/coding_farmer'
wrapper={'source_path':prefix+'/hao.atlas','atlas_data':(source/'hao.atlas').read_text(encoding='utf-8'),
         'normal_texture_prefix':'','specular_texture_prefix':''}
(target/'hao.spatlas').write_text(json.dumps(wrapper,indent=2)+'\n',encoding='utf-8')
(target/'hao.tres').write_text(f'''[gd_resource type="SpineSkeletonDataResource" load_steps=3 format=3]

[ext_resource type="SpineAtlasResource" path="{prefix}/hao.spatlas" id="1_atlas"]
[ext_resource type="SpineSkeletonFileResource" path="{prefix}/hao.spjson" id="2_skeleton"]

[resource]
atlas_res = ExtResource("1_atlas")
skeleton_file_res = ExtResource("2_skeleton")
default_mix = 0.10
''',encoding='utf-8')
print('Deployed character Spine resources: '+str(target))
