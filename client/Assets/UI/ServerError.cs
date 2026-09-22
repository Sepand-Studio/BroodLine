using System;
using System.Collections.Generic;
using Broodline.Api;
using Broodline.Net;

namespace Broodline.UI
{
    /// The codes THIS BUILD knows how to act on.
    ///
    /// Mirrors `ErrorCode` in services/api/src/http/errors.ts. It is a mirror
    /// and not a contract: the generated API client types `ErrorResponse.code`
    /// as a bare `string`, so the wire vocabulary can grow without this
    /// enum - and `deployment_mismatch` is itself a code Phase 6 added after
    /// the client's contract was generated.
    ///
    /// `Unrecognised` is a real member, not a failure. See `ServerError`.
    public enum ServerErrorKind
    {
        Unrecognised = 0,

        InvalidRequest,
        IdempotencyKeyReused,
        Unauthorized,
        NotFound,
        Conflict,
        ClientTooOld,
        Internal,

        // Phase 5
        WaveLocked,
        ReplayCapReached,
        IssuanceInvalid,
        SubmissionRejected,
        EngineTooOld,
        SimUnavailable,

        // Phase 6
        DeploymentMismatch,
        RosterFull,
        CreatureNotOwned,
        CreatureCommitted,
        GenerationCeiling,
        InsufficientCharges,

        // Phase 7
        NotAFounder,
        FtueStockUnavailable,
    }

    /// What the screen should OFFER the player next. errors.ts keeps three
    /// distinct codes on one HTTP status precisely so that this can differ per
    /// code - "collapsing them into `conflict` would make the screen guess."
    public enum Remedy
    {
        /// No offer. The reason a code lands here is either that this build
        /// does not recognise it, or that nothing the player can do changes
        /// the outcome - and inventing a button for either is worse than
        /// showing the server's own sentence and stopping.
        None = 0,

        RefreshRoster,
        MakeRoomInRoster,
        WaitForCreature,
        WaitForCharge,
        UpgradeChamber,
        UpdateClient,
        SignInAgain,
        RetryLater,
        StartOver,
        ReportDefect,
    }

    /// A refusal, as the screens see it.
    ///
    /// **THE UNKNOWN-CODE POLICY, which is the point of this file.**
    ///
    /// `Generated.Api` has no compile-time knowledge of the error vocabulary -
    /// `ErrorResponse.Code` is a `string`, and Phase 6 added
    /// `deployment_mismatch` to the server after the client was generated. So
    /// a code this build has never heard of is a NORMAL event, not a defect,
    /// and the three ways of getting it wrong are all worse than handling it:
    ///
    ///   1. Throwing it away and showing "something went wrong" loses the one
    ///      piece of information that identifies the refusal.
    ///   2. Mapping it onto the nearest known code makes the screen offer a
    ///      remedy for a refusal it did not receive.
    ///   3. Treating a refusal it cannot classify as a SUCCESS is the failure
    ///      that spends a splice charge and reports a child.
    ///
    /// What this type does instead:
    ///
    ///   - `Code` is ALWAYS the raw wire string, recognised or not, so logs
    ///     and telemetry carry the real code and the next build can add it.
    ///   - `Kind` is `Unrecognised` and `Remedy` is `None`. The screen shows
    ///     the refusal and offers no action, rather than guessing one.
    ///   - `PlayerMessage` is the SERVER's own sentence either way. Every
    ///     `fail(...)` on the server carries a written message, and it is a
    ///     better sentence for an unknown code than any the client could
    ///     invent. The client still never SWITCHES on that text
    ///     (solo_execution 6.2) - it only displays it.
    ///   - A refusal is never silently a success: every construction path
    ///     returns a non-null `ServerError`, including the one where NSwag
    ///     could not even type the body, which keeps `RawBody` verbatim.
    public sealed class ServerError
    {
        /// The raw `code` string exactly as the server sent it. Empty only
        /// when the response carried no parseable body at all.
        public string Code { get; private set; }

        public ServerErrorKind Kind { get; private set; }

        /// The server's own message. Displayed, never matched on.
        public string PlayerMessage { get; private set; }

        public int StatusCode { get; private set; }

        public Remedy Remedy { get; private set; }

        /// The response body as received, kept when it could not be typed.
        /// Nothing is discarded just because it could not be understood.
        public string RawBody { get; private set; }

