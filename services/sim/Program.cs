using Broodline.Sim.Service;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();          // the api -> sim contract, Task 3

var app = builder.Build();
app.MapOpenApi();                       // /openapi/v1.json

app.MapGet("/healthz", () => Results.Ok(new { ok = true }));

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
   .WithName("Simulate");

app.Run();

// WebApplicationFactory<Program> needs the implicit entry point to be
// reachable from the test assembly.
public partial class Program { }
