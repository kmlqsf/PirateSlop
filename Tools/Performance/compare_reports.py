import argparse
import json
from pathlib import Path


CONDITIONS = (
    "mode", "routeVersion", "routeHash", "mapRevision", "mapChecksum", "buildType",
    "unityVersion", "cpu", "gpu", "graphicsApi", "width", "height", "quality",
    "pipeline", "renderScale", "fov", "nearClip", "farClip", "antialiasing", "vSync", "fpsLimit", "tickRate", "role",
    "expectedShips", "expectedPlayers", "warmupSeconds", "requestedSeconds",
    "warmState", "waveStrength", "waveSteepness", "waveSpeed", "stormRadius",
    "telemetryVisible", "profilerEnabled", "environmentSignature", "fleetScenario", "renderExperiment",
    "benchmarkHudVisible", "benchmarkHudVersion",
)


def compare(baseline, candidate):
    differences = []
    if baseline.get("schemaVersion") not in (2, 3) or candidate.get("schemaVersion") not in (2, 3):
        differences.append("Report schema v2/v3 is required")
    if baseline.get("schemaVersion") != candidate.get("schemaVersion"):
        differences.append("Report schema changed: repeat baseline with the same instrumentation")
    for title, report in (("baseline", baseline), ("candidate", candidate)):
        if report.get("status") != "COMPLETE":
            differences.append(f"{title}: status={report.get('status')}")
        if report.get("metadata", {}).get("mode") not in ("benchmark", "isolation", "combat"):
            differences.append(f"{title}: only automated benchmark/isolation/combat reports are baselines")
    a, b = baseline.get("metadata", {}), candidate.get("metadata", {})
    if a.get("instrumentationVersion", "") != b.get("instrumentationVersion", ""):
        differences.append(f"instrumentationVersion: {a.get('instrumentationVersion', '')!r} -> {b.get('instrumentationVersion', '')!r}")
    for key in CONDITIONS:
        if key == "renderExperiment":
            if (a.get(key) or "") != (b.get(key) or ""):
                differences.append(f"{key}: {a.get(key)!r} -> {b.get(key)!r}")
            continue
        if key not in a or key not in b:
            differences.append(f"{key}: missing")
        elif a[key] != b[key]:
            differences.append(f"{key}: {a[key]!r} -> {b[key]!r}")
    old_combat, new_combat = baseline.get("combat"), candidate.get("combat")
    if old_combat is not None or new_combat is not None:
        if old_combat is None or new_combat is None:
            differences.append("Combat workload is missing in one report")
        else:
            if old_combat.get("schedule") != new_combat.get("schedule"):
                differences.append("Combat control schedules differ")
            if any(old_combat.get(key) != new_combat.get(key) for key in ("shipsLost", "minAliveShips")):
                differences.append("Combat ship losses differ")
            old_times, new_times = old_combat.get("firstSinkingSeconds"), new_combat.get("firstSinkingSeconds")
            if old_times is None or new_times is None or len(old_times) != 6 or len(new_times) != 6:
                differences.append("Combat sinking timeline missing")
            elif any((a < 0) != (b < 0) or abs(a - b) > 5 for a, b in zip(old_times, new_times)):
                differences.append("Combat sinking timing differs materially")
            for key, tolerance, minimum in (("cannonShots", .05, 2), ("firearmShots", .05, 3), ("cannonImpacts", .2, 2)):
                before_value, after_value = old_combat.get(key, 0), new_combat.get(key, 0)
                if abs(after_value - before_value) > max(minimum, before_value * tolerance):
                    differences.append(f"Actual combat workload differs: {key} {before_value} -> {after_value}")
            if any(abs(old_combat.get(key, 0) - new_combat.get(key, 0)) > 3 for key in ("deaths", "minAlivePlayers")) or old_combat.get("cameraFallbacks") != new_combat.get("cameraFallbacks"):
                differences.append("Combat casualties or POV fallbacks differ materially")
    rows = []
    before = {item["segment"]: item for item in [baseline["total"], *baseline["segments"]]}
    after = {item["segment"]: item for item in [candidate["total"], *candidate["segments"]]}
    if set(before) != set(after):
        differences.append("Segment sets differ")
    for name, old in before.items():
        if name not in after:
            continue
        new = after[name]
        row = {"segment": name}
        for metric in ("averageFps", "low1Fps", "meanMs", "p95Ms", "p99Ms", "p999Ms", "maxMs", "cpuMeanMs", "renderMeanMs", "gpuMeanMs", "gcBytes"):
            previous, current = old.get(metric, -1), new.get(metric, -1)
            row[metric] = {"before": previous, "after": current, "changePercent": (current / previous - 1) * 100 if previous > 0 and current >= 0 else None}
        rows.append(row)
    return {"shipCompactLod": {"before": a.get("shipCompactLod", False), "after": b.get("shipCompactLod", False)}, "comparable": not differences, "differences": differences, "segments": rows, "playerGuiLayoutBudget": {"before": a.get("playerGuiLayoutBudget", False), "after": b.get("playerGuiLayoutBudget", False)}, "worldVisualBudget": {"before": a.get("worldVisualBudget", False), "after": b.get("worldVisualBudget", False)}, "shipBodyPoseFastPath": {"before": a.get("shipBodyPoseFastPath", False), "after": b.get("shipBodyPoseFastPath", False)}, "hullSweepFastPath": {"before": a.get("hullSweepFastPath", False), "after": b.get("hullSweepFastPath", False)}, "ropeReadbackFastPath": {"before": a.get("ropeReadbackFastPath", False), "after": b.get("ropeReadbackFastPath", False)}, "shipMeshLods": {"before": a.get("shipMeshLods", False), "after": b.get("shipMeshLods", False)}, "fixedRopeShapes": {"before": a.get("fixedRopeShapes", False), "after": b.get("fixedRopeShapes", False)}}


def format_result(result):
    lines = ["COMPARABLE" if result["comparable"] else "NOT COMPARABLE — do not claim a performance improvement"]
    lines.extend(result["differences"])
    compact = result["shipCompactLod"]
    lines.append(f"Compact ship LOD (intentional optimization A/B): {compact['before']} -> {compact['after']}")
    gui = result["playerGuiLayoutBudget"]
    lines.append(f"Player GUI layout budget (intentional optimization A/B): {gui['before']} -> {gui['after']}")
    budget = result["worldVisualBudget"]
    lines.append(f"World visual budget (intentional optimization A/B): {budget['before']} -> {budget['after']}")
    body = result["shipBodyPoseFastPath"]
    lines.append(f"Ship body pose fast path (intentional optimization A/B): {body['before']} -> {body['after']}")
    hull = result["hullSweepFastPath"]
    lines.append(f"Hull sweep fast path (intentional optimization A/B): {hull['before']} -> {hull['after']}")
    rope = result["ropeReadbackFastPath"]
    lines.append(f"Rope readback fast path (intentional optimization A/B): {rope['before']} -> {rope['after']}")
    lod = result["shipMeshLods"]
    lines.append(f"Ship Mesh LODs (intentional optimization A/B): {lod['before']} -> {lod['after']}")
    lines.append("Fixed local rope shapes (intentional optimization A/B): " + str(result.get("fixedRopeShapes", {}).get("before", False)) + " -> " + str(result.get("fixedRopeShapes", {}).get("after", False)))
    lines.append("Segment | avg FPS before/after | 1% low before/after | mean ms change | P99 ms before/after | P99 change")
    for row in result["segments"]:
        fps, low, mean, p99 = (row[key] for key in ("averageFps", "low1Fps", "meanMs", "p99Ms"))
        def change(value):
            return "N/A" if value is None else f"{value:+.2f}%"
        lines.append(f"{row['segment']} | {fps['before']:.2f}/{fps['after']:.2f} | {low['before']:.2f}/{low['after']:.2f} | {change(mean['changePercent'])} | {p99['before']:.3f}/{p99['after']:.3f} | {change(p99['changePercent'])}")
    lines.append("Negative ms change is faster. Changes within run-to-run variation are inconclusive; repeat only when the result needs confirmation.")
    return "\n".join(lines)


def main():
    parser = argparse.ArgumentParser(description="Compare PirateSlop benchmark JSON reports with explicit condition checks.")
    parser.add_argument("baseline", type=Path)
    parser.add_argument("candidate", type=Path)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    baseline = json.loads(args.baseline.read_text(encoding="utf-8-sig"))
    candidate = json.loads(args.candidate.read_text(encoding="utf-8-sig"))
    result = compare(baseline, candidate)
    output = format_result(result)
    print(output)
    if args.output:
        args.output.write_text(output + "\n", encoding="utf-8")
    return 0 if result["comparable"] else 2


if __name__ == "__main__":
    raise SystemExit(main())
