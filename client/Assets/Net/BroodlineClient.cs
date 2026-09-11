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
    /// NO OUTBOX HERE YET. client_architecture section 8 scopes it to durable
    /// queued MUTATIONS, and this phase's only mutation is account creation,
    /// which cannot be queued - a player with no account has nothing to queue
    /// against. It arrives with the first queueable mutation, in Phase 6.
    ///
    /// NOTE on the generated client's real shape (differs from a first draft
    /// written against the expected NSwag output - see task-10-report.md):
    /// BroodlineApiClient's constructor takes only an HttpClient - there is no
    /// (baseUrl, HttpClient) overload, and BaseUrl is a settable property that
    /// defaults to the OpenAPI document's server URL. There is also no public
    /// HttpClient accessor on the generated client, so the Authorization
    /// header is set on the HttpClient this class was handed directly, not
    /// through the generated wrapper.
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

            return new PlayerSnapshot
            {
                PlayerId = res.Player.PlayerId.ToString(),
                ServerId = res.Player.ServerId,
                Balances = balances,
                HighestWaveCleared = res.Campaign.HighestWaveCleared,
                BundleVersion = res.Config.BundleVersion,
                MinimumClientVersion = res.Config.MinimumClientVersion,
            };
        }
    }
}
