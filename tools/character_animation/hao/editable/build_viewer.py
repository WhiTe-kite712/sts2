from pathlib import Path
import argparse
import base64
import hashlib
import json

ROOT = Path(__file__).resolve().parents[1]

MESH_HELPERS = r'''
function meshVertices(att,pose,slotBone){
 const v=att.vertices,result=[],count=att.uvs.length/2;
 if(v.length===att.uvs.length){const m=pose.world.get(slotBone);for(let i=0;i<count;i++){const x=v[i*2],y=v[i*2+1];result.push([m.a*x+m.c*y+m.x,m.b*x+m.d*y+m.y]);}}
 else {let cursor=0;for(let i=0;i<count;i++){const n=v[cursor++];let x=0,y=0;for(let j=0;j<n;j++){const index=v[cursor++],vx=v[cursor++],vy=v[cursor++],weight=v[cursor++],m=pose.world.get(data.bones[index].name);x+=(m.a*vx+m.c*vy+m.x)*weight;y+=(m.b*vx+m.d*vy+m.y)*weight;}result.push([x,y]);}if(cursor!==v.length)throw new Error('网格顶点数据长度不一致');}
 return result;
}
function textureAffine(uv,p){
 const x1=uv[1][0]-uv[0][0],y1=uv[1][1]-uv[0][1],x2=uv[2][0]-uv[0][0],y2=uv[2][1]-uv[0][1],det=x1*y2-x2*y1;
 if(Math.abs(det)<1e-8)return null;
 const px1=p[1][0]-p[0][0],py1=p[1][1]-p[0][1],px2=p[2][0]-p[0][0],py2=p[2][1]-p[0][1],a=(px1*y2-px2*y1)/det,c=(px2*x1-px1*x2)/det,b=(py1*y2-py2*y1)/det,d=(py2*x1-py1*x2)/det;
 return [a,b,c,d,p[0][0]-a*uv[0][0]-c*uv[0][1],p[0][1]-b*uv[0][0]-d*uv[0][1]];
}
function validateMesh(att){
 if(!Array.isArray(att.uvs)||att.uvs.length%2||!Array.isArray(att.vertices)||!Array.isArray(att.triangles)||att.triangles.length%3)throw new Error('网格拓扑数据不完整');
 const count=att.uvs.length/2;
 for(const index of att.triangles)if(!Number.isInteger(index)||index<0||index>=count)throw new Error('网格三角形索引越界');
 if(att.vertices.length!==att.uvs.length){let cursor=0;for(let i=0;i<count;i++){const n=att.vertices[cursor++];if(!Number.isInteger(n)||n<1)throw new Error('网格顶点权重数量无效');for(let j=0;j<n;j++){const index=att.vertices[cursor++],x=att.vertices[cursor++],y=att.vertices[cursor++],weight=att.vertices[cursor++];if(!Number.isInteger(index)||index<0||index>=data.bones.length||![x,y,weight].every(Number.isFinite))throw new Error('网格骨骼权重数据无效');}}if(cursor!==att.vertices.length)throw new Error('网格顶点数据长度不一致');}
}
function drawMeshAttachment(att,pose,slotBone,source){
 const world=meshVertices(att,pose,slotBone),screen=world.map(p=>[ORIGIN[0]+p[0]*SCALE,ORIGIN[1]-p[1]*SCALE]);
 for(let t=0;t<att.triangles.length;t+=3){
  const ids=att.triangles.slice(t,t+3),p=ids.map(i=>screen[i]),uv=ids.map(i=>[att.uvs[i*2]*source.width,att.uvs[i*2+1]*source.height]),affine=textureAffine(uv,p);
  if(!affine)continue;
  const area=(p[1][0]-p[0][0])*(p[2][1]-p[0][1])-(p[1][1]-p[0][1])*(p[2][0]-p[0][0]);
  if(Math.abs(area)<1e-8)continue;
  // Slightly overlap triangle clips to suppress Canvas2D antialias cracks.
  const cx=(p[0][0]+p[1][0]+p[2][0])/3,cy=(p[0][1]+p[1][1]+p[2][1])/3;
  const clip=p.map(q=>{const dx=q[0]-cx,dy=q[1]-cy,length=Math.hypot(dx,dy)||1;return [q[0]+dx/length*.4,q[1]+dy/length*.4];});
  ctx.save();ctx.beginPath();ctx.moveTo(...clip[0]);ctx.lineTo(...clip[1]);ctx.lineTo(...clip[2]);ctx.closePath();ctx.clip();
  ctx.transform(...affine);
  ctx.drawImage(image,source.x,source.y,source.width,source.height,0,0,source.width,source.height);
  ctx.restore();
 }
}
'''

def safe_json(data):
    return json.dumps(data, ensure_ascii=False, separators=(',', ':')).replace('<', '\\u003c')

def replace_once(template, before, after):
    if template.count(before) != 1:
        raise ValueError('Viewer template changed; expected exactly one occurrence: ' + before[:80])
    return template.replace(before, after, 1)

def main():
    parser = argparse.ArgumentParser(description='Build an offline Canvas2D viewer for the exported Hao Spine region/weighted-mesh rig.')
    parser.add_argument('--out', type=Path, default=ROOT, help='Artifact directory containing source/ and editable/rig.json.')
    parser.add_argument('--width', type=int, default=1100)
    parser.add_argument('--height', type=int, default=800)
    parser.add_argument('--origin-x', type=float, default=400)
    parser.add_argument('--origin-y', type=float, default=742)
    parser.add_argument('--scale', type=float, default=1.25)
    parser.add_argument('--output', type=Path, help='Optional HTML destination; defaults to OUT/preview/index.html.')
    args = parser.parse_args()
    if args.width <= 0 or args.height <= 0 or args.scale <= 0:
        parser.error('Canvas size and scale must be positive.')
    out = args.out.resolve()
    source_path = out / 'source' / 'hao.spine-json'
    rig_path = out / 'editable' / 'rig.json'
    png_path = out / 'source' / 'hao.png'
    spine = json.loads(source_path.read_text(encoding='utf-8-sig'))
    rig = json.loads(rig_path.read_text(encoding='utf-8-sig'))
    png = png_path.read_bytes()
    metadata = {name: hashlib.sha256(path.read_bytes()).hexdigest() for name, path in [('spine', source_path), ('rig', rig_path), ('png', png_path)]}
    metadata.update(bones=len(spine['bones']), slots=len(spine['slots']), regions=len(rig['regions']), fps=spine['skeleton'].get('fps',30), canvas=[args.width,args.height], origin=[args.origin_x,args.origin_y], scale=args.scale)
    template = Path(__file__).with_name('viewer.template.html').read_text(encoding='utf-8')
    template = replace_once(template, 'Math.round(time*30)', 'Math.round(time*(sourceMeta.fps||30))')
    template = replace_once(template, "(e.code==='ArrowRight'?1:-1)/30", "(e.code==='ArrowRight'?1:-1)/(sourceMeta.fps||30)")
    template = replace_once(template, 'const W=900,H=650,ORIGIN=[330,580],SCALE=1,DEG=Math.PI/180;', f'const W={args.width},H={args.height},ORIGIN=[{args.origin_x},{args.origin_y}],SCALE={args.scale},DEG=Math.PI/180;')
    template = replace_once(template, 'width="900" height="650"', f'width="{args.width}" height="{args.height}"')
    template = replace_once(template, 'aspect-ratio:900/650', f'aspect-ratio:{args.width}/{args.height}')
    template = replace_once(template, '900 × 650 · 原点 (330, 580)', f'{args.width} × {args.height} · 原点 ({args.origin_x:g}, {args.origin_y:g})')
    template = replace_once(template, "+' 插槽 · 900 × 650'", f"+' 插槽 · {args.width} × {args.height}'")
    template = replace_once(template, 'ctx.ellipse(337.5,581.5,137.5,17.5,0,0,Math.PI*2)', 'ctx.ellipse(ORIGIN[0]+7.5*SCALE,ORIGIN[1]+1.5*SCALE,137.5*SCALE,17.5*SCALE,0,0,Math.PI*2)')
    template = replace_once(template, "if(att.type&&att.type!=='region')throw new Error('当前预览仅支持本源文件中的区域附件');", "if(att.type&&!['region','mesh'].includes(att.type))throw new Error('未实现的附件类型：'+att.type);if(att.type==='mesh')validateMesh(att);")
    template = replace_once(template, 'function render(){', MESH_HELPERS + '\nfunction render(){')
    template = replace_once(template, 'ctx.translate(ORIGIN[0],ORIGIN[1]);ctx.scale(SCALE,-SCALE);ctx.transform(m.a,m.b,m.c,m.d,m.x,m.y);', "if(att.type==='mesh'){drawMeshAttachment(att,pose,slot.bone,source);ctx.restore();continue;}ctx.translate(ORIGIN[0],ORIGIN[1]);ctx.scale(SCALE,-SCALE);ctx.transform(m.a,m.b,m.c,m.d,m.x,m.y);")
    template = replace_once(template, 'window.haoViewer={sourceMeta,durations,', 'window.haoViewer={sourceMeta,durations,meshVertices(name,t,slotName){const p=poseAt(name,t),s=p.slots.find(s=>s.name===slotName),att=skin.attachments[slotName][s.attachment];return meshVertices(att,p,s.bone);},')
    html = template.replace('__SPINE_JSON__', safe_json(spine)).replace('__REGIONS_JSON__', safe_json(rig['regions'])).replace('__SOURCE_META__', safe_json(metadata)).replace('__ATLAS_DATA_URI__', 'data:image/png;base64,' + base64.b64encode(png).decode('ascii'))
    destination = args.output.resolve() if args.output else out / 'preview' / 'index.html'
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_text(html, encoding='utf-8')
    print(json.dumps({'file': str(destination), 'bytes': destination.stat().st_size, **metadata}, ensure_ascii=False))

if __name__ == '__main__':
    main()
