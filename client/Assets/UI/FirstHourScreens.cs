using System;
using System.Collections.Generic;
using System.Globalization;
using Broodline.Api;
using Broodline.Model;

namespace Broodline.UI
{
    /// The first hour's player-facing copy and its two derivations, authored
    /// OUTSIDE the views - the convention `WaveScreens.cs` states in full and
    /// the Task 15 review paid for: "a test that asserts a view renders
    /// 'Start' cannot tell a view reading its model from a view holding a
    /// literal." Every sentence below has a test comparing a view's text to
    /// THIS function's output, so moving a string back into a view fails.

    /// Beat 4 - bible 3.3's "optional naming prompt and a sensible default".
    public static class FounderNamingScreen
    {
        /// The scaffold's header, which is a different sentence from
        /// `Prompt`: the header names the SCREEN and never changes, while the
        /// prompt addresses THIS creature and is rebuilt on every Bind. One
        /// string doing both jobs would either put a creature's name in a
        /// header that outlives it or flatten the prompt into a label.
        public const string Title = "Name your Founder";

        /// The handoff's kicker, over the title. `Onboarding.dc.html` step 1
        /// titles itself "This is your Gene Ark"; the screen's own title is
        /// the instruction, so the Ark is what the eyebrow says instead -
        /// where in the app the player is standing.
        ///
        /// UPPERCASE IN THE CONSTANT, WHICH IS NOT A STYLE CHOICE MADE HERE.
        /// The handoff renders every eyebrow through `.lbl { text-transform:
        /// uppercase }` (`Onboarding.dc.html:15`), and UI Toolkit has no
        /// `text-transform` at all - Task 13 established this and
        /// `CostCtaRow.CostEyebrow` is the same constant for the same reason.
        /// So the casing is baked into the string, in ONE place, and
        /// everything that renders or asserts it reads this rather than
        /// repeating the literal.
        public const string Eyebrow = "YOUR GENE ARK";

        /// The step counter beside the progress pips. Onboarding is five
        /// steps and the Ark is the first of them, which is the handoff's
        /// own `stepLabel` for that screen.
        ///
        /// A NUMBER, SO IT RENDERS ON `.t-num`. bible 10.6's tabular face
        /// and 11px floor apply to any figure a decision depends on, and
        /// "how much of this is left" is one.
        public const string Step = "1 / 5";

        /// The violet tip note under the name field. bible 3.3 makes the
        /// prompt optional and the Roster rename the fallback, so this is the
        /// caveat that makes `SkipLabel` a real answer rather than a discard:
        /// the name is permanent for the Founder, which is why the screen
        /// exists at all.
        ///
        /// IT WAS THE SCAFFOLD'S FOOTER NOTE UNTIL PHASE 9 TASK 14. The
        /// handoff's step 1 puts its note INSIDE the card, on a violet panel
        /// behind a glyph, rather than as grey type under the CTA - and the
        /// sentence is the same sentence, so it moved rather than being
        /// replaced. Renamed with it: a constant called `FooterNote` that no
        /// longer reaches `ScreenScaffold.FooterNote` is a name that lies.
        public const string Note = "Founders keep their names for life.";

        /// bible 3.3's "sensible default": the species. It is the only name
        /// the client has that is about THIS creature, and a default the
        /// player keeps is still a name they chose to keep.
        ///
        /// ON THE MODEL, not on the view, though task-17-brief.md writes it
        /// `FounderNamingView.DefaultFor`. Same rule as every other string
        /// here; a default that lives in the view is a default no test can
        /// tell from a hardcoded one.
        public static string DefaultFor(CreatureDto creature)
        {
            return creature == null ? string.Empty : creature.Species ?? string.Empty;
        }

        /// `DisplayName` rather than `Species` so a RE-prompt (the beat is
        /// offered once per session start while wave 2 is uncleared, and a
        /// player can name, background the app and come back) addresses the
        /// creature by the name they just gave it.
        public static string Prompt(CreatureDto creature)
        {
            if (creature == null) return string.Empty;
            return CreatureLabel.DisplayName(creature) + " is yours. Give it a name.";
        }

        public const string ConfirmLabel = "Name it";

