using System;
using System.Linq;
using System.Reflection;
using Broodline.UI.Components;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Broodline.UI.Tests
{
    /// The handoff's layout frame, and the rule that every screen uses it.
    ///
    /// The frame is: status bar -> header -> [tab bar] -> content (flex 1,
    /// scrolls) -> CTA row -> footer note -> bottom nav. The shell owns the
    /// bars; this owns everything between them.
    ///
    /// EveryScreenComposesTheScaffold is the load-bearing one. Phase 7 built
    /// twelve screens as bare VisualElements with no shared chrome, which is
    /// why nothing lined up. A reflection sweep means a thirteenth cannot
    /// quietly do the same.
    ///
    /// IT IS DELIBERATELY RED AT THE COMMIT THAT ADDS IT, and it names the ten
    /// screens that owe the work. Tasks 9-12 empty that list one group at a
    /// time; the test going green is how they know they are done. A skip here
    /// would be indistinguishable from success.
    ///
    /// NAVIGATION IS NOT HERE. ScreenHost already enforces that a pushed
    /// sub-screen hides the tab bar (client_architecture section 9). The
    /// scaffold renders a back affordance when told to and calls back; it does
    /// not decide depth. Two owners of that rule would be one too many.
    public class ScaffoldTests
    {
        [Test]
        public void TheScaffoldCarriesHeaderContentAndCtaRow()
        {
            var s = new ScreenScaffold("Gene Ark");
            Assert.IsNotNull(s.Q<VisualElement>("header"), "no header");
            Assert.IsNotNull(s.Content, "no content region");
            Assert.IsNotNull(s.CtaRow, "no CTA row");
            Assert.AreEqual("Gene Ark", s.Q<Label>("title").text);
        }

        /// The scaffold decides the scroller once, for all ten screens, so it
        /// is pinned once. Without this a Task 9 edit could restore Unity's
        /// desktop chrome across every screen at the same time and nothing
        /// would say so - the capture corpus only notices on a screen whose
        /// content happens to overflow.
        [Test]
        public void TheContentRegionCarriesNoDesktopScrollerChrome()
        {
            var s = new ScreenScaffold("Gene Ark");
            var content = s.Content as ScrollView;
            Assert.IsNotNull(content, "the content region is not a ScrollView, so it does not scroll at all");
            Assert.AreEqual(ScrollerVisibility.Hidden, content.verticalScrollerVisibility,
                "the content region would draw Unity's 22px desktop scroller, arrow buttons and all, the "
                + "moment a screen overflowed it. Measured on the Scaffold fixture: it eats into the 12px "
                + "gutter and clips the right-hand card.");
        }

        [Test]
        public void ATopLevelScreenHasNoBackChevron()
        {
            var s = new ScreenScaffold("Roster");
            Assert.IsNull(s.Q<Button>("back"),
                "a top-level screen showed a back chevron; the handoff gives one only to pushed sub-screens");
        }

        [Test]
        public void APushedScreenHasABackChevronThatCallsBack()
        {
            var called = 0;
            var s = new ScreenScaffold("Splice Reveal", pushed: true, onBack: () => called++);
            var back = s.Q<Button>("back");
            Assert.IsNotNull(back, "a pushed sub-screen has no back chevron");
            Assert.IsNotNull(back.Q<VisualElement>(className: "icon--back"), "the chevron has no glyph");

            // No panel in this assembly, so the callback is invoked directly -
            // the constraint ComponentTests documents. What is proven is that
            // the button is wired to the caller's action, not that a click
            // dispatches. See RaiseClicked for why it is not one line.
            RaiseClicked(back);
            Assert.AreEqual(1, called, "the back button is not wired to onBack");
        }

        /// A VISIBLE AFFORDANCE THAT DOES NOTHING MUST NOT SHIP, and this is
        /// the half of the rule `pushed` alone cannot express.
        ///
        /// `Broodline.UI` does not reference `Broodline.Game`, so no screen in
        /// this assembly can name `ScreenHost.Pop`; a pushed screen is
        /// constructed before its caller has handed over the action, and
        /// `LineageView` is constructed that way today. `Pop` is a documented
        /// no-op at depth zero, so a chevron wired to it there would also do
        /// nothing - which is what makes "harmless" the wrong test. A player
        /// taps it, gets no error, no transition and no feedback, and reads
        /// the app as broken.
        [Test]
        public void APushedScreenDrawsNoChevronUntilItIsGivenSomewhereToGo()
        {
            var s = new ScreenScaffold("Lineage", pushed: true);
            Assert.IsNull(s.Q<Button>("back"),
                "a pushed screen with no back action drew a chevron anyway; tapping it does nothing at all");

            var called = 0;
            s.OnBack = () => called++;
            var back = s.Q<Button>("back");
            Assert.IsNotNull(back, "giving a pushed scaffold an action did not restore its chevron");
            Assert.IsNotNull(back.Q<VisualElement>(className: "icon--back"),
                "the restored chevron has no glyph");

            RaiseClicked(back);
            Assert.AreEqual(1, called, "the restored chevron is not wired to the action that restored it");

            // And it goes again when the action does - a screen re-bound as a
            // tab destination must not keep the chevron its pushed bind gave
            // it. RaiseClicked here would prove the handler is gone too, but
            // the button is no longer in the tree to raise it on, which is
            // the stronger statement.
            s.OnBack = null;
            Assert.IsNull(s.Q<Button>("back"), "clearing the action left the chevron behind");
        }

        /// A TOP-LEVEL SCREEN NEVER GETS ONE, action or not. `pushed` is the
        /// handoff's push table and it still governs; the callback can only
        /// take a chevron away, never add one to a screen that has no parent.
        [Test]
        public void ATopLevelScreenHasNoChevronEvenWithABackAction()
        {
            var s = new ScreenScaffold("Roster", pushed: false, onBack: () => { });
            Assert.IsNull(s.Q<Button>("back"),
                "a top-level screen grew a back chevron because it was handed an action");

            s.OnBack = () => { };
            Assert.IsNull(s.Q<Button>("back"));
        }

        [Test]
        public void AnEmptyFooterNoteHidesItsRow()
        {
            var s = new ScreenScaffold("Roster");
            Assert.AreEqual(DisplayStyle.None, s.Q<Label>("footer-note").resolvedStyle.display,
                "an unset footer note still occupies a row");
            s.FooterNote = "Consumes both parents.";
            Assert.AreEqual(DisplayStyle.Flex, s.Q<Label>("footer-note").resolvedStyle.display);
        }

        /// THE HANDOFF'S HEADER IS THREE THINGS AND THE SCAFFOLD CARRIED
        /// ONE. Every screen in the bundle puts an eyebrow over its title
        /// ("Gene Lab" over "Splicing Chamber", "Hollow Reach · defense" over
        /// "Wave 7") and most put a resource readout at the right edge. Both
        /// were missing, and ten screens were composing a header that said
        /// less than the design's.
        ///
        /// THE TEN EXISTING SCREENS MUST NOT MOVE, which is what the rest of
        /// this file asserts and why both additions are opt-in: a scaffold
        /// built the old way gets a hidden eyebrow and no pill at all.
        [Test]
        public void Scaffold_ShowsAnEyebrow_AndAResourcePill_WhenGiven()
        {
            var s = new ScreenScaffold("Splicing Chamber", eyebrow: "Gene Lab");
            Assert.AreEqual("Gene Lab", s.Q<Label>("eyebrow").text);
            Assert.AreEqual(DisplayStyle.Flex, s.Q<Label>("eyebrow").style.display.value);

            s.SetResourcePill("icon--charge", "4", "/5");
            Assert.AreEqual("4", s.Q<Label>("pill-value").text);
            Assert.IsTrue(s.Q<Label>("pill-value").ClassListContains("t-num"),
                "the charge count is a number a decision depends on - bible 10.6's floor and tabular face "
                + "are enforced through this marker and nothing else");
            Assert.AreEqual("/5", s.Q<Label>("pill-suffix").text);
            Assert.IsNotNull(s.Q<VisualElement>(className: "icon--charge"), "the pill has no glyph");
            Assert.IsNotNull(s.HeaderSlot.Q<VisualElement>("resource-pill"),
                "the pill belongs to the header slot, so a screen that wants something else there can "
                + "still have it");

            // A RE-BOUND SCREEN MUST NOT GROW A SECOND PILL. The charge count
            // changes every splice, so this is called again and again on one
            // scaffold.
            s.SetResourcePill("icon--charge", "3", "/5");
            Assert.AreEqual(1, s.Query<VisualElement>("resource-pill").ToList().Count);
            Assert.AreEqual("3", s.Q<Label>("pill-value").text);

            s.SetResourcePill(null, null);
            Assert.IsNull(s.Q<VisualElement>("resource-pill"),
                "a screen with no resource to state kept an empty white pill in its header");
        }

        /// The state every screen written before this task is in, asserted
        /// rather than assumed: ten of them pass no eyebrow and no pill, and
        /// this change has to be invisible to all ten.
        [Test]
        public void AScaffoldWithNoEyebrowReservesNoRowForOne()
        {
            var s = new ScreenScaffold("Roster");
            Assert.AreEqual(DisplayStyle.None, s.Q<Label>("eyebrow").style.display.value);
            Assert.IsNull(s.Q<VisualElement>("resource-pill"));

            s.Eyebrow = "Hatchery";
            Assert.AreEqual(DisplayStyle.Flex, s.Q<Label>("eyebrow").style.display.value);
            s.Eyebrow = null;
            Assert.AreEqual(DisplayStyle.None, s.Q<Label>("eyebrow").style.display.value,
                "clearing the eyebrow left its row behind");
        }

        /// A SCREEN WITH NO TITLE HAS NO PAGE HEADER, which is one handoff
        /// screen and one only.
        ///
        /// `Onboarding.dc.html` starts its column with the progress row (line
        /// 33) and puts its 26px Baloo heading inside the white card (line
        /// 161). Every other screen in the bundle has a header and titles it
        /// at 20-21px - `Creature Roster.dc.html:31` is 21 - which is what
        /// `--text-screen-title` already is. So this is about the presence of
        /// a header, never about the size of a title.
        ///
        /// THE SECOND HALF IS WHAT KEEPS IT FREE FOR THE OTHER NINE SCREENS.
        /// All nine pass a non-empty literal, so none can reach the hidden
        /// branch; asserted rather than reasoned about.
        ///
        /// `style.display.value` RATHER THAN `resolvedStyle`: no panel here,
        /// and `Flex` is also the default computed value, so a resolvedStyle
        /// read would pass on a scaffold that had never set anything.
        [Test]
        public void AScaffoldWithNoTitleDrawsNoPageHeaderAtAll()
        {
            var bare = new ScreenScaffold(title: null);
            Assert.AreEqual(DisplayStyle.None, bare.Q<VisualElement>("header").style.display.value,
                "a titleless scaffold still drew the header row, so a screen composing the handoff's "
                + "headerless onboarding gets a band of empty chrome above its first card");
            Assert.AreEqual(string.Empty, bare.Q<Label>("title").text);

            // Empty reads the same as null. A screen saying `""` means the
            // same thing and must not get a different frame for it.
            Assert.AreEqual(DisplayStyle.None,
                new ScreenScaffold(string.Empty).Q<VisualElement>("header").style.display.value);

            var titled = new ScreenScaffold("Gene Ark");
            Assert.AreEqual(DisplayStyle.Flex, titled.Q<VisualElement>("header").style.display.value,
                "a titled scaffold lost its header; nine screens draw their whole chrome there");
            Assert.AreEqual("Gene Ark", titled.Q<Label>("title").text);
        }

        /// Step 4 took `flex-grow` off `.screen-scaffold__title` and gave it
        /// to a new `.screen-scaffold__titles` column so the eyebrow could
        /// sit above the title (ScreenScaffold.uss's header comment) -
        /// nothing before this test proved the REPARENTING itself, only that
        /// the eyebrow row collapses when empty. A mistake that left `title`
        /// a direct child of `header` again, or that put the grow back on
        /// the wrong element, would have passed every other test here and
        /// only shown up as a capture that looked subtly different for the
        /// ten screens that already compose the scaffold. Fix round 1,
        /// Minor 6 - the sheets are already checked by eye against
        /// RosterView.png et al.; this is what pins it in code.
        [Test]
        public void TheTitleLivesInsideTheTitlesColumn_WhichCarriesTheGrowTheTitleGaveUp()
        {
            var s = new ScreenScaffold("Roster");
            var header = s.Q<VisualElement>("header");
            var titles = s.Q<VisualElement>("titles");
            var title = s.Q<Label>("title");

            Assert.IsNotNull(titles, "no titles column");
            Assert.AreSame(titles, title.parent, "the title is no longer inside the titles column");
            Assert.AreSame(header, titles.parent, "the titles column is no longer a direct child of the header");
            Assert.IsTrue(titles.ClassListContains("screen-scaffold__titles"),
                "the titles column lost the class that carries flex-grow: 1");
            Assert.IsTrue(title.ClassListContains("screen-scaffold__title"),
                "the title lost the class that carries flex-grow: 0");
        }

        [Test]
        public void EveryScreenComposesTheScaffold()
        {
            // The two OVERLAYS in this namespace are not screens and take no
            // scaffold. Exempted BY NAME, not by a namespace accident:
            //   CodexSheet  - a bottom sheet. ScreenHost.ShowSheet overlays it,
            //                 never pushes it, and it never touches the back
            //                 stack (client_architecture section 9).
            //   WaveHudView - a HUD drawn over the wave scene, not a screen
            //                 the host swaps in.
            // Both ARE in Broodline.UI.Screens - an earlier draft of this
            // exemption claimed they were not, and the namespace declarations
            // at the top of their own files say otherwise. The list is
            // asserted to be exactly these two, so a third screen cannot be
            // quietly excused by adding a name here.
            var exempt = new[] { "CodexSheet", "WaveHudView" };

            var all = typeof(Broodline.UI.Screens.RosterView).Assembly
                .GetTypes()
                .Where(t => t.Namespace == "Broodline.UI.Screens"
                            && typeof(VisualElement).IsAssignableFrom(t)
                            && !t.IsAbstract
                            && t.GetConstructor(Type.EmptyTypes) != null)
                .ToList();

            Assert.That(all.Count, Is.EqualTo(12),
                "the namespace holds 12 constructible VisualElements; if this moved, the " +
                "sweep's exemption list below needs re-deciding rather than silently widening");

            var screens = all.Where(t => !exempt.Contains(t.Name)).ToList();
            Assert.That(screens.Count, Is.EqualTo(10), "10 screens must carry the frame");

            var bare = screens
                .Where(t => ((VisualElement)Activator.CreateInstance(t))
                            .Q<VisualElement>(className: ScreenScaffold.UssClassName) == null)
                .Select(t => t.Name)
                .ToList();

            Assert.IsEmpty(bare,
                "these screens do not compose ScreenScaffold, so they carry no header, no CTA row " +
                "and no scrolling content region:\n  " + string.Join("\n  ", bare));
        }

        [Test]
        public void EveryTitledScreenStillDrawsItsPageHeader()
        {
            // Task 14b gave `ScreenScaffold` a headerless mode: a null or
            // empty title hides `#header` outright, because
            // `Onboarding.dc.html` has no page header and draws its progress
            // row straight into the content column.
            //
            // EXACTLY ONE SCREEN WANTS THAT. The other nine pass a non-empty
            // `const string Title` and must still get a header - and with it
            // the back chevron, the eyebrow and the resource pill, all of
            // which live inside the row the headerless branch hides.
            //
            // Until this test, that rested on a report claim and a capture
            // corpus that is gitignored. A screen that lost its header would
            // look exactly like a screen that never had one, and the loss
            // would be one empty string away.
            var noScaffold = new[] { "CodexSheet", "WaveHudView" };
            var headerless = new[] { "FounderNamingView" };

            var titled = typeof(Broodline.UI.Screens.RosterView).Assembly
                .GetTypes()
                .Where(t => t.Namespace == "Broodline.UI.Screens"
                            && typeof(VisualElement).IsAssignableFrom(t)
                            && !t.IsAbstract
                            && t.GetConstructor(Type.EmptyTypes) != null
                            && !noScaffold.Contains(t.Name)
                            && !headerless.Contains(t.Name))
                .ToList();

            Assert.That(titled.Count, Is.EqualTo(9),
                "nine screens pass a non-empty title; if this moved, decide which list the " +
                "new screen belongs in rather than widening one silently");

            var lost = titled
                .Where(t =>
                {
                    var header = ((VisualElement)Activator.CreateInstance(t)).Q<VisualElement>("header");
                    return header == null
                        || header.style.display.value == DisplayStyle.None;
                })
                .Select(t => t.Name)
                .ToList();

            Assert.IsEmpty(lost,
                "these screens compose ScreenScaffold but draw no page header, so they have " +
                "silently lost their back chevron, eyebrow and resource pill:\n  " +
                string.Join("\n  ", lost));
        }

        /// Raises a `Button`'s `clicked` with no `Panel` attached.
        ///
        /// THE ONE LINE THIS TASK'S PLAN WROTE - `back.clicked?.Invoke()` -
        /// DOES NOT COMPILE. `Button.clicked` is an event declared with
        /// explicit add/remove accessors, and C# lets only the declaring type
        /// raise an event (CS0070); there is no backing delegate on `Button`
        /// to read at all, because its `add` forwards straight to
        /// `Button.clickable`. The two obvious alternatives were already ruled
        /// out in this assembly and are recorded in `TabBarTests`' class
        /// comment: `Clickable.SimulateSingleClick` is `internal`, and
        /// `VisualElement.SendEvent` needs an attached `Panel` that a bare
        /// `new ScreenScaffold(...)` does not have.
        ///
        /// What IS reachable: `Button.clickable` is public, and `Clickable`
        /// declares `clicked` as a plain field-like event, so the compiler
        /// generates a private instance field of type `Action` behind it.
        /// Reading that field and calling it runs exactly the subscriber list
        /// `+= onBack` appended - which is the wiring this test exists to
        /// prove, and strictly more than `TabBarTests` was able to prove about
        /// its own buttons.
        ///
        /// THROWS RATHER THAN NO-OPPING if that field is not where it is
        /// expected. A helper that quietly did nothing would leave
        /// `APushedScreenHasABackChevronThatCallsBack` passing on a scaffold
        /// that had never wired anything, which is the failure mode this whole
        /// file exists to make impossible.
        static void RaiseClicked(Button button)
        {
            var clickable = button.clickable;
            Assert.IsNotNull(clickable, "the Button has no Clickable manipulator to raise");

            const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            var field = typeof(Clickable).GetField("clicked", Instance);
            if (field == null || field.FieldType != typeof(Action))
            {
                // Named differently by a future Editor, but still the only
                // Action-typed field on the type - `clickedWithEventInfo` is
                // an Action<EventBase> and does not collide.
                var candidates = typeof(Clickable).GetFields(Instance)
                    .Where(f => f.FieldType == typeof(Action))
                    .ToList();
                field = candidates.Count == 1 ? candidates[0] : null;
            }

            if (field == null)
            {
                throw new MissingFieldException(
                    "UnityEngine.UIElements.Clickable no longer backs its `clicked` event with a single "
                    + "Action field, so ScaffoldTests.RaiseClicked cannot raise a click without a Panel. "
                    + "It needs a new hook rather than a weaker assertion.");
            }

            ((Action)field.GetValue(clickable))?.Invoke();
        }
    }
}
