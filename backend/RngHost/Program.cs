using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseExceptionHandler();
app.UseSwagger();
app.UseSwaggerUI();

app.MapPost("/pools", (JurisdictionPoolsRequest request) =>
{
    var random = Random.Shared;
    var pools = request.Pools.Select(pool =>
    {
        var results = Enumerable.Range(0, pool.DrawCount)
            .Select(_ => random.Next(int.MaxValue).ToString())
            .ToArray();
        return new PoolResult(pool.PoolId, results, pool.Metadata);
    }).ToList();

    return Results.Ok(new PoolsResponse(Guid.NewGuid().ToString("N"), pools));
});

app.Run();

public sealed record JurisdictionPoolsRequest(IReadOnlyList<PoolRequest> Pools);

public sealed record PoolRequest(string PoolId, int DrawCount, IDictionary<string, object>? Metadata);

public sealed record PoolsResponse(string TransactionId, IReadOnlyList<PoolResult> Pools);

public sealed record PoolResult(string PoolId, IReadOnlyList<string> Results, IDictionary<string, object>? Metadata);
