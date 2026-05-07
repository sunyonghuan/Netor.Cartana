using Netor.Cortana.Platform.Entitys;
using Netor.Cortana.Platform.Entitys.Data;
using Netor.Cortana.Platform.Services;
using Netor.Cortana.Platform.Services.Market;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddPlatformDbContext(builder.Configuration);
builder.Services.AddPlatformServices(builder.Configuration);

var app = builder.Build();

await app.InitializeAsync();

var api = app.MapGroup("/api");

api.MapGet("/health", () => Results.Ok(new { status = "ok", application = "Netor.Cortana.Platform.Api" }));

api.MapGroup("/market")
	.MapGet("/assets", async (MarketService marketService, CancellationToken cancellationToken) =>
	{
		var assets = await marketService.GetPublishedAssetsAsync(cancellationToken);
		return Results.Ok(assets);
	});

app.Run();
