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

        [Test]
        public void AnEmptyFooterNoteHidesItsRow()
        {
            var s = new ScreenScaffold("Roster");
            Assert.AreEqual(DisplayStyle.None, s.Q<Label>("footer-note").resolvedStyle.display,
                "an unset footer note still occupies a row");
            s.FooterNote = "Consumes both parents.";
            Assert.AreEqual(DisplayStyle.Flex, s.Q<Label>("footer-note").resolvedStyle.display);
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
