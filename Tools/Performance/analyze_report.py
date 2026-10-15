import argparse
import itertools
import json
import math
from pathlib import Path


def analyze(path, seconds=10):
    report = json.loads(path.read_text(encoding="utf-8-sig"))
    frames = report.get("frames", [])
    names = report.get("cpuStageNames", [])
    values = report.get("cpuStageMilliseconds", [])
    windows = []
    buckets = {}
    for i, frame in enumerate(frames):
        buckets.setdefault(int(frame["time"] // seconds) * seconds, []).append(i)
    for start, indices in sorted(buckets.items()):
        costs = {}
        for column, name in enumerate(names):
            samples = [values[i * len(names) + column] for i in indices]
            valid = [sample for sample in samples if sample >= 0]
            costs[name] = sum(valid) / len(valid) if valid else None
        gpu = [frames[i]["gpuMs"] for i in indices if frames[i]["gpuMs"] >= 0]
        duration = sum(frames[i]["frameMs"] for i in indices)
        windows.append({"start": start, "frames": len(indices), "fps": len(indices) * 1000 / duration if duration else 0,
                        "gpuMeanMs": sum(gpu) / len(gpu) if gpu else None, "cpuStageMeanMs": costs})
    fleet = {}
    for sample in report.get("shipSamples", []):
        if sample["teamId"] > 0:
            fleet.setdefault(sample["teamId"], []).append(sample)
    ship_summary = []
    for team, samples in sorted(fleet.items()):
        transitions = []
        previous = None
        for sample in samples:
            state = (sample["insideWhirlpool"], sample["sinking"], int(sample["floodLevel"] * 10))
            if state != previous:
                transitions.append({"time": sample["time"], "insideWhirlpool": state[0], "sinking": state[1],
                                    "floodLevel": sample["floodLevel"], "distance": sample["whirlpoolDistance"], "position": sample["position"]})
            previous = state
        ship_summary.append({"team": team, "first": samples[0], "last": samples[-1], "transitions": transitions})
    near_fleet = []
    for start in sorted(buckets):
        samples = [min(samples, key=lambda s: abs(s["time"] - start)) for samples in fleet.values()]
        samples = [sample for sample in samples if abs(sample["time"] - start) <= 1.5]
        distances = [math.dist((a["position"]["x"], a["position"]["z"]), (b["position"]["x"], b["position"]["z"]))
                     for a, b in itertools.combinations(samples, 2)]
        near_fleet.append({"time": start, "minShipSeparationXZ": min(distances) if distances else None})
    return {"file": str(path), "status": report["status"], "reason": report["reason"], "metadata": report["metadata"],
            "total": report["total"], "segments": report["segments"], "windows": windows, "fleet": ship_summary,
            "fleetSeparation": near_fleet, "errors": report.get("errors"), "warnings": report.get("warnings"),
            "events": [event for event in report.get("events", []) if event["kind"] != "Warning"]}


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("report", type=Path)
    parser.add_argument("--seconds", type=int, default=10)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    if args.seconds <= 0:
        parser.error("--seconds must be positive")
    output = json.dumps(analyze(args.report, args.seconds), ensure_ascii=False, indent=2)
    if args.output:
        args.output.write_text(output, encoding="utf-8")
    else:
        print(output)
