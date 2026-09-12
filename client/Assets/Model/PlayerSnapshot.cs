using System.Collections.Generic;

namespace Broodline.Model
{
    /// The last /v1/sync response, cached.
    ///
    /// client_architecture section 7: the client is a cache with an outbox and
    /// is never a source of truth. Balances here are for DISPLAY - a cached
    /// balance is a label, not a number the client may do arithmetic on before
    /// spending. The next sync replaces this wholesale, with no merge.
    public sealed class PlayerSnapshot
    {
        public string PlayerId;
        public int ServerId;
        public IReadOnlyDictionary<string, int> Balances;
        public int HighestWaveCleared;
        public string BundleVersion;
        public string MinimumClientVersion;
    }
}
