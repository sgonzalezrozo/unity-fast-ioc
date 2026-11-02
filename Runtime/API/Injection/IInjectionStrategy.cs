using FastIoC.Core;

namespace FastIoC.API.Injection
{
    public interface IInjectionStrategy
    {
        string StrategyName { get; }
        bool CanExecute();
        void Inject(System.Type[] injectableTypes, ContextRuntime context);
    }
}

