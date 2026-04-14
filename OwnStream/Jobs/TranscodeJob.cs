using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OwnStream.Database;
using OwnStream.Database.Models;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Streams.SubtitleStream;

namespace OwnStream.Jobs;

public class TranscodeJob : IJob
{
	private DatabaseContext db = null!;

	public void Initialize(IServiceProvider serviceProvider)
	{
		db = serviceProvider.GetRequiredService<DatabaseContext>();
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

		Directory.CreateDirectory(job.OutputPath);
		conv.AddParameter("-hide_banner", ParameterPosition.PreInput);
		IVideoStream video = media.VideoStreams.First();
		Dictionary<int, string> videoStreams = [];
		Dictionary<int, string> audioStreams = [];
		conv.AddParameter($"-i \"{job.InputPath}\"");
		foreach (Arguments.ResolutionInfo res in args.Resolutions)
		{
			if (video.Width < res.Width) continue;

			int videoIndex = videoStreams.Count;
			conv.AddParameter($"-map 0:v:{video.Index}");
			conv.AddParameter($"-filter:v:{videoIndex} scale={res.Width}:-2");
			conv.AddParameter($"-b:v:{videoIndex} {res.Bitrate}");
			conv.AddParameter($"-c:v:{videoIndex} {res.Codec}");
			if (video.PixelFormat.EndsWith("10le") && res.Codec == "h264_nvenc")
			{
				// h264_nvenc cannot do 10-bit color.
				// TODO: Use an option to either
				//       - skip
				//       - fallback to software
				//       - fallback to another codec (hevc_nvenc)
				//       - Convert to non-10-bit pixfmt
				conv.AddParameter($"-pix_fmt:v:{videoIndex} {video.PixelFormat.Replace("10le", "")}");
			}

			videoStreams.Add(videoIndex, res.Name);
		}

		foreach (IAudioStream audio in media.AudioStreams)
		{
			foreach (Arguments.AudioResolutionInfo res in args.AudioResolutions)
			{
				int audioIndex = audioStreams.Count;
				conv.AddParameter($"-map 0:a:{audioIndex}");
				conv.AddParameter($"-b:a:{audioIndex} {res.Bitrate}");
				conv.AddParameter($"-acodec:a:{audioIndex} {res.Codec}");
				conv.AddParameter($"-ac:a:{audioIndex} {res.Channels}");
				audioStreams.Add(audioIndex, audio.Language + "-" + audio.Title);
			}
		}

		StringBuilder streamMap = new();
		foreach ((int index, string name) in videoStreams)
			streamMap.Append("v:").Append(index).Append(",agroup:aud,name:").Append(name).Append(' ');
		foreach ((int index, string name) in audioStreams)
		{
			string[] parts = name.Split('-', 2, StringSplitOptions.TrimEntries);
			string title = name.Length switch
			{
				2 => parts[1],
				_ => parts[0]
			};
			if (title.Length == 0) title = parts[0];
			streamMap.Append("a:").Append(index).Append(",agroup:aud,language:").Append(parts[0])
				.Append(",name:").Append($"{index}-{title}");
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
		conv.OnProgress += async (_, eventArgs) =>
		{
			DateTimeOffset now = DateTimeOffset.UtcNow;
			if (!((now - lastProgressUpdate).TotalSeconds >= 5)) return;
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
			name.Append($".{subtitle.Index}");
			try
			{
				string extension = subtitle.Codec switch
				{
					"webvtt" => "vtt",
					"subrip" => "srt",
					"ssa" => "ssa",
					"ass" => "ass",
					_ => throw new IndexOutOfRangeException("Unknown subtitle codec: " + subtitle.Codec)
				};
				IConversion subConv = new Conversion()
					.AddStream(subtitle)
					.SetOutput(Path.Join(job.OutputPath, "captions", $"{name}.{extension}"));
				await subConv.Start(cancellationToken);
				if (subtitle.Codec != "ass") continue;
				subConv = new Conversion()
					.AddStream(subtitle.SetCodec(SubtitleCodec.webvtt))
					.SetOutput(Path.Join(job.OutputPath, "captions", name + ".vtt"));
				await subConv.Start(cancellationToken);
			}
			catch (Exception)
			{
				// Ignored
			}
		}

		DatabaseFfmpegJob trickplayJob = new()
		{
			Id = Guid.NewGuid(),
			JobType = "GenerateTrickplay",
			InputPath = job.InputPath,
			OutputPath = job.OutputPath,
			Arguments = JsonSerializer.Serialize(new GenerateTrickplayJob.Arguments
			{
				DeleteAfterTranscode = args.DeleteAfterTranscode,
				VideoId = args.VideoId,
				LibraryId = args.LibraryId
			}),
			Status = DatabaseFfmpegJob.JobStatus.Pending,
			CreatedAt = DateTimeOffset.UtcNow,
		};

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
		};
		db.FfmpegJobs.AddRange(trickplayJob, metadataJob);
		await db.SaveChangesAsync(cancellationToken);

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
				?.Language ?? "Unknown",
			LibraryId = args.LibraryId
		};
		db.Videos.Add(dbVideo);
		await db.SaveChangesAsync(cancellationToken);
	}

	public class Arguments
	{
		public ResolutionInfo[] Resolutions { get; set; }
		public AudioResolutionInfo[] AudioResolutions { get; set; }
		public bool DeleteAfterTranscode { get; set; }
		public Dictionary<string, string> Metadata { get; set; }
		public Guid VideoId { get; set; }
		public Guid LibraryId { get; set; }

		public class ResolutionInfo
		{
			public string Name { get; set; }
			public int Width { get; set; }
			public int Bitrate { get; set; }
			public string Codec { get; set; }
		}

		public class AudioResolutionInfo
		{
			public int Bitrate { get; set; }
			public int Channels { get; set; }
			public string Codec { get; set; }
		}
	}
}