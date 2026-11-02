using UnityEngine;

namespace FastIoC.Tests.PlayMode
{
    /// <summary>
    /// Test component for async service initialization testing.
    /// Receives injected async services for verification.
    /// </summary>
    public class AsyncTestComponent : MonoBehaviour
    {
        [SerializeField]
        private IAsyncService asyncService;

        [SerializeField]
        private ILongRunningService longRunningService;

        public IAsyncService AsyncService => asyncService;
        public ILongRunningService LongRunningService => longRunningService;

        private void Awake()
        {
            // Initialize services if not already set via dependency injection
            if (asyncService == null)
            {
                asyncService = new AsyncService();
            }

            if (longRunningService == null)
            {
                longRunningService = new LongRunningService();
            }
        }

        public void SetServices(IAsyncService asyncSvc, ILongRunningService longRunSvc)
        {
            asyncService = asyncSvc;
            longRunningService = longRunSvc;
        }
    }
}

