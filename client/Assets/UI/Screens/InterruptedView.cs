using System;
using Broodline.UI.Components;
using UnityEngine.UIElements;

namespace Broodline.UI.Screens
{
    /// What the player is left looking at when the walk stops.
    ///
    /// IT EXISTS BECAUSE THE ALTERNATIVE WAS NOTHING - Phase 9 Task 21g.
    /// `ScreenFlow.ShowAsync` presents with `after: null`, so the last screen
    /// shown stays presented until the next `ScreenHost.Show`. Every way
    /// `FtueDirector`'s walk could end was a bare `return` with no next
    /// `Show` behind it, so the screen the player was on stayed up with its
    /// only control no longer resuming anything - a live-LOOKING screen, in a
    /// build where relaunching was the only way out. The sentence explaining
    /// it went to `NoticeToast`, which holds a row for four seconds. This is
    /// where that sentence goes instead, beside a button that is actually
    /// wired to something.
    ///
    /// COMPOSED FROM WHAT ALREADY EXISTS, AND DELIBERATELY SO: a scaffold, an
    /// `EmptyState` and a `btn-primary`. It authors no `.uxml` and no `.uss`
    /// of its own. `EmptyState`'s class comment already settled the question
    /// this screen would otherwise re-open - "THERE IS NO EMPTY-STATE DESIGN
    /// IN THE HANDOFF ... this is the minimum the bible asks for and not a
    /// reinterpretation of something that exists" - and the handoff has no
    /// error screen either. Inventing a second visual language for the one
    /// screen nobody is supposed to see would be the wrong place to spend it.
    ///
    /// THE REASON IS THE WALK'S OWN SENTENCE, NOT THIS SCREEN'S. Everything
    /// the director can stop on already has copy in `FtueNotice` or in
    /// `ServerError.PlayerMessage`, both of which are written to be read by a
    /// player; a second sentence composed here would be this assembly
    /// re-deciding what a refusal means. `UnexplainedReason` is the only
    /// string this screen contributes and it is the fallback for a stop that
    /// said nothing, which should not happen and is not worth crashing over.
    ///
    /// TITLED RATHER THAN HEADERLESS. The four headerless screens are
    /// headerless because the handoff draws them that way, over a hero band.
    /// This one has no handoff source at all, and a page header is what every
    /// other screen without a hero has - `ScaffoldTests` keeps the two lists
    /// separate on purpose and this belongs in the titled one.
    public sealed class InterruptedView : VisualElement
    {
        public const string UssClassName = "interrupted-view";

        /// The state, not the cause. The cause is the sentence in the body,
        /// and it differs every time.
        public const string Title = "Interrupted";

        public const string RetryLabel = "Try again";

        /// What the control says while the walk it restarted is still running,
        /// and the reason it says anything at all - Phase 9 Task 21h.
        ///
        /// THE CONTROLLER CONCLUDED THIS BUTTON WAS BROKEN AND TAPPED IT
        /// AGAIN. On an iPhone 17, "Try again" was tapped at 15:25:43 and the
        /// next screen arrived at 15:27:17 - ninety-four seconds in which the
        /// screen did not change in any way: no spinner, no disabled control,
        /// no sentence. `ScreenFlow`'s "a second answer is ignored" swallowed
        /// the extra taps and nothing broke, but the person who wrote the brief
        /// for this screen's own fix still read it as dead. A tester will not be
        /// more patient and will report that the app froze.
        ///
        /// A DISABLED, RELABELLED BUTTON RATHER THAN A SPINNER. `Theme.uss`
        /// already draws `Button:disabled` as a grey fill with muted text, so
        /// the state is visible without authoring anything; the handoff has no
        /// spinner and `InterruptedView`'s own header records why this screen
        /// does not invent visual language. The label is what carries the
        /// meaning, and "..." is the punctuation `FtueNotice.ServerWakingUp`
        /// already uses for the same situation.
        ///
        /// IT IS HONEST ABOUT A HANG RATHER THAN MISLEADING ABOUT ONE. If the
        /// call in flight never answers, the player is now stuck on a control
        /// that says so instead of one that looks ready. That is not a loss of
        /// capability - the extra taps were already being discarded - and what
        /// bounds the wait is `HttpClient.Timeout`, which nothing in this build
        /// sets. Task 21h's report carries the measurement and the argument for
        /// leaving that number alone.
        public const string RetryingLabel = "Trying again...";

