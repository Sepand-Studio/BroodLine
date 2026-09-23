using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Broodline.Api;
using Broodline.Model;

namespace Broodline.UI
{
    /// What the three wave screens share: where the fight is and how long the
    /// run is, in one place because three screens state it and the handoff
    /// states it the same way on each.
    ///
    /// TWELVE IS THE AUTHORED ARC AND NOT A DRAWING. `Wave Defense.dc.html:40`
    /// and `Wave Defeat.dc.html:40` both write "/ 12" and "wave 7 of 12", and
    /// `specs/broodline_waves_01_12.md` is the document that authors them -
    /// its section 4 is titled "What the twelve waves do together". So the
    /// number is a design fact with a source, which is what
    /// `DeployView.uss`'s note (":58 - This project does not draw '/ 12'
    /// (nothing in `config.waves` promises twelve)") was missing when it
    /// declined to draw it. THE DEPLOY SCREEN IS NOT CHANGED HERE: it is Task
    /// 17's capture and its title line is a two-Label row this constant cannot
    /// reach without rebuilding it. Recorded as owed.
    ///
    /// NOT READ FROM `WaveDef`. `WaveDef.ForId` knows four ids (1, 2, 6, 7) -
    /// it is the ENGINE's authored set, not the campaign's length - and it
    /// lives in an assembly `Broodline.UI` deliberately cannot reference
    /// (`WaveDefeatView`'s class comment has why that constraint has teeth).
    public static class WaveCampaign
    {
        public const int Waves = 12;

        /// The place, uppercase, because USS has no `text-transform` and the
        /// user ruled the handoff's uppercase kickers are baked into the named
        /// constants only. Spelled rather than shared with
        /// `DeployScreen.Eyebrow` ("HOLLOW REACH · DEFENSE") because that one
        /// is a whole kicker and this is half of a different one; the two
        /// agreeing is pinned by `WaveScreens_TheRegionAgreesWithTheDeployKicker`
        /// rather than by a reference, so the day the region is not Hollow
        /// Reach the test names both call sites.
        ///
        /// THAT TEST WAS WRITTEN AFTER THIS COMMENT CLAIMED IT. Fix round 1's
        /// re-review grepped for it and found nothing pinning either of this
        /// file's two "pinned by a test" claims. A comment asserting a gate
        /// that does not exist is worse than no comment, because the next
        /// reader stops looking.
        public const string Region = "HOLLOW REACH";

        /// `Wave Defeat.dc.html:40` - "Hollow Reach · wave 7 of 12", the
        /// kicker BOTH verdict screens carry. Post-Wave has no handoff screen
        /// of its own and takes the defeat screen's, because the two are the
        /// same beat on opposite arms.
        public static string Eyebrow(int waveId)
        {
            return Region + " · WAVE " + waveId.ToString(CultureInfo.InvariantCulture) +
                   " OF " + Waves.ToString(CultureInfo.InvariantCulture);
        }
    }

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
        /// `Wave Defeat.dc.html:41` - the two-word verdict at the top of the
        /// band, in the display face at 34px.
        ///
        /// IT IS NOT `Headline` AND DOES NOT REPLACE IT. The handoff draws
        /// BOTH: a verdict that never changes (`:41`) and a sentence under it
        /// that says what happened (`:42`). `Headline` is this project's
        /// version of the second - bible 4.11's "names the raider that broke
        /// through and the trait that would have answered it" - and it is the
        /// sentence this screen exists for, so it keeps its element, its name
        /// and its tests. What was missing was the first: the screen opened
        /// with a raider's name and never said the Ark was breached.
        public const string Verdict = "Ark breached";

        /// The band's three cells - `Wave Defeat.dc.html:45,49,53`.
        ///
        /// UPPERCASE, which is the handoff's `.lbl` (`:15`,
        /// `text-transform: uppercase`) and the addendum's rule for every
        /// named constant a `t-micro` label reads. `StatCell`'s label IS
        /// `t-micro`, so this is the same class the deploy screen's
        /// `FOES`/`DEPLOYED`/`REWARD` already ship in; Task 17's report §5
        /// flagged that the older screens had not followed, and these two
        /// screens are the ones this task owns.
        public const string LeakedStatLabel = "LEAKED";
        public const string IntegrityStatLabel = "INTEGRITY";
        public const string KeptStatLabel = "KEPT";

