using UnityEngine;

namespace Tools.FastIoC.Tests.PlayMode
{
    /// <summary>
    /// Test component for scene transition testing.
    /// Receives injected services for verification.
    /// </summary>
    public class SceneTransitionTestComponent : MonoBehaviour
    {
        // Static singleton instances
        private static ITrackableService cachedTrackableService;
        private static IQuickService cachedQuickService;

        [SerializeField]
        private ITrackableService trackableService;

        [SerializeField]
        private IQuickService quickService;

        public ITrackableService TrackableService => trackableService;
        public IQuickService QuickService => quickService;

        private void Awake()
        {
            // Initialize services if not already injected
            if (trackableService == null)
            {
                trackableService = new TrackableService();
                cachedTrackableService = trackableService;
            }

            // For QuickService: reuse singleton across scenes
            if (quickService == null)
            {
                if (cachedQuickService == null)
                {
                    cachedQuickService = new QuickService();
                }
                quickService = cachedQuickService;
            }
            else
            {
                // Cache the injected singleton
                cachedQuickService = quickService;
            }
        }

        public void SetServices(ITrackableService trackable, IQuickService quick)
        {
            trackableService = trackable;
            quickService = quick;
            cachedQuickService = quick;
        }
    }
}

