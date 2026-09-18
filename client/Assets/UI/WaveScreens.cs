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
        ///
        /// NOT "Try again", WHICH IS WHAT PHASE 8 TASK 11 STEP 3 ASKS FOR.
        /// The whole load of this button is in the second word: bible 4.11
        /// makes the ABSENCE OF A COST the lesson, and a label that says only
        /// "Try again" teaches the retry while withholding the thing the
        /// retry is teaching. The task is a look-and-ship pass over the
        /// frame; it does not get to retire a sentence the bible authored.
        public const string RetryLabel = "Retry — free";

        /// The scaffold's header - the handoff's own name for this screen
        /// (README section 11, "Wave Defeat"), which its push table reaches
        /// from Wave Defense "(on loss)". So this screen is `pushed: true`.
        ///
        /// NOT "Wave lost", WHICH IS WHAT TASK 11 STEP 3 ASKS FOR, and the
        /// rule that settles it was already settled twice:
        /// `FounderNamingScreen.Title`'s comment, quoted again by
        /// `SpliceRevealScreen.Title`, says the header NAMES THE SCREEN and
        /// never changes while the headline says which of two things just
        /// happened. `Headline` is already the sentence that states the loss,
        /// and it states it with the raider's name in it. A header repeating
        /// the verdict in three flatter words is the payoff said twice.
        public const string Title = "Wave Defeat";

        /// The secondary CTA, offered only to a caller that has somewhere for
        /// it to go - `WaveDefeatView.Bind`'s `roster` argument.
        ///
        /// `screen_inventory_v2` section 10 pairs the free retry with a way
        /// back to the roster, because the OTHER answer to a defeat is to
        /// change the deployment rather than to re-run it. It is the
        /// handoff's own name for that destination (README section 6,
        /// "Creature Roster") shortened to the one word a secondary CTA has
        /// room for, and `RosterScreen.Title` still owns the header there.
        public const string RosterLabel = "Roster";
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

        /// The scaffold's header.
        ///
        /// NOT "Wave cleared", WHICH IS WHAT TASK 11 STEP 2 ASKS FOR, and
        /// this one is not a matter of taste: "Wave cleared" is
        /// `Headline(WinResult)` MINUS ITS FULL STOP. Putting it in the
        /// header would print the same three words twice on one screen, and
        /// - the half that actually matters - it would assert the verdict as
        /// a CONSTANT. The header is set at construction and the verdict
        /// arrives at `Bind`, from the server; `Headline`'s own comment and
        /// `PostWave_TheHeadlineFollowsTheServersVerdictAndNotTheClients`
        /// both exist because a non-win response can reach this screen, and a
        /// header reading "Wave cleared" over a headline reading "Wave not
        /// cleared." is the client contradicting the server in its own
        /// chrome.
        ///
        /// SO IT NAMES THE SCREEN, per `FounderNamingScreen.Title`'s rule,
        /// and the name is design section 5.1's own - this is the one wave
        /// screen the handoff does not draw (it ends at Wave Defense and Wave
        /// Defeat), so there is no section heading to take, and
        /// `DeployScreen.Title` already settled what to do then: use the word
        /// the design already uses rather than invent copy for a header.
        public const string Title = "Post-Wave";

        /// The two facts the stat row states.
        ///
        /// INTEGRITY WAS ON THIS SCREEN'S `Bind` SINCE IT WAS WRITTEN AND ON
        /// NO SCREEN AT ALL. `WaveSubmitResponse.IntegrityRemaining` is
        /// `Required.Always` on the wire, it is the server's own count of
        /// what survived, and `PostWaveView` took the whole response and
        /// printed the reward only - so a player who cleared a wave at 1
        /// integrity and one who cleared it untouched read the identical
        /// screen. combat_engine section 8 makes integrity the loss
        /// condition, which makes "how much of it is left" the one number on
        /// this screen a next decision depends on. A one-cell stat row is
        /// also a contradiction in terms.
        public const string RewardStatLabel = "Reward";
        public const string IntegrityStatLabel = "Integrity left";

        /// InvariantCulture for the reason every other number in this
        /// assembly takes it - `DeployScreen.WaveStatValue`'s comment has it:
        /// a device locale that groups digits must not make one screen's
        /// number read differently from another's.
        public static string IntegrityStatValue(int remaining)
        {
            return remaining.ToString(CultureInfo.InvariantCulture);
        }
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
