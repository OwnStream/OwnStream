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
builder.Services.AddControllers()
	.AddJsonOptions(options => { options.JsonSerializerOptions.Converters.Add(new DateTimeOffsetConverter()); });
builder.Services.AddDbContext<DatabaseContext>();
builder.Services.AddAuthentication(options =>
	{
		options.DefaultAuthenticateScheme = "ApiToken";
		options.DefaultChallengeScheme = "ApiToken";
	})
	.AddScheme<JwtAuth.SchemeOptions, JwtAuth>("ApiToken", options =>
	{
		string? jwtKey = Environment.GetEnvironmentVariable("JWT_KEY");
		if (jwtKey is null)
			Console.WriteLine(
				"Environment variable JWT_KEY is not set! Using a random JWT key, which means that logins will not be persisted across service restarts.");

		options.JwtKey = Convert.FromHexString(jwtKey ?? new Random().GetHexString(32));
	});
builder.Services.AddAuthorization();
builder.Services.AddSingleton(Configuration.LoadConfiguration());
builder.Services.AddSingleton<JobManager>();
builder.Services.AddSingleton<JobCancellationService>();
builder.Services.AddSingleton<FrontendManager>();
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

string? frontendPath = null;
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

	FrontendManager frontendManager = scope.ServiceProvider.GetRequiredService<FrontendManager>();
	frontendPath = await frontendManager.InstallFrontend();
}

if (!app.Environment.IsDevelopment())
{
	// TODO: Custom JSON-based error response
	// app.UseExceptionHandler("/Home/Error");
	app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("Api");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
	name: "default",
	pattern: "{controller=Home}/{action=Index}/{id?}");

if (frontendPath != null)
	app.MapFallback("{**path}", async context =>
	{
		FileInfo filePath = new(Path.Combine(frontendPath!, context.Request.Path.ToString().TrimStart("/").ToString()));
		FileInfo indexPath = new(Path.Combine(frontendPath!, "index.html"));

		if (filePath.Exists && !Directory.Exists(filePath.FullName))
		{
			string extension = filePath.Extension.ToLowerInvariant();
			context.Response.ContentType = extension switch
			{
				".js" => "application/javascript",
				".css" => "text/css",

				_ => "application/octet-stream"
			};
			await context.Response.SendFileAsync(filePath.FullName);
		}
		else if (indexPath.Exists)
		{
			context.Response.ContentType = "text/html";
			await context.Response.SendFileAsync(indexPath.FullName);
		}
		else
		{
			context.Response.StatusCode = 404;
			await context.Response.WriteAsync("Frontend not installed properly.");
		}
	});
else
	Console.WriteLine("Frontend not installed properly, web app will not be available");

app.Run();