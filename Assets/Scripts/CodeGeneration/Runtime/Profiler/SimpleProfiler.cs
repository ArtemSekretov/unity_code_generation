using System;

namespace CodeGeneration.Runtime.Profiler
{
    public struct ZoneInfo
    {
        public string Name;
    }
    
    public struct Zone
    {
        public long ElapseExclusive;
        public long ElapseInclusive;
        public ulong HitCount;
    }

    public struct CurrentZone
    {
        public int ParentIndex;
        public long Start;
        public long OldInclusive;
    }
    
    public class ProfilerData
    {
        public long Start;
        public long End;
        
        public Zone[] Zones;
        public ZoneInfo[] ZoneInfos;
        public int ProfilerParent;
    }
    
    public static class SimpleProfiler
    {
        public static readonly ProfilerData Profiler = new();

        public static long GetFrequency()
        {
            return System.Diagnostics.Stopwatch.Frequency;
        }

        public static void ResetProfiler()
        {
            Profiler.Start = System.Diagnostics.Stopwatch.GetTimestamp();
            Profiler.End = 0;
            Profiler.ProfilerParent = 0;
            
            Array.Clear(Profiler.Zones, 0, Profiler.Zones.Length);
        }
        
        public static void EndProfile()
        {
            Profiler.End = System.Diagnostics.Stopwatch.GetTimestamp();
        }
        
        public static CurrentZone BeginTimedBlock(int index)
        {
            ref var zone = ref Profiler.Zones[index];
            var parentIndex = Profiler.ProfilerParent;
            Profiler.ProfilerParent = index;

            return new CurrentZone
            {
                Start = System.Diagnostics.Stopwatch.GetTimestamp(),
                OldInclusive = zone.ElapseInclusive,
                ParentIndex = parentIndex,
            };
        }
        
        public static void EndTimedBlock(CurrentZone currentZone, int index)
        {
            var elapsed = System.Diagnostics.Stopwatch.GetTimestamp() - currentZone.Start;
            Profiler.ProfilerParent = currentZone.ParentIndex;

            ref var parent = ref Profiler.Zones[currentZone.ParentIndex];
            parent.ElapseExclusive -= elapsed;
            
            ref var zone = ref Profiler.Zones[index];
            zone.ElapseExclusive += elapsed;
            zone.ElapseInclusive = currentZone.OldInclusive + elapsed;
            zone.HitCount++;
        }
    }
}
