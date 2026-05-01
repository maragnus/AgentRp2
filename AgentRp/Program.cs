using AgentRp.Components;
using AgentRp.Data;
using AgentRp.Services;
using AgentRp.Session;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContextFactory<RpDbContext>((serviceProvider, options) =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var connectionString = configuration.GetConnectionString("agentrp-db2")
        ?? configuration.GetConnectionString("agentrp-db")
        ?? throw new InvalidOperationException("Connection string 'agentrp-db2' was not found.");

    options.UseSqlServer(connectionString);
});

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddHttpClient();
builder.Services.AddScoped<IAiProviderConnectionService, AiProviderConnectionService>();
builder.Services.AddScoped<IImageGenerationService, ImageGenerationService>();
builder.Services.AddSingleton<IRoleplayPersistence, SqlRoleplayPersistence>();
builder.Services.AddSingleton<ILiveRoleplayStore, LiveRoleplayStore>();
builder.Services.AddScoped<RoleplaySession>();
builder.Services.AddCascadingValue<RoleplaySession>(provider =>
    provider.GetRequiredService<RoleplaySession>());

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<RpDbContext>>();
    await using var dbContext = await dbContextFactory.CreateDbContextAsync();
    await dbContext.Database.MigrateAsync();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAntiforgery();

app.MapGet("/story-images/{imageId}", async (
    string imageId,
    IDbContextFactory<RpDbContext> dbContextFactory,
    CancellationToken cancellationToken) =>
{
    await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
    var image = await dbContext.ImageAssets
        .AsNoTracking()
        .Where(x => x.Id == imageId)
        .Select(x => new { x.Bytes, x.ContentType, x.FileName })
        .FirstOrDefaultAsync(cancellationToken);

    return image is null
        ? Results.NotFound()
        : Results.File(image.Bytes, image.ContentType, image.FileName, enableRangeProcessing: true);
});

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
