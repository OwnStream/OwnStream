using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using OwnStream;
using OwnStream.Database;
using OwnStream.Database.Models;
using OwnStream.Jobs;
using OwnStream.JsonConverter;
using OwnStream.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews()
	.AddJsonOptions(options =>
	{
		options.JsonSerializerOptions.Converters.Add(new DateTimeOffsetConverter());
	});
builder.Services.AddDbContext<DatabaseContext>();
builder.Services.AddAuthentication(options =>
	{
		options.DefaultAuthenticateScheme = "Cookies";
		options.DefaultChallengeScheme = "Cookies";
	})
	.AddScheme<JwtAuth.SchemeOptions, JwtAuth>("ApiToken", options =>
	{
		string? jwtKey = Environment.GetEnvironmentVariable("JWT_KEY");
		if (jwtKey is null)
			Console.WriteLine(
				"Environment variable JWT_KEY is not set! Using a random JWT key, which means that logins will not be persisted across service restarts.");

		options.JwtKey = Convert.FromHexString(jwtKey ?? new Random().GetHexString(32));
	})
	.AddCookie("Cookies", options =>
	{
		options.LoginPath = "/Auth/Login";
		options.Events = new CookieAuthenticationEvents
		{
			OnValidatePrincipal = async context =>
			{
				DatabaseContext db = context.HttpContext.RequestServices.GetRequiredService<DatabaseContext>();
				Guid? id = Guid.TryParse(context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "",
					out Guid userId)
					? userId
					: null;
				if (id == null || !db.Users.Any(x => x.Id == id))
				{
					context.RejectPrincipal();
					await context.HttpContext.SignOutAsync("Cookies");
				}
			}
		};
	});
builder.Services.AddAuthorization();
builder.Services.AddSingleton(Configuration.LoadConfiguration());
builder.Services.AddSingleton<JobManager>();
builder.Services.AddSingleton<JobCancellationService>();
builder.Services.AddScoped<IFfmpegJobQueueService, FfmpegJobQueueService>();
builder.Services.AddHostedService<FfmpegJobBackgroundService>();
builder.Services.AddCors(options =>
	{
		options.AddPolicy("Api", policyBuilder =>
		{
			policyBuilder.AllowAnyOrigin();
			policyBuilder.AllowAnyHeader();
			policyBuilder.AllowAnyMethod();
		});	
	});

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

	await db.FfmpegJobs.Where(x =>
			x.Status == DatabaseFfmpegJob.JobStatus.Processing || x.Status == DatabaseFfmpegJob.JobStatus.Starting)
		.ForEachAsync(x =>
		{
			x.Status = DatabaseFfmpegJob.JobStatus.Pending;
			x.Progress = null;
			x.ProgressMax = null;
		});
	await db.SaveChangesAsync();
	
	JobManager jobManager = scope.ServiceProvider.GetRequiredService<JobManager>();
	jobManager.Init();
}

if (!app.Environment.IsDevelopment())
{
	app.UseExceptionHandler("/Home/Error");
	app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("Api");
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
		name: "default",
		pattern: "{controller=Home}/{action=Index}/{id?}")
	.WithStaticAssets();

app.Run();
