using System;
using System.Collections.Generic;
using Broodline.Api;
using Broodline.Model;

namespace Broodline.Game
{
    /// The eight beats of bible 9.2, as the seven states a session can be
    /// resumed into.
    ///
    /// Beats and states are not one-to-one, deliberately. Beats 1-3 (cold
    /// open, the wave resolving, the creature drop) are one uninterrupted
    /// run of screens with no point a player can leave and come back to
    /// halfway, so they are one state: `ColdOpen`. Beat 7's `Reveal` is the
    /// reverse case - a real screen that is NOT derivable, because nothing
    /// in a snapshot distinguishes "spliced a moment ago" from "spliced
    /// yesterday". See `Derive`.
    public enum Beat
    {
        /// Beats 1-3. Nothing cleared yet: place the pair, watch wave 1,
        /// take the drop.
        ColdOpen,

        /// Beat 4. Wave 1 is cleared and the Founder has no name yet.
        NameFounder,

        /// Beat 5. Wave 2, three creatures, the Lash.
        SecondWave,

        /// Beat 6. The guided splice, on two provided creatures.
        GuidedSplice,

        /// Beat 7. NOT DERIVED - see `Derive`'s own comment. The director
        /// shows it between a commit and the tree.
        Reveal,

        /// Beat 8. The two-generation tree that closes session one.
        Lineage,

        /// The first hour is over; the ordinary app takes the screen.
        Done,
    }

    /// Which beat of the first hour a session resumes into, DERIVED from the
    /// facts `/v1/sync` and `/v1/roster` already return and stored nowhere.
    ///
    /// **Pure, and with no Unity types in it** - design section 5 makes the
    /// beat "a pure function of the snapshot and the roster", and the reason
    /// is the failure a stored cursor has: a client-side "ftueStep" is a
    /// second source of truth about progress the server already owns, and it
    /// goes wrong in the direction that re-runs a beat whose mutation the
    /// server has already taken (or skips one it has not).
    ///
    /// NAMESPACE NOTE, AND IT IS NOT COSMETIC. This lives in
    /// `Broodline.Game` rather than in a `Broodline.Game.Ftue` matching its
    /// folder, which is the convention `Broodline.Game.Shell` set. A
    /// namespace `Broodline.Game.Ftue` containing a type `Ftue` makes every
    /// `Ftue.Derive` written from a sibling namespace ambiguous between the
    /// type and the namespace - a compile error at the one call site this
    /// class exists for.
    ///
    /// The cost of the choice, stated so the next person does not find it
    /// the way this task did: **there is already a generated
    /// `Broodline.Api.Ftue`** (the `/v1/sync` response's `ftue` block), and
    /// a type in an ENCLOSING namespace beats anything a `using` imports.
    /// So inside `Broodline.Game` and everything under it, a bare `Ftue`
    /// means THIS class, and a file wanting the DTO must write
    /// `Broodline.Api.Ftue`. That is exactly one site today
    /// (`Game/Tests/SessionTests.cs`'s `SyncJsonFor`, qualified and
    /// commented), and the failure is CS0712 at compile time rather than a
    /// silent mis-binding - which is why this is a tolerable trade and a
    /// `using Broodline.Api;` shadowing one is not.
    public static class Ftue
    {
        /// The wave that ends session one's derivation. Bible 9.2 stops at
        /// beat 8 and design section 5 sends the player on to wave 6 and the
        /// designed first loss (9.3) from Campaign Select, so a player who
        /// has cleared 6 is out of the first hour and into the game.
        /// `screen_inventory_v2` 9's session-two boundary.
        public const int SessionTwoWave = 6;

        /// Beat 4 is offered while wave 2 is uncleared and not after. Bible
        /// 3.3: one name in the first session, and "all five are renameable
        /// at any time from the Roster" - so the prompt is a beat, not a
        /// gate, and the Roster is where naming lives once the beat passes.
        public const int NamingClosesAtWave = 2;

        public static Beat Derive(PlayerSnapshot s, IReadOnlyList<CreatureDto> roster)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            if (roster == null) throw new ArgumentNullException(nameof(roster));

            // A snapshot with no `ftue` block is one that has not been told
            // anything about onboarding, which is exactly what a fresh
            // account's block says. Read as fresh rather than thrown on: the
            // snapshot can come from `SnapshotStore`'s cache, written by an
            // older client build, and a cold start that throws here is a
            // cold start that shows no screen at all.
            var facts = s.Ftue ?? new FtueFacts();

            var hasFounder = false;
            for (var i = 0; i < roster.Count; i++)
            {
                var creature = roster[i];
                if (creature != null && creature.IsFounder) { hasFounder = true; break; }
            }

            if (s.HighestWaveCleared < 1) return Beat.ColdOpen;

            // `hasFounder` GATES the prompt rather than being assumed: beat 3
            // awards the creature beat 4 names, and a wave-1 clear whose
            // grant has not reached this roster yet (an outbox still holding
            // the submission, a roster load that failed) has nobody to name.
            // Sending such a session to wave 2 is recoverable; showing a
            // naming screen bound to nothing is not.
            if (s.HighestWaveCleared < NamingClosesAtWave)
                return hasFounder && !facts.FounderNamed ? Beat.NameFounder : Beat.SecondWave;

            // SKIPPING DOES NOT NAME THE CREATURE, so `FounderNamed` stays
            // false and this beat would be offered forever if it were not
            // bounded by the wave. It is offered once per session start while
            // wave 2 is uncleared, and never again after - bible 3.3's
            // "optional naming prompt", with the Roster carrying the rename.
            if (facts.Splices == 0) return Beat.GuidedSplice;

            // `Reveal` IS NOT RETURNED FROM HERE, and cannot be: it is the
            // screen that follows a commit inside the same session, and by
            // the time a commit has landed `Splices` is 1 whether that
            // happened a second ago or last week. The director shows it
            // directly off its own commit and then falls through to here,
            // which answers `Lineage`.
            if (s.HighestWaveCleared < SessionTwoWave) return Beat.Lineage;

            return Beat.Done;
        }
    }
}
