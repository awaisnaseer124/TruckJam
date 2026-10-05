namespace TankerJam.Core
{
    /// <summary>Which lot model GameRules uses to answer "can this truck drive out?".</summary>
    public enum LotModel
    {
        /// <summary>Rotated rectangles on a continuous board (any angle). The runtime default.</summary>
        Geometric,
        /// <summary>The original cell grid (U/D/L/R only). Kept as the reference oracle for tests.</summary>
        Grid,
    }

    public readonly struct ExitResult
    {
        public readonly bool Clear;
        /// <summary>Distance the truck can move forward before touching the first obstacle (bump animation).</summary>
        public readonly float FreeDistance;
        /// <summary>Whole free cells in front (grid view of FreeDistance).</summary>
        public int FreeCells => (int)(FreeDistance + 1e-3f);

        public ExitResult(bool clear, float freeDistance) { Clear = clear; FreeDistance = freeDistance; }
    }

    /// <summary>The parking lot as far as the rules care: which trucks are still in it and who blocks whom.</summary>
    public interface ILot
    {
        ExitResult CheckExit(int truckId);
        void Remove(int truckId);
    }
}
