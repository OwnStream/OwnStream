using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using OwnStream;
using OwnStream.Database;
using OwnStream.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<DatabaseContext>();
builder.Services.AddAuthentication(options =>
	{
		options.DefaultAuthenticateScheme = "Cookies";
		options.DefaultChallengeScheme = "Cookies";
	})
	.AddCookie("Cookies", options => { options.LoginPath = "/Auth/Login"; });
builder.Services.AddAuthorization();
builder.Services.AddScoped<IFfmpegJobQueueService, FfmpegJobQueueService>();
builder.Services.AddHostedService<FfmpegJobBackgroundService>();

WebApplication app = builder.Build();

using (IServiceScope scope = app.Services.CreateScope())
{
	DatabaseContext db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
	ILogger<Program> logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

	string[] pendingMigrations = db.Database.GetPendingMigrations().ToArray();
	if (pendingMigrations.Length > 0)
	{
		logger.LogInformation("Applying pending migrations: {Migrations}", string.Join(", ", pendingMigrations));
		db.Database.Migrate();
		logger.LogInformation("Database migrations applied successfully");
	}
}

if (!app.Environment.IsDevelopment())
{
	app.UseExceptionHandler("/Home/Error");
	app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
		name: "default",
		pattern: "{controller=Home}/{action=Index}/{id?}")
	.WithStaticAssets();

app.Run();