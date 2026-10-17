using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text.Json;

// Metadata-only: never load or execute the mod, Godot, BaseLib or game assemblies.
// Usage: CampfireContracts <CodeFarmer.dll> <repo-or-overlay> <hao.spjson> [report.json]
if (args.Length < 3)
{
    Console.Error.WriteLine("Usage: CampfireContracts <CodeFarmer.dll> <repo-or-overlay> <hao.spjson> [report.json]");
    return 2;
}
var checks = new List<object>();
var failures = 0;
void Check(string name, bool passed, string evidence)
{
    checks.Add(new { name, passed, evidence });
    Console.WriteLine($"{(passed ? "PASS" : "FAIL")} {name}: {evidence}");
    if (!passed) failures++;
}
using var image = File.OpenRead(args[0]);
using var pe = new PEReader(image);
var md = pe.GetMetadataReader();
var opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
    .Where(f => f.FieldType == typeof(OpCode))
    .Select(f => (OpCode)f.GetValue(null)!).ToDictionary(o => unchecked((ushort)o.Value));
string TypeName(EntityHandle handle)
{
    if (handle.Kind == HandleKind.TypeReference)
    {
        var t = md.GetTypeReference((TypeReferenceHandle)handle);
        return md.GetString(t.Namespace) + "." + md.GetString(t.Name);
    }
    if (handle.Kind == HandleKind.TypeDefinition)
    {
        var t = md.GetTypeDefinition((TypeDefinitionHandle)handle);
        var parent = t.GetDeclaringType();
        return parent.IsNil ? md.GetString(t.Namespace) + "." + md.GetString(t.Name)
            : TypeName(parent) + "+" + md.GetString(t.Name);
    }
    return handle.Kind.ToString();
}
string MemberName(EntityHandle handle)
{
    if (handle.Kind == HandleKind.MethodSpecification)
        return MemberName(md.GetMethodSpecification((MethodSpecificationHandle)handle).Method);
    if (handle.Kind == HandleKind.MethodDefinition)
    {
        var m = md.GetMethodDefinition((MethodDefinitionHandle)handle);
        return TypeName(m.GetDeclaringType()) + "::" + md.GetString(m.Name);
    }
    if (handle.Kind == HandleKind.MemberReference)
    {
        var m = md.GetMemberReference((MemberReferenceHandle)handle);
        return TypeName(m.Parent) + "::" + md.GetString(m.Name);
    }
    return handle.Kind.ToString();
}
List<string> IL(MethodDefinitionHandle method)
{
    var def = md.GetMethodDefinition(method);
    var result = new List<string>();
    if (def.RelativeVirtualAddress == 0) return result;
    var code = pe.GetMethodBody(def.RelativeVirtualAddress).GetILBytes()!;
    for (var at = 0; at < code.Length;)
    {
        ushort key = code[at++];
        if (key == 0xfe) key = (ushort)(0xfe00 | code[at++]);
        var op = opcodes[key];
        result.Add("op:" + op.Name);
        if (op.OperandType == OperandType.InlineString)
            result.Add("string:" + md.GetUserString(MetadataTokens.UserStringHandle(BitConverter.ToInt32(code, at) & 0xffffff)));
        else if (op.OperandType == OperandType.InlineMethod)
        {
            var token = MetadataTokens.EntityHandle(BitConverter.ToInt32(code, at));
            result.Add("call:" + MemberName(token));
            if (token.Kind == HandleKind.MethodSpecification)
            {
                var spec = md.GetMethodSpecification((MethodSpecificationHandle)token);
                // RegisterSceneType<T> here has one concrete class type argument.
                var signature = md.GetBlobBytes(spec.Signature);
                result.Add("generic:" + Convert.ToHexString(signature));
                var blob = md.GetBlobReader(spec.Signature);
                if (blob.ReadByte() == 0x0a && blob.ReadCompressedInteger() == 1
                    && blob.ReadByte() is 0x11 or 0x12)
                {
                    var codedIndex = blob.ReadCompressedInteger();
                    if ((codedIndex & 3) == 1)
                        result.Add("generic-type:" + TypeName(MetadataTokens.TypeReferenceHandle(codedIndex >> 2)));
                }
            }
        }
        else if (op.OperandType == OperandType.InlineType)
            result.Add("type:" + TypeName(MetadataTokens.EntityHandle(BitConverter.ToInt32(code, at))));
        at += op.OperandType switch
        {
            OperandType.InlineNone => 0,
            OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
            OperandType.InlineVar => 2,
            OperandType.InlineI8 or OperandType.InlineR => 8,
            OperandType.InlineSwitch => 4 + BitConverter.ToInt32(code, at) * 4,
            _ => 4,
        };
    }
    return result;
}
TypeDefinitionHandle FindType(string suffix) => md.TypeDefinitions.FirstOrDefault(t => TypeName(t).EndsWith(suffix, StringComparison.Ordinal));
MethodDefinitionHandle FindMethod(TypeDefinitionHandle owner, string name) => owner.IsNil ? default :
    md.GetTypeDefinition(owner).GetMethods().FirstOrDefault(m => md.GetString(md.GetMethodDefinition(m).Name) == name);
var character = FindType(".CodingFarmer");
var rest = FindType(".CodingFarmerRestSiteScene");
var getter = FindMethod(character, "get_CustomRestSiteAnimPath");
Check("character overrides placeholder rest-site path", !getter.IsNil
    && md.GetMethodDefinition(getter).Attributes.HasFlag(MethodAttributes.Virtual)
    && !md.GetMethodDefinition(getter).Attributes.HasFlag(MethodAttributes.NewSlot),
    getter.IsNil ? "override absent" : string.Join("; ", IL(getter)));
Check("override selects own scene", !getter.IsNil && IL(getter).Any(s => s.EndsWith(".CodingFarmerRestSiteScene::GetScenePath")), "per-character virtual getter");
var sceneMethod = FindMethod(rest, "GetScenePath");
var sceneIL = sceneMethod.IsNil ? [] : IL(sceneMethod);
Check("BaseLib factory registration", sceneIL.Any(s => s.EndsWith("::RegisterSceneType"))
    && sceneIL.Contains("generic-type:MegaCrit.Sts2.Core.Nodes.RestSite.NRestSiteCharacter"), "RegisterSceneType<NRestSiteCharacter> call in scene getter");
Check("native rest-site generic type available", md.TypeReferences.Any(h => TypeName(h) == "MegaCrit.Sts2.Core.Nodes.RestSite.NRestSiteCharacter"), "native NRestSiteCharacter metadata reference");
Check("generated scene cached", sceneIL.Any(s => s.EndsWith("::SetAsset")) && sceneIL.Any(s => s.EndsWith("::TakeOverPath")), "SetAsset + TakeOverPath");
Check("existing Spine resource reused", sceneIL.Contains("string:res://CodeFarmer/animations/coding_farmer/hao.tres"), "compiled skeleton resource path");
var keeper = FindType(".KeepCodingFarmerRestSiteSceneCached");
var prefix = FindMethod(keeper, "Prefix");
var keeperIL = prefix.IsNil ? [] : IL(prefix);
var nestedIL = keeper.IsNil ? [] : md.TypeDefinitions.Where(t => TypeName(t).StartsWith(TypeName(keeper) + "+"))
    .SelectMany(t => md.GetTypeDefinition(t).GetMethods()).SelectMany(IL).ToList();
bool HasOptimizedCacheIdentityGuard(MethodDefinitionHandle method)
{
    if (method.IsNil) return false;
    var code = pe.GetMethodBody(md.GetMethodDefinition(method).RelativeVirtualAddress).GetILBytes()!;
    // Release C# lowers ReferenceEquals(arg0, PreloadManager.Cache) to a
    // reference bne.un[.s] that skips the filtering body and lands on ret.
    if (code.Length < 9 || code[0] != unchecked((byte)OpCodes.Ldarg_0.Value)
        || code[1] != unchecked((byte)OpCodes.Call.Value)
        || MemberName(MetadataTokens.EntityHandle(BitConverter.ToInt32(code, 2)))
            != "MegaCrit.Sts2.Core.Assets.PreloadManager::get_Cache") return false;
    int destination;
    if (code[6] == unchecked((byte)OpCodes.Bne_Un_S.Value))
        destination = 8 + unchecked((sbyte)code[7]);
    else if (code.Length >= 12 && code[6] == unchecked((byte)OpCodes.Bne_Un.Value))
        destination = 11 + BitConverter.ToInt32(code, 7);
    else return false;
    return destination == code.Length - 1 && code[destination] == unchecked((byte)OpCodes.Ret.Value);
}
var cacheGuard = keeperIL.Any(s => s.EndsWith("::ReferenceEquals"))
    || HasOptimizedCacheIdentityGuard(prefix);
Check("only scene retained in asset-cache unload", cacheGuard
    && keeperIL.Contains("call:System.Linq.Enumerable::Where")
    && nestedIL.Contains("string:res://CodeFarmer/scenes/rest_site/coding_farmer_rest_site.tscn")
    && nestedIL.Contains("call:System.String::op_Inequality"),
    "cache-instance guard (ReferenceEquals or optimized bne.un to ret) + scene-specific inequality predicate");

var sceneSource = Path.Combine(args[1], "MySts2Mod", "MySts2ModCode", "Character", "CodingFarmerRestSiteScene.cs");
var source = File.ReadAllText(sceneSource);
Check("native direct-Spine/UI hierarchy", source.Contains("Add(root, root, body);")
    && source.Contains("Add(root, root, controls);") && source.Contains("Add(root, controls, hitbox, unique: true);")
    && source.Contains("Add(root, controls, reticle, unique: true);"), "direct SpineSprite; ControlRoot owns hitbox and reticle");
Check("native targeting visuals reused", source.Contains("SceneHelper.Instantiate<NSelectionReticle>(\"ui/selection_reticle\")")
    && source.Contains("Name = \"ThoughtBubbleLeft\"") && source.Contains("Name = \"ThoughtBubbleRight\""), "native reticle + both mirrored thought anchors");
using var skeleton = JsonDocument.Parse(File.ReadAllText(args[2]));
var animations = skeleton.RootElement.GetProperty("animations");
foreach (var loop in new[] { "overgrowth_loop", "hive_loop", "glory_loop" })
    Check("native act loop " + loop, animations.TryGetProperty(loop, out var animation)
        && animation.ValueKind == JsonValueKind.Object && animation.EnumerateObject().Any(), "required by NRestSiteCharacter._Ready");
var report = JsonSerializer.Serialize(new { failures, passed = checks.Count - failures, checks }, new JsonSerializerOptions { WriteIndented = true });
if (args.Length > 3) File.WriteAllText(args[3], report + Environment.NewLine);
return failures == 0 ? 0 : 1;
