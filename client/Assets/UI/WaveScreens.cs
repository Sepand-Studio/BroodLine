using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Broodline.Api;
using Broodline.Model;

namespace Broodline.UI
{
    /// The wave screens' player-facing copy, authored OUTSIDE the views.
    ///
    /// Same convention as `SpliceScreen.Cta`, `DeployScreen.Cta` and
    /// `RegionScreen.UnlimitedLabel`, and for the reason the Task 15 review
    /// found the hard way: three strings lived in `*View.cs` and walked past
    /// a 382-line suite, because a test that asserts a view renders "Start"
    /// cannot tell a view reading its model from a view holding a literal.
    /// Every sentence below has a test that compares the view's text to THIS
    /// function's output, so moving a string back into a view fails.
    ///
    /// These are static rather than `*ScreenModel` classes because neither
    /// screen has state to model: Post-Wave renders one server response and
    /// Wave Defeat renders one `WaveReport`. There is nothing to derive, so
    /// there is nothing to hold.
    public static class WaveDefeatScreen
    {
        /// bible 4.11 and 9.3, which say the same thing twice: "Wave Defeat
        /// names the raider that broke through and the trait that would have
        /// answered it." Both halves, in one sentence, because this screen
        /// exists for that sentence.
        ///
        /// The trait half is DROPPED rather than faked when the bundle names
        /// no answer for the raider. Writing "and nothing would have answered
        /// it" would teach a rule the closed counter pool (bible 4.12: "a new
        /// raider must be answerable by an existing counter") says is false;
        /// naming the raider alone is merely incomplete.
        public static string Headline(BreachSummary breach)
        {
            if (breach == null) return string.Empty;
            var raider = breach.RaiderType ?? string.Empty;
            if (string.IsNullOrEmpty(breach.Counter))
                return raider + " broke through.";
            return raider + " broke through. " + breach.Counter + " would have answered it.";
        }

        /// combat_engine section 7's three booleans as the three messages
        /// bible 4.11 asks for: absent, present at insufficient coverage, or
        /// misplaced. Evaluated in the SAME order the engine evaluated them,
        /// because the first false is the diagnosis and the later fields are
        /// not claimed to be meaningful once one has gone false.
        public static string Diagnosis(BreachSummary breach)
        {
            if (breach == null) return string.Empty;
            var raider = breach.RaiderType ?? string.Empty;
            var counter = breach.Counter;
            if (string.IsNullOrEmpty(counter)) return string.Empty;

            if (!breach.Access)
                return "No creature you deployed carried " + counter + ".";
            if (!breach.Coverage)
                return counter + " was there, but not enough of it for every " + raider + " on the field at once.";
            if (!breach.Placement)
                return counter + " was there, but none of its carriers could reach where " + raider + " broke through.";

            // Answered on all three and it still got past. Nothing about the
            // roster explains this one, so the screen says so rather than
            // picking a boolean to blame.
            return counter + " answered " + raider + ", and it still reached the Ark.";
        }

        /// bible 9.3: "The counter trait is available immediately: a campaign
        /// reward, a species in the next drop, something the player can act on
        /// within minutes." waves_01_12 section 3 makes wave 6's that Pale.
        /// The screen names what arrived; an empty grant says nothing rather
        /// than promising a resupply that is not there.
        public static string Resupply(IReadOnlyList<CreatureDto> granted)
        {
            if (granted == null || granted.Count == 0) return string.Empty;

            var names = new StringBuilder();
            for (var i = 0; i < granted.Count; i++)
            {
                if (i > 0) names.Append(i == granted.Count - 1 ? " and " : ", ");
                names.Append(CreatureLabel.DisplayName(granted[i]));
            }
            // "joined" reads correctly for one name and for several, so the
            // sentence needs no plural branch - and a branch that produced
            // the same string either way would be a test that cannot fail.
            return names + " joined your roster.";
        }

        /// bible 4.11: "then offers a free retry ... there is no paywall on
        /// failure." The label says free because the absence of a cost is the
        /// thing being taught.
        public const string RetryLabel = "Retry — free";
    }

    /// Post-Wave: design section 5.1 beat 3, "the creature drop".
    public static class PostWaveScreen
    {
        /// The reward as the server sent it. Currency and amount both come
        /// off the wire - `client_architecture` section 7 makes a cached
        /// balance a label, and the same rule binds a reward: this renders
        /// the server's number and computes none.
        /// Named `RewardLine` rather than `Reward`: a static method called
        /// `Reward` inside this class shadows the generated `Reward` TYPE at
        /// every call site in it, and `PostWaveScreen.Reward(response.Reward)`
        /// then does not compile (CS0118).
        public static string RewardLine(Reward reward)
        {
            if (reward == null) return string.Empty;
            return reward.Amount.ToString(CultureInfo.InvariantCulture) + " " + (reward.Currency ?? string.Empty);
        }

        /// The SERVER's verdict, never a re-derivation from integrity or
        /// breach count - `client_architecture` section 7 makes the client a
        /// cache and never a source of truth, and the verdict is the most
        /// load-bearing thing on this screen.
        ///
        /// Post-Wave is the WIN surface; a loss routes to Wave Defeat, which
        /// design section 5.1 lists as a separate screen. The other arm
        /// exists so a non-win response that reaches here anyway says
        /// something true rather than congratulating the player.
        public const string WinResult = "Win";

        public static string Headline(string result)
        {
            if (string.IsNullOrEmpty(result)) return string.Empty;
            return result == WinResult ? "Wave cleared." : "Wave not cleared.";
        }

        public const string NextLabel = "Continue";
    }

    /// The live HUD's two lines of text. Everything else it draws is a bar.
    public static class WaveHudScreen
    {
        /// What `WaveHud.DrawIntegrity` drew, minus the rich-text markup:
        /// the loss condition and the live tick. The tick is here because the
        /// tracked-capture procedure reads it off the screen - DeviceReplay
        /// Tests' re-capture message says so in as many words: "The HUD
        /// prints the live tick beside Integrity."
        public static string Integrity(HudSnapshot snapshot)
        {
            if (snapshot == null) return string.Empty;
            return "Integrity " + snapshot.Integrity.ToString(CultureInfo.InvariantCulture) +
                   "   tick " + snapshot.Tick.ToString(CultureInfo.InvariantCulture);
        }

        /// The tag hanging under a bar, or null for a body with nothing to
        /// say. Rally's countdown is the player's only feedback that their
        /// one input was taken - combat_engine section 8 makes Rally the only
        /// input during a wave, and `WaveRunner`'s own comment names the
        /// failure this prevents: "a tap with no visible consequence is
        /// indistinguishable from a tap that was dropped."
        public static string BarTag(BodyBar bar)
        {
            switch (bar.State)
            {
                case BodyState.Breaching: return "BREACH";
                case BodyState.Chilled: return "CHILLED";
                case BodyState.Rallied: return "RALLY " + bar.RallyRemaining.ToString(CultureInfo.InvariantCulture);
                default: return null;
            }
        }
    }
}
