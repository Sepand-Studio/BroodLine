using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Broodline.Api;
using Broodline.Model;

namespace Broodline.Net
{
    /// The client's one call at cold start.
    ///
    /// client_architecture section 7: render the cached snapshot immediately,
    /// call /v1/sync, replace. A player never watches a spinner to see their
    /// own roster.
    ///
    /// The outbox lives in Outbox.cs / OutboxClient.cs (Phase 7). It was
    /// booked to arrive "with the first queueable mutation, in Phase 6" and
    /// did not.
    ///
    /// NOTE on the generated client's real shape (differs from a first draft
    /// written against the expected NSwag output - see task-10-report.md):
    /// BroodlineApiClient's constructor takes only an HttpClient - there is no
    /// (baseUrl, HttpClient) overload, and BaseUrl is a settable property that
    /// defaults to the OpenAPI document's server URL.
    ///
    /// NOTE on auth: openapi.ts now declares a proper HTTP bearer security
    /// scheme and applies it to SyncAsync and DeleteAccountAsync (see the
    /// OpenAPI document's components.securitySchemes.bearerAuth, and each
    /// operation's `security`). This is a contract gap that was fixed, not a
    /// leftover quirk - but fixing it did not change how the C# client is
    /// called: NSwag's openApiToCSharpClient generator (with the settings in
    /// nswag.json) does not turn a declared security scheme into a per-call
    /// token parameter or a SetBearerToken-style method. The only hook it
    /// generates for this is the generic, per-request `partial void
    /// PrepareRequest(...)` - there is no public HttpClient accessor on the
    /// generated client either, so the Authorization header is set on the
    /// HttpClient this class was handed directly, which is how NSwag
    /// actually surfaces a bearer scheme for this generator.
    public sealed class BroodlineClient
    {
        private readonly BroodlineApiClient _api;
        private readonly HttpClient _http;

        public BroodlineClient(string baseUrl, HttpClient http)
        {
            _http = http;
            _api = new BroodlineApiClient(http) { BaseUrl = baseUrl };
        }

        public async Task<PlayerSnapshot> ColdStartAsync(string accessToken, string clientVersion)
        {
            _http.DefaultRequestHeaders.Remove("Authorization");
            _http.DefaultRequestHeaders.Add("Authorization", "Bearer " + accessToken);

            var res = await _api.SyncAsync(clientVersion).ConfigureAwait(false);

            var balances = new Dictionary<string, int>();
            if (res.Balances != null)
            {
                foreach (var kv in res.Balances) balances[kv.Key] = kv.Value;
            }

            var tabs = new Dictionary<string, int>();
            if (res.Config.Tabs != null)
            {
                foreach (var kv in res.Config.Tabs) tabs[kv.Key] = kv.Value;
            }

            var waves = new List<WaveSummary>();
            if (res.Config.Waves != null)
            {
                foreach (var w in res.Config.Waves)
                {
                    waves.Add(new WaveSummary
                    {
                        Id = w.Id,
                        RewardCurrency = w.Reward?.Currency,
                        RewardAmount = w.Reward?.Amount ?? 0,
                    });
                }
            }

            var traits = new List<TraitSummary>();
            if (res.Config.Traits != null)
            {
                foreach (var t in res.Config.Traits)
                {
                    traits.Add(new TraitSummary { Id = t.Id, Species = t.Species, Counters = t.Counters });
                }
            }

            var ftue = new FtueFacts
            {
                FounderNamed = res.Ftue?.FounderNamed ?? false,
                TutorialStockGranted = res.Ftue?.TutorialStockGranted ?? false,
                Splices = res.Ftue?.Splices ?? 0,
            };

            return new PlayerSnapshot
            {
                PlayerId = res.Player.PlayerId.ToString(),
                ServerId = res.Player.ServerId,
                Balances = balances,
                HighestWaveCleared = res.Campaign.HighestWaveCleared,
                BundleVersion = res.Config.BundleVersion,
                MinimumClientVersion = res.Config.MinimumClientVersion,
                Tabs = tabs,
                Waves = waves,
                Traits = traits,
                Ftue = ftue,
            };
        }
    }
}
