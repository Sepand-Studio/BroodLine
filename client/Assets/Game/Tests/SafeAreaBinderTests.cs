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

        // ---------------------------------------------------------------
        // Who gets the inset - Phase 9 Task 21h, D2
        // ---------------------------------------------------------------

        [Test]
        public void BindSafeAreas_PadsTheNoticeToast_AndNotOnlyThePanelRoot()
        {
            // THE DEFECT, MEASURED ON AN iPHONE 17: the notice toast drew with
            // its first line behind the Dynamic Island - "did not finish
            // within" cut through by the black pill - on a build where every
            // other screen cleared the inset.
            //
            // ONE BINDER WAS NOT ENOUGH AND THE REASON IS A LAYOUT RULE.
            // `#notice-layer` is a sibling of `#shell-root` and is
            // `position: absolute`; UI Toolkit offsets an absolutely
            // positioned child from its parent's BORDER box rather than its
            // padding box, so the inset on the panel root reached
            // `#shell-root` (an in-flow child) and did not reach the layer.
            // `.notice-toast` is absolute inside the layer too, so padding on
            // the LAYER would not have reached it either - it has to go on the
            // toast, whose rows are in-flow children of it.
            //
            // THE NUMBERS ARE THE DEVICE'S. The walk was captured at 402x874
            // and this is that frame's real inset pair: 60 at the top for the
            // island, 34 at the bottom for the home indicator.
            var root = new VisualElement { name = "root" };
            var shell = new VisualElement { name = "shell-root" };
            var toast = new VisualElement { name = "notice-toast" };
            root.Add(shell);
            root.Add(toast);

            var safeArea = new Rect(0, 34, 402, 780);   // yMin=34, yMax=814
            var screenHeight = 874f;
            var bound = BootController.BindSafeAreas(root, toast, element => new SafeAreaBinder(element,
                isReady: () => true,
                getSafeArea: () => safeArea,
                getScreenWidth: () => 402f,
                getScreenHeight: () => screenHeight,
                screenYToPanelY: y => y));

            Assert.AreEqual(2, bound.Count, "something other than the root and the toast is being bound");
            Assert.AreEqual(60f, root.style.paddingTop.value.value);      // 874 - 814
            Assert.AreEqual(34f, root.style.paddingBottom.value.value);   // yMin

            // THE ASSERTION THE DEFECT FAILS. Before this task nothing gave
            // the toast an inset of its own, so this read 0.
            Assert.AreEqual(60f, toast.style.paddingTop.value.value,
                "the notice toast has no safe-area inset, so it draws under the Dynamic Island");

            // AND THE BINDERS HANDED BACK ARE THE LIVE ONES, so the
            // GeometryChangedEvent registration re-applies to BOTH rather than
            // to a root whose toast has been forgotten. Driven directly
            // because dispatching a real `GeometryChangedEvent` needs a
            // `Panel` - see this file's header.
            safeArea = new Rect(0, 34, 402, 750);       // the island grew: yMax=784
            for (var i = 0; i < bound.Count; i++) Assert.IsTrue(bound[i].ApplyIfChanged());
            Assert.AreEqual(90f, root.style.paddingTop.value.value);      // 874 - 784
            Assert.AreEqual(90f, toast.style.paddingTop.value.value);
        }

        [Test]
        public void BindSafeAreas_RefusesToBindNothing()
        {
            // A null here is a shell that composed in the wrong order, and it
            // would be a silently un-inset element rather than a visible
            // failure - the class of defect D2 is.
            var element = new VisualElement();
            Assert.Throws<System.ArgumentNullException>(() => BootController.BindSafeAreas(null, element));
            Assert.Throws<System.ArgumentNullException>(() => BootController.BindSafeAreas(element, null));
        }
    }
}