        /// The DEVELOPER's text for a failure that produced no response body
        /// at all, and the reason `PlayerMessage` no longer carries it.
        ///
        /// `From(Exception)`'s transport branch used to hand `error.Message`
        /// straight to `PlayerMessage`, so a `TimeoutException` built for a
        /// console became the sentence on a player's screen - see that branch.
        /// Discarding the message instead would have taken a developer's only
        /// record of the failure away with it on a device where nobody can
        /// attach a debugger, so it lands here and in `ToString()`, which this
        /// file already marks "for the log line, not for the player".
        public string Diagnostic { get; private set; }

        public bool IsRecognised { get { return Kind != ServerErrorKind.Unrecognised; } }

        private static readonly Dictionary<string, ServerErrorKind> Known =
            new Dictionary<string, ServerErrorKind>(StringComparer.Ordinal)
            {
                { "invalid_request", ServerErrorKind.InvalidRequest },
                { "idempotency_key_reused", ServerErrorKind.IdempotencyKeyReused },
                { "unauthorized", ServerErrorKind.Unauthorized },
                { "not_found", ServerErrorKind.NotFound },
                { "conflict", ServerErrorKind.Conflict },
                { "client_too_old", ServerErrorKind.ClientTooOld },
                { "internal", ServerErrorKind.Internal },
                { "wave_locked", ServerErrorKind.WaveLocked },
                { "replay_cap_reached", ServerErrorKind.ReplayCapReached },
                { "issuance_invalid", ServerErrorKind.IssuanceInvalid },
                { "submission_rejected", ServerErrorKind.SubmissionRejected },
                { "engine_too_old", ServerErrorKind.EngineTooOld },
                { "sim_unavailable", ServerErrorKind.SimUnavailable },
                { "deployment_mismatch", ServerErrorKind.DeploymentMismatch },
                { "roster_full", ServerErrorKind.RosterFull },
                { "creature_not_owned", ServerErrorKind.CreatureNotOwned },
                { "creature_committed", ServerErrorKind.CreatureCommitted },
                { "generation_ceiling", ServerErrorKind.GenerationCeiling },
                { "insufficient_charges", ServerErrorKind.InsufficientCharges },
                { "not_a_founder", ServerErrorKind.NotAFounder },
                { "ftue_stock_unavailable", ServerErrorKind.FtueStockUnavailable },
            };

        /// The next action per code. Each one is the action errors.ts itself
        /// names in its comment beside that code.
        private static Remedy RemedyFor(ServerErrorKind kind)
        {
            switch (kind)
            {
                // "the roster the screen is showing is stale; refresh it", and
                // "the roster the deployment screen is showing no longer
                // agrees with what was issued".
                case ServerErrorKind.CreatureNotOwned:
                case ServerErrorKind.DeploymentMismatch:
                    return Remedy.RefreshRoster;

                // "splice or retire a creature and claim again"
                case ServerErrorKind.RosterFull:
                    return Remedy.MakeRoomInRoster;

                // "recall it, or wait for the wave to settle"
                case ServerErrorKind.CreatureCommitted:
                    return Remedy.WaitForCreature;

                // "wait for regen, or buy"
                case ServerErrorKind.InsufficientCharges:
                    return Remedy.WaitForCharge;

                // "upgrade the Splicing Chamber - design 5.2 wants the upgrade
                // surfaced, not a bare error"; the details carry the ceiling.
                case ServerErrorKind.GenerationCeiling:
                    return Remedy.UpgradeChamber;

                case ServerErrorKind.ClientTooOld:
                case ServerErrorKind.EngineTooOld:
                    return Remedy.UpdateClient;

                case ServerErrorKind.Unauthorized:
                    return Remedy.SignInAgain;

                case ServerErrorKind.SimUnavailable:
                case ServerErrorKind.Internal:
                case ServerErrorKind.ReplayCapReached:
                    return Remedy.RetryLater;

                case ServerErrorKind.WaveLocked:
                case ServerErrorKind.IssuanceInvalid:
                case ServerErrorKind.SubmissionRejected:
                    return Remedy.StartOver;

                // NOT a defect, and the only 404 a player actually meets.
                // Both splice routes answer `not_found` when a parent is no
                // longer live - a deliberate anti-enumeration choice on the
                // server, made so that a wrong id and an unowned id are
                // indistinguishable. For the player that is an ordinary stale
                // roster: a cache older than a relaunch, or a parent consumed
                // by an earlier splice. Refreshing fixes it, and this assembly
                // already names that remedy for the same condition caught
                // earlier - RosterScreen.CanSplice blocks with "That creature
                // is not loaded. Refresh your roster."
                //
                // Grouping it with the defect codes told those players to file
                // a bug and offered them no button. The residual cost of
                // moving it is the genuine "no player on this server" 404,
                // which refreshing will not fix - but that is a bootstrap
                // error a player cannot hit mid-session, and offering the
                // wrong button there is far cheaper than offering none here.
                // Closing that gap properly needs its own code on the server;
                // `not_found` cannot carry both meanings.
                case ServerErrorKind.NotFound:
                    return Remedy.RefreshRoster;

                // Well-formed requests this client should not have been able to
                // send. Nothing the player can do, so nothing is offered to
                // them - but it is a defect worth reporting.
                //
                // NotAFounder and FtueStockUnavailable join this group rather
                // than getting their own remedy: both are beat-gated actions
                // the client tracks itself from `/v1/sync`'s own facts - only
                // a Founder's Roster entry should ever offer "Name" (bible
                // §3.3), and only the FTUE screen between wave 2 and the
                // player's first splice should ever offer the guided splice's
                // stock grant (`ftue/stock.ts`'s three gates). Reaching
                // either refusal means the screen offered an action it
                // should not have.
                case ServerErrorKind.InvalidRequest:
                case ServerErrorKind.IdempotencyKeyReused:
                case ServerErrorKind.Conflict:
                case ServerErrorKind.NotAFounder:
                case ServerErrorKind.FtueStockUnavailable:
                    return Remedy.ReportDefect;

                // Including Unrecognised. No guessing.
                default:
                    return Remedy.None;
            }
        }

