using System;
using System.Threading.Tasks;
using Broodline.Game.Shell;
using Broodline.UI.Shell;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Broodline.Game.Tests
{
    /// `ScreenFlow` is the sequencing model Task 17 had to design, because
    /// `ScreenHost.Show` returns `void` and every view's `Bind` returns
    /// `void` - there was nothing in the codebase to await.
    ///
    /// It is tested the way `ScreenHostTests` tests the host: plain
    /// `VisualElement` graphs, no `UIDocument`, no `Panel`, no running
    /// Player. That is possible here for exactly the reason the director's
    /// end-to-end walk is NOT (see `FtueDirectorTests`): this class's whole
    /// contract is about the resume delegate, which a test can hold and
    /// invoke directly. A real screen's resume is behind
    /// `Button.clicked`, which needs an attached `Panel`.
    public class ScreenFlowTests
    {
        static ScreenFlow NewFlow(out VisualElement screenHost, out VisualElement sheetLayer, out TabBar tabBar)
        {
            screenHost = new VisualElement { name = "screen-host" };
            sheetLayer = new VisualElement { name = "sheet-layer" };
            tabBar = new TabBar();
            return new ScreenFlow(new ScreenHost(screenHost, sheetLayer, tabBar));
        }

        [Test]
        public void AScreenIsBoundBeforeItIsShown()
        {
            // There must be no frame in which a screen is on-screen with no
            // handler attached - a player tapping in that window taps
            // nothing, and the turn never ends.
            var flow = NewFlow(out var screenHost, out _, out _);
            var screen = new VisualElement { name = "screen" };
            var hostWasEmptyAtBindTime = false;

            flow.ShowAsync<bool>(screen, _ => hostWasEmptyAtBindTime = screenHost.childCount == 0);

            Assert.IsTrue(hostWasEmptyAtBindTime, "the screen was presented before it was bound");
            Assert.AreSame(screen, screenHost.ElementAt(0), "and then it was presented");
        }

        [Test]
        public void TheTurnDoesNotEndUntilTheScreenAnswers()
        {
            var flow = NewFlow(out _, out _, out _);
            Action<string> resume = null;

            var turn = flow.ShowAsync<string>(new VisualElement(), r => resume = r);

            Assert.IsFalse(turn.IsCompleted, "the turn ended without the screen answering");

            resume("Ash");

            Assert.IsTrue(turn.IsCompleted);
            Assert.AreEqual("Ash", turn.Result);
        }

        [Test]
        public void ASecondAnswerIsIgnoredRatherThanThrowing()
        {
            // A double tap, or a screen wiring two paths to one resume.
            // `TaskCompletionSource.SetResult` on a completed source throws
            // `InvalidOperationException` - on the UI thread, inside a click
            // handler, where nothing would report it. The first answer wins.
            var flow = NewFlow(out _, out _, out _);
            Action<string> resume = null;
            var turn = flow.ShowAsync<string>(new VisualElement(), r => resume = r);

            resume("first");
            Assert.DoesNotThrow(() => resume("second"));

            Assert.AreEqual("first", turn.Result);
        }

        [Test]
        public void AVoidTurnCompletesOnItsCallback()
        {
            var flow = NewFlow(out _, out _, out _);
            Action resume = null;

            var turn = flow.ShowAsync(new VisualElement(), r => resume = r);

            Assert.IsFalse(turn.IsCompleted);
            resume();
            Assert.IsTrue(turn.IsCompleted);
        }

        [Test]
        public void ASheetOverlaysAndThenDismissesItself()
        {
            // client_architecture 9: "it overlays, it dismisses, it does not
            // push." A sheet left up over the screen the caller goes on to
            // show is the failure this covers.
            var flow = NewFlow(out var screenHost, out var sheetLayer, out var tabBar);
            var under = new VisualElement { name = "under" };
            screenHost.Add(under);

            Action<bool> resume = null;
            var turn = flow.ShowSheetAsync<bool>(new VisualElement { name = "sheet" }, r => resume = r);

            Assert.AreEqual(DisplayStyle.Flex, sheetLayer.style.display.value, "the sheet never came up");
            Assert.AreEqual(1, sheetLayer.childCount);
            // The overlay is not a navigation destination: what is under it
            // is untouched, and so is the tab bar.
            Assert.AreSame(under, screenHost.ElementAt(0));
            Assert.AreEqual(DisplayStyle.Flex, tabBar.style.display.value);

            resume(true);

            Assert.IsTrue(turn.Result);
            Assert.AreEqual(DisplayStyle.None, sheetLayer.style.display.value, "the sheet stayed up after answering");
            Assert.AreEqual(0, sheetLayer.childCount);
        }

        [Test]
        public void APushedTurnHidesTheTabBarAndAnswersTheSameWay()
        {
            var flow = NewFlow(out var screenHost, out _, out var tabBar);

            // A ROOT FIRST, and that is not ceremony. `ScreenHost.Push` only
            // pushes onto the back stack `if (_screenHost.childCount > 0)`,
            // so a push into an empty host is still depth zero and the tab
            // bar correctly stays up. This test asserted otherwise and
            // failed, which is the assertion being wrong rather than the
            // host - a pushed sub-screen with nothing underneath is not a
            // sub-screen. Pinned in both directions below.
            Action<bool> closeRoot = null;
            flow.ShowAsync<bool>(new VisualElement { name = "root" }, r => closeRoot = r);
            Assert.AreEqual(DisplayStyle.Flex, tabBar.style.display.value);
            Assert.IsNotNull(closeRoot);

            Action<int> resume = null;
            var turn = flow.PushAsync<int>(new VisualElement { name = "detail" }, r => resume = r);

            Assert.AreEqual(DisplayStyle.None, tabBar.style.display.value);
            Assert.AreEqual("detail", screenHost.ElementAt(0).name);

            resume(7);
            Assert.AreEqual(7, turn.Result);
        }

        [Test]
        public void APushWithNothingUnderneathIsStillDepthZero()
        {
            // The behaviour the test above was written against by mistake,
            // stated as its own fact so it cannot be broken silently.
            var flow = NewFlow(out var screenHost, out _, out var tabBar);

            flow.PushAsync<int>(new VisualElement { name = "detail" }, _ => { });

            Assert.AreEqual("detail", screenHost.ElementAt(0).name);
            Assert.AreEqual(DisplayStyle.Flex, tabBar.style.display.value);
        }

        [Test]
        public void AFactoryTurnWiresTheResumeAtConstruction()
        {
            // The overload `ConfirmDialog.Standard`/`.Named` need: their
            // callbacks are constructor arguments, so there is no instance
            // to hand to the bind-an-instance overload before the resume
            // exists.
            var flow = NewFlow(out var screenHost, out _, out _);
            Action<bool> captured = null;

            var turn = flow.ShowSheetAsync<bool>(resume =>
            {
                captured = resume;
                return new VisualElement { name = "built" };
            });

            Assert.IsNotNull(captured, "the factory never saw a resume");
            Assert.IsFalse(turn.IsCompleted);

            captured(false);
            Assert.IsFalse(turn.Result);
            Assert.AreEqual(0, screenHost.childCount, "a sheet must not reach the screen host");
        }

        [Test]
        public void AFactoryThatBuildsNothingIsAnError()
        {
            var flow = NewFlow(out _, out _, out _);
            Assert.Throws<InvalidOperationException>(() => flow.ShowAsync<bool>(_ => null));
        }

        [Test]
        public void NullArgumentsAreRefused()
        {
            var flow = NewFlow(out _, out _, out _);
            Assert.Throws<ArgumentNullException>(() => flow.ShowAsync<bool>((VisualElement)null, _ => { }));
            Assert.Throws<ArgumentNullException>(() => flow.ShowAsync<bool>(new VisualElement(), null));
            Assert.Throws<ArgumentNullException>(() => new ScreenFlow(null));
        }
    }
}