        /// How many raiders reached the Ark. The breach LIST's length, not a
        /// separate count, so it cannot disagree with the breach the headline
        /// names.
        public static string LeakedStatValue(IReadOnlyList<BreachSummary> breaches)
        {
            return (breaches == null ? 0 : breaches.Count).ToString(CultureInfo.InvariantCulture);
        }

        /// `Wave Defeat.dc.html:50` draws "0%" and this draws "0".
        ///
        /// NO PER CENT SIGN, AND THAT IS THE MODEL DISAGREEING WITH THE
        /// DRAWING. combat_engine section 8 makes integrity a POOL and
        /// `HudSnapshot.Integrity`'s own comment says so ("a pool, not a life
        /// count") - wave 6 is authored at integrity 2, so "0%" would be a
        /// percentage of two. `PostWaveScreen.IntegrityStatValue` has printed
        /// the bare count since it was written and these two screens state the
        /// same number about the same wave.
        public static string IntegrityStatValue(int remaining)
        {
            return remaining.ToString(CultureInfo.InvariantCulture);
        }

        /// How many creatures came home, which on a defeat is ALL OF THEM.
        ///
        /// THE BRIEF ASKS FOR "the response's breach count against the
        /// deployment count" AND THAT ARITHMETIC IS NOT AVAILABLE AND WOULD BE
        /// WRONG IF IT WERE. A breach is a RAIDER that reached the Ark; it is
        /// not a creature that died, and nothing the client holds records a
        /// creature dying - `WaveReport` carries Result, Ticks,
        /// IntegrityRemaining, ReplayBytes and Breaches
        /// (`Model/WaveReport.cs:48-75`) and `WaveSubmitResponse` carries
        /// Result, IntegrityRemaining, Breaches, Reward and Granted. Subtracting
        /// one from the other would put a fabricated casualty count on the one
        /// screen the bible cares most about not lying on.
        ///
        /// It is also the WRONG LESSON. `Wave Defeat.dc.html:42` is explicit
        /// about what this screen has to say - "your hybrids and their lineage
        /// are intact" - and bible 4.11's free retry depends on it. So the cell
        /// states the size of the deployment that went in, and the caller
        /// passes it because only the caller knows it.
        ///
        /// `int?` FOR `DeployScreen.FoesStatValue`'s REASON: a caller that does
        /// not know the deployment must not be made to say nothing survived.
        public static string KeptStatValue(int? deployed)
        {
            return deployed == null
                ? string.Empty
                : deployed.Value.ToString(CultureInfo.InvariantCulture);
        }

        /// The card under the band - `Wave Defeat.dc.html:62`, whose own
        /// heading is "Defender performance" over a per-creature damage chart
        /// this project's report carries nothing for. What replaces it is the
        /// handoff's OWN teaching row two lines further down (`:88-93`): an
        /// answer named, in a tinted row, under the verdict.
        public const string DiagnosisHeading = "WHAT WOULD HAVE ANSWERED IT";

        /// The trait the chip beside the diagnosis names, or empty when the
        /// bundle names no answer for the raider.
        ///
        /// SAME CONTENT-BUNDLE VALUE THE SENTENCE READS, through the same
        /// field, so a chip and a sentence cannot name different traits. See
        /// `BreachSummary.Counter`: it is the bundle's answer and not
        /// `Stats.CounterFor`'s.
        public static string Counter(BreachSummary breach)
        {
            return breach == null ? string.Empty : breach.Counter ?? string.Empty;
        }

