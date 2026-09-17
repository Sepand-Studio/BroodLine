using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.Game.Shell
{
    /// Applies `client_architecture` section 10's safe-area padding to a root
    /// `VisualElement`, and keeps it correct as the observed safe area or
    /// screen size changes - not just once at startup.
    ///
    /// Fix round 1 finding: the first cut of this (in `BootController`)
    /// applied the inset once in `Start()` and never again. An iPad in Split
    /// View or Slide Over resizes the window without any device rotation, so
    /// portrait-lock and disabled auto-rotate (this project's current
    /// settings, per `verify-unity-settings.sh`) do not protect against a
    /// stale inset there - only reachability by ROTATION was ruled out.
    ///
    /// The four "how do I read the current state" hooks (`isReady`,
    /// `getSafeArea`, `getScreenWidth`, `getScreenHeight`) and the
    /// screen-to-panel-space conversion (`screenYToPanelY`) are injected
    /// rather than hardcoded to `Screen.*`/`RuntimePanelUtils.ScreenToPanel`,
    /// so `ApplyIfChanged`'s change-detection and math can be exercised with
    /// a plain `VisualElement` and no live `Panel` - see
    /// `SafeAreaBinderTests`. `ForRuntimePanel` is the real, Unity-backed
    /// wiring `BootController` actually uses.
    public sealed class SafeAreaBinder
    {
        readonly VisualElement _root;
        readonly Func<bool> _isReady;
        readonly Func<Rect> _getSafeArea;
        readonly Func<float> _getScreenWidth;
        readonly Func<float> _getScreenHeight;
        readonly Func<float, float> _screenYToPanelY;

        bool _applied;
        Rect _lastSafeArea;
        float _lastScreenWidth;
        float _lastScreenHeight;

        public SafeAreaBinder(VisualElement root, Func<bool> isReady, Func<Rect> getSafeArea,
            Func<float> getScreenWidth, Func<float> getScreenHeight, Func<float, float> screenYToPanelY)
        {
            _root = root;
            _isReady = isReady;
            _getSafeArea = getSafeArea;
            _getScreenWidth = getScreenWidth;
            _getScreenHeight = getScreenHeight;
            _screenYToPanelY = screenYToPanelY;
        }

        /// The real wiring: `Screen.safeArea`/`Screen.width`/`Screen.height`,
        /// and `RuntimePanelUtils.ScreenToPanel` against `root`'s own panel -
        /// which is what actually varies under `PanelScaleMode.ScaleWithScreenSize`,
        /// so the inset is computed per device rather than assumed. `isReady`
        /// guards the window before `root` is attached to a panel, when
        /// `ScreenToPanel` would have nothing to convert against.
        public static SafeAreaBinder ForRuntimePanel(VisualElement root) => new SafeAreaBinder(
            root,
            isReady: () => root.panel != null,
            getSafeArea: () => Screen.safeArea,
            getScreenWidth: () => Screen.width,
            getScreenHeight: () => Screen.height,
            screenYToPanelY: y => RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(0f, y)).y);

        /// Re-applies padding only if the safe area or screen size actually
        /// moved since the last successful apply (or this is the first one).
        /// Returns whether it did - callers that poll or listen for layout
        /// events can use this to tell "still current" from "just changed."
        public bool ApplyIfChanged()
        {
            if (!_isReady()) return false;

            var safeArea = _getSafeArea();
            var width = _getScreenWidth();
            var height = _getScreenHeight();

            if (_applied && safeArea == _lastSafeArea && width == _lastScreenWidth && height == _lastScreenHeight)
                return false;

            float topInsetScreen = height - safeArea.yMax;
            float bottomInsetScreen = safeArea.yMin;

            _root.style.paddingTop = Mathf.Abs(_screenYToPanelY(topInsetScreen) - _screenYToPanelY(0f));
            _root.style.paddingBottom = Mathf.Abs(_screenYToPanelY(height) - _screenYToPanelY(height - bottomInsetScreen));

            _lastSafeArea = safeArea;
            _lastScreenWidth = width;
            _lastScreenHeight = height;
            _applied = true;
            return true;
        }
    }
}