        /// `icons.uss`'s own vocabulary - see `EmptyState`'s note on why a
        /// name outside that file renders as an empty box with no error.
        public const string Glyph = "warning";

        /// Shown when the walk stopped without saying why.
        ///
        /// NOTHING SHOULD REACH IT TODAY: every exit in `FtueDirector`
        /// notices immediately before it returns, and the director hands the
        /// last thing it said to `Bind`. It is here because that property is
        /// held by inspection rather than by the compiler, and a blank screen
        /// is the one outcome this whole screen exists to prevent.
        public const string UnexplainedReason = "The game could not continue.";

        readonly ScreenScaffold _scaffold;
        readonly VisualElement _reason;
        readonly Button _retry;

        Action _onRetry;

        public InterruptedView()
        {
            AddToClassList(UssClassName);

            // THE THREE `flexGrow`s ARE THE WHOLE OF THIS SCREEN'S LAYOUT,
            // AND THEY ARE INLINE BECAUSE THE ALTERNATIVE IS WORSE. Every
            // other screen gets `flex-grow: 1` on its root from its own
            // stylesheet, loaded by its own `.uxml`; this one authors
            // neither, and an `.uxml` holding nothing but a `<ui:Style>` is
            // exactly the empty tree `check-silent-drops.sh` exists to catch.
            // So the one layout primitive it needs is written here.
            //
            // WITHOUT THE FIRST ONE THE SCREEN IS WRONG IN A WAY A TEST
            // CANNOT SEE, and the first capture of it showed that: the column
            // sized itself to its content, so the button sat a third of the
            // way down the frame with 700px of bare paper under it while
            // every other screen in the app pins its CTA to the bottom edge.
            // The other two hand the reason the free space that buys, so
            // `.empty-state`'s own `justify-content: center` has something to
            // centre in.
            style.flexGrow = 1;

            _scaffold = new ScreenScaffold(Title);

            _reason = new VisualElement { name = "reason" };
            _reason.style.flexGrow = 1;
            _scaffold.Content.Add(_reason);

            _retry = new Button { name = "retry", text = RetryLabel };
            _retry.AddToClassList("btn-primary");
            _scaffold.CtaRow.Add(_retry);

            Add(_scaffold);

            // Subscribed once, here, against a field `Bind` overwrites - the
            // arrangement every screen on this branch uses, and the hazard it
            // avoids is a second handler stacking behind the first on a
            // re-bind. It matters more here than elsewhere: two handlers on
            // this button would re-enter the walk twice from one tap.
            //
            // BUSY BEFORE THE INVOKE, NOT AFTER, AND THE ORDER IS LOAD-BEARING.
            // `onRetry` is `ScreenFlow`'s resume: it completes the turn this
            // screen is parked on, and the walk's continuation runs from inside
            // this call - far enough, on a healthy path, to replace this screen
            // entirely. Setting the state afterwards would dress a view that
            // has already been dropped, and would leave the live one untouched
            // for however long the first network call takes. See `RetryingLabel`.
            _retry.clicked += () =>
            {
                _retry.SetEnabled(false);
                _retry.text = RetryingLabel;
                _onRetry?.Invoke();
            };
        }

        /// `reason` is what the walk said before it stopped; null or empty
        /// falls back to `UnexplainedReason`. `onRetry` is what the button
        /// does, and a null one leaves a button that does nothing - which is
        /// the defect this screen exists to end, so callers pass one.
        public void Bind(string reason, Action onRetry)
        {
            // REBUILT RATHER THAN RE-TEXTED, because `EmptyState` takes its
            // message at construction and this screen is cheap. Cleared
            // first, on `ScreenScaffold.SetResourcePill`'s rule: a second
            // sentence appearing under the first is the same bug as a second
            // click handler.
            _reason.Clear();

            var said = new EmptyState(string.IsNullOrEmpty(reason) ? UnexplainedReason : reason, Glyph);
            said.style.flexGrow = 1;
            _reason.Add(said);

            // A BIND FULLY DETERMINES THIS SCREEN'S STATE, which is the same
            // rule `_reason.Clear()` above follows. Nothing re-binds an
            // instance today - `AnotherTryAsync` constructs one per stop - but
            // Task 21g's concern 5 contemplates reusing it, and a recycled
            // screen arriving with the previous stop's spent control would be
            // the dead button this screen exists to end.
            _retry.SetEnabled(true);
            _retry.text = RetryLabel;

            _onRetry = onRetry;
        }
    }
}