        /// The granted creature that carries `counter`, or null.
        ///
        /// bible 9.3: "The counter trait is available immediately: a campaign
        /// reward, a species in the next drop, something the player can act on
        /// within minutes", and waves_01_12 section 3 makes wave 6's drop the
        /// Pale that carries Chill. So the resupply and the answer are the
        /// same fact stated twice, and matching them is a LOOKUP rather than
        /// an inference - the chip then carries the tier and the tint of a
        /// creature the player actually owns.
        ///
        /// THE FIRST MATCH, and ordinal case-sensitive. Trait names come off
        /// the same content bundle on both sides - `BreachSummary.Counter` is
        /// `PlayerSnapshot.Traits[].Counters` and `CreatureDto.Trait1/2` is
        /// the same table's spelling - so a case-insensitive compare would be
        /// papering over a bundle that disagreed with itself.
        public static CreatureDto CarrierOf(IReadOnlyList<CreatureDto> granted, string counter)
        {
            if (granted == null || string.IsNullOrEmpty(counter)) return null;
            for (var i = 0; i < granted.Count; i++)
            {
                var c = granted[i];
                if (c == null) continue;
                if (c.Trait1 == counter || c.Trait2 == counter) return c;
            }
            return null;
        }

        /// That carrier's tier in `counter`, or null when there is no carrier.
        ///
        /// NULL IS NOT ZERO, which data_model 2 is explicit about - "zero
        /// would sort and display as 'less than tier I'" - and it is also what
        /// `TraitChip` reads as "no tier": the chip then renders the bare
        /// trait name. See `WaveDefeatView.Bind` for what that costs.
        public static int? TierOf(CreatureDto carrier, string counter)
        {
            if (carrier == null || string.IsNullOrEmpty(counter)) return null;
            if (carrier.Trait1 == counter) return carrier.Tier1;
            if (carrier.Trait2 == counter) return carrier.Tier2;
            return null;
        }

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
        /// AND AS OF PHASE 9 TASK 18 IT IS NOT DRAWN, because the handoff's
        /// own screen has no page header at all: `Wave Defeat.dc.html:31` is
        /// the hero band, and the element above it (`:26`) is the status bar.
        /// `ScreenScaffold(title: null)` hides the whole header row, which is
        /// the call `SpliceRevealView` already made on the same evidence in
        /// Task 16b. The constant stays because it is this screen's NAME - it
        /// is the handoff README's own (section 11) and `ScreenFixtures` and
        /// the inventory both use it - and because the day a caller pushes
        /// this screen onto a back stack it needs a header again.
        public const string Title = "Wave Defeat";

        /// The kicker, now inside the band rather than in the scaffold's
        /// header, because the header is gone. `Wave Defeat.dc.html:40` draws
        /// it there - first line inside the coral panel, above the verdict.
        public static string Eyebrow(int waveId) => WaveCampaign.Eyebrow(waveId);

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

        /// TWO WORDS, NOT A SENTENCE, AS OF PHASE 9 TASK 18. It was "Wave
        /// cleared." / "Wave not cleared." - a full sentence in the 28px hero
        /// face, where the screen on the other arm of the same branch says
        /// "Ark breached" in two. `Wave Defeat.dc.html:41` is the shape both
        /// halves of this beat want: a verdict at hero size, and the sentence
        /// that explains it underneath at reading size. Post-Wave has no
        /// handoff screen of its own (see the note where this screen's title
        /// used to be), so it takes the defeat screen's, which is what makes
        /// the pair read as one beat rather than two designs.
        ///
        /// NO FULL STOP, for the same reason: a headline is not a sentence,
        /// and the one across the branch has none.
        ///
        /// THE TOGGLE IS UNCHANGED AND IT IS THE POINT. The verdict is the
        /// SERVER's; a non-win response reaching this screen is a documented
        /// path and it must say something true rather than congratulate.
        public static string Headline(string result)
        {
            if (string.IsNullOrEmpty(result)) return string.Empty;
            return result == WinResult ? "Wave held" : "Wave not held";
        }

        /// The kicker. IN THE CONTENT COLUMN AS OF PHASE 9 TASK 21e, and not
        /// in the scaffold's eyebrow slot: that slot is inside `#header`,
        /// which this screen no longer draws. The note where this screen's
        /// title used to be has why, and `PostWaveView.uxml`'s `#kicker` is
        /// where it went. Wave Defeat made the same move in Task 18 and put
        /// its kicker in the hero band; this screen has no band, so it goes at
        /// the top of the column the way `SpliceRevealView`'s does.
        public static string Eyebrow(int waveId) => WaveCampaign.Eyebrow(waveId);

        public const string NextLabel = "Continue";

