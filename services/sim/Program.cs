using Broodline.Sim.Service;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();          // the api -> sim contract, Task 3

var app = builder.Build();
app.MapOpenApi();                       // /openapi/v1.json

app.MapGet("/healthz", () => Results.Ok(new { ok = true }));

// INTERNAL ingress only - there is no authentication here and there must be
// no public route. Cloud Run's ingress setting is the control (Task 11); the
// path prefix is a reminder, not a guard.
app.MapPost("/internal/simulate",
    (SimulateRequest request) => Results.Ok(SimulateEndpoint.Handle(request)))
   .WithName("Simulate");

app.Run();

// WebApplicationFactory<Program> needs the implicit entry point to be
// reachable from the test assembly.
public partial class Program { }
