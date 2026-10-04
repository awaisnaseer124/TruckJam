using Unity.Profiling;

namespace TankerJam.Game
{
    /// <summary>
    /// Live render and memory counters for the debug HUD, via ProfilerRecorder (works in the editor and in
    /// development builds on device): draw calls, batches, SetPass calls, triangles and GC bytes per frame.
    /// Budget from the GDD: under 150 draw calls and 100k triangles on a mid-range Android phone.
    /// </summary>
    public sealed class RenderStats : System.IDisposable
    {
        ProfilerRecorder drawCalls, batches, setPass, triangles, gcAlloc;

        public RenderStats()
        {
            drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
        }

        public long DrawCalls => drawCalls.LastValue;
        public long Batches => batches.LastValue;
        public long SetPass => setPass.LastValue;
        public long Triangles => triangles.LastValue;
        public long GcBytes => gcAlloc.LastValue;

        public void Dispose()
        {
            drawCalls.Dispose(); batches.Dispose(); setPass.Dispose(); triangles.Dispose(); gcAlloc.Dispose();
        }
    }
}
