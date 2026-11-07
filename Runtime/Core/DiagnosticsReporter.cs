using System.Collections.Generic;

namespace FastIoC.Core
{
    public class DiagnosticsReporter
    {
        private int cacheHitCount;
        private int cacheMissCount;
        private HashSet<int> cachedTypeIds;

        public void RecordHit(int typeId)
        {
            cacheHitCount++;
            (cachedTypeIds ??= new HashSet<int>()).Add(typeId);
        }

        public void RecordMiss(int typeId)
        {
            cacheMissCount++;
            (cachedTypeIds ??= new HashSet<int>()).Add(typeId);
        }

        public void ReportCacheStatistics()
        {
            if (cacheHitCount + cacheMissCount > 0)
            {
                float hitRatio = (float)cacheHitCount / (cacheHitCount + cacheMissCount) * 100f;
            }
        }

        public void Reset()
        {
            cacheHitCount = 0;
            cacheMissCount = 0;
            cachedTypeIds?.Clear();
        }

        public int TotalRequests => cacheHitCount + cacheMissCount;

        public float HitRatio
        {
            get
            {
                if (TotalRequests == 0)
                    return 0f;
                return (float)cacheHitCount / TotalRequests * 100f;
            }
        }
    }
}