        /// THERE IS NO TITLE AND THERE IS NO PAGE HEADER - PHASE 9 TASK 21e.
        ///
        /// THIS CONST WAS `"Post-Wave"` AND A PLAYER READ IT AS ONE. Design
        /// section 5.1's SECTION NAME was rendered as the screen's page title
        /// because there was nothing else to render: this is the one wave
        /// screen the handoff does not draw - it ends at Wave Defense and
        /// Wave Defeat, and Task 18 confirmed `Wave Defense.dc.html` has no
        /// `won` phase at all - so there was no heading to take and the
        /// previous ruling here was `DeployScreen.Title`'s ("use the word the
        /// design already uses rather than invent copy"). That rule is right
        /// for "Deploy", which is a word a player uses. "Post-Wave" is a word
        /// a SPEC uses, and the exit gate's walk of the packaged app named it
        /// as a defect in those terms. It is the same shape as the HUD's
        /// "2 tick 155" and the granted card's `"None"` chips: an internal
        /// value reaching the player unedited.
        ///
        /// THE ANSWER IS NO TITLE, NOT A BETTER ONE, AND THE SCREEN ALREADY
        /// PROVED IT. `Headline` is directly underneath and states the
        /// verdict in the player's own words ("Wave held"), in --green-text,
        /// at 28px; a title above it either repeats that or says nothing. The
        /// old note here spent its length arguing that "Wave cleared" must
        /// not go in the header because the header is a CONSTANT and the
        /// verdict is the SERVER's - which is the same argument one step
        /// short of its conclusion. A header whose only possible honest
        /// content is already on the screen is a header the screen does not
        /// want.
        ///
        /// AND THE OTHER ARM OF THIS BRANCH IS ALREADY HEADERLESS.
        /// `WaveDefeatView` passes `title: null` (Task 18, `Wave Defeat
        /// .dc.html:26-31`: status bar, then hero band, no header row) and
        /// `KeptStatLabel`'s note already states the principle these two
        /// screens are held to - "the same fact stated on both arms of one
        /// branch". A win that carries a page header and a loss that does not
        /// is that principle broken in chrome.
        ///
        /// WHAT THE HEADER TOOK WITH IT, AND WHERE EACH WENT. The row also
        /// holds the eyebrow, the back chevron and the resource pill
        /// (`ScreenScaffold`'s own note). The eyebrow is now `#kicker` in the
        /// content column, the way `SpliceRevealView`'s went in Task 16b. The
        /// chevron was never drawn - `PostWaveView.Bind`'s `onBack` is null
        /// at every call site and `ScreenScaffold.OnBack` removes the button
        /// when it is. No caller has ever set a resource pill on this screen.
        ///
        /// THE CONST IS DELETED RATHER THAN KEPT, WHICH IS THE OPPOSITE OF
        /// WHAT THE OTHER TWO HEADERLESS SCREENS DID AND IS DELIBERATE.
        /// `SpliceRevealScreen.Title` ("Splice Reveal") and
        /// `WaveDefeatScreen.Title` ("Wave Defeat") both survive their own
        /// screens going headerless, with notes saying why: each is the
        /// handoff's own heading for a screen it actually draws, so each is a
        /// real piece of copy that a later header could take back. "Post-Wave"
        /// is not copy and never was. Leaving it here as a dead literal is
        /// leaving the defect where the next task can reach it.

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
        /// UPPERCASE AS OF PHASE 9 TASK 18, and a third cell beside them.
        /// `StatCell`'s label carries `t-micro`, which IS this project's
        /// version of the handoff's `.lbl` (9px, `.1em`, uppercase) - Task
        /// 17's report §5 named these two as the screens still shipping
        /// sentence case under that class. The deploy screen's
        /// `FOES`/`DEPLOYED`/`REWARD` and the defeat screen's three are now
        /// the same shape as these.
        public const string RewardStatLabel = "REWARD";
        public const string IntegrityStatLabel = "INTEGRITY LEFT";

        /// The third cell, mirroring `WaveDefeatScreen.KeptStatLabel` - the
        /// same fact stated on both arms of the branch. Its comment has why
        /// it is the deployment's size and not a survival count.
        public const string KeptStatLabel = "KEPT";