        public static ServerError From(int statusCode, string code, string message)
        {
            var raw = code ?? string.Empty;
            ServerErrorKind kind;
            if (!Known.TryGetValue(raw, out kind)) kind = ServerErrorKind.Unrecognised;

            return new ServerError
            {
                Code = raw,
                Kind = kind,
                PlayerMessage = message ?? string.Empty,
                StatusCode = statusCode,
                Remedy = RemedyFor(kind),
                RawBody = null,
            };
        }

        /// Whatever the generated client threw, as a refusal the screens can
        /// render. Never returns null and never throws: an exception that
        /// reaches a screen unclassified is an exception the screen will treat
        /// as a crash rather than as the refusal it is.
        ///
        /// NSwag only types the body for statuses the OpenAPI document
        /// DECLARES on that operation. Several real refusals fall outside
        /// that - /v1/splice/preview declares no 409, and /v1/wave/submit's
        /// 503 arrives on a route whose other codes are typed - so the untyped
        /// path below is reached in normal operation, not only on a server
        /// fault. It keeps the body verbatim in `RawBody`.
        public static ServerError From(Exception error)
        {
            var typed = error as BroodlineApiException<ErrorResponse>;
            if (typed != null && typed.Result != null)
            {
                var classified = From(typed.StatusCode, typed.Result.Code, typed.Result.Message);
                classified.RawBody = typed.Response;
                return classified;
            }

            var api = error as BroodlineApiException;
            if (api != null)
            {
                // The body is still the one error shape - the server has no
                // other - so the `code` is read out of it rather than thrown
                // away because NSwag had no declared status to bind it to.
                // This is the client switching on `code`, which is the
                // contract (solo_execution 6.2); it is not a second parser for
                // a second shape.
                string code, message;
                ReadErrorBody(api.Response, out code, out message);

                // `api.Message` is NSwag's own assembled string and carries the
                // status and a slice of the body - a diagnostic, not a sentence
                // to show a player. Only the server's `message` is used for
                // that, and a neutral one stands in when there is none.
                var untyped = From(api.StatusCode, code,
                    message.Length > 0 ? message : UnreadableRefusal);
                untyped.RawBody = api.Response;
                return untyped;
            }

            // A transport failure, not a refusal. Status 0 says so.
            //
            // AND `error.Message` DOES NOT REACH THE PLAYER, WHICH IS THE
            // ASYMMETRY THIS FUNCTION USED TO HAVE WITH ITSELF. The branch
            // twenty lines up already refuses to show `api.Message` because it
            // is "a diagnostic, not a sentence to show a player"; this one
            // passed a raw `Exception.Message` through verbatim. On an iPhone
            // 17 that put
            //
            //   "[WaveHost] Wave did not finish within 120s of wall clock. Its
            //    WaveRunner stopped signalling; the scene is unloaded and the
            //    wave abandoned."
            //
            // on `InterruptedView` as the permanent, authoritative explanation
            // of why the game stopped - a bracketed subsystem tag, an internal
            // type name and a sentence about scene management. Phase 9 Task
            // 21g had already written the ruling down at the one call site it
            // touched (`FtueNotice.WalkThrew`: "`PlayerMessage` degrades to
            // the raw `Exception.Message` ... a stack trace wearing a
            // sentence's clothes") and left the other EIGHT able to do it.
            // Counted, not estimated: nine production sites read
            // `PlayerMessage`, all of them in `FtueDirector`, and eight of
            // those are not the one that comment sat on. It is settled here
            // instead, once, where every one of them meets it.
            //
            // TWO SENTENCES BECAUSE THERE ARE TWO SITUATIONS, AND THE SPLIT IS
            // `Retry.IsTransient`'s RATHER THAN A SECOND ONE OF THIS FILE'S.
            // Its division is "did the request reach an answer" - exactly the
            // question that decides which of these is true - and it is already
            // tested against the three shapes Mono actually produces. A
            // `NullReferenceException` out of a request builder is not the
            // server being unreachable and must not claim to be; telling a
            // player their connection failed when this build has a defect is
            // the confidently-wrong explanation this phase keeps finding.
            var transport = From(0, string.Empty,
                Retry.IsTransient(error) ? UnreachableServer : UnexpectedProblem);
            transport.Remedy = Remedy.RetryLater;
            transport.Diagnostic = error == null ? string.Empty : error.Message;
            return transport;
        }

