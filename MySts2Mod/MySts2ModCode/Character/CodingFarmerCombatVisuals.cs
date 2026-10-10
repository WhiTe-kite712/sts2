using Godot;
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace MySts2Mod.MySts2ModCode.Character;

internal static class CodingFarmerCombatVisuals
{
    internal const string SkeletonPath = MainFile.ResPath + "/animations/coding_farmer/hao.tres";

    public static NCreatureVisuals Create()
    {
        var data = ResourceLoader.Load<Resource>(SkeletonPath)
            ?? throw new InvalidOperationException("Missing character Spine data: " + SkeletonPath);
        var root = new NCreatureVisuals { Name = "CodingFarmer" };
        var body = ClassDB.Instantiate("SpineSprite").As<Node2D>();
        body.Name = "Visuals";
        body.Position = new Vector2(0, -4);
        body.Scale = Vector2.One * 0.55f;
        body.Set("skeleton_data_res", data);
        AddUnique(root, body);
        AddUnique(root, new Control
        {
            Name = "Bounds", Position = new Vector2(-105, -278), Size = new Vector2(210, 278),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });
        AddUnique(root, new Marker2D { Name = "CenterPos", Position = new Vector2(0, -155) });
        AddUnique(root, new Marker2D { Name = "IntentPos", Position = new Vector2(0, -305) });
        AddUnique(root, new Marker2D { Name = "OrbPos", Position = new Vector2(0, -305) });
        AddUnique(root, new Marker2D { Name = "TalkPos", Position = new Vector2(20, -275) });
        AddUnique(root, new Control { Name = "FormVfx", MouseFilter = Control.MouseFilterEnum.Ignore });
        return root;
    }

    private static void AddUnique(NCreatureVisuals root, Node child)
    {
        root.AddChild(child);
        child.Owner = root;
        child.UniqueNameInOwner = true;
    }

    public static CreatureAnimator CreateAnimator(MegaSprite sprite)
    {
        var idle = new AnimState("idle_loop", true);
        var attack = new AnimState("attack") { NextState = idle };
        var cast = new AnimState("cast") { NextState = idle };
        var hurt = new AnimState("hurt") { NextState = idle };
        var dead = new AnimState("die");
        var animator = new CreatureAnimator(idle, sprite);
        animator.AddAnyState("Idle", idle);
        animator.AddAnyState("Relaxed", idle);
        animator.AddAnyState("Revive", idle);
        animator.AddAnyState("Attack", attack);
        animator.AddAnyState("Cast", cast);
        animator.AddAnyState("PowerUp", cast);
        animator.AddAnyState("Hit", hurt);
        animator.AddAnyState("Dead", dead);
        return animator;
    }
}
