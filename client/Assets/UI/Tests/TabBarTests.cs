using System.Collections.Generic;
using System.Linq;
using Broodline.UI.Shell;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Broodline.UI.Tests
{
    /// `TabBar.Render` as a pure function from (tabs, active) to a
    /// `VisualElement` tree - constructed and inspected with no scene, no
    /// `Panel` and no running Player. This is the answer to "how do you test
    /// a VisualElement tree headlessly": a `VisualElement` graph is plain C#
    /// object state until something asks it to render pixels, so
    /// constructing one, calling a method on it, and reading its children,
    /// names and USS classes back needs none of that.
    ///
    /// What this file does NOT do: simulate an actual click and assert
    /// `onSelect` fired. `Clickable.SimulateSingleClick` looked like exactly
    /// that hook (found by disassembling UnityEngine.UIElementsModule.dll for
    /// this Editor version), but it is `internal` - `CS1061` confirms it is
    /// not visible from this assembly - and dispatching a real `ClickEvent`
    /// through `VisualElement.SendEvent` needs an attached `Panel`, which a
    /// bare `new TabBar()` in an EditMode test does not have. So the click
    /// wiring (`() => onSelect(tab)` per button) is trusted rather than
    /// independently re-fired here; what IS verified is everything the
    /// wiring depends on being correct - the right button, in the right
    /// order, closing over the right tab name.
    public class TabBarTests
    {
        [Test]
        public void Render_DrawsOneButtonPerTabInOrder()
        {
            var bar = new TabBar();
            bar.Render(new List<string> { "Map", "Ark", "Splice" }, "Map", _ => { });

            Assert.AreEqual(3, bar.childCount);
            CollectionAssert.AreEqual(
                new[] { "Map", "Ark", "Splice" },
                bar.Children().Select(c => ((Button)c).text).ToList());
        }

        [Test]
        public void Render_MarksExactlyTheActiveTab()
        {
            var bar = new TabBar();
            bar.Render(new List<string> { "Map", "Ark", "Splice" }, "Ark", _ => { });

            var buttons = bar.Children().Cast<Button>().ToList();
            Assert.IsFalse(buttons[0].ClassListContains(TabBar.ActiveTabUssClassName));
            Assert.IsTrue(buttons[1].ClassListContains(TabBar.ActiveTabUssClassName));
            Assert.IsFalse(buttons[2].ClassListContains(TabBar.ActiveTabUssClassName));
        }

        [Test]
        public void Render_TwiceReplacesRatherThanAccumulates()
        {
            var bar = new TabBar();
            bar.Render(new List<string> { "Map", "Ark", "Splice", "Lab", "Allies" }, "Map", _ => { });
            Assert.AreEqual(5, bar.childCount);

            bar.Render(new List<string> { "Map", "Ark" }, "Map", _ => { });
            Assert.AreEqual(2, bar.childCount);
        }

        [Test]
        public void Render_WithNoActiveMatch_MarksNoTabActive()
        {
            var bar = new TabBar();
            bar.Render(new List<string> { "Map", "Ark" }, active: null, onSelect: _ => { });

            Assert.IsTrue(bar.Children().Cast<Button>().All(b => !b.ClassListContains(TabBar.ActiveTabUssClassName)));
        }

        /// The plan wrote this against a `Tab` ENUM that does not exist -
        /// `TabBar.Render` takes `IReadOnlyList<string>` and names each button
        /// `"tab-" + tab`, and a bare `new TabBar()` has no children at all
        /// until Render is called. Same assertions, same messages, against the
        /// API that is actually here.
        ///
        /// The literals ("icon", "icon--" + lowercase) are hardcoded rather
        /// than read off `TabBar.IconUssClassName`, deliberately: `icons.uss`
        /// hardcodes them too, and a test that renames itself in step with the
        /// code it guards would not have noticed the sheet going stale.
        [Test]
        public void EveryTabCarriesItsOwnGlyph()
        {
            var tabs = new List<string> { "Map", "Ark", "Splice", "Lab", "Allies" };
            var bar = new TabBar();
            bar.Render(tabs, "Map", _ => { });

            foreach (var tab in tabs)
            {
                var button = bar.Q<Button>("tab-" + tab);
                Assert.IsNotNull(button, $"no button for {tab}");
                var icon = button.Q<VisualElement>(className: "icon");
                Assert.IsNotNull(icon, $"{tab} has no .icon child - a nav bar of bare words");
                Assert.IsTrue(icon.ClassListContains("icon--" + tab.ToLowerInvariant()),
                              $"{tab}'s glyph is not its own");
            }
        }

        [Test]
        public void Render_NamesEachButtonAfterItsOwnTab()
        {
            // Each button is named for the tab it closes over
            // (`() => onSelect(tab)` per iteration) - this is the structural
            // half of "clicking Ark calls onSelect(Ark)" that a headless test
            // can pin without dispatching a click (see the class comment).
            var bar = new TabBar();
            bar.Render(new List<string> { "Map", "Ark", "Splice" }, "Map", _ => { });

            CollectionAssert.AreEqual(
                new[] { "tab-Map", "tab-Ark", "tab-Splice" },
                bar.Children().Select(c => c.name).ToList());
        }
    }
}