        /// Shown when the server refused but said nothing this client could
        /// read. Deliberately says only what is known to be true.
        public const string UnreadableRefusal = "The server refused this request.";

        /// Shown when the request never reached an answer, so there is no
        /// server sentence to show and no refusal to report.
        ///
        /// IT REPLACES A STRICTLY WORSE SENTENCE, which is the test the broad
        /// fix had to pass: the message a player got here was Mono's own
        /// "An error occurred while sending the request." for a dropped
        /// connection and "A task was canceled." for `HttpClient`'s timeout.
        /// Both name the plumbing and neither names a remedy.
        public const string UnreachableServer =
            "The server could not be reached. Try again.";

        /// Shown when what arrived was neither a refusal nor a transport
        /// failure, which leaves a defect in this build.
        ///
        /// THE SAME SENTENCE AS `FtueNotice.WalkThrew`, DELIBERATELY AND BY
        /// REFERENCE: that constant is said when a defect throws its way out
        /// of the whole walk, this one when a defect is caught at the call
        /// that made it, and a player cannot tell those apart and should not
        /// be given two sentences that mean the same thing. `WalkThrew` is
        /// defined as this string so there is one place to change it.
        public const string UnexpectedProblem =
            "The game hit an unexpected problem. Try again.";

        /// Pull `code` and `message` out of an error body. Never throws: a
        /// body that is not the error shape - an HTML error page from a proxy,
        /// a truncated response - yields empty strings and leaves the refusal
        /// Unrecognised, which is the honest outcome rather than a failure.
        private static void ReadErrorBody(string body, out string code, out string message)
        {
            code = string.Empty;
            message = string.Empty;
            if (string.IsNullOrEmpty(body)) return;

            try
            {
                var parsed = Newtonsoft.Json.Linq.JToken.Parse(body) as Newtonsoft.Json.Linq.JObject;
                if (parsed == null) return;

                var codeToken = parsed["code"];
                if (codeToken != null && codeToken.Type == Newtonsoft.Json.Linq.JTokenType.String)
                {
                    code = codeToken.ToString();
                }

                var messageToken = parsed["message"];
                if (messageToken != null && messageToken.Type == Newtonsoft.Json.Linq.JTokenType.String)
                {
                    message = messageToken.ToString();
                }
            }
            catch (Exception)
            {
                // Not the error shape. Both stay empty.
            }
        }

        /// For the log line, not for the player. Always names the raw code, so
        /// an unrecognised one is visible in telemetry the day it ships.
        public override string ToString()
        {
            return "ServerError(status=" + StatusCode
                + ", code=" + (Code.Length == 0 ? "<none>" : Code)
                + ", recognised=" + IsRecognised
                + ", remedy=" + Remedy
                + (string.IsNullOrEmpty(Diagnostic) ? string.Empty : ", diagnostic=" + Diagnostic)
                + ")";
        }
    }
}
