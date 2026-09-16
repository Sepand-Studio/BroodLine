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
        public static string Consumption(CreatureDto parentA, CreatureDto parentB)
        {
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
        public const string EmptyNotice = "No lineage yet.";

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

        /// splice_confirm_spec section 5 and design section 5: the consumed
        /// parents are STILL HERE, and the screen says so in words as well as
        /// in a class name. "Individuals are consumed; the record survives."
        public const string ConsumedLabel = "Consumed";
    }

    /// Campaign Select - design section 5.2's second added screen: "how a
    /// player reaches wave 6 after wave 2."
    public static class CampaignSelectScreen
    {
        public const string Title = "Campaign";

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
            if (waveId <= highestWaveCleared) return "Cleared";
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
