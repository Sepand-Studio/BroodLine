using System.Collections.Generic;
using Broodline.Net;
using UnityEngine;

namespace Broodline.Game.Shell
{
    /// Drives `OutboxClient.FlushAsync` at the two moments
    /// `client_architecture` section 8 names: "flushed on foreground and on
    /// connectivity, with exponential backoff." The backoff itself lives in
    /// `Outbox.Fail`; this component only decides *when* to ask for a flush.
    ///
    /// - `OnApplicationFocus(true)` covers the app being brought back to the
    ///   foreground after being backgrounded (including relaunch on
    ///   platforms that report it as a focus event).
    /// - A 2-second reachability poll covers the device regaining a signal
    ///   while the app stays foregrounded the whole time, which never raises
    ///   a focus event.
    ///
    /// This is the composition root's job to construct, via `Configure` -
    /// `Broodline.Net` cannot reference `Broodline.Game` (that would be a
    /// cycle), so this class only ever talks to `OutboxClient`'s public
    /// surface, never to `Outbox` directly.
    public sealed class OutboxPump : MonoBehaviour
    {
        private const float PollIntervalSeconds = 2f;

        private OutboxClient _client;
        private NetworkReachability _lastReachability;
        private float _pollTimer;

        private readonly List<string> _notices = new List<string>();

        /// Player-facing notices accumulated from expired outbox entries -
        /// the shell's notice list. A future notice/toast UI binds to this;
        /// until then it is at least not silently dropped.
        public IReadOnlyList<string> Notices => _notices;

        /// Wires this pump to the outbox it should drive. Called once by
        /// the composition root (`BootController`) after `OutboxClient` is
        /// constructed for the session.
        public void Configure(OutboxClient client)
        {
            _client = client;
            _lastReachability = Application.internetReachability;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus) FlushNow();
        }

        private void Update()
        {
            _pollTimer += Time.unscaledDeltaTime;
            if (_pollTimer < PollIntervalSeconds) return;
            _pollTimer = 0f;

            var reachability = Application.internetReachability;
            var cameBackOnline = _lastReachability == NetworkReachability.NotReachable
                && reachability != NetworkReachability.NotReachable;
            _lastReachability = reachability;

            if (cameBackOnline) FlushNow();
        }

        // async void is otherwise unavoidable here (Unity's event methods -
        // OnApplicationFocus, Update - are not async-aware), and anything
        // that escapes it surfaces as an unhandled exception on the main
        // thread. Fix round 1: everything in the body, including a second
        // failure from OutboxClient's own Ack/Save bookkeeping, is caught
        // and logged instead of allowed to propagate.
        private async void FlushNow()
        {
            if (_client == null) return;

            try
            {
                var result = await _client.FlushAsync();
                if (result?.Notices == null) return;

                foreach (var notice in result.Notices) _notices.Add(notice);
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
            }
        }
    }
}
