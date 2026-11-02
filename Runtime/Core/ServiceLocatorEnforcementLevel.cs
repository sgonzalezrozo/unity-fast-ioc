namespace FastIoC.Core
{
    /// <summary>
    /// Enforcement level for Service Locator anti-pattern detection.
    /// Controls how aggressively violations are reported.
    /// </summary>
    public enum ServiceLocatorEnforcementLevel
    {
        /// <summary>
        /// No enforcement - Service Locator calls are allowed (development only).
        /// </summary>
        Off = 0,

        /// <summary>
        /// Warning level - Logs warnings but doesn't throw.
        /// Useful for legacy code migration.
        /// </summary>
        Warning = 1,

        /// <summary>
        /// Strict level - Throws exceptions immediately.
        /// Default for new code (recommended).
        /// </summary>
        Strict = 2,

        /// <summary>
        /// Log all calls for analysis, includes stack traces.
        /// Useful for auditing and finding violations.
        /// </summary>
        Audit = 3,
    }
}

