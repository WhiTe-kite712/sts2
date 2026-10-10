from pathlib import Path
import argparse, hashlib, json, struct

root = Path(__file__).resolve().parent
parser = argparse.ArgumentParser()
parser.add_argument('--mod-dll', required=True, type=Path)
parser.add_argument('--mod-pck', required=True, type=Path)
parser.add_argument('--game-data', required=True, type=Path)
parser.add_argument('--baselib-dll', required=True, type=Path)
parser.add_argument('--spine-dll', required=True, type=Path)
args = parser.parse_args()
config = {
    'mod_dll': str(args.mod_dll.resolve()), 'mod_pck': str(args.mod_pck.resolve()),
    'game_data': str(args.game_data.resolve()), 'baselib_dll': str(args.baselib_dll.resolve()),
    'factory_type': 'MySts2Mod.MySts2ModCode.Character.CodingFarmerCombatVisuals',
    'resource': 'res://MySts2Mod/animations/coding_farmer/hao.tres',
    'result': str(root / 'factory-result.json')
}
(root / 'probe-config.json').write_text(json.dumps(config, indent=2) + '\n', encoding='utf-8')
extension = '[configuration]\nentry_symbol="spine_godot_library_init"\ncompatibility_minimum="4.1"\n[libraries]\nwindows.release.x86_64="' + args.spine_dll.resolve().as_posix() + '"\nwindows.debug.x86_64="' + args.spine_dll.resolve().as_posix() + '"\n'
files = {name: (root / name).read_bytes() for name in ['project.godot','Probe.tscn','IntegrationProbe.cs','probe-config.json']}
files['probe_spine.gdextension'] = extension.encode()
files['.godot/extension_list.cfg'] = b'res://probe_spine.gdextension\n'
binary = root / '.godot/mono/temp/bin/Debug'
for item in binary.glob('*'):
    if item.is_file():
        files['.godot/mono/temp/bin/Debug/' + item.name] = item.read_bytes()
        files['.godot/mono/publish/' + item.name] = item.read_bytes()
        files['.godot/mono/publish/win-x64/' + item.name] = item.read_bytes()
files = dict(sorted(files.items()))
entries=[]; directory_length=0
for name, payload in files.items():
    filename=name.encode()+b'\0'
    filename+=b'\0'*((-len(filename))%4)
    entries.append((filename,payload)); directory_length+=4+len(filename)+8+8+16+4
offset=100+directory_length
header=b'GDPC'+struct.pack('<IIIIIQ',2,4,5,1,0,0)+b'\0'*64+struct.pack('<I',len(entries))
table=bytearray(); body=bytearray()
for filename,payload in entries:
    table+=struct.pack('<I',len(filename))+filename+struct.pack('<QQ',offset,len(payload))+hashlib.md5(payload).digest()+struct.pack('<I',0)
    body+=payload;offset+=len(payload)
(root/'factory-probe.pck').write_bytes(header+table+body)
print(json.dumps({'pack':str(root/'factory-probe.pck'),'files':len(files),'assembly':str(binary/'SpineIntegrationProbe.dll')}))
