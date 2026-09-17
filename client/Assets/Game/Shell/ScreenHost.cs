using System.Collections.Generic;
using Broodline.UI.Shell;
using UnityEngine.UIElements;

namespace Broodline.Game.Shell
{
    /// Owns `#screen-host` and `#sheet-layer`.
    ///
    /// `client_architecture` section 9: "pushed sub-screens hide the bottom
    /// nav and carry a back chevron" and "bottom sheets are an overlay layer,
    /// not screens ... it overlays, it dismisses, it does not push."
    /// `screen_inventory_v2` section 2 is the same rule from the screen side:
    /// "pushed sub-screens carry a back chevron and no bottom nav."
    public sealed class ScreenHost
    {
        readonly VisualElement _screenHost;
        readonly VisualElement _sheetLayer;
        readonly TabBar _tabBar;
        readonly Stack<VisualElement> _backStack = new Stack<VisualElement>();

        public ScreenHost(VisualElement screenHost, VisualElement sheetLayer, TabBar tabBar)
        {
            _screenHost = screenHost;
            _sheetLayer = sheetLayer;
            _tabBar = tabBar;
            _sheetLayer.style.display = DisplayStyle.None;
        }

        /// The current tab's top-level destination. Clears any back stack -
        /// this is tab navigation, not a push, and the tab bar is always
        /// visible at depth zero.
        public void Show(VisualElement screen)
        {
            _backStack.Clear();
            SetScreen(screen);
        }

        /// A pushed sub-screen. The tab bar hides for as long as depth is
        /// greater than zero.
        public void Push(VisualElement screen)
        {
            if (_screenHost.childCount > 0) _backStack.Push(_screenHost.ElementAt(0));
            SetScreen(screen);
        }

        /// Back one level. A no-op at depth zero - there is nothing to pop
        /// back to under the current tab's own top-level screen.
        public void Pop()
        {
            if (_backStack.Count == 0) return;
            SetScreen(_backStack.Pop());
        }

        /// An overlay, never a navigation destination - it never touches the
        /// back stack, and the tab bar's visibility does not change under it.
        public void ShowSheet(VisualElement sheet)
        {
            _sheetLayer.Clear();
            _sheetLayer.Add(sheet);
            _sheetLayer.style.display = DisplayStyle.Flex;
        }

        public void HideSheet()
        {
            _sheetLayer.Clear();
            _sheetLayer.style.display = DisplayStyle.None;
        }

        void SetScreen(VisualElement screen)
        {
            _screenHost.Clear();
            if (screen != null) _screenHost.Add(screen);
            _tabBar.style.display = _backStack.Count > 0 ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }
}
