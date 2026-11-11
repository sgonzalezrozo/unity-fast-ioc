using System.Threading;
using System.Threading.Tasks;

namespace FastIoC.Core
{
    public interface IInitializable
    {
        void Initialize();
    }

    public interface IAsyncInitializable
    {
        Task InitializeAsync(CancellationToken ct = default);
    }
}

