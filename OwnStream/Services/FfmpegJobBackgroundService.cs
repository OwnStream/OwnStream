using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OwnStream.Database;
using OwnStream.Database.Models;
using OwnStream.Services.JobArguments;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Streams.SubtitleStream;

namespace OwnStream.Services;

public class FfmpegJobBackgroundService(IServiceScopeFactory scopeFactory, ILogger<FfmpegJobBackgroundService> logger)
	: BackgroundService
{
	private readonly SemaphoreSlim singleJobLock = new(1, 1);

	protected override async Task ExecuteAsync(CancellationToken cancellationToken)
	{
		while (!cancellationToken.IsCancellationRequested)
		{
			await singleJobLock.WaitAsync(cancellationToken);
			try
			{
				DatabaseFfmpegJob? job = await DequeueNextPendingJobAsync(cancellationToken);

				if (job is null)
				{
					await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
					continue;
				}

				await ProcessJobAsync(job.Id, cancellationToken);
			}
			catch (OperationCanceledException)
			{
				break;
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Unexpected error in FFmpeg background service");
				await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
			}
			finally
			{
				singleJobLock.Release();
			}
		}
	}

	private async Task<DatabaseFfmpegJob?> DequeueNextPendingJobAsync(CancellationToken cancellationToken)
	{
		using IServiceScope scope = scopeFactory.CreateAsyncScope();
		DatabaseContext db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();


		DatabaseFfmpegJob? job = await db.FfmpegJobs
			.Where(j => j.Status == DatabaseFfmpegJob.JobStatus.Pending)
			.OrderBy(j => j.CreatedAt)
			.FirstOrDefaultAsync(cancellationToken);

		if (job == null) return null;

		job.Status = DatabaseFfmpegJob.JobStatus.Starting;
		job.StartedAt = DateTime.UtcNow;

		await db.SaveChangesAsync(cancellationToken);

		return job;
	}

	private async Task ProcessJobAsync(Guid jobId, CancellationToken cancellationToken)
	{
		using IServiceScope scope = scopeFactory.CreateAsyncScope();
		DatabaseContext db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
		DatabaseFfmpegJob? job =
			await db.FfmpegJobs.FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);

		if (job == null) return;

		try
		{
			logger.LogInformation("Processing job {JobId}", job.Id);
			job.Status = DatabaseFfmpegJob.JobStatus.Processing;
			job.Message = "Reading file...";
			await db.SaveChangesAsync(cancellationToken);

			IMediaInfo media = await FFmpeg.GetMediaInfo(job.InputPath, cancellationToken);
			Conversion conv = new();

			switch (job.JobType)
			{
				case "TranscodeFull":
				{
					TranscodeJobArguments? args = JsonSerializer.Deserialize<TranscodeJobArguments>(job.Arguments);
					if (args == null) throw new Exception("Invalid arguments");

					Directory.CreateDirectory(job.OutputPath);
					conv.AddParameter("-hide_banner", ParameterPosition.PreInput);
					IVideoStream video = media.VideoStreams.First();
					Dictionary<int, string> videoStreams = [];
					Dictionary<int, string> audioStreams = [];
					conv.AddParameter($"-i \"{job.InputPath}\"");
					foreach (TranscodeJobArguments.ResolutionInfo res in args.Resolutions)
					{
						if (video.Width < res.Width) continue;

						if (video.PixelFormat.EndsWith("10le") && res.Codec == "h264_nvenc")
						{
							// h264_nvenc cannot do 10-bit color.
							// TODO: Use an option to either
							//       - skip
							//       - fallback to software
							//       - fallback to another codec (hevc_nvenc)
							res.Codec = "hevc_nvenc";
						}

						int videoIndex = videoStreams.Count;
						conv.AddParameter($"-map 0:v:{video.Index}");
						conv.AddParameter($"-filter:v:{videoIndex} scale={res.Width}:-2");
						conv.AddParameter($"-b:v:{videoIndex} {res.Bitrate}");
						conv.AddParameter($"-c:v:{videoIndex} {res.Codec}");
						videoStreams.Add(videoIndex, res.Name);
					}

					foreach (IAudioStream audio in media.AudioStreams)
					{
						foreach (TranscodeJobArguments.AudioResolutionInfo res in args.AudioResolutions)
						{
							int audioIndex = audioStreams.Count;
							conv.AddParameter($"-map 0:a:{audioIndex}");
							conv.AddParameter($"-b:a:{audioIndex} {res.Bitrate}");
							conv.AddParameter($"-acodec:a:{audioIndex} {res.Codec}");
							conv.AddParameter($"-ac:a:{audioIndex} {res.Channels}");
							audioStreams.Add(audioIndex, audio.Language);
						}
					}

					StringBuilder streamMap = new();
					foreach ((int index, string name) in videoStreams)
						streamMap.Append("v:").Append(index).Append(",agroup:aud,name:").Append(name).Append(' ');
					foreach ((int index, string name) in audioStreams)
					{
						streamMap.Append("a:").Append(index).Append(",agroup:aud,language:").Append(name)
							.Append(",name:").Append(name);
						if (index == 0) streamMap.Append(",default:yes");
						streamMap.Append(' ');
					}

					conv.AddParameter("-var_stream_map \"" + streamMap.ToString().TrimEnd(' ') + '"');
					conv.AddParameter("-f hls");
					conv.AddParameter("-hls_list_size 0");
					conv.AddParameter("-hls_init_time 10");
					conv.AddParameter("-hls_time 5");
					conv.AddParameter("-g 48");
					conv.AddParameter("-keyint_min 48");
					conv.AddParameter("-sc_threshold 0");
					conv.AddParameter("-hls_flags independent_segments");
					conv.AddParameter("-hls_playlist_type vod");
					conv.AddParameter("-hls_segment_type fmp4");
					conv.AddParameter("-hls_segment_filename " + job.OutputPath + "/%v/%05d.ts");
					conv.AddParameter("-master_pl_name master.m3u8");
					conv.SetOutput(job.OutputPath + "/%v/index.m3u8");

					DateTimeOffset lastProgressUpdate = DateTimeOffset.MinValue;
					conv.OnProgress += async (sender, eventArgs) =>
					{
						DateTimeOffset now = DateTimeOffset.UtcNow;
						if (!((now - lastProgressUpdate).TotalSeconds >= 1)) return;
						lastProgressUpdate = now;
						job.Message = "%" + eventArgs.Percent;
						job.Status = DatabaseFfmpegJob.JobStatus.Processing;
						await db.SaveChangesAsync(cancellationToken);
					};
					await conv.Start(cancellationToken);

					ISubtitleStream[] subtitles = media.SubtitleStreams.ToArray();
					if (subtitles.Length > 0)
						Directory.CreateDirectory(Path.Join(job.OutputPath, "captions"));

					for (int i = 0; i < subtitles.Length; i++)
					{
						ISubtitleStream subtitle = subtitles[i];
						job.Message = $"Extracting subtitles {i + 1}/{subtitles.Length}";
						await db.SaveChangesAsync(cancellationToken);

						StringBuilder name = new();
						if (subtitle.Language.Length > 0) name.Append(subtitle.Language);
						if (subtitle.Title.Length > 0) name.Append('.').Append(subtitle.Title);
						if (subtitle.Forced > 0) name.Append(".forced");
						if (subtitle.Default > 0) name.Append(".default");
						name.Append($".{subtitle.Index}.{subtitle.Codec}");
						IConversion subConv = new Conversion()
							.AddStream(subtitle)
							.SetOutput(Path.Join(job.OutputPath, "captions", name.ToString()));
						await subConv.Start(cancellationToken);
						if (subtitle.Codec != "ass") continue;
						subConv = new Conversion()
							.AddStream(subtitle.SetCodec(SubtitleCodec.webvtt))
							.SetOutput(Path.Join(job.OutputPath, "captions", name + ".vtt"));
						await subConv.Start(cancellationToken);
					}

					if (args.DeleteAfterTranscode)
					{
						try
						{
							File.Delete(job.InputPath);
						}
						catch (Exception)
						{
							// Ignored
						}
					}

					DatabaseVideo dbVideo = new()
					{
						Id = args.VideoId,
						EncodingSettings = MD5.HashData(Encoding.UTF8.GetBytes(
							JsonSerializer.Serialize(args.Resolutions) + '\0' +
							JsonSerializer.Serialize(args.AudioResolutions))),
						Width = video.Width,
						Height = video.Height,
						Fps = (int)Math.Round(video.Framerate),
						Language = media.AudioStreams
							.FirstOrDefault(x => x?.Language.Length > 0, media.AudioStreams.FirstOrDefault())
							?.Language ?? "Unknown"
					};
					db.Videos.Add(dbVideo);
					await db.SaveChangesAsync(cancellationToken);

					break;
				}
				default: throw new Exception($"Unexpected job type '{job.JobType}'");
			}

			job.Status = DatabaseFfmpegJob.JobStatus.Completed;
			job.Message = null;
			job.CompletedAt = DateTime.UtcNow;
			await db.SaveChangesAsync(cancellationToken);
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Job {JobId} failed", job.Id);

			job.Status = DatabaseFfmpegJob.JobStatus.Failed;
			job.Message = ex.Message;
			job.CompletedAt = DateTime.UtcNow;
			await db.SaveChangesAsync(cancellationToken);
		}
	}
}