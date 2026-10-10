"""Check the requested forward/down sword strike using native sampled poses.

This reads probe.gd's measured SpineBone world coordinates. It does not compute
poses with an alternate renderer or edit any animation data.
"""

from __future__ import annotations

from datetime import datetime, timezone
import hashlib
import json
import math
from pathlib import Path


def main() -> int:
    directory = Path(__file__).resolve().parent
    native_path = directory / "validation.json"
    native_bytes = native_path.read_bytes()
    native = json.loads(native_bytes)
    if not native.get("ok") or not native.get("runtime_resource_loader", {}).get("ok"):
        raise ValueError("Native source/resource validation must pass first")
    source_path = Path(native["source"]["skeleton"])
    current_hash = hashlib.sha256(source_path.read_bytes()).hexdigest()
    if current_hash != native["source_sha256"]["skeleton"]:
        raise ValueError("Source skeleton changed after native validation")
    attack = next(item for item in native["animations"] if item["name"] == "attack")

    def sample_at(time: float) -> dict:
        matches = [item for item in attack["samples"] if math.isclose(item["time"], time, abs_tol=0.00001)]
        if len(matches) != 1:
            raise ValueError(f"Expected one native attack sample at {time}s")
        return matches[0]

    before = sample_at(0.25)
    after = sample_at(0.4)
    measurements = {}
    for bone_name in ("weapon", "hand_main"):
        start = before["bones"][bone_name]
        end = after["bones"][bone_name]
        values = [start["world_x"], start["world_y"], end["world_x"], end["world_y"]]
        if not all(math.isfinite(value) for value in values):
            raise ValueError(f"Non-finite native coordinate for {bone_name}")
        measurements[bone_name] = {
            "before": {"x": start["world_x"], "y": start["world_y"]},
            "after": {"x": end["world_x"], "y": end["world_y"]},
            "delta": {"x": end["world_x"] - start["world_x"], "y": end["world_y"] - start["world_y"]},
        }
    # The weapon bone is the grip. An arcing slash may pull that grip back
    # while the blade's contact point continues forward toward the enemy.
    source = json.loads(source_path.read_text(encoding="utf-8"))
    authoring = directory.parent
    bind = json.loads((authoring / "editable/bind.json").read_text(encoding="utf-8"))
    png = (authoring / "source/weapon.png").read_bytes()
    image_height = int.from_bytes(png[20:24], "big")
    attachment = source["skins"][0]["attachments"]["weapon"]["weapon"]
    tip_distance = attachment["height"] * bind["weaponGripPixel"][1] / image_height
    tip_points = []
    for pose in (before, after):
        bone = pose["bones"]["weapon"]
        angle = math.radians(bone["world_rotation_x"])
        tip_points.append({"x": bone["world_x"] + tip_distance * math.cos(angle),
                           "y": bone["world_y"] + tip_distance * math.sin(angle)})
    measurements["blade_tip"] = {"before": tip_points[0], "after": tip_points[1],
        "delta": {axis: tip_points[1][axis] - tip_points[0][axis] for axis in ("x", "y")}}
    upper_lift = before["bones"]["upper_arm_main"]["world_rotation_x"] - attack["samples"][0]["bones"]["upper_arm_main"]["world_rotation_x"]
    forearm_lift = before["bones"]["forearm_main"]["world_rotation_x"] - attack["samples"][0]["bones"]["forearm_main"]["world_rotation_x"]
    checks = {
        "blade_contact_moves_forward_positive_x": measurements["blade_tip"]["delta"]["x"] > 0.0001,
        "blade_contact_moves_down_positive_y": measurements["blade_tip"]["delta"]["y"] > 0.0001,
        "weapon_moves_down_positive_y": measurements["weapon"]["delta"]["y"] > 0.0001,
        "sword_hand_moves_down_positive_y": measurements["hand_main"]["delta"]["y"] > 0.0001,
        "forearm_raises_more_than_upper_arm": abs(forearm_lift) > abs(upper_lift) > 0.0001,
    }
    result = {
        "ok": all(checks.values()),
        "checked_at_utc": datetime.now(timezone.utc).isoformat(),
        "measurement_source": "probe.gd: native SpineSprite update_skeleton and SpineBone get_world_x/get_world_y",
        "coordinate_system": "Native game/Godot world coordinates; forward is +X and downward is +Y",
        "animation": "attack",
        "before_time": 0.25,
        "after_time": 0.4,
        "source_skeleton_sha256": current_hash,
        "native_report_sha256": hashlib.sha256(native_bytes).hexdigest(),
        "native_started_at": native["started_at"],
        "measurements": measurements,
        "checks": checks,
    }
    (directory / "direction-check.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print("PASS" if result["ok"] else "FAIL", "native sword strike direction:", measurements)
    return 0 if result["ok"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
