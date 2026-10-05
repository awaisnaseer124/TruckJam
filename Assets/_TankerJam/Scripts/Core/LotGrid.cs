// Cell occupancy for a grid lot: O(1) lookups for exit checks. The original rules model; kept as the
// reference oracle that the geometric lot (FreeLot) is tested against.

namespace TankerJam.Core
{
    public sealed class LotGrid : ILot
    {
        public const int Empty = -1;
        public const int Cone = -2;

        readonly int[] cells;
        readonly LevelDef level;
        public readonly int Size;

        public LotGrid(LevelDef level)
        {
            this.level = level;
            Size = level.Size;
            cells = new int[Size * Size];
            for (int i = 0; i < cells.Length; i++) cells[i] = Empty;
            foreach (var c in level.Cones) cells[Index(c.X, c.Y)] = Cone;
            foreach (var t in level.Trucks)
                for (int i = 0; i < t.Len; i++)
                {
                    var c = t.CellAt(i);
                    int idx = Index(c.X, c.Y);
                    if (cells[idx] != Empty)
                        throw new System.InvalidOperationException($"Truck {t.Id} overlaps cell {c} (occupied by {cells[idx]}).");
                    cells[idx] = t.Id;
                }
        }

        int Index(int x, int y) => y * Size + x;

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Size && y < Size;

        /// <summary>Empty, Cone, or the id of the truck covering the cell.</summary>
        public int At(int x, int y) => cells[Index(x, y)];

        public void Remove(int truckId)
        {
            var t = level.Trucks[truckId];
            for (int i = 0; i < t.Len; i++)
            {
                var c = t.CellAt(i);
                int idx = Index(c.X, c.Y);
                if (cells[idx] == truckId) cells[idx] = Empty;
            }
        }

        /// <summary>Walks from the truck's head in its facing direction until it leaves the grid or hits something.</summary>
        public ExitResult CheckExit(int truckId)
        {
            var t = level.Trucks[truckId];
            TruckDef.Step(t.Facing, out int dx, out int dy);
            var h = t.Head();
            int x = h.X, y = h.Y, free = 0;
            while (true)
            {
                x += dx; y += dy;
                if (!InBounds(x, y)) return new ExitResult(true, free);
                int v = cells[Index(x, y)];
                if (v != Empty && v != truckId) return new ExitResult(false, free);
                free++;
            }
        }
    }
}
