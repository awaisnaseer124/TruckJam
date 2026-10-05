// The shape-level difficulty curve (Data/Generation/curve_v2.json): which tier a level belongs to and the
// PatternSpec that generates it. Same sawtooth as the grid curve: every hardEvery-th level is Hard and aims
// at the low end of its band, the next one is a breather at the high end, the rest drift from the easy
// side of the band to the hard side across the tier.
using System;
using System.Collections.Generic;
using System.Globalization;

namespace TankerJam.Core
{
    public sealed class PatternTier
    {
        public int Tier, From, To, Lot, Slots, Colors, Vessels = 4, MinTrucks, MaxTrucks, Depth;
        public int[] Lens;
        public float RateLo, RateHi;
        public PatternFamily[] Families;
    }

    public sealed class PatternCurve
    {
        public int FirstLevel = 11, LastLevel = 50, HardEvery = 5;
        public readonly List<PatternTier> Tiers = new List<PatternTier>();

        public static PatternCurve Parse(string json)
        {
            var root = (Dictionary<string, object>)MiniJson.Parse(json);
            var c = new PatternCurve
            {
                FirstLevel = I(root, "firstLevel"),
                LastLevel = I(root, "lastLevel"),
                HardEvery = I(root, "hardEvery"),
            };
            foreach (Dictionary<string, object> t in (List<object>)root["tiers"])
            {
                var trucks = (List<object>)t["trucks"];
                var rate = (List<object>)t["rate"];
                var lens = (List<object>)t["lens"];
                var fams = (List<object>)t["families"];
                var tier = new PatternTier
                {
                    Tier = I(t, "tier"), From = I(t, "from"), To = I(t, "to"), Lot = I(t, "lot"), Slots = I(t, "slots"),
                    Colors = I(t, "colors"), Depth = I(t, "depth"), Vessels = t.ContainsKey("vessels") ? I(t, "vessels") : 4,
                    MinTrucks = Convert.ToInt32(trucks[0]), MaxTrucks = Convert.ToInt32(trucks[1]),
                    RateLo = F(rate[0]), RateHi = F(rate[1]),
                    Lens = new int[lens.Count], Families = new PatternFamily[fams.Count],
                };
                for (int i = 0; i < lens.Count; i++) tier.Lens[i] = Convert.ToInt32(lens[i]);
                for (int i = 0; i < fams.Count; i++)
                    tier.Families[i] = (PatternFamily)Enum.Parse(typeof(PatternFamily), (string)fams[i], ignoreCase: true);
                c.Tiers.Add(tier);
            }
            return c;
        }

        static int I(Dictionary<string, object> d, string k) => Convert.ToInt32(d[k], CultureInfo.InvariantCulture);
        static float F(object o) => (float)Convert.ToDouble(o, CultureInfo.InvariantCulture);

        public PatternTier TierFor(int level)
        {
            foreach (var t in Tiers) if (level >= t.From && level <= t.To) return t;
            return null;
        }

        public bool IsHard(int level) => HardEvery > 0 && level % HardEvery == 0;

        /// <summary>Accepted band and target rate for one level.</summary>
        public void Band(int level, out float lo, out float hi, out float target)
        {
            var t = TierFor(level) ?? throw new ArgumentException($"Level {level} is outside the shape curve.");
            float mid = (t.RateLo + t.RateHi) / 2f, span = t.RateHi - t.RateLo;
            if (IsHard(level)) { lo = t.RateLo; hi = mid; target = t.RateLo + 0.2f * span; return; }
            if (HardEvery > 0 && level % HardEvery == 1) { lo = mid; hi = t.RateHi; target = t.RateHi - 0.2f * span; return; }
            float progress = t.To > t.From ? (float)(level - t.From) / (t.To - t.From) : 0.5f;
            lo = t.RateLo;
            hi = t.RateHi;
            target = t.RateHi - (0.3f + 0.4f * progress) * span;
        }

        /// <summary>The generator job for a level; <paramref name="family"/> narrows the layout family (null = any in the tier).</summary>
        public PatternSpec SpecFor(int level, PatternFamily? family = null, int seedOffset = 0)
        {
            var t = TierFor(level) ?? throw new ArgumentException($"Level {level} is outside the shape curve.");
            Band(level, out float lo, out float hi, out float target);
            return new PatternSpec
            {
                Seed = level * 7919 + seedOffset,
                LotSize = t.Lot, Slots = t.Slots, Colors = t.Colors,
                MinTrucks = t.MinTrucks, MaxTrucks = t.MaxTrucks, Lens = t.Lens,
                RateLo = lo, RateHi = hi, TargetRate = target, MinDepth = t.Depth, MinVessels = t.Vessels,
                Families = family.HasValue ? new[] { family.Value } : t.Families,
            };
        }

        /// <summary>
        /// Generates one curve level: first with the family this level's slot in the cycle prefers (so the
        /// curve alternates petals / traced shapes / mirrors), then with any family of the tier.
        /// </summary>
        public PatternResult Generate(int level)
        {
            var t = TierFor(level) ?? throw new ArgumentException($"Level {level} is outside the shape curve.");
            var preferred = t.Families[(level - FirstLevel) % t.Families.Length];
            var spec = SpecFor(level, preferred);
            spec.Attempts = 250;
            var r = PatternGenerator.Generate(spec);
            if (r.Ok) return Finish(r, level, t);
            spec = SpecFor(level, null, seedOffset: 1);
            spec.Attempts = 600;
            r = PatternGenerator.Generate(spec);
            return r.Ok ? Finish(r, level, t) : r;
        }

        PatternResult Finish(PatternResult r, int level, PatternTier t)
        {
            r.Draft.Index = level;
            r.Draft.Tier = t.Tier;
            r.Draft.Hard = IsHard(level);
            return r;
        }
    }
}
