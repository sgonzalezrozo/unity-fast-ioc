namespace FastIoC.Factories
{
    /// <summary>
    /// Base factory interface for runtime object spawning.
    /// All factories are generated at editor time.
    /// Only way to create instances at runtime.
    /// </summary>
    public interface IFactory<out T> where T : class
    {
        /// <summary>
        /// Create or resolve an instance.
        /// </summary>
        T Create();
    }
}

