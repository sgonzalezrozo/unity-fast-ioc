using UnityEngine;

namespace FastIoCBench
{
    /// <summary>
    /// Placed in every injected scene. Its Awake runs before any other Awake of the scene,
    /// and its Start runs after every container has injected the scene:
    /// Zenject and VContainer inject in Awake, Fast IoC in SceneManager.sceneLoaded, all before Start.
    /// The concrete FastIoCBench.Gen.BenchSceneMarker is generated into Assets with [DefaultExecutionOrder(-32000)].
    /// </summary>
    public abstract class BenchSceneMarkerBase : MonoBehaviour
    {
        public static long FirstAwake { get; private set; }
        public static long Ready { get; private set; }
        public static bool IsReady { get; private set; }

        public static void ResetMarks()
        {
            FirstAwake = 0;
            Ready = 0;
            IsReady = false;
        }

        protected void Awake()
        {
            FirstAwake = BenchClock.Now;
        }

        protected void Start()
        {
            Ready = BenchClock.Now;
            IsReady = true;
        }
    }
}
