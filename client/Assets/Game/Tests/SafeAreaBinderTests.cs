using Broodline.Game.Shell;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.Game.Tests
{
    /// `SafeAreaBinder.ApplyIfChanged` against injected fakes - no
    /// `UIDocument`, no `Panel`, no live screen.
    ///
    /// What this file does NOT do: prove that a real `GeometryChangedEvent`
    /// fired by a live `Panel` actually reaches `BootController`'s
    /// registered callback. `RuntimePanelUtils.ScreenToPanel` needs a real
    /// `IPanel` to convert against, and dispatching an event through
    /// `VisualElement.SendEvent` needs a `Panel` to route it - the same wall
    /// `TabBarTests.cs` names for click dispatch. Creating a live runtime
    /// `Panel` outside a running `UIDocument`/Player loop goes through
    /// `UIElementsRuntimeUtility.FindOrCreateRuntimePanel`, which takes an
    /// internal delegate type not reachable from this assembly, so that half
    /// is not exercised here. What IS exercised, with one `SafeAreaBinder`
    /// instance mutated across calls exactly like `BootController`'s
    /// registered callback would drive it over a device's lifetime, is the
    /// half that is actually this class's own logic: given a safe area and
    /// screen size that change, decide whether to reapply, and compute the
    /// right padding when it does - which is where a bug in the "written
    /// once, never again" defect this fix addresses would actually live.
    public class SafeAreaBinderTests
    {
        [Test]
        public void ApplyIfChanged_NotReady_AppliesNothingAndReportsNoChange()
        {
            var root = new VisualElement();
            var binder = new SafeAreaBinder(root,
                isReady: () => false,
                getSafeArea: () => new Rect(0, 0, 400, 800),
                getScreenWidth: () => 400f,
                getScreenHeight: () => 800f,
                screenYToPanelY: y => y);

            Assert.IsFalse(binder.ApplyIfChanged());
        }

        [Test]
        public void ApplyIfChanged_FirstCall_AppliesAndReportsChanged()
        {
            var root = new VisualElement();
            // yMax=780 on an 800-tall screen: 20px unsafe at the top, 0 at
            // the bottom.
            var safeArea = new Rect(0, 0, 400, 780);
            var binder = new SafeAreaBinder(root,
                isReady: () => true,
                getSafeArea: () => safeArea,
                getScreenWidth: () => 400f,
                getScreenHeight: () => 800f,
                screenYToPanelY: y => y); // identity: panel space == screen space

            Assert.IsTrue(binder.ApplyIfChanged());
            Assert.AreEqual(20f, root.style.paddingTop.value.value);
            Assert.AreEqual(0f, root.style.paddingBottom.value.value);
        }

        [Test]
        public void ApplyIfChanged_CalledAgainWithNoChange_IsANoOp()
        {
            var root = new VisualElement();
            var safeArea = new Rect(0, 0, 400, 780);
            var binder = new SafeAreaBinder(root,
                isReady: () => true,
                getSafeArea: () => safeArea,
                getScreenWidth: () => 400f,
                getScreenHeight: () => 800f,
                screenYToPanelY: y => y);

            Assert.IsTrue(binder.ApplyIfChanged());
            Assert.IsFalse(binder.ApplyIfChanged());   // nothing moved
        }

        [Test]
        public void ApplyIfChanged_WhenTheObservedSafeAreaChanges_ThePaddingFollows()
        {
            // THE PROPERTY fix round 1 asked for: this is the test that
            // fails on the "written once in Start()" version of this code,
            // because there this computation only ever runs once. ONE
            // binder instance, called twice, with the fakes mutated in
            // between - the same shape BootController's registered
            // GeometryChangedEvent callback drives it in over a device's
            // lifetime.
            var root = new VisualElement();
            var safeArea = new Rect(0, 0, 400, 780);   // yMin=0,  yMax=780
            var screenHeight = 800f;
            var binder = new SafeAreaBinder(root,
                isReady: () => true,
                getSafeArea: () => safeArea,
                getScreenWidth: () => 400f,
                getScreenHeight: () => screenHeight,
                screenYToPanelY: y => y);

            binder.ApplyIfChanged();
            Assert.AreEqual(20f, root.style.paddingTop.value.value);      // 800 - 780
            Assert.AreEqual(0f, root.style.paddingBottom.value.value);    // yMin

            // The window resized (an iPad Split View/Slide Over transition,
            // in production) so the safe area's top inset grew from 20 to 30
            // and the bottom gained a 30px inset it did not have before -
            // with no rotation and no Start() re-run.
            safeArea = new Rect(0, 30, 400, 740);      // yMin=30, yMax=770

            Assert.IsTrue(binder.ApplyIfChanged());
            Assert.AreEqual(30f, root.style.paddingTop.value.value);      // 800 - 770
            Assert.AreEqual(30f, root.style.paddingBottom.value.value);   // yMin
        }

        [Test]
        public void ApplyIfChanged_WhenOnlyTheScreenSizeChanges_StillReapplies()
        {
            // The same safe-area rect reported against a taller screen is
            // still a real change - e.g. an iPad window growing without the
            // OS having moved where the notch/home-indicator insets sit.
            var root = new VisualElement();
            var safeArea = new Rect(0, 0, 400, 780);
            var screenHeight = 800f;
            var binder = new SafeAreaBinder(root,
                isReady: () => true,
                getSafeArea: () => safeArea,
                getScreenWidth: () => 400f,
                getScreenHeight: () => screenHeight,
                screenYToPanelY: y => y);

            binder.ApplyIfChanged();
            Assert.AreEqual(20f, root.style.paddingTop.value.value);

            screenHeight = 900f;

            Assert.IsTrue(binder.ApplyIfChanged());
            Assert.AreEqual(120f, root.style.paddingTop.value.value); // 900 - 780
        }
    }
}
