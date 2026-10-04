"""Tanker Jam level generator, solver and difficulty scorer.

Rules match Assets/_TankerJam/Scripts/Core/GameRules.cs exactly:
- A truck can leave the lot if the cells in front of it (the way it faces) are empty to the edge.
- A freed truck takes a bay (logical slot, in arrival order).
- Settle: repeatedly, for each slot by arrival, for each vessel left to right, if the vessel's
  BOTTOM unit matches the slot's color and the slot has room, move one unit. Full slots leave.
- A level is valid if some tap order clears everything using only the regular bays (slots).

Usage:
  python levelgen.py --seed 400 --trucks 11 --lens 2,2,3,3,4 --colors 4 --vessels 4 --slots 3 \
                     --min-rate 0.2 --max-rate 0.45 --out level_005.json
"""
import argparse, json, random

DIRS = {'U': (0, -1), 'D': (0, 1), 'L': (-1, 0), 'R': (1, 0)}
CAP = {2: 2, 3: 4, 4: 6}
COLOR_KEYS = 'PYCVG'  # pink, yellow, cyan, purple, green


def cells(c):
    x, y, l, d = c['x'], c['y'], c['len'], c['d']
    return [(x + i, y) if d in 'LR' else (x, y + i) for i in range(l)]


def can_exit(c, cars, cones, size):
    occ = set(cones)
    for o in cars:
        if o is not c:
            occ.update(cells(o))
    dx, dy = DIRS[c['d']]
    cs = cells(c)
    x, y = cs[-1] if c['d'] in 'DR' else cs[0]
    while True:
        x += dx; y += dy
        if not (0 <= x < size and 0 <= y < size):
            return True
        if (x, y) in occ:
            return False


def settle(slots, vessels, trace=None):
    """slots: tuple of (truck_id, color, fill, cap) in arrival order; vessels: tuple of tuples (bottom first)."""
    slots = [list(s) for s in slots]
    vessels = [list(v) for v in vessels]
    while True:
        changed = True
        while changed:
            changed = False
            for s in slots:
                if s[2] >= s[3]:
                    continue
                for vi, v in enumerate(vessels):
                    if s[2] < s[3] and v and v[0] == s[1]:
                        v.pop(0); s[2] += 1; changed = True
                        if trace is not None:
                            trace.append((s[1], vi, s[0]))
        full = [s for s in slots if s[2] == s[3]]
        if not full:
            break
        slots = [s for s in slots if s[2] < s[3]]
    return tuple(tuple(s) for s in slots), tuple(tuple(v) for v in vessels)


def assign(slots, vessels, car_id, car, trace=None):
    return settle(slots + ((car_id, car['color'], 0, CAP[car['len']]),), vessels, trace)


def solve(level, limit=400000):
    cars, size, SL = level['cars'], level['size'], level['slots']
    cones = [tuple(c) for c in level['cones']]
    seen, count = set(), [0]

    def key(rem, slots, vessels):
        return (rem, tuple((s[1], s[2], s[3]) for s in slots), vessels)

    def rec(rem, slots, vessels):
        count[0] += 1
        if count[0] > limit:
            raise RuntimeError('search limit')
        if not rem and not slots:
            return []
        k = key(rem, slots, vessels)
        if k in seen:
            return None
        seen.add(k)
        if len(slots) >= SL:
            return None
        rc = [cars[i] for i in rem]
        for i in sorted(rem):
            if can_exit(cars[i], rc, cones, size):
                ns, nv = assign(slots, vessels, i, cars[i])
                r = rec(rem - {i}, ns, nv)
                if r is not None:
                    return [i] + r
        return None

    return rec(frozenset(range(len(cars))), (), tuple(tuple(t) for t in level['tubes']))


def random_play(level, rng):
    cars, size, SL = level['cars'], level['size'], level['slots']
    cones = [tuple(c) for c in level['cones']]
    rem, slots, vessels = set(range(len(cars))), (), tuple(tuple(t) for t in level['tubes'])
    while True:
        if not rem and not slots:
            return True
        if len(slots) >= SL:
            return False
        rc = [cars[i] for i in rem]
        opts = [i for i in rem if can_exit(cars[i], rc, cones, size)]
        if not opts:
            return False
        i = rng.choice(opts)
        rem.discard(i)
        slots, vessels = assign(slots, vessels, i, cars[i])


def gen_lot(rng, size, n, lens, n_cones, min_depth=3):
    for _ in range(4000):
        occ, cars, cones = set(), [], []
        for _ in range(n_cones):
            p = (rng.randrange(size), rng.randrange(size))
            if p not in occ:
                occ.add(p); cones.append(p)
        tries = 0
        while len(cars) < n and tries < 500:
            tries += 1
            l, d = rng.choice(lens), rng.choice('UDLR')
            if d in 'LR':
                x, y = rng.randrange(size - l + 1), rng.randrange(size)
            else:
                x, y = rng.randrange(size), rng.randrange(size - l + 1)
            c = {'x': x, 'y': y, 'len': l, 'd': d}
            cs = cells(c)
            if any(p in occ for p in cs):
                continue
            occ.update(cs); cars.append(c)
        if len(cars) < n:
            continue
        rem, depth = list(cars), 0
        while rem:
            ex = [c for c in rem if can_exit(c, rem, cones, size)]
            if not ex:
                break
            rem = [c for c in rem if c not in ex]; depth += 1
        if not rem and depth >= min_depth:
            return cars, cones
    raise RuntimeError('could not place a clearable lot')


def make_level(seed, size=7, trucks=11, lens=(2, 2, 3, 3, 4), colors=4, vessels=4, slots=3,
               cones=2, rate=(0.2, 0.45), samples=400, attempts=300):
    rng = random.Random(seed)
    keys = COLOR_KEYS[:colors]
    for _ in range(attempts):
        try:
            cars, cone_cells = gen_lot(rng, size, trucks, list(lens), cones)
        except RuntimeError:
            continue
        for i, c in enumerate(cars):
            c['color'] = keys[i % colors]
        pool = {}
        for c in cars:
            pool[c['color']] = pool.get(c['color'], 0) + CAP[c['len']]
        runs = []
        while sum(pool.values()):
            col = rng.choice([k for k, v in pool.items() if v])
            r = min(pool[col], rng.choice([1, 2, 2, 3]))
            pool[col] -= r; runs.append([col] * r)
        tubes = [[] for _ in range(vessels)]
        for r in runs:
            t = min(range(vessels), key=lambda k: len(tubes[k]) + rng.random() * 2)
            tubes[t] += r
        level = {'size': size, 'slots': slots, 'cars': cars, 'cones': [list(p) for p in cone_cells], 'tubes': tubes}
        try:
            sol = solve(level)
        except RuntimeError:
            continue
        if sol is None:
            continue
        win = sum(random_play(level, random.Random(k)) for k in range(samples)) / samples
        if rate[0] <= win <= rate[1]:
            level['sol'] = sol
            level['randomWinRate'] = win
            return level
    return None


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--seed', type=int, default=400)
    ap.add_argument('--size', type=int, default=7)
    ap.add_argument('--trucks', type=int, default=11)
    ap.add_argument('--lens', default='2,2,3,3,4')
    ap.add_argument('--colors', type=int, default=4)
    ap.add_argument('--vessels', type=int, default=4)
    ap.add_argument('--slots', type=int, default=3)
    ap.add_argument('--cones', type=int, default=2)
    ap.add_argument('--min-rate', type=float, default=0.2)
    ap.add_argument('--max-rate', type=float, default=0.45)
    ap.add_argument('--tries', type=int, default=60, help='seeds to try, starting at --seed')
    ap.add_argument('--out', default='level.json')
    a = ap.parse_args()
    lens = tuple(int(x) for x in a.lens.split(','))
    for s in range(a.seed, a.seed + a.tries):
        lv = make_level(s, a.size, a.trucks, lens, a.colors, a.vessels, a.slots, a.cones, (a.min_rate, a.max_rate))
        if lv:
            json.dump(lv, open(a.out, 'w'), indent=1)
            print(f'seed {s}: random win rate {lv["randomWinRate"]:.0%}, solution {lv["sol"]} -> {a.out}')
            return
    print('No level found; widen the rate band or change the parameters.')


if __name__ == '__main__':
    main()
