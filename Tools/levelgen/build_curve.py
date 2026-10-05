"""Builds the level set from curve.json: one solvable, difficulty-scored level per slot of the curve.

For each level it picks parameters from its tier (trucks, colors and slots spread across the tier's range),
narrows the random-win-rate band for the sawtooth (Hard levels at the low end, the level after a Hard one
at the high end), and searches seeds with levelgen.make_level until a level lands in the band.

Output per level: Assets/_TankerJam/Data/Levels/level_NNN.json, with a "meta" block (index, tier, hard,
seed, introduces). Also writes Python parity traces for the Unity tests.

Usage:
  python build_curve.py                 # all levels, parallel
  python build_curve.py --only 1-10     # a range
  python build_curve.py --jobs 4
"""
import argparse, json, os, random, sys, time
from concurrent.futures import ProcessPoolExecutor, as_completed

from levelgen import make_level, can_exit, assign

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '..', '..'))
LEVEL_DIR = os.path.join(ROOT, 'Assets', '_TankerJam', 'Data', 'Levels')
TRACE_DIR = os.path.join(ROOT, 'Assets', '_TankerJam', 'Tests', 'EditMode', 'Fixtures', 'Levels')


def pick(values, t):
    """Spread a tier range across its levels: t in 0..1 picks low -> high."""
    if len(values) == 1:
        return values[0]
    lo, hi = values[0], values[-1]
    return int(round(lo + (hi - lo) * t))


def jobs_from_curve(curve):
    jobs = []
    hard_every = curve['hardEvery']
    for tier_index, tier in enumerate(curve['tiers']):
        span = max(1, tier['to'] - tier['from'])
        for level in range(tier['from'], tier['to'] + 1):
            t = (level - tier['from']) / span
            lo, hi = tier['rate']
            hard = level % hard_every == 0
            after_hard = level > 1 and (level - 1) % hard_every == 0
            third = (hi - lo) / 3
            if hard:
                band = (lo, lo + third)
            elif after_hard:
                band = (hi - third, hi)
            else:
                band = (lo, hi)
            jobs.append({
                'index': level,
                'tier': tier_index + 1,
                'hard': hard,
                'size': curve['size'],
                'slots': pick(tier['slots'], t),
                'colors': pick(tier['colors'], t),
                'trucks': pick(tier['trucks'], t),
                'lens': tier['lens'],
                'vessels': tier['vessels'],
                'cones': tier['cones'],
                'band': band,
                'introduces': curve.get('introduces', {}).get(str(level)),
            })
    return jobs


def build_one(job, tries=40):
    start = time.time()
    for k in range(tries):
        seed = job['index'] * 1000 + k
        lv = make_level(seed, job['size'], job['trucks'], tuple(job['lens']), job['colors'], job['vessels'],
                        job['slots'], job['cones'], job['band'], samples=300, attempts=60)
        if lv:
            lv['meta'] = {'index': job['index'], 'tier': job['tier'], 'hard': job['hard'], 'seed': seed}
            if job['introduces']:
                lv['meta']['introduces'] = job['introduces']
            return job['index'], lv, time.time() - start
    return job['index'], None, time.time() - start


def traces(level, count=20):
    """Random valid tap sequences and the unit order Python produces (same as make_traces.py)."""
    out = []
    cars, size = level['cars'], level['size']
    cones = [tuple(c) for c in level['cones']]
    for k in range(count):
        rng = random.Random(1000 + k)
        rem, slots, vessels = set(range(len(cars))), (), tuple(tuple(t) for t in level['tubes'])
        taps, trace = [], []
        while rem and len(slots) < level['slots']:
            rc = [cars[i] for i in rem]
            opts = [i for i in rem if can_exit(cars[i], rc, cones, size)]
            if not opts:
                break
            i = rng.choice(sorted(opts))
            rem.discard(i)
            taps.append(i)
            slots, vessels = assign(slots, vessels, i, cars[i], trace)
        out.append({'taps': taps, 'units': [f'{c}:v{v}->t{t}' for c, v, t in trace]})
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--curve', default=os.path.join(HERE, 'curve.json'))
    ap.add_argument('--only', help='level range, e.g. 1-10')
    ap.add_argument('--jobs', type=int, default=max(1, (os.cpu_count() or 2) - 1))
    a = ap.parse_args()

    curve = json.load(open(a.curve))
    jobs = jobs_from_curve(curve)
    if a.only:
        lo, hi = (int(x) for x in a.only.split('-'))
        jobs = [j for j in jobs if lo <= j['index'] <= hi]

    os.makedirs(LEVEL_DIR, exist_ok=True)
    os.makedirs(TRACE_DIR, exist_ok=True)
    failed = []
    with ProcessPoolExecutor(max_workers=a.jobs) as pool:
        futures = {pool.submit(build_one, j): j for j in jobs}
        for f in as_completed(futures):
            index, lv, secs = f.result()
            name = f'level_{index:03d}'
            if lv is None:
                failed.append(index)
                print(f'{name}: FAILED after {secs:.0f}s', flush=True)
                continue
            json.dump(lv, open(os.path.join(LEVEL_DIR, name + '.json'), 'w'), indent=1)
            json.dump(traces(lv), open(os.path.join(TRACE_DIR, name + '.traces.json'), 'w'), indent=1)
            m = lv['meta']
            print(f'{name}: tier {m["tier"]}{" HARD" if m["hard"] else ""} seed {m["seed"]} '
                  f'trucks {len(lv["cars"])} slots {lv["slots"]} rate {lv["randomWinRate"]:.0%} ({secs:.0f}s)', flush=True)
    if failed:
        print('FAILED levels:', sorted(failed), '- widen their band or change the tier parameters.')
        sys.exit(1)
    print('All levels built.')


if __name__ == '__main__':
    main()
