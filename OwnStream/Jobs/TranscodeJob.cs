using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using OwnStream.Database;
using OwnStream.Database.Models;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Exceptions;
using Xabe.FFmpeg.Streams.SubtitleStream;

namespace OwnStream.Jobs;

[Job("TranscodeFull")]
public class TranscodeJob : IJob
{
	private DatabaseContext db = null!;
	private Configuration config = null!;

	public void Initialize(IServiceProvider serviceProvider)
	{
		db = serviceProvider.GetRequiredService<DatabaseContext>();
		config = serviceProvider.GetRequiredService<Configuration>();
	}

	public async Task ExecuteJob(Guid jobId, CancellationToken cancellationToken)
	{
		DatabaseFfmpegJob? job = await db.FfmpegJobs.FindAsync([jobId], cancellationToken: cancellationToken);
		if (job == null) throw new Exception($"Job with ID {jobId} not found");
		job.Status = DatabaseFfmpegJob.JobStatus.Processing;
		job.Message = "Reading file...";
		await db.SaveChangesAsync(cancellationToken);

		IMediaInfo media = await FFmpeg.GetMediaInfo(job.InputPath, cancellationToken);
		Conversion conv = new();
		Arguments? args = JsonSerializer.Deserialize<Arguments>(job.Arguments);
		if (args == null) throw new Exception("Invalid arguments");

		if (!config.Transcode.IsValid()) throw new Exception("Invalid transcoding configuration");

		job.Message = "Checking for existing video...";
		await db.SaveChangesAsync(cancellationToken);
		DatabaseVideo? existingVideo = await db.Videos.Include(x => x.Library)
			.FirstOrDefaultAsync(x => x.Id == args.VideoId, cancellationToken: cancellationToken);
		if (existingVideo != null)
		{
			job.Message = "Deleting existing video from the database...";
			await db.SaveChangesAsync(cancellationToken);
			db.Videos.Remove(existingVideo);
			await db.SaveChangesAsync(cancellationToken);
		}

		DatabaseLibrary? library = await db.Libraries.FindAsync([args.LibraryId], cancellationToken);
		if (library == null) throw new Exception("Invalid library ID");
		string existingPath = Path.Join(library.Path, args.VideoId.ToString());
		if (Directory.Exists(existingPath)) Directory.Delete(existingPath, recursive: true);

		DirectoryInfo tmpDir = Directory.CreateTempSubdirectory("os_transcode_");
		DirectoryInfo tmpFingerprintsDir = Directory.CreateTempSubdirectory("os_fingerprint_");
		conv.AddParameter("-hide_banner", ParameterPosition.PreInput);
		IVideoStream video = media.VideoStreams.First();
		List<EncodedVideo> videoStreams = [];
		List<EncodedAudio> audioStreams = [];
		conv.AddParameter($"-i \"{job.InputPath}\"");

		int gVal = (int)Math.Round(video.Framerate * 2);

		// TODO: Make configurable
		List<string> hlsFlags =
		[
			"-f hls",
			"-hls_list_size 0",
			"-hls_init_time 10",
			"-hls_time 10",
			$"-g {gVal}",
			$"-keyint_min {gVal}",
			"-sc_threshold 0",
			"-hls_flags independent_segments",
			"-hls_playlist_type vod",
			"-hls_segment_type fmp4"
		];

		// Preview & Intro Fingerprint generation
		Directory.CreateDirectory(Path.Join(tmpDir.FullName, "imageSequences"));
		List<string> complexFilter =
		[
			"[0:v]fps=1/5,scale=-2:180,tile=5x5[previewmedium]",
			$"[0:v]fps=100/{Math.Floor(video.Duration.TotalSeconds)},scale=-2:27,tile=10x10[previewsmall]",
			"[0:v]fps=1,scale=128x128[fingerprint]",
		];
		List<string> mapArgs = [];
		float videoAspectRatio = (float)video.Width / video.Height;

		foreach (TranscodeConfiguration.VideoPreset res in
		         config.Transcode.VideoPresets.OrderByDescending(x => x.Width))
		{
			if (video.Width < res.Width) continue;

			int videoIndex = videoStreams.Count;
			if (videoIndex >= config.Transcode.MaxVideoStreams) continue;
			string key = "v_" + res.Name;
			string codec = res.Codec;
			complexFilter.Add($"[0:v]scale={res.Width}:-2[{key}]");
			mapArgs.Add($"-map \"[{key}]\"");
			if (video.PixelFormat.EndsWith("10le"))
			{
				// 10-bit color isn't always supported on all clients / encoders
				switch (config.Transcode.PixelFormatHandling)
				{
					case TranscodeConfiguration.PixFmtHandling.DownsampleIfUnsupported:
						if (res.Codec == "h264_nvenc") // TODO: Add more codecs that can't do 10-bit color
							mapArgs.Add($"-pix_fmt {video.PixelFormat.Replace("10le", "")}");
						break;
					case TranscodeConfiguration.PixFmtHandling.DownsampleAlways:
						mapArgs.Add($"-pix_fmt {video.PixelFormat.Replace("10le", "")}");
						break;
					case TranscodeConfiguration.PixFmtHandling.UseSoftware:
						int underscore = res.Codec.IndexOf('_');
						if (underscore > 0)
						{
							string hw = res.Codec[underscore..];
							codec = res.Codec.Replace($"_{hw}", "");
						}

						break;
					default:
						continue;
				}
			}
			
			// DISCLAIMER: The 8 lines below are an AI generated "fix". I have no clue if
			// it actually does what it says or if its just a hallucination.
			// - kuylar
			// -----------------------------------------------------------------------------------
			// MSE-based players (HLS.js, Shaka) require the 'hvc1' sample entry for HEVC in fMP4;
			// FFmpeg defaults to 'hev1', which browsers reject.
			if (codec.Contains("hevc", StringComparison.OrdinalIgnoreCase) ||
			    codec.Contains("265", StringComparison.OrdinalIgnoreCase))
				mapArgs.Add("-tag:v hvc1");
			// NVENC emits non-IDR keyframes by default, breaking 'independent_segments'.
			if (codec.EndsWith("_nvenc", StringComparison.OrdinalIgnoreCase))
				mapArgs.Add("-forced-idr 1");
			// END OF AI CODE --------------------------------------------------------------------

			mapArgs.Add($"-b:v {res.Bitrate}");
			mapArgs.Add($"-c:v {codec}");
			mapArgs.AddRange(hlsFlags);
			mapArgs.Add($"-hls_segment_filename \"{Path.Join(tmpDir.FullName, key, "%05d.m4s")}\"");
			mapArgs.Add($"\"{Path.Join(tmpDir.FullName, key, "index.m3u8")}\"");
			videoStreams.Add(new EncodedVideo
			{
				Key = key,
				Bandwidth = res.Bitrate,
				Resolution = $"{res.Width}x{res.CalculateHeight(videoAspectRatio)}",
				AudioGroup = "aud"
			});
			tmpDir.CreateSubdirectory(key);
		}

		if (videoStreams.Count == 0)
			throw new Exception($"No video streams were selected (input file was {video.Width}x{video.Height})");

		foreach (IAudioStream audio in media.AudioStreams)
		{
			if (config.Transcode.AudioLanguages.Count > 0 &&
			    !config.Transcode.AudioLanguages.Contains(audio.Language) &&
			    audio.Language != "und") continue;
			foreach (TranscodeConfiguration.AudioPreset res in config.Transcode.AudioPresets)
			{
				if (res.Channels > audio.Channels) continue;
				string key = $"a-{res.Codec}_{res.Bitrate}_{res.Channels}-{audio.Index}-{audio.Language}-{audio.Title}";
				int audioIndex = audioStreams.Count;
				complexFilter.Add($"[0:{audio.Index}]anull[{key}]");
				mapArgs.Add($"-map \"[{key}]\"");
				mapArgs.Add($"-b:a {res.Bitrate}");
				mapArgs.Add($"-acodec:a {res.Codec}");
				mapArgs.Add($"-ac:a:{audioIndex} {res.Channels}");
				mapArgs.AddRange(hlsFlags);
				mapArgs.Add($"-hls_segment_filename \"{Path.Join(tmpDir.FullName, key, "%05d.m4s")}\"");
				mapArgs.Add($"\"{Path.Join(tmpDir.FullName, key, "index.m3u8")}\"");
				audioStreams.Add(new EncodedAudio
				{
					Key = key,
					AudioGroup = "aud",
					// TODO: Get full name from audio.Language
					Name = !string.IsNullOrWhiteSpace(audio.Title) ? audio.Title.Trim() : audio.Language,
					Default = audio.Default > 0,
					Language = audio.Language,
					Channels = res.Channels
				});
				tmpDir.CreateSubdirectory(key);
			}
		}

		conv.AddParameter($"-filter_complex \"{string.Join(';', complexFilter)}\"");
		conv.AddParameter(
			$"-map \"[previewmedium]\" \"{Path.Join(tmpDir.FullName, "imageSequences", "preview_medium_%03d.png")}\"");
		conv.AddParameter(
			$"-map \"[previewsmall]\" \"{Path.Join(tmpDir.FullName, "imageSequences", "preview_small_%d.png")}\"");
		conv.AddParameter($"-map \"[fingerprint]\" \"{Path.Join(tmpFingerprintsDir.FullName, "%07d.png")}\"");
		foreach (string a in mapArgs) conv.AddParameter(a);

		job.Message = "Transcoding video...";
		await db.SaveChangesAsync(cancellationToken);
		bool dbLock = false;
		conv.OnDataReceived += async (_, eventArgs) =>
		{
			if (dbLock) return;
			dbLock = true;

			int secs = tmpFingerprintsDir.GetFiles().Length;
			double speed = (secs / DateTimeOffset.UtcNow.Subtract(job.StartedAt ?? new DateTimeOffset()).TotalSeconds);
			job.Message = $"Transcoding at {speed:F2}x speed";

			job.Progress = secs;
			job.ProgressMax = (int)Math.Floor(video.Duration.TotalSeconds);
			job.Status = DatabaseFfmpegJob.JobStatus.Processing;
			await db.SaveChangesAsync(cancellationToken);
			dbLock = false;
		};
		try
		{
			await conv.Start(cancellationToken);
		}
		catch (ConversionException e)
		{
			throw new Exception($"Transcode failed.\n$ ffmpeg {conv.Build().Replace(" -", " \\\n\t-")}\n{e.Message}");
		}

		job.ProgressMax = null;
		job.Message = "Putting the image files in the correct places";
		await db.SaveChangesAsync(cancellationToken);

		string imagesDir = Path.Join(tmpDir.FullName, "imageSequences");
		string previewsDir = Path.Join(tmpDir.FullName, "trickplay");
		Directory.CreateDirectory(previewsDir);
		string[] imageFiles = Directory.GetFiles(imagesDir);
		foreach (string name in imageFiles)
		{
			FileInfo f = new(name);
			if (f.Name.StartsWith("preview_small_"))
			{
				f.MoveTo(Path.Join(previewsDir, "small.png"));
			}
			else if (f.Name.StartsWith("preview_medium_"))
			{
				int index = int.Parse(f.Name.Split('.')[0].Split('_')[^1]);
				f.MoveTo(Path.Join(previewsDir, $"medium_{index}.png"));
			}
		}

		Directory.Delete(imagesDir, true);

		ISubtitleStream[] subtitles = media.SubtitleStreams.ToArray();
		if (subtitles.Length > 0)
			Directory.CreateDirectory(Path.Join(job.OutputPath, "captions"));

		for (int i = 0; i < subtitles.Length; i++)
		{
			ISubtitleStream subtitle = subtitles[i];
			job.Message = $"Extracting subtitles {i + 1}/{subtitles.Length}";
			job.Progress = i;
			job.ProgressMax = subtitles.Length;
			await db.SaveChangesAsync(cancellationToken);

			StringBuilder name = new();
			name.Append(subtitle.Language).Append('.').Append(subtitle.Title ?? subtitle.Language);
			if (subtitle.Forced > 0) name.Append(".forced");
			if (subtitle.Default > 0) name.Append(".default");
			name.Append($".{subtitle.Index}");
			try
			{
				bool isBitmap = subtitle.Codec == "hdmv_pgs_subtitle";
				string extension = subtitle.Codec switch
				{
					"webvtt" => "vtt",
					"subrip" => "srt",
					"ssa" => "ssa",
					"ass" => "ass",
					"hdmv_pgs_subtitle" => "sup",
					_ => throw new IndexOutOfRangeException("Unknown subtitle codec: " + subtitle.Codec)
				};
				IConversion subConv = new Conversion()
					.AddStream(subtitle)
					.AddParameter("-y")
					.SetOutput(Path.Join(job.OutputPath, "captions", $"{name}.{extension}"));
				await subConv.Start(cancellationToken);

				// Try to convert text-based subtitles to WebVTT for web playback
				if (isBitmap || extension == "vtt") continue;
				subConv = new Conversion()
					.AddStream(subtitle.SetCodec(SubtitleCodec.webvtt))
					.AddParameter("-y")
					.SetOutput(Path.Join(job.OutputPath, "captions", $"{name}.vtt"));
				await subConv.Start(cancellationToken);
			}
			catch (Exception)
			{
				// Ignored
			}
		}

		JsonObject probeRes = JsonSerializer.Deserialize<JsonObject>(await new Probe().Start(
			$"-v quiet -show_chapters -show_streams -select_streams t -of json \"{job.InputPath}\"",
			cancellationToken))!;
		JsonArray streams = probeRes["streams"]?.AsArray() ?? [];
		JsonArray chapters = probeRes["chapters"]?.AsArray() ?? [];
		if (streams.Count > 0)
		{
			DirectoryInfo attachmentsDir = tmpDir.CreateSubdirectory("attachments");
			for (int i = 0; i < streams.Count; i++)
			{
				JsonObject stream = streams[i]!.AsObject();
				try
				{
					string filename = stream["tags"]?["filename"]?.GetValue<string>()!;
					job.Message = $"[{filename}] Exporting attachment...";
					job.Progress = i + 1;
					job.ProgressMax = streams.Count;
					await db.SaveChangesAsync(cancellationToken);
					Conversion dumpConv = new();
					dumpConv.AddParameter(
						$"-dump_attachment:{stream["index"]!.GetValue<int>()} \"{Path.Join(attachmentsDir.FullName, filename)}\"");
					dumpConv.AddParameter($"-i \"{job.InputPath}\"");
					await dumpConv.Start(cancellationToken);
				}
				catch (Exception)
				{
					continue;
				}
			}
		}

		if (chapters.Count > 0)
		{
			StringBuilder sb = new();
			sb.AppendLine("WEBVTT").AppendLine();
			foreach (JsonObject chapter in chapters.Select(x => x!.AsObject()))
			{
				double start = double.Parse(chapter["start_time"]?.GetValue<string>() ?? "0",
					CultureInfo.InvariantCulture);
				double end = double.Parse(chapter["end_time"]?.GetValue<string>() ?? "0", CultureInfo.InvariantCulture);
				TimeSpan startTime = TimeSpan.FromSeconds(start);
				TimeSpan endTime = TimeSpan.FromSeconds(end);

				sb.AppendLine($@"{startTime:hh\:mm\:ss\.fff} --> {endTime:hh\:mm\:ss\.fff}")
					.AppendLine(chapter["tags"]?["title"]?.GetValue<string>() ?? "Untitled Chapter")
					.AppendLine();
			}

			await File.WriteAllTextAsync(Path.Join(tmpDir.FullName, "chapters.vtt"), sb.ToString(), cancellationToken);
		}

		job.Message = "Generating the master playlist...";
		await db.SaveChangesAsync(cancellationToken);

		StringBuilder hls = new();
		hls.AppendLine("#EXTM3U");
		hls.AppendLine("#EXT-X-VERSION:7");
		foreach (EncodedAudio ea in audioStreams)
		{
			hls.Append("#EXT-X-MEDIA:TYPE=AUDIO,")
				.Append($"GROUP-ID=\"{ea.AudioGroup}\",")
				.Append($"NAME=\"{ea.Name}\",")
				.Append($"DEFAULT={(ea.Default ? "YES" : "NO")},")
				.Append($"LANGUAGE=\"{ea.Language}\",")
				.Append($"CHANNELS=\"{ea.Channels}\",")
				.AppendLine($"URI=\"{ea.Key}/index.m3u8\"");
		}

		foreach (EncodedVideo ev in videoStreams)
		{
			ProcessStartInfo psi = new("ffprobe",
				[Path.Join(tmpDir.FullName, ev.Key, "index.m3u8"), "-show_streams", "-print_format", "json"])
			{
				RedirectStandardOutput = true
			};
			Process p = Process.Start(psi)!;
			await p.WaitForExitAsync(cancellationToken);
			string? codec = JsonSerializer.Deserialize<JsonObject>(p.StandardOutput.BaseStream)?
				["streams"]?[0]?["mime_codec_string"]?.GetValue<string>();
			hls.AppendLine()
				.Append("#EXT-X-STREAM-INF:")
				.Append($"BANDWIDTH={ev.Bandwidth},")
				.Append($"RESOLUTION={ev.Resolution},");
			if (codec != null)
				hls.Append($"CODECS=\"{codec}\",");
			hls.AppendLine($"AUDIO=\"{ev.AudioGroup}\"");
			hls.AppendLine(ev.Key + "/index.m3u8");
		}

		await File.WriteAllTextAsync(Path.Join(tmpDir.FullName, "master.m3u8"), hls.ToString(), cancellationToken);

		job.ProgressMax = null;
		job.Message = "Moving the video file to the library";
		await db.SaveChangesAsync(cancellationToken);
		long size = 0;
		List<FileInfo> fileList = [];
		fileList.AddRange(tmpDir.GetDirectories().SelectMany(x => x.GetFiles()));
		fileList.AddRange(tmpDir.GetFiles());
		Directory.CreateDirectory(job.OutputPath);
		job.ProgressMax = fileList.Count;
		for (int i = 0; i < fileList.Count; i++)
		{
			FileInfo fileInfo = fileList[i];
			size += fileInfo.Length;
			string targetPath = fileInfo.FullName.Replace(tmpDir.FullName, job.OutputPath);
			string? dir = Path.GetDirectoryName(targetPath);
			if (dir != null && !Directory.Exists(dir))
				Directory.CreateDirectory(dir);
			fileInfo.MoveTo(targetPath, true);
			job.Progress = i + 1;
			await db.SaveChangesAsync(cancellationToken);
		}

		job.Message = "Verifying video...";
		await db.SaveChangesAsync(cancellationToken);

		IMediaInfo info = await FFmpeg.GetMediaInfo(Path.Join(job.OutputPath, "master.m3u8"), cancellationToken);
		IVideoStream bestVideoStream = info.VideoStreams.MaxBy(x => x.Width)!;
		string lang = string.Join(",", media.AudioStreams.Select(x => x.Language));
		if (lang.Length == 0) lang = "und";

		DatabaseVideo dbVideo = new()
		{
			Id = args.VideoId,
			EncodingSettings = config.Transcode.GetHash(),
			Width = bestVideoStream.Width,
			Height = bestVideoStream.Height,
			Fps = (int)Math.Round(bestVideoStream.Framerate),
			Length = (int)Math.Round(bestVideoStream.Duration.TotalMilliseconds),
			Language = lang,
			LibraryId = args.LibraryId,
			Size = size
		};
		db.Videos.Add(dbVideo);
		job.RelevantVideoId = args.VideoId;
		job.Message = "Transcoding complete.";
		if (args.DeleteAfterTranscode)
		{
			try
			{
				File.Delete(job.InputPath);
			}
			catch (Exception e)
			{
				job.Message = $"Transcoding complete, but failed to delete the input file ({e.Message})";
			}
		}

		if (args.Metadata != null)
		{
			DatabaseFfmpegJob metadataJob = new()
			{
				Id = Guid.NewGuid(),
				JobType = "FetchMetadata",
				InputPath = "",
				OutputPath = "",
				Arguments = JsonSerializer.Serialize(new FetchMetadataJob.Arguments
				{
					VideoId = args.VideoId,
					LibraryId = args.LibraryId,
					Type = args.Metadata["type"],
					Season = int.Parse(args.Metadata["season"]),
					Episode = int.Parse(args.Metadata["episode"]),
					ProviderIds = args.Metadata.Where(x => x.Key.EndsWith("id"))
						.ToDictionary(x => x.Key[..^2], x => x.Value)
				}),
				Status = DatabaseFfmpegJob.JobStatus.Pending,
				// We want it done as soon as possible, so abuse this field to make it run right after this job
				CreatedAt = job.CreatedAt.AddSeconds(1),
				RelevantVideoId = job.RelevantVideoId,
				RelevantEpisodeId = job.RelevantEpisodeId,
				RelevantContentId = job.RelevantContentId,
				RelevantLibraryId = job.RelevantLibraryId,
				RelevantWebhookId = job.RelevantWebhookId,
			};
			db.FfmpegJobs.AddRange(metadataJob);
		}

		DatabaseFfmpegJob fingerprintsJob = new()
		{
			Id = Guid.NewGuid(),
			JobType = "DetectIntroSections",
			InputPath = tmpFingerprintsDir.FullName,
			OutputPath = job.OutputPath,
			Arguments = JsonSerializer.Serialize(new DetectIntroSectionsJob.Arguments
			{
				VideoId = args.VideoId
			}),
			Status = DatabaseFfmpegJob.JobStatus.Pending,
			CreatedAt = DateTimeOffset.UtcNow,
			RelevantVideoId = job.RelevantVideoId,
			RelevantEpisodeId = job.RelevantEpisodeId,
			RelevantContentId = job.RelevantContentId,
			RelevantLibraryId = job.RelevantLibraryId,
			RelevantWebhookId = job.RelevantWebhookId,
		};
		db.FfmpegJobs.AddRange(fingerprintsJob);
		await db.SaveChangesAsync(cancellationToken);
	}

	public class Arguments
	{
		public bool DeleteAfterTranscode { get; set; }
		public Dictionary<string, string>? Metadata { get; set; }
		public Guid VideoId { get; set; }
		public Guid LibraryId { get; set; }
	}

	private class EncodedVideo
	{
		public string Key { get; set; }
		public int Bandwidth { get; set; }
		public string Resolution { get; set; }
		public string AudioGroup { get; set; }
	}

	private class EncodedAudio
	{
		public string Key { get; set; }
		public string AudioGroup { get; set; }
		public string Name { get; set; }
		public bool Default { get; set; }
		public string Language { get; set; }
		public int Channels { get; set; }
	}
}