        public static string KeptStatValue(int? deployed)
        {
            return WaveDefeatScreen.KeptStatValue(deployed);
        }

        /// InvariantCulture for the reason every other number in this
        /// assembly takes it - `DeployScreen.WaveStatValue`'s comment has it:
        /// a device locale that groups digits must not make one screen's
        /// number read differently from another's.
        public static string IntegrityStatValue(int remaining)
        {
            return remaining.ToString(CultureInfo.InvariantCulture);
        }
    }

    /// The live HUD's text. Everything else it draws is a bar.
    public static class WaveHudScreen
    {
        /// THE SAME KICKER THE DEPLOY SCREEN CARRIES, BY REFERENCE. They are
        /// two phases of ONE handoff screen - `Wave Defense.dc.html:39`, which
        /// Task 17 rendered in `phase: 'placing'` and this renders in
        /// `running` - so a second literal here would be the same sentence
        /// written twice with nothing holding the two together.
        public const string Eyebrow = DeployScreen.Eyebrow;

        /// "Wave", in the display face, with the numerals beside it in
        /// `WaveOf`. `DeployScreen.Title`'s comment has why the word and the
        /// number are two Labels: bible 10.6 keeps numerals out of the display
        /// face and puts `t-num` on every decision-bearing one, so "Wave 6"
        /// cannot be one run of text.
        ///
        /// SPELLED, NOT ALIASED, AND THE RULE THIS FILE FOLLOWS IS WORTH
        /// STATING ONCE - fix round 1 found two of them forty lines apart.
        /// `Eyebrow` and `IntegrityLabel` ARE `DeployScreen`'s by reference,
        /// because each is literally the same handoff ELEMENT in the screen's
        /// other phase (`:39` and `:63`) and a second literal would be one
        /// sentence written twice. This is not: `DeployScreen.Title` is a PAGE
        /// HEADER - the word in the scaffold's chrome, which
        /// `FounderNamingScreen.Title`'s rule says names the screen - and this
        /// is a display word inside a band. Binding them would mean renaming
        /// the deploy screen's header silently renames the HUD's word. They
        /// happen to agree today, AND NOTHING PINS THAT, DELIBERATELY - they
        /// are allowed to diverge and this constant is what lets them.
        /// `WaveHud_TheTopBandNamesTheWaveAndItsKicker` asserts the VIEW
        /// against this constant, not this constant against
        /// `DeployScreen.Title`; the earlier version of this comment claimed
        /// otherwise and was wrong.
        public const string WaveWord = "Wave";

        /// The two controls that arrived in Phase 10 Task 1.7, once a tap on
        /// them could be told from a Rally tap (`WaveHudView.PicksControlAt`).
        public const string PauseLabel = "Pause";
        public const string ResumeLabel = "Resume";
        public static string SpeedLabel(double scale) => scale >= 2 ? "2\u00d7" : "1\u00d7";

        /// `Wave Defense.dc.html:40` - "Wave {{ wave }} / 12", both numerals
        /// in one `t-num` Label because they are one reading ("six of twelve")
        /// and a span between them would be a third element for a space.
        ///
        /// THE DEPLOY SCREEN DECLINED THE "/ 12" AND THIS DRAWS IT.
        /// `DeployView.uss:58` says "nothing in `config.waves` promises
        /// twelve"; `WaveCampaign.Waves` has the source that does. Recorded
        /// rather than fixed on that screen - see that constant's note.
        public static string WaveOf(int waveId)
        {
            return waveId.ToString(CultureInfo.InvariantCulture) + " / " +
                   WaveCampaign.Waves.ToString(CultureInfo.InvariantCulture);
        }

        /// The readout's caption - `Wave Defense.dc.html:63`, the right-hand
        /// one of the handoff's two stat pills. By reference to the deploy
        /// screen's for `Eyebrow`'s reason: one handoff element, two phases.
        ///
        /// THE LEFT PILL - `Gene energy` (`:54`) - IS NOT DRAWN, and this is
        /// the brief's step 1.2 refuted by the data path rather than declined.
        /// `HudSnapshot` carries three fields (`Model/WaveReport.cs:146-158`:
        /// Integrity, Tick, Bodies) and `WaveRunner.Snapshot` fills them from
        /// `_runner.Integrity` and `_runner.Tick`; there is no energy in this
        /// game's combat loop at all - the deploy screen's own energy readout
        /// is a SHARD BALANCE the director hands it before the wave
        /// (`DeployScreen.EnergyValue`, "the player's spendable balance"), and
        /// nothing is spent during one. The brief's substitute - the wave's
        /// reward, as "+150 on a win" - is the server's number
        /// (`client_architecture` section 7 makes the client a cache and never
        /// a source of truth, and `PostWaveScreen.RewardLine` renders the
        /// server's reward on the screen after this one), and the runner has
        /// never been handed it.
        public const string IntegrityLabel = DeployScreen.IntegrityLabel;

        /// The integrity pool, and NOTHING ELSE ON THE LINE - Phase 9 Task
        /// 21e.
        ///
        /// THE TICK WAS ON THIS LINE AND A PLAYER READ IT AS A BUG. Phase 9
        /// Task 18 made this string the VALUE of a pill whose caption is
        /// `IntegrityLabel` ("ARK INTEGRITY") and left the tick appended to
        /// it, so the exit gate's walk of the packaged app read
        /// "2 tick 155" under "ARK INTEGRITY" - a frame counter presented as
        /// part of the number the loss condition is measured in. It is the
        /// same defect as `PostWaveScreen`'s old "Post-Wave" title and the
        /// `"None"` trait chip: an internal value reaching the player
        /// unedited.
        ///
        /// THE TICK IS NOT DELETED, IT IS MOVED, AND `Tick` BELOW SAYS WHY.
        /// Two mechanisms depend on a live tick being ON SCREEN, and neither
        /// depends on it being on THIS string.
        ///
        /// NO PER CENT SIGN, for `WaveDefeatScreen.IntegrityStatValue`'s
        /// reason: the handoff draws "82%" over a percentage and wave 6 is
        /// authored at integrity 2.
        public static string Integrity(HudSnapshot snapshot)
        {
            if (snapshot == null) return string.Empty;
            return snapshot.Integrity.ToString(CultureInfo.InvariantCulture);
        }

        /// The live tick, on a readout of its own.
        ///
        /// IT STAYS ON SCREEN BECAUSE TWO MECHANISMS READ IT OFF THE SCREEN,
        /// AND THIS IS WHY IT IS A SEPARATE ELEMENT RATHER THAN A DELETION:
        ///
        ///   - THE DEVICE RE-CAPTURE PROCEDURE AIMS A TAP BY IT.
        ///     `DeviceReplayTests.ReCaptureOwed` tells the capturer to aim at
        ///     ticks 184..207 and to expect about ten ticks of reaction lag,
        ///     which is unreadable without a live counter in front of them.
        ///     That message names the element this now lives on; the two were
        ///     changed together.
        ///   - `WaveCapturePlayTests` ASSERTS A HUD READOUT ADVANCES within a
        ///     frame budget, and that assertion has to watch a number that
        ///     MOVES. Integrity is a pool, not a life count
        ///     (`HudSnapshot.Integrity`), and wave 6 holds a constant 2 for
        ///     roughly 500 of its 540 ticks - so a watch on the integrity
        ///     line would fail on a TIMEOUT rather than on a wrong value.
        ///     That test now watches this element. PlayMode deadlocks in
        ///     batchmode here, so it was changed in the same commit and the
        ///     report names it as owed to the developer's Editor pass.
        ///
        /// SO IT READS AS AN INSTRUMENT AND NOT AS A STAT. The word leads,
        /// lower case, in --mute at --text-secondary under the pills rather
        /// than inside one - see `.wave-hud-view__tick`. A player who does
        /// not know what a tick is reads a small grey diagnostic; a capturer
        /// who needs one reads it at a glance.
        ///
        /// InvariantCulture for `DeployScreen.WaveStatValue`'s reason: a
        /// locale that groups digits must not make the capturer's number read
        /// differently from the engine's.
        public static string Tick(HudSnapshot snapshot)
        {
            if (snapshot == null) return string.Empty;
            return "tick " + snapshot.Tick.ToString(CultureInfo.InvariantCulture);
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
