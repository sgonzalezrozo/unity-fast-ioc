using Tools.FastIoC.Runtime.Core;

namespace Tools.FastIoC.Runtime.API.Injection
{
    public interface IInjectionStrategy
    {
        string StrategyName { get; }
        bool CanExecute();
        void Inject(System.Type[] injectableTypes, ContextRuntime context);
    }
}

