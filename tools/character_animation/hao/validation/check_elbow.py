"""Verify the main elbow is screen-right of its shoulder in native attack poses."""

from __future__ import annotations

from datetime import datetime, timezone
import hashlib
import json
import math
from pathlib import Path


def main() -> int:
    directory = Path(__file__).resolve().parent
    report_bytes = (directory / "validation.json").read_bytes()
    native = json.loads(report_bytes)
    if not native.get("ok") or not native.get("runtime_resource_loader", {}).get("ok"):
        raise ValueError("Native source/resource validation must pass first")
    current_hash = hashlib.sha256(Path(native["source"]["skeleton"]).read_bytes()).hexdigest()
    if current_hash != native["source_sha256"]["skeleton"]:
        raise ValueError("Source skeleton changed after native validation")
    attack = next(item for item in native["animations"] if item["name"] == "attack")
    poses = []
    for sample in attack["samples"]:
        shoulder = sample["bones"]["upper_arm_main"]
        elbow = sample["bones"]["forearm_main"]
        dx = elbow["world_x"] - shoulder["world_x"]
        if not math.isfinite(dx):
            raise ValueError("Non-finite native elbow/shoulder coordinate")
        poses.append({
            "time": sample["time"],
            "ratio": sample["ratio"],
            "active_attack": 0.0 < sample["ratio"] < 1.0,
            "shoulder": {"x": shoulder["world_x"], "y": shoulder["world_y"]},
            "elbow": {"x": elbow["world_x"], "y": elbow["world_y"]},
            "elbow_minus_shoulder_x": dx,
            "elbow_is_screen_right": dx > 0.0001,
        })
    active = [pose for pose in poses if pose["active_attack"]]
    if not active:
        raise ValueError("No native active-attack samples found")
    result = {
        "ok": all(pose["elbow_is_screen_right"] for pose in active),
        "checked_at_utc": datetime.now(timezone.utc).isoformat(),
        "measurement_source": "Native SpineBone get_world_x/get_world_y from probe.gd",
        "coordinate_system": "Native game/Godot world coordinates; screen-right is +X",
        "criterion": "forearm_main origin worldX > upper_arm_main origin worldX for every active attack sample",
        "shoulder_bone": "upper_arm_main",
        "elbow_bone": "forearm_main",
        "active_sample_count": len(active),
        "minimum_active_elbow_minus_shoulder_x": min(pose["elbow_minus_shoulder_x"] for pose in active),
        "all_sampled_poses_elbow_right": all(pose["elbow_is_screen_right"] for pose in poses),
        "source_skeleton_sha256": current_hash,
        "native_report_sha256": hashlib.sha256(report_bytes).hexdigest(),
        "native_started_at": native["started_at"],
        "poses": poses,
    }
    (directory / "elbow-right-check.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print("PASS" if result["ok"] else "FAIL", "native main elbow right:",
          [(pose["time"], pose["elbow_minus_shoulder_x"]) for pose in active])
    return 0 if result["ok"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