        /// bible 3.3: "Founders 2-5 arrive ... each with an optional naming
        /// prompt", and all five "are renameable at any time from the
        /// Roster". Skipping is a real answer, so it says where the name can
        /// still be given rather than reading as a discard.
        public const string SkipLabel = "Not now";

        /// Shown instead of submitting an empty name. The server's
        /// `CreatureNameRequest.name` is required, so an empty submit is a
        /// round trip that can only be refused; this refuses it here and says
        /// which of the two buttons the player wanted.
        public const string EmptyNameBlocker =
            "A name cannot be blank. Tap " + SkipLabel + " to keep the default for now.";

        /// What actually gets sent: the field's text with surrounding
        /// whitespace removed. Empty (never null) when there is nothing to
        /// send - the caller checks length rather than nullness.
        public static string Sanitize(string raw)
        {
            return string.IsNullOrWhiteSpace(raw) ? string.Empty : raw.Trim();
        }
    }

    /// Beat 7 - `broodline_splice_confirm_spec` section 5.
    public static class SpliceRevealScreen
    {
        /// The scaffold's header - the handoff's own name for this screen
        /// (README section 7), which its push table reaches from the Splice
        /// Chamber "(after splice)". So this screen is `pushed: true`.
        ///
        /// A DIFFERENT SENTENCE FROM `Headline`, and the pair is
        /// `FounderNamingScreen.Title`/`Prompt` again: the header names the
        /// screen and never changes, the headline says which of two things
        /// just happened. One string doing both jobs would either put "A
        /// mutation fired." in a header that outlives the moment or flatten
        /// the payoff into a label.
        public const string Title = "Splice Reveal";

        /// What the parents row says when it was handed neither parent.
        ///
        /// THE ROW IS THE SCREEN'S WHOLE LESSON, so its empty case cannot be
        /// nothing. bible 2.1 makes "the parents are still drawn on the
        /// screen that says they are gone" the FTUE's teaching moment, and a
        /// caller that passed nulls would otherwise produce a reveal that
        /// silently taught the opposite - the parents simply absent, with no
        /// sentence saying whether they were consumed or never arrived.
        public const string NoParentsMessage =
            "The parents for this splice were not loaded.";

        /// section 7: "No 'SPLICE FAILED' state ... there is no failure
        /// outcome - the worst result is rolling traits the player already
        /// had." So the non-mutated arm states what happened and does not
        /// apologise for it.
        public static string Headline(bool mutated)
        {
            return mutated ? "A mutation fired." : "The splice took.";
        }

        /// section 5: Splice Reveal "keeps its existing line - that parents
        /// are consumed and their traits live on in the pedigree - but it is
        /// now a confirmation of something already understood rather than a
        /// disclosure."
        ///
        /// PRESENT TENSE where `SpliceScreen.DestructionNoticeFor` is future
        /// ("will be permanently removed"), because that is the difference
        /// between a warning and a confirmation. The vocabulary is otherwise
        /// the same on purpose - splice_confirm_spec section 3: "three
        /// different phrasings of the same fact reads as evasion."
        ///
        /// EMPTY WHEN EITHER PARENT IS MISSING, rather than a sentence with a
        /// hole in it. `CreatureLabel.WithGeneration(null)` is the empty
        /// string, so the unguarded version wrote " and  are consumed. Their
        /// traits live on in the pedigree." - a claim about two creatures it
        /// cannot name, on the screen whose whole job is naming them.
        /// `NoParentsMessage` is what the reveal says in that case, and it
        /// says the honest thing: the parents were not loaded.
        public static string Consumption(CreatureDto parentA, CreatureDto parentB)
        {
            if (parentA == null || parentB == null) return string.Empty;
            return CreatureLabel.WithGeneration(parentA) + " and "
                + CreatureLabel.WithGeneration(parentB)
                + " are consumed. Their traits live on in the pedigree.";
        }

        /// The child, as this screen names it.
        public static string ChildLine(CreatureDto child)
        {
            if (child == null) return string.Empty;
            return CreatureLabel.WithGeneration(child) + " — "
                + CreatureLabel.TraitWithTier(child.Trait1, child.Tier1) + ", "
                + CreatureLabel.TraitWithTier(child.Trait2, child.Tier2);
        }

