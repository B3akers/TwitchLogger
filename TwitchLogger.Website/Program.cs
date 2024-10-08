using TwitchLogger.Website.Interfaces;
using TwitchLogger.Website.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAntiforgery(options =>
{
    options.FormFieldName = "csrfToken";
    options.HeaderName = "X-Csrf-Token-Value";
});

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddMemoryCache();
builder.Services.AddResponseCaching();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<DatabaseService>();
builder.Services.AddSingleton<IPasswordHasher, PasswordHasher>();
builder.Services.AddSingleton<IJwtTokenHandler, JwtTokenHandlerService>();
builder.Services.AddSingleton<IAccountRepository, AccountRepositoryService>();
builder.Services.AddSingleton<IUserAuthentication, UserAuthenticationService>();
builder.Services.AddSingleton<IChannelRepository, ChannelRepositoryService>();
builder.Services.AddSingleton<IChannelStatsRepository, ChannelStatsRepositoryService>();
builder.Services.AddSingleton<ITwitchAccountRepository, TwitchAccountRepositoryService>();
builder.Services.AddSingleton<IChannelLiveStats, ChannelLiveStatsService>();
builder.Services.AddSingleton<IOptChannelRepository, OptChannelRepositoryService>();

builder.Services.AddHostedService<ConfigureMongoDbService>();
builder.Services.AddHostedService<ChannelUpdateService>();
builder.Services.AddHostedService<LivetimeServerPipeService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseResponseCaching();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}");

app.Run();
