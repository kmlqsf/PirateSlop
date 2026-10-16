import argparse
import json
import statistics
from pathlib import Path


def analyze(report):
    if report['metadata']['mode'] != 'isolation':
        raise ValueError('An isolation report is required')
    names = report['cpuStageNames']
    values = report['cpuStageMilliseconds']
    frames = report['frames']
    rows = []
    for segment, summary in enumerate(report['segments']):
        indices = [i for i, f in enumerate(frames) if f['segment'] == segment and segment * 6 + 2 <= f['time'] < (segment + 1) * 6]
        if not indices:
            continue
        def mean(key):
            samples = [frames[i][key] for i in indices if frames[i][key] >= 0]
            return statistics.mean(samples) if samples else None
        stages = {}
        for column, name in enumerate(names):
            samples = [values[i * len(names) + column] for i in indices if values[i * len(names) + column] >= 0]
            stages[name] = statistics.mean(samples) if samples else None
        rows.append({'segment': summary['segment'], 'index': segment, 'frames': len(indices), 'meanMs': mean('frameMs'),
                     'cpuMs': mean('cpuMs'), 'gpuMs': mean('gpuMs'), 'triangles': mean('triangles'),
                     'draws': mean('draws'), 'setPass': mean('setPass'), 'cpuStages': stages,
                     'frustumShips': mean('frustumCrewShips'), 'frustumPlayers': mean('frustumPlayers')})
    complete = report['status'] == 'COMPLETE' and len(rows) == len(report['segments'])
    drift = None
    if complete:
        first, last = rows[0], rows[-1]
        drift = (last['meanMs'] / first['meanMs'] - 1) * 100
        for row in rows:
            t = row['index'] / (len(rows) - 1)
            row['savingsMs'] = {}
            for key in ('meanMs', 'cpuMs', 'gpuMs'):
                if first[key] is None or last[key] is None or row[key] is None:
                    row['savingsMs'][key] = None
                else:
                    reference = first[key] * (1 - t) + last[key] * t
                    row['savingsMs'][key] = reference - row[key]
    ship_cost = {}
    by_name = {row['segment']: row for row in rows}
    if complete and 'Half scale no ships' in by_name:
        full = by_name['No ship rendering']['savingsMs']['gpuMs']
        half_on = by_name['Half render scale']['gpuMs']
        half_off = by_name['Half scale no ships']['gpuMs']
        half = half_on - half_off if half_on is not None and half_off is not None else None
        ship_cost = {'fullScaleGpuMs': full, 'halfScaleGpuMs': half, 'retainedFraction': half / full if full and half is not None else None, 'note': 'Paired visible/hidden ship deltas. Half scale has one quarter of the pixels. Approximate diagnostic only: passes, occlusion, bandwidth and background interactions can be nonlinear.'}
    return {'shipCostByResolution': ship_cost, 'status': report['status'], 'reason': report['reason'], 'metadata': report['metadata'],
            'complete': complete, 'baselineFrameDriftPercent': drift, 'rows': rows,
            'inventory': [e['kind'] for e in report['events'] if e['kind'].startswith(('IsolationInventory:', 'ShipIsolationInventory:'))],
            'notes': 'Diagnostic removals are not shipping optimizations. First 2 seconds of every 6-second stage excluded. Savings use interpolated start/end baseline; not additive. CPU timings may include waits. Fixed camera, stationary fleet, live waves. Missing data is null. A single short run ranks hypotheses; it does not confirm an optimization.'}


def markdown(result):
    def fmt(x):
        return 'N/A' if x is None else f'{x:.2f}'
    lines = [f"Status: {result['status']} {result['reason']}", f"Start/end frame drift: {fmt(result['baselineFrameDriftPercent'])}%",
             'Stage | frame ms | CPU ms | GPU ms | saved frame ms | saved GPU ms',
             '--- | ---: | ---: | ---: | ---: | ---:']
    for row in result['rows']:
        savings = row.get('savingsMs', {})
        lines.append(' | '.join([row['segment'], fmt(row['meanMs']), fmt(row['cpuMs']), fmt(row['gpuMs']), fmt(savings.get('meanMs')), fmt(savings.get('gpuMs'))]))
    if result.get('shipCostByResolution'):
        lines.append('Ship GPU delta by resolution: ' + json.dumps(result['shipCostByResolution']))
    lines.extend(result['inventory'])
    lines.append(result['notes'])
    return '\n'.join(lines)


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('report', type=Path)
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    result = analyze(json.loads(args.report.read_text(encoding='utf-8-sig')))
    if args.output:
        args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
        args.output.with_suffix('.md').write_text(markdown(result), encoding='utf-8')
    print(markdown(result))