        /// splice_confirm_spec section 5: "Then show the lineage." The button
        /// says where it goes, because beat 8 is the point of the session.
        public const string NextLabel = "See the lineage";
    }

    /// Beat 8 - the two-generation tree that closes session one.
    public static class LineageScreen
    {
        /// bible 9.2: "A player closing the app after five minutes should be
        /// carrying one image: a family tree with a creature they named at
        /// the bottom of it."
        public const string Title = "Lineage";

        /// Shown when the server returned a tree with nothing in it. Not a
        /// blank screen: an empty tree after a splice would be the one
        /// failure this screen exists to make impossible to miss.
        ///
        /// IT GAINED A SECOND SENTENCE IN PHASE 8 TASK 9, and the sentence is
        /// the point. "No lineage yet." alone states the absence and stops;
        /// read before a first splice - which is when a player actually sees
        /// it - that is indistinguishable from a screen that failed to load.
        /// The second half says what fills it, which is the one thing an
        /// empty state owes the player.
        public const string EmptyNotice = "No lineage yet. Your first splice starts it.";

        /// A generation's card heading. Spelled out rather than
        /// `CreatureLabel.Generation`'s "G1" badge: the badge is a marker
        /// beside a creature's name, where the word has to carry a heading on
        /// its own.
        public static string GenerationHeading(int generation)
        {
            return "Generation " + generation.ToString(CultureInfo.InvariantCulture);
        }

        /// The two facts a generation's stat row states.
        ///
        /// NOT "COVERAGE", WHICH IS WHAT THE PHASE 8 PLAN ASKED FOR. Coverage
        /// is a trait-tier fact and `LineageResponse` carries no coverage of
        /// any kind - `splice_confirm_spec`'s `coverageLost` reaches
        /// `SpliceScreen` and never this route. What the tree does know about
        /// a generation is how many of it the record holds and how many are
        /// still alive, and `splice_confirm_spec` section 5 makes exactly
        /// that contrast the lesson: "individuals are consumed, the record
        /// survives."
        public const string RecordedStatLabel = "In the record";
        public const string LivingStatLabel = "Living";

        /// "Ash (G1)" for a lineage node. A separate function from
        /// `CreatureLabel.WithGeneration` because `LineageNode` is a
        /// different wire type from `CreatureDto` - it carries `consumedAt`
        /// and `mutated` and carries no `committedTo` - and the two are
        /// deliberately not unified behind an interface the generated client
        /// would lose on the next regeneration. The FORMAT is shared:
        /// `CreatureLabel.Generation` writes the badge for both.
        public static string NodeLabel(LineageNode node)
        {
            if (node == null) return string.Empty;
            var name = string.IsNullOrWhiteSpace(node.Name) ? node.Species : node.Name;
            return name + " (" + CreatureLabel.Generation(node.Generation) + ")";
        }

        /// Every generation present, ascending, with no gaps invented.
        ///
        /// ASCENDING PUTS THE FOUNDERS FIRST, which is the root of the tree -
        /// `screen_inventory_v2` 6 asks for "Founder roots", and generation 1
        /// is where they are. A descending order would read as a pedigree
        /// chart of a single creature rather than as a family growing.
        public static IReadOnlyList<int> Generations(ICollection<LineageNode> nodes)
        {
            var seen = new List<int>();
            if (nodes == null) return seen;
            foreach (var node in nodes)
            {
                if (node == null) continue;
                if (!seen.Contains(node.Generation)) seen.Add(node.Generation);
            }
            seen.Sort();
            return seen;
        }

        /// Where beat 8 lets go. bible 9.2 ends the session here, but
        /// `Ftue.Derive` answers `Lineage` on every launch until wave 6 is
        /// cleared, so the tree needs a way onward or a returning player is
        /// parked on it forever - see `LineageView.Bind`.
        public const string NextLabel = "Continue";

        /// splice_confirm_spec section 5 and design section 5: the consumed
        /// parents are STILL HERE, and the screen says so in words as well as
        /// in a class name. "Individuals are consumed; the record survives."
        public const string ConsumedLabel = "Consumed";

