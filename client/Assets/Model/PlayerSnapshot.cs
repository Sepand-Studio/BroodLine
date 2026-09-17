using System.Collections.Generic;

namespace Broodline.Model
{
    /// One entry of `config.waves` from `/v1/sync` - just enough to label a
    /// wave's reward. Balances and this table share the same rule from the
    /// class comment below: a display value stays a display value.
    public sealed class WaveSummary
    {
        public int Id;
        public string RewardCurrency;
        public int RewardAmount;
    }

    /// One entry of `config.traits` from `/v1/sync`. `client_architecture`
    /// section 9's closing rule for the probability table applies here too:
    /// this renders published numbers, it never computes them.
    public sealed class TraitSummary
    {
        public string Id;
        public string Species;
        public string Counters;
    }

    /// `ftue` from `/v1/sync` - three facts about onboarding progress, not a
    /// state machine. What to do with them is Task 17's.
    public sealed class FtueFacts
    {
        public bool FounderNamed;
        public bool TutorialStockGranted;
        public int Splices;
    }

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

        /// The tab bar's reveal thresholds - `config.tabs`, verbatim.
        /// `Progression.TabsFor` is the pure function that turns this plus
        /// `HighestWaveCleared` into the five-tab order (client_architecture
        /// section 9: "a pure function of campaign progress ... stores
        /// nothing"); this snapshot only carries the numbers it needs.
        public IReadOnlyDictionary<string, int> Tabs;

        public IReadOnlyList<WaveSummary> Waves;
        public IReadOnlyList<TraitSummary> Traits;
        public FtueFacts Ftue;
    }
}
