using Broodline.Sim.Service;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();          // the api -> sim contract, Task 3

var app = builder.Build();
app.MapOpenApi();                       // /openapi/v1.json

app.MapGet("/healthz", () => Results.Ok(new { ok = true }));

// ONE spelling of the route's name, used twice below: the name the route is
// registered under, and the name the always-200 middleware scopes on. A
// second literal here is a silent scoping failure the moment one is edited.
const string SimulateRouteName = "Simulate";

// ALWAYS 200 - including when the body never binds.
//
// A body that is not JSON, or whose `replay` member is not a string, fails
// minimal-API PARAMETER BINDING and throws BadHttpRequestException. That
// happens before the endpoint delegate runs and therefore before any
// endpoint filter, so a filter cannot see it; middleware is the lowest
// layer that can. Left alone it surfaces as ASP.NET's own 400, and api's
// SimVerdict (services/api/src/sim/client.ts) maps every non-2xx to
// `unavailable` - the retryable branch that leaves the issuance live. A
// submission this service cannot parse is not an outage: it is a fact about
// the submission, it will fail identically on every retry, and it must
// arrive as the service's own `rejected` verdict - design 3.2.
//
// SCOPED BY MATCHED ENDPOINT, NOT BY REQUEST PATH. `ctx.Request.Path ==
// "/internal/simulate"` looks equivalent and is not: routing also matches
// "/internal/simulate/" to this same endpoint (verified by direct
// experiment - a well-formed body posted to the trailing-slash spelling
// returns this endpoint's 200 verdict), while PathString equality, which is
// OrdinalIgnoreCase and so does forgive the "/Internal/Simulate" spelling,
// does NOT forgive the trailing slash. A path comparison would therefore
// answer the SAME endpoint with a verdict on one spelling and a bare 400 on
// another - reintroducing, on a narrower input, exactly the hole this
// middleware exists to close. The endpoint is what has the always-200
// contract, so the endpoint is what is matched on.
//
// The verdict is built by SimulateResponse.Rejected - the same factory
// SimulateEndpoint uses for its own replay_malformed rejections - rather
// than hand-written JSON here, so the two cannot drift in shape, casing or
// engineVersion. A test asserts the two raw bodies are byte-identical.
app.Use(async (ctx, next) =>
{
    try { await next(); }
    catch (BadHttpRequestException) when (
        ctx.GetEndpoint()?.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName
            == SimulateRouteName)
    {
        ctx.Response.StatusCode = 200;
        await ctx.Response.WriteAsJsonAsync(SimulateResponse.Rejected("replay_malformed"));
    }
});

// INTERNAL ingress only - there is no authentication here and there must be
// no public route. Cloud Run's ingress setting is the control (Task 11); the
// path prefix is a reminder, not a guard.
//
// Returns SimulateResponse DIRECTLY rather than Results.Ok(...) - Task 3.
// Handle always returns a SimulateResponse (verified or rejected; it never
// throws out to here), and minimal APIs auto-wrap a plain return value as a
// 200 JSON body exactly like Results.Ok(...) did, so this is not a runtime
// behaviour change. It IS a compile-time one: Results.Ok(SimulateResponse)
// erases to the non-generic IResult, and the OpenAPI generator that Task 3
// depends on to describe this endpoint's response cannot see a response
// type through IResult - it emitted a document with SimulateRequest but no
// SimulateResponse/SimulateEcho/SimulateOutcome/BreachDto at all. Returning
// the record's own type lets MapOpenApi describe the actual response shape,
// which is the entire point of generating this document rather than hand-
// writing it - a schema that never described the response could never
// have caught a drift in it either.
app.MapPost("/internal/simulate",
    (SimulateRequest request) => SimulateEndpoint.Handle(request))
   .WithName(SimulateRouteName);

app.Run();

// WebApplicationFactory<Program> needs the implicit entry point to be
// reachable from the test assembly.
public partial class Program { }
