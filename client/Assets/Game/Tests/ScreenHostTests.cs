using Broodline.Game.Shell;
using Broodline.UI.Shell;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace Broodline.Game.Tests
{
    /// ScreenHost owns `#screen-host` and `#sheet-layer`. These are plain
    /// `VisualElement` object graphs built directly here, with no
    /// `UIDocument`, no `Panel` and no running Player - proof that this
    /// component's whole contract (Show replaces, Push/Pop keep a back stack
    /// and hide the tab bar, ShowSheet overlays without touching either) is
    /// inspectable headlessly, by reading plain object state rather than by
    /// rendering anything.
    public class ScreenHostTests
    {
        static ScreenHost NewHost(out VisualElement screenHost, out VisualElement sheetLayer, out TabBar tabBar)
        {
            screenHost = new VisualElement { name = "screen-host" };
            sheetLayer = new VisualElement { name = "sheet-layer" };
            tabBar = new TabBar();
            return new ScreenHost(screenHost, sheetLayer, tabBar);
        }

        [Test]
        public void Construction_HidesTheSheetLayer()
        {
            NewHost(out _, out var sheetLayer, out _);
            Assert.AreEqual(DisplayStyle.None, sheetLayer.style.display.value);
        }

        [Test]
        public void Show_ReplacesWhatWasThereAndKeepsTheTabBarVisible()
        {
            var host = NewHost(out var screenHost, out _, out var tabBar);
            var a = new VisualElement { name = "a" };
            var b = new VisualElement { name = "b" };

            host.Show(a);
            Assert.AreEqual(1, screenHost.childCount);
            Assert.AreSame(a, screenHost.ElementAt(0));

            host.Show(b);
            Assert.AreEqual(1, screenHost.childCount);
            Assert.AreSame(b, screenHost.ElementAt(0));
            Assert.AreEqual(DisplayStyle.Flex, tabBar.style.display.value);
        }

        [Test]
        public void Push_HidesTheTabBar_AndPopRestoresIt()
        {
            var host = NewHost(out var screenHost, out _, out var tabBar);
            var root = new VisualElement { name = "root" };
            var detail = new VisualElement { name = "detail" };

            host.Show(root);
            host.Push(detail);

            Assert.AreSame(detail, screenHost.ElementAt(0));
            Assert.AreEqual(DisplayStyle.None, tabBar.style.display.value);

            host.Pop();

            Assert.AreSame(root, screenHost.ElementAt(0));
            Assert.AreEqual(DisplayStyle.Flex, tabBar.style.display.value);
        }

        [Test]
        public void Pop_AtDepthZero_IsANoOp()
        {
            var host = NewHost(out var screenHost, out _, out _);
            var root = new VisualElement { name = "root" };
            host.Show(root);

            host.Pop();

            Assert.AreEqual(1, screenHost.childCount);
            Assert.AreSame(root, screenHost.ElementAt(0));
        }

        [Test]
        public void ShowSheet_OverlaysWithoutTouchingTheBackStackOrTabBar()
        {
            var host = NewHost(out var screenHost, out var sheetLayer, out var tabBar);
            var root = new VisualElement { name = "root" };
            var sheet = new VisualElement { name = "sheet" };

            host.Show(root);
            host.ShowSheet(sheet);

            Assert.AreEqual(DisplayStyle.Flex, sheetLayer.style.display.value);
            Assert.AreSame(sheet, sheetLayer.ElementAt(0));
            // Unaffected: a sheet is not a screen and not a push.
            Assert.AreSame(root, screenHost.ElementAt(0));
            Assert.AreEqual(DisplayStyle.Flex, tabBar.style.display.value);

            host.HideSheet();
            Assert.AreEqual(DisplayStyle.None, sheetLayer.style.display.value);
            Assert.AreEqual(0, sheetLayer.childCount);
        }

        /// THE ONE THING ABOUT THE SHEET LAYER THAT IS NOT IN THIS CLASS'S
        /// OBJECT GRAPH: WHERE IT SITS AMONG ITS SIBLINGS - Phase 9 Task 21i.
        ///
        /// Every test above builds `#sheet-layer` as a loose `VisualElement`,
        /// which is right for `ShowSheet`'s contract and blind to the defect
        /// that closed this phase. `Shell.uxml` declared the layer BEFORE
        /// `#tab-bar`. UI Toolkit has no z-index, so sibling order is both
        /// paint order AND hit-test order: the bar was drawn across the bottom
        /// of every sheet and took the taps there too. Measured on an iPhone 17
        /// at 402x874 - the lower 16 of the Forfeit button's 55 points resolved
        /// to `TabBar`, and `BootController.OnTabSelected` is deliberately
        /// empty, so a player pressed the only button on the sheet and the app
        /// did nothing at all, twice, for 90 seconds each time.
        ///
        /// WHAT THIS CANNOT ASSERT, said plainly because this phase has
        /// shipped assertions that could not fail. Not the geometry and not the
        /// pick: there is no panel in EditMode, so every rect here reads zero
        /// (`ScaffoldTests` and `FirstHourScreensTests` both say so out loud)
        /// and `IPanel.Pick` has nothing to ask.
        /// `implementation/scripts/probe-shell.sh` is where that one lives - a
        /// live panel under `-batchmode` with a graphics device, which went red
        /// on this defect before the fix and green after.
        ///
        /// WHAT IT CAN ASSERT IS THE ORDER, and the order is the whole of the
        /// fix: it is a property of the document, not of a layout. Put
        /// `#sheet-layer` back above `#tab-bar` and this test reddens.
        [Test]
        public void TheSheetLayerIsDeclaredLastSoAnOverlayIsActuallyOnTop()
        {
            var shell = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Shell/Shell.uxml");
            Assert.IsNotNull(shell, "Shell.uxml did not load, so nothing below this line is a measurement");

            var tree = new VisualElement();
            shell.CloneTree(tree);

            var shellRoot = tree.Q<VisualElement>("shell-root");
            Assert.IsNotNull(shellRoot, "#shell-root is not in the tree Shell.uxml builds");

            var sheetLayer = shellRoot.Q<VisualElement>("sheet-layer");
            var tabBar = shellRoot.Q<VisualElement>("tab-bar");
            Assert.IsNotNull(sheetLayer, "#sheet-layer is not inside #shell-root");
            Assert.IsNotNull(tabBar, "#tab-bar is not inside #shell-root");

            Assert.Greater(shellRoot.IndexOf(sheetLayer), shellRoot.IndexOf(tabBar),
                "#sheet-layer is declared before #tab-bar, so the tab bar paints over every sheet and "
                + "swallows the taps in its own band - which is how the lower third of the only button on "
                + "AbandonedWaveSheet came to be dead on an iPhone 17");

            // LAST, NOT MERELY AFTER THE BAR. Anything added below it takes the
            // band straight back, and the next element to join this file is
            // exactly the one that would.
            Assert.AreEqual(shellRoot.childCount - 1, shellRoot.IndexOf(sheetLayer),
                "#sheet-layer is no longer the last child of #shell-root, so whatever now follows it draws "
                + "and picks over every sheet");
        }
    }
}
