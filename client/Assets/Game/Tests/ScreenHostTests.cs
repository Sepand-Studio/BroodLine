using Broodline.Game.Shell;
using Broodline.UI.Shell;
using NUnit.Framework;
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
    }
}
