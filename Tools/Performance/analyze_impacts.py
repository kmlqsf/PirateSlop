import argparse
import json
from bisect import bisect_left, bisect_right
from pathlib import Path


KINDS = ("PlayerDamage", "PlayerDeath", "Ragdoll:", "FirearmShot:", "FirearmImpact:", "CharacterProbe:")


def analyze(report):
    frames = report.get("frames", [])
    times = [frame["time"] for frame in frames]
    by_number = {frame["unityFrame"]: i for i, frame in enumerate(frames)}
    seen = set()
    results = []
    for event in report.get("events", []):
        kind = event["kind"]
        if not kind.startswith(KINDS) or event["time"] < 0:
            continue
        index = by_number.get(event["unityFrame"])
        first = bisect_left(times, event["time"] - .1)
        end = bisect_right(times, event["time"] + .25)
        if index is not None:
            first = min(first, index)
            end = max(end, index + 1)
        indices = list(range(first, end))
        worst = max(indices, key=lambda i: frames[i]["frameMs"]) if indices else None
        identity = (kind, event.get("target", ""))
        row = {"event": event, "firstForTargetAndKind": identity not in seen,
               "exactFrameAvailable": index is not None,
               "eventFrame": frames[index] if index is not None else None,
               "windowSeconds": [-.1, .25], "windowFrames": len(indices),
               "worstNearbyFrame": frames[worst] if worst is not None else None}
        seen.add(identity)
        for prefix in ("cpuStage", "marker"):
            names = report.get(prefix + "Names", [])
            values = report.get(prefix + "Milliseconds", [])
            timings = {}
            if index is not None:
                for column, name in enumerate(names):
                    offset = index * len(names) + column
                    if offset < len(values) and values[offset] >= 0:
                        timings[name] = values[offset]
            row[prefix + "Milliseconds"] = timings
        results.append(row)
    return {"status": report.get("status"), "reason": report.get("reason"),
            "metadata": report.get("metadata"), "events": results,
            "note": "Events match completed frames by unityFrame. Nearby maxima show correlation, not causation. GPU/counter data may be delayed; nested scopes must not be added. First means first observed event of this kind for this target in this recording, not necessarily cold asset state."}


def main():
    parser = argparse.ArgumentParser(description="Match character impact/death events to frame data.")
    parser.add_argument("report", type=Path)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    result = analyze(json.loads(args.report.read_text(encoding="utf-8-sig")))
    if args.output:
        args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"{result['status']} character events={len(result['events'])}")
    print("time | kind | target | first observed | exact frame ms | nearby worst ms")
    for row in result["events"]:
        event, frame, worst = row["event"], row["eventFrame"], row["worstNearbyFrame"]
        current = f"{frame['frameMs']:.3f}" if frame else "N/A"
        nearby = f"{worst['frameMs']:.3f}" if worst else "N/A"
        print(f"{event['time']:.3f} | {event['kind']} | {event.get('target', '')} | {row['firstForTargetAndKind']} | {current} | {nearby}")
    print(result["note"])


if __name__ == "__main__":
    main()