        /// THE OTHER TWO FACTS A NODE CARRIES, IN WORDS. Phase 8 Task 13.
        ///
        /// bible 10.4: "Colour never carries information alone." Until this
        /// commit `Consumed` was the only one of the three that obeyed it -
        /// Founder was an amber rail and Mutated a violet rail and NOTHING
        /// else, on a screen that draws no creature at all, so there was no
        /// silhouette to reinforce either. A node is two labels and a 3px
        /// coloured border; colour was the whole channel.
        ///
        /// That was a 10.4 violation on its own terms, before any species
        /// colour existed. It also happened to be the reason Task 6's
        /// deferred palette collision mattered: amber IS Skitter and violet
        /// IS Hollow at dE 0.0, so the tree's rails read as species marks to
        /// a player who learned the species colours anywhere else. Adding the
        /// words fixes the violation, and the collision stops mattering as a
        /// consequence - which is why no colour moved.
        /// implementation/results/species-collision.md has the rendered
        /// evidence and the three rejected alternatives.
        ///
        /// NEUTRAL INK, DELIBERATELY. LineageView.uss draws all three marks
        /// in --mute. Colouring "Founder" amber would put the fact straight
        /// back onto the colour it was just taken off.
        public const string FounderLabel = "Founder";
        public const string MutatedLabel = "Mutated";
    }

    /// Campaign Select - design section 5.2's second added screen: "how a
    /// player reaches wave 6 after wave 2."
    public static class CampaignSelectScreen
    {
        public const string Title = "Campaign";

        /// The handoff's kicker, over the title: the region these waves are
        /// fought in. design 5.2 gives this screen no `.dc.html` of its own,
        /// so the eyebrow is the one the rest of the bundle uses for a wave -
        /// `Wave Defense.dc.html` heads its own with "Hollow Reach · defense".
        ///
        /// UPPERCASE IN THE CONSTANT, for the reason
        /// `FounderNamingScreen.Eyebrow` states in full: the handoff gets it
        /// from `text-transform`, and USS has none.
        public const string Eyebrow = "HOLLOW REACH";

        /// The word a cleared wave is marked with, in the detail line AND in
        /// the green chip at the row's right edge.
        ///
        /// ONE CONSTANT FOR BOTH, deliberately. `StateLabel` returns it and
        /// the chip renders it, so a row cannot end up saying "Cleared" in
        /// one place and "Complete" in the other - which is exactly what a
        /// second literal at the chip's call site would eventually produce.
        public const string ClearedLabel = "Cleared";

        /// Whether this wave has already been beaten - the replay branch.
        ///
        /// SEPARATE FROM `IsPlayable`, though a cleared wave is always
        /// playable. The two answer different questions: `IsPlayable` gates
        /// the tap, this decides which MARK the row wears, and a screen that
        /// derived the mark from the gate would put the green chip on the
        /// next unplayed wave as well.
        public static bool IsCleared(int waveId, int highestWaveCleared)
        {
            return waveId >= 1 && waveId <= highestWaveCleared;
        }

        /// What the list says when the bundle authored no waves at all.
        /// `FtueDirector.CampaignAsync` returns early on an empty set today,
        /// so this is reachable only through a direct `Bind` - but a list
        /// that CAN come back empty and renders as blank paper is the exact
        /// defect `EmptyState` was built for, and "unreachable today" is a
        /// property of one caller rather than of the view.
        public const string EmptyMessage = "No waves to play yet.";

        /// The smallest AUTHORED wave id past what has been cleared, or null
        /// when the bundle authors nothing beyond it.
        ///
        /// MIRRORED FROM services/api/src/wave/issuance.ts, whose own comment
        /// spells out why it is not `cleared + 1`: "wave ids are dense and
        /// 1-indexed. They are not" - this bundle authors 1, 2, 6 and 7, so
        /// `cleared + 1` computes 3 for a player who has cleared 2 and locks
        /// the only wave they can actually play.
        public static int? NextWave(IReadOnlyList<int> authored, int highestWaveCleared)
        {
            if (authored == null) return null;
            int? next = null;
            foreach (var id in authored)
            {
                if (id <= highestWaveCleared) continue;
                if (next == null || id < next.Value) next = id;
            }
            return next;
        }

