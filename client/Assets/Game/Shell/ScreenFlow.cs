using System;
using System.Threading.Tasks;
using UnityEngine.UIElements;

namespace Broodline.Game.Shell
{
    /// **How a directed sequence waits for a screen.**
    ///
    /// THIS IS THE MECHANISM TASK 17's BRIEF ASSUMED AND THE CODEBASE DID NOT
    /// HAVE. That brief writes `await _host.Show(new DeployView().Bind(...))`
    /// throughout, and neither half of it type-checks: `ScreenHost.Show`
    /// returns `void` (so do `Push`, `Pop`, `ShowSheet` and `HideSheet` -
    /// there is nothing on that class to await), and every view's `Bind`
    /// returns `void` too, so `new DeployView().Bind(...)` is a statement and
    /// not an expression. "Show a screen, wait until the player is finished
    /// with it, continue" was unspecified.
    ///
    /// THE SHAPE, AND WHY IT IS THIS ONE. A screen already tells its caller
    /// it is finished - `onStart`, `onName`/`onSkip`, `next`, `retry`,
    /// `onSplice`. That callback IS the completion signal; it just has no
    /// return path. So this class turns one of them into a `Task` with a
    /// `TaskCompletionSource`, and the caller says WHICH callback ends the
    /// turn by passing its own `Bind` call:
    ///
    ///     var view = new PostWaveView();
    ///     await flow.ShowAsync(view, resume =&gt; view.Bind(response, granted, next: resume));
    ///
    /// **No view changed to make this work**, which was the requirement:
    /// Tasks 15 and 16's `Bind` signatures are reviewed and settled, and the
    /// sequencing is the director's problem, not theirs. A view that
    /// returned an awaitable would also be a view that knows it is part of a
    /// sequence, and the same `PostWaveView` has to work as a plain tab
    /// destination with nothing waiting on it.
    ///
    /// THREE PROPERTIES THIS CLASS OWNS, each with a test:
    ///
    /// 1. **Bind before show.** A screen is never on-screen unbound - not
    ///    even for a frame - so there is no window in which a player can tap
    ///    a button whose handler has not been attached.
    ///
    /// 2. **A second answer is ignored, not fatal.** `TrySetResult`, never
    ///    `SetResult`. `Button.clicked` can fire twice before the
    ///    continuation replaces the screen (a double tap, or a screen that
    ///    wires two paths to the same resume), and `SetResult` on a
    ///    completed source throws `InvalidOperationException` - on the UI
    ///    thread, inside a click handler, where nothing would report it. The
    ///    first answer wins and the rest are dropped.
    ///
    /// 3. **A sheet dismisses itself.** `client_architecture` section 9:
    ///    "it overlays, it dismisses, it does not push." So the sheet layer
    ///    is hidden as the turn ends, rather than left over the screen the
    ///    caller goes on to show.
    ///
    /// THREADING. Everything here runs on Unity's main thread and the
    /// continuations resume there, because Unity installs a
    /// `SynchronizationContext` for it - the same fact `WaveHost.Await`
    /// depends on to make the line after its `await` legal to touch
    /// GameObjects with.
    public sealed class ScreenFlow
    {
        readonly ScreenHost _host;

        public ScreenFlow(ScreenHost host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
        }

        /// A screen whose completion carries no value - `next`, `retry`.
        public Task ShowAsync(VisualElement screen, Action<Action> bind)
        {
            if (bind == null) throw new ArgumentNullException(nameof(bind));
            return ShowAsync<bool>(screen, resume => bind(() => resume(true)));
        }

        /// A screen whose completion carries the player's answer - the name
        /// they typed, the wave they picked, whether they confirmed.
        public Task<T> ShowAsync<T>(VisualElement screen, Action<Action<T>> bind)
        {
            return TurnAsync(screen, bind, _host.Show, after: null);
        }

        /// A pushed sub-screen: same turn, but the tab bar hides for as long
        /// as it is up and `ScreenHost.Pop` can come back to what was under
        /// it.
        public Task<T> PushAsync<T>(VisualElement screen, Action<Action<T>> bind)
        {
            return TurnAsync(screen, bind, _host.Push, after: null);
        }

        /// The same turn, for an element whose callbacks are wired at
        /// CONSTRUCTION rather than in a `Bind` - `ConfirmDialog.Standard`
        /// and `.Named` are static factories taking `confirm`/`cancel`, so
        /// there is no instance to hand to the overload above before the
        /// resume exists. `build` receives the resume and returns the
        /// element it wired into.
        public Task<T> ShowSheetAsync<T>(Func<Action<T>, VisualElement> build)
        {
            return TurnAsync(build, _host.ShowSheet, after: _host.HideSheet);
        }

        public Task<T> ShowAsync<T>(Func<Action<T>, VisualElement> build)
        {
            return TurnAsync(build, _host.Show, after: null);
        }

        /// An overlay that answers and goes away. Never a navigation
        /// destination: it does not touch the back stack, and the tab bar's
        /// visibility does not change under it.
        public Task<T> ShowSheetAsync<T>(VisualElement sheet, Action<Action<T>> bind)
        {
            return TurnAsync(sheet, bind, _host.ShowSheet, after: _host.HideSheet);
        }

        Task<T> TurnAsync<T>(VisualElement screen, Action<Action<T>> bind,
            Action<VisualElement> present, Action after)
        {
            if (screen == null) throw new ArgumentNullException(nameof(screen));
            if (bind == null) throw new ArgumentNullException(nameof(bind));

            return TurnAsync<T>(resume => { bind(resume); return screen; }, present, after);
        }

        Task<T> TurnAsync<T>(Func<Action<T>, VisualElement> build,
            Action<VisualElement> present, Action after)
        {
            if (build == null) throw new ArgumentNullException(nameof(build));

            var turn = new TaskCompletionSource<T>();

            // BUILT AND BOUND FIRST, PRESENTED SECOND. See property 1 above.
            var screen = build(answer =>
            {
                // `after` runs BEFORE the result is published, so a
                // continuation that shows the next screen cannot race a
                // sheet that is still up.
                //
                // GUARDED, because the turn must complete even if it does
                // not. `after` is `ScreenHost.HideSheet`, which touches a
                // `VisualElement` that a teardown may already have disposed;
                // unguarded, a throw there escapes into the `Button.clicked`
                // handler that Unity logs and moves on from, and
                // `TrySetResult` below never runs. The director awaiting this
                // turn then stops forever on a screen whose button visibly
                // worked - no beat advance, no notice, and no bound of the
                // kind `WaveHost.Completion` has.
                if (!turn.Task.IsCompleted)
                {
                    try
                    {
                        after?.Invoke();
                    }
                    catch (Exception error)
                    {
                        UnityEngine.Debug.LogException(error);
                    }
                }
                turn.TrySetResult(answer);
            });
            if (screen == null) throw new InvalidOperationException("A screen flow was given nothing to show.");

            present(screen);
            return turn.Task;
        }
    }
}
