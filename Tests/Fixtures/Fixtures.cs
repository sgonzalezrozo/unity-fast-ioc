// Types the Edit Mode tests generate code for.
// They live outside the test assembly on purpose: the generators skip assemblies whose name contains "Test".
// None of these installers has [MonoInstaller], so a project's real generation never picks them up.
using System;
using System.Runtime.CompilerServices;
using FastIoC.API;
using FastIoC.Core;
using UnityEngine;

[assembly: InternalsVisibleTo("Assembly-CSharp")]
[assembly: InternalsVisibleTo("FastIoC.Tests.Editor")]

namespace FastIoC.Fixtures
{
    public interface IService { }
    public class Service : IService { }
    public class Other { }

    public class Consumer
    {
        public Consumer(IService service, Lazy<IService> lazyService, Func<Other> createOther) { }
    }

    public class CycleA { public CycleA(CycleB b) { } }
    public class CycleB { public CycleB(CycleA a) { } }

    public class LazyCycleA { public LazyCycleA(Lazy<LazyCycleB> b) { } }
    public class LazyCycleB { public LazyCycleB(LazyCycleA a) { } }

    public interface IMissing { }
    public class NeedsMissing { public NeedsMissing(IMissing missing) { } }
    public class NeedsLazyMissing { public NeedsLazyMissing(Lazy<IMissing> missing) { } }
    public class OptionalMissing { public OptionalMissing(IMissing missing = null) { } }

    public class FixtureView : MonoBehaviour
    {
#pragma warning disable CS0169, CS0649
        internal IService service;
        internal Lazy<IService> lazyService;
        internal Func<Other> createOther;
        private IService hidden;
#pragma warning restore CS0169, CS0649
    }

    public class SpawnedView : MonoBehaviour, IInstanciable { }

    public class FixtureInstaller : BindingRecorder
    {
        public override void InstallBindings()
        {
            Bind<IService>().To<Service>().AsSingle();
            Bind<Other>().AsTransient();
        }
    }
}
