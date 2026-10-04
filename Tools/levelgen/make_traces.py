"""Writes random valid tap sequences and the unit order Python produces, for the C# parity test."""
import json, random, sys
from levelgen import can_exit, assign
level = json.load(open(sys.argv[1]))
out = []
for k in range(20):
    rng = random.Random(1000 + k)
    cars, size = level['cars'], level['size']
    cones = [tuple(c) for c in level['cones']]
    rem, slots, vessels = set(range(len(cars))), (), tuple(tuple(t) for t in level['tubes'])
    taps, trace = [], []
    while rem and len(slots) < level['slots']:
        rc = [cars[i] for i in rem]
        opts = [i for i in rem if can_exit(cars[i], rc, cones, size)]
        if not opts: break
        i = rng.choice(opts); rem.discard(i); taps.append(i)
        slots, vessels = assign(slots, vessels, i, cars[i], trace)
    out.append({'taps': taps, 'units': [f'{c}:v{v}->t{t}' for c, v, t in trace]})
json.dump(out, open(sys.argv[2], 'w'), indent=1)
print(len(out), 'traces')
