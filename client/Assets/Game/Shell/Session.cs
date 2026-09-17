using System;
using System.Net.Http;
using System.Threading.Tasks;
using Broodline.Api;
using Broodline.Model;
using Broodline.Net;
using UnityEngine;

namespace Broodline.Game.Shell
{
    /// The cold-start sequence. `client_architecture` section 7: "render the
    /// cached snapshot immediately, call /v1/sync, replace. A player never
    /// watches a spinner to see their own roster."
    ///
    /// Constructor shape note: task-13-brief.md's Step 5 sketch threads
    /// `baseUrl` through the constructor as a second parameter. It is not
    /// here - `Api.BaseUrl` is already a public, settable property on the
    /// generated client (`Broodline.Net.BroodlineClient`'s own header
    /// explains why: the generated constructor takes only an `HttpClient`),
    /// so `BootController` sets `session.Api.BaseUrl` from
    /// `BroodlineConfig.json` right after construction instead of the
    /// session taking a second constructor argument for the same value. This
    /// is also the shape `SessionTests.cs`'s two given tests actually call
    /// (`new Session(ApiOver(handler), store, auth, onSnapshot: ...)` -
    /// four arguments, no `baseUrl`), which is what a passing test asks for.
    public sealed class Session
    {
        public BroodlineApiClient Api { get; }
        public PlayerSnapshot Snapshot { get; private set; }

        readonly HttpClient _http;
        readonly ISnapshotStore _snapshots;
        readonly IAuthStore _auth;
        readonly Action<PlayerSnapshot> _onSnapshot;

        public Session(HttpClient http, ISnapshotStore snapshots, IAuthStore auth, Action<PlayerSnapshot> onSnapshot)
        {
            _http = http;
            _snapshots = snapshots;
            _auth = auth;
            _onSnapshot = onSnapshot;
            Api = new BroodlineApiClient(http);
        }

        void SetBearer(string token)
        {
            _http.DefaultRequestHeaders.Remove("Authorization");
            _http.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
        }

        public async Task<PlayerSnapshot> ColdStartAsync()
        {
            var cached = _snapshots.Load();
            if (cached != null)
            {
                Snapshot = cached;
                _onSnapshot(cached);   // render first - never a spinner over a cached roster
            }

            var tokens = _auth.Load() ?? await CreateGuestAsync();
            SetBearer(tokens.AccessToken);

            // BroodlineClient does its own mapping from SyncResponse - built
            // against Api.BaseUrl, which BootController sets before this
            // ever runs (or the generated client's own default, in tests).
            var client = new BroodlineClient(Api.BaseUrl, _http);
            try
            {
                Snapshot = await client.ColdStartAsync(tokens.AccessToken, Application.version);
            }
            catch (BroodlineApiException e) when (e.StatusCode == 401)
            {
                tokens = await RefreshAsync(tokens);
                SetBearer(tokens.AccessToken);
                Snapshot = await client.ColdStartAsync(tokens.AccessToken, Application.version);
            }

            _snapshots.Save(Snapshot);
            _onSnapshot(Snapshot);
            return Snapshot;
        }

        async Task<Tokens> CreateGuestAsync()
        {
            // The Age Gate is deferred to the first build meant for external
            // testers (phase7 design section 12; screen_inventory_v2's Age
            // Gate row) - every internal tester is an adult, so the guest
            // account this creates is sent with that band rather than one
            // asked for on a screen that does not exist yet.
            var res = await Api.CreateAccountAsync(Guid.NewGuid().ToString(),
                new CreateAccountRequest { BirthdateBand = "adult", StorefrontRegion = "us-central1" });
            var tokens = new Tokens { AccessToken = res.AccessToken, RefreshToken = res.RefreshToken };
            _auth.Save(tokens);
            return tokens;
        }

        async Task<Tokens> RefreshAsync(Tokens expired)
        {
            var res = await Api.RefreshSessionAsync(new RefreshRequest { RefreshToken = expired.RefreshToken });
            var tokens = new Tokens { AccessToken = res.AccessToken, RefreshToken = res.RefreshToken };
            _auth.Save(tokens);
            return tokens;
        }
    }
}
