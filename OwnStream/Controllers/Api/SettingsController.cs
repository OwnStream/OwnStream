using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using OwnStream.Database;
using OwnStream.Database.Models;
using Xabe.FFmpeg;

namespace OwnStream.Controllers.Api;

[ApiController, Route("/api/settings/"), EnableCors("Api")]
public class SettingsController(DatabaseContext db, Configuration config) : Controller
{
	private static Dictionary<string, string> encoders = new()
	{
		["libx264"] = "H.264 (Software)",
		["h264_amf"] = "H.264 (AMD AMF)",
		["h264_nvenc"] = "H.264 (NVIDIA NVENC)",
		["h264_qsv"] = "H.264 (Intel Quick Sync)",
		["h264_vaapi"] = "H.264 (Linux VAAPI)",
		["h264_vulkan"] = "H.264 (Vulkan)",

		["libx265"] = "H.265/HEVC (Software)",
		["hevc_amf"] = "H.265/HEVC (AMD AMF)",
		["hevc_nvenc"] = "H.265/HEVC (NVIDIA NVENC)",
		["hevc_qsv"] = "H.265/HEVC (Intel Quick Sync)",
		["hevc_vaapi"] = "H.265/HEVC (Linux VAAPI)",
		["hevc_vulkan"] = "H.265/HEVC (Vulkan)",

		["libvpx-vp9"] = "VP9 (Software)",
		["vp9_vaapi"] = "VP9 (VAAPI)",
		["vp9_qsv"] = "VP9 (Intel Quick Sync)",

		["libsvtav1"] = "AV1 (Software, SVT-AV1)",
		["librav1e"] = "AV1 (Software, librav1e)",
		["av1_amf"] = "AV1 (AMD AMF)",
		["av1_nvenc"] = "AV1 (NVIDIA NVENC)",
		["av1_qsv"] = "AV1 (Intel Quick Sync)",
		["av1_vaapi"] = "AV1 (Linux VAAPI)",
		["av1_vulkan"] = "AV1 (Vulkan)",
	};

	private static bool benchmarkRunning = false;

	[HttpGet("get"), Authorize(Roles = nameof(UserPermissions.ReadSettings))]
	public IActionResult Get()
	{
		return Json(config);
	}

	[HttpPost("update"), Authorize(Roles = nameof(UserPermissions.WriteSettings))]
	public IActionResult Update([FromBody] Configuration newConfig)
	{
		config.Transcode = newConfig.Transcode;
		config.SaveConfiguration();
		return Json(config);
	}

	[HttpGet("benchmark"), Authorize(Roles = nameof(UserPermissions.Admin))]
	public async Task RunBenchmark()
	{
		Response.ContentType = "text/event-stream";
		Response.Headers.CacheControl = "no-cache";
		Response.Headers.Connection = "keep-alive";
		Dictionary<string, long?> results = new();
		foreach ((string key, string _) in encoders) 
			results[key] = null;

		if (benchmarkRunning) return;
		benchmarkRunning = true;
		await EventStreamOut("encoders", encoders);
		await EventStreamOut("results", results);
		foreach ((string encoder, string label) in encoders)
		{
			if (HttpContext.RequestAborted.IsCancellationRequested) break;
			await EventStreamOut("state", new { encoder, state = "RUNNING" });
			long time = await ExecuteBenchmark(
				"/home/kuylar/bbb_480p_30s_h264.mp4",
				encoder,
				HttpContext.RequestAborted);
			results[encoder] = time;
			await EventStreamOut("state", new { encoder, state = time < 0 ? "FAIL" : "COMPLETE" });
			await EventStreamOut("results", results);
		} 
		await EventStreamOut("results", results);
		await EventStreamOut("done", "[DONE]");
		benchmarkRunning = false;
	}

	private async Task<long> ExecuteBenchmark(string inputFile, string encoder, CancellationToken cancellationToken)
	{
		IConversion conv = new Conversion()
			.AddParameter("-hide_banner")
			.AddParameter($"-i \"{inputFile}\"")
			.AddParameter("-an")
			.AddParameter("-dn")
			.AddParameter("-sn")
			.AddParameter($"-c:v {encoder}")
			.AddParameter("-f null")
			.AddParameter("benchmark");

		try
		{
			Stopwatch sp = Stopwatch.StartNew();
			await conv.Start(cancellationToken);
			sp.Stop();
			return sp.ElapsedMilliseconds;
		}
		catch (Exception _)
		{
			return -1;
		}
	}

	private async Task EventStreamOut(string? eventName, object data)
	{
		if (!string.IsNullOrEmpty(eventName))
		{
			await Response.WriteAsync($"event: {eventName}\n");
		}

		if (data is string)
			await Response.WriteAsync($"data: {data}\n\n");
		else
			await Response.WriteAsync($"data: {JsonSerializer.Serialize(data)}\n\n");

		await Response.Body.FlushAsync();
	}
}