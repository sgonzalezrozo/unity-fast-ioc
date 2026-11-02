using System.Collections.Generic;
using UnityEngine;

namespace FastIoC.Tests.PlayMode
{
    public class InjectedTestComponent : MonoBehaviour
    {
        [SerializeField] private ITestService injectedService;
        private List<string> injectionLog = new();

        public ITestService InjectedService
        {
            get => injectedService;
            set => injectedService = value;
        }

        public List<string> InjectionLog => injectionLog;

        private void Awake()
        {
            if (injectedService != null) 
                return;
            
            injectedService = new TestService();
        }

        private void OnEnable()
        {
            if (injectedService != null)
                injectionLog.Add($"[{Time.frameCount}] Injected service enabled: {injectedService.GetType().Name}");
            else
                injectionLog.Add($"[{Time.frameCount}] WARNING: Injected service is null");
        }

        private void Start()
        {
            if (injectedService != null)
                injectionLog.Add($"[{Time.frameCount}] Start: Service available");
            else
            {
                Debug.LogError($"[InjectedTestComponent] Service injection failed on {gameObject.name}", gameObject);
                injectionLog.Add($"[{Time.frameCount}] ERROR: Service injection failed");
            }
        }
    }

    public interface ITestService
    {
        string GetTestData();
    }

    public class TestService : ITestService
    {
        private int instanceId = Random.Range(1000, 9999);

        public string GetTestData()
        {
            return $"TestService#{instanceId}";
        }
    }
}