        /// Whether `wave/start` would issue for this id: an already-cleared
        /// wave (the replay branch) or exactly the next authored one.
        ///
        /// THE REPLAY CAP IS NOT MIRRORED, deliberately. `issueWave` counts
        /// consumed issuances since the UTC day boundary and refuses over
        /// `replay_cap_reached`; the client is not told that count by any
        /// route, so a client-side cap would be a guess that greys out a
        /// button the server would have honoured. The server refuses, and
        /// `ServerError` carries the refusal - which is the shape
        /// `RegionScreen` already uses for `roster_full`.
        public static bool IsPlayable(int waveId, IReadOnlyList<int> authored, int highestWaveCleared)
        {
            if (waveId < 1 || authored == null) return false;
            var isAuthored = false;
            foreach (var id in authored) { if (id == waveId) { isAuthored = true; break; } }
            if (!isAuthored) return false;

            if (waveId <= highestWaveCleared) return true;
            var next = NextWave(authored, highestWaveCleared);
            return next != null && next.Value == waveId;
        }

        /// The row's state in one word. Three states, three sentences - the
        /// same rule `RosterScreen.IncompleteNotice` is written to: "Cleared"
        /// and "Locked" must never read the same as each other.
        public static string StateLabel(int waveId, IReadOnlyList<int> authored, int highestWaveCleared)
        {
            if (IsCleared(waveId, highestWaveCleared)) return ClearedLabel;
            return IsPlayable(waveId, authored, highestWaveCleared) ? "Next" : "Locked";
        }

        public static string RowLabel(int waveId)
        {
            return "Wave " + waveId.ToString(CultureInfo.InvariantCulture);
        }

        /// The element name a row carries, so a test - and the director -
        /// can find one wave without counting children.
        public static string RowName(int waveId)
        {
            return "wave-" + waveId.ToString(CultureInfo.InvariantCulture);
        }
    }

    /// The Trait Codex index - `screen_inventory_v2` 4: "Without it the
    /// counter system is learnable only by losing."
    public static class TraitCodexScreen
    {
        public const string Title = "Trait Codex";

        /// The sheet's own dismissal. `client_architecture` section 9: a
        /// sheet "overlays, it dismisses, it does not push" - so it carries a
        /// close, not a back chevron, and the word says which.
        ///
        /// Here rather than in `CodexSheet.cs` because it was the one
        /// player-facing literal left in the five new screens, in the one
        /// file whose every other string already came from this class - and
        /// nothing asserted it, which is exactly how the Task 15 review's
        /// three strings survived a 382-line suite.
        public const string DismissLabel = "Close";

        /// What the index says when `config.traits` is empty. bible 10.5
        /// makes recognition the counter system's teaching surface, so the
        /// sheet must not open on nothing and leave the player to guess
        /// whether the codex is empty or the sheet is broken.
        public const string EmptyMessage = "No traits recorded yet.";

        /// What this trait answers, from the BUNDLE's own `config.traits`
        /// table. `Broodline.UI` references no engine assembly, so the
        /// counter printed here is the one the content bundle authored and
        /// not one `Stats.CounterFor` derived - retuning an answer is a
        /// bundle publish, exactly as `WaveDefeatView` documents.
        ///
        /// "Counters nothing" IS AN ANSWER, not a gap. bible 1.2 names four
        /// traits that "counter nothing by design" - Carapace, Litter, Regrow
        /// and Screen - and a codex that left their line blank would read as
        /// missing data about the one fact a player needs.
        public static string Counters(TraitSummary trait)
        {
            if (trait == null) return string.Empty;
            return string.IsNullOrWhiteSpace(trait.Counters)
                ? "Counters nothing."
                : "Counters " + trait.Counters + ".";
        }

        /// Where the trait is found. Empty rather than "unknown" when the
        /// bundle names no species: an absent row is honest, an invented one
        /// is not.
        public static string FoundOn(TraitSummary trait)
        {
            if (trait == null || string.IsNullOrWhiteSpace(trait.Species)) return string.Empty;
            return "Found on " + trait.Species + ".";
        }
    }
}
