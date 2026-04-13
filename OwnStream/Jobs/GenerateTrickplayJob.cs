using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OwnStream.Database;
using OwnStream.Database.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Xabe.FFmpeg;

namespace OwnStream.Jobs;

public class GenerateTrickplayJob : IJob
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
		Arguments? args = JsonSerializer.Deserialize<Arguments>(job.Arguments);
		if (args == null) throw new Exception("Invalid arguments");

		job.Message = "Checking for existing trickplay files...";
		await db.SaveChangesAsync(cancellationToken);
		DatabaseLibrary? library = await db.Libraries.FindAsync([args.LibraryId], cancellationToken);
		if (library == null) throw new Exception("Invalid library ID");
		string existingPath = Path.Join(library.Path, args.VideoId.ToString(), "trickplay");
		if (Directory.Exists(existingPath)) Directory.Delete(existingPath, recursive: true);

		Directory.CreateDirectory(job.OutputPath);
		Directory.CreateDirectory(Path.Join(job.OutputPath, "trickplay"));
		IVideoStream video = media.VideoStreams.First();

		DirectoryInfo tmp = Directory.CreateTempSubdirectory("os_trickplay");
		try
		{
			IConversion conv = new Conversion()
				.AddParameter("-hide_banner", ParameterPosition.PreInput)
				.AddStream(video.SetCodec(VideoCodec.png))
				.AddParameter("-vf scale=-2:180")
				.AddParameter("-r .2")
				.SetOutput(Path.Join(tmp.FullName, "trickplay_medium_%d.png"));
			DateTimeOffset lastProgressUpdate = DateTimeOffset.MinValue;
			conv.OnProgress += async (_, eventArgs) =>
			{
				DateTimeOffset now = DateTimeOffset.UtcNow;
				if (!((now - lastProgressUpdate).TotalSeconds >= 1)) return;
				lastProgressUpdate = now;
				job.Message = "%" + (eventArgs.Percent / 2);
				job.Status = DatabaseFfmpegJob.JobStatus.Processing;
				await db.SaveChangesAsync(cancellationToken);
			};
			await conv.Start(cancellationToken);

			await db.SaveChangesAsync(cancellationToken);

			int columns = 5;
			int rows = 5;
			string[][] mediumImages = Directory.GetFiles(tmp.FullName, "trickplay_medium_*.png")
				.OrderBy(f => int.Parse(Path.GetFileNameWithoutExtension(f).Split('_').Last()))
				.Chunk(columns * rows)
				.ToArray();

			for (int i = 0; i < mediumImages.Length; i++)
			{
				string[] images = mediumImages[i];
				using Image firstImage = await Image.LoadAsync(images[0], cancellationToken);
				int tileWidth = firstImage.Width;
				int tileHeight = firstImage.Height;
				int mosaicWidth = tileWidth * columns;
				int mosaicHeight = tileHeight * rows;

				using Image<Rgba32> mosaic = new(mosaicWidth, mosaicHeight);

				for (int j = 0; j < images.Length; j++)
				{
					using Image tile = await Image.LoadAsync(images[j], cancellationToken);
					int x = (j % columns) * tileWidth;
					int y = (j / columns) * tileHeight;

					mosaic.Mutate(ctx => ctx.DrawImage(tile, new Point(x, y), 1f));
				}

				await mosaic.SaveAsPngAsync(Path.Join(job.OutputPath, "trickplay", $"medium_{i}.png"),
					cancellationToken);
			}

			double durationSeconds = video.Duration.TotalSeconds;
			double fps = 100.0 / durationSeconds;
			conv = new Conversion()
				.AddParameter("-hide_banner", ParameterPosition.PreInput)
				.AddStream(video.SetCodec(VideoCodec.png))
				.AddParameter("-vf scale=-2:27")
				.AddParameter($"-r {fps.ToString(System.Globalization.CultureInfo.InvariantCulture)}")
				.SetOutput(Path.Join(tmp.FullName, "trickplay_small_%d.png"));
			lastProgressUpdate = DateTimeOffset.MinValue;
			conv.OnProgress += async (_, eventArgs) =>
			{
				DateTimeOffset now = DateTimeOffset.UtcNow;
				if (!((now - lastProgressUpdate).TotalSeconds >= 1)) return;
				lastProgressUpdate = now;
				job.Message = "%" + ((eventArgs.Percent / 2) + 50);
				job.Status = DatabaseFfmpegJob.JobStatus.Processing;
				await db.SaveChangesAsync(cancellationToken);
			};
			await conv.Start(cancellationToken);

			job.Message = "Building small trickplay...";
			await db.SaveChangesAsync(cancellationToken);

			string[] smallImages = Directory.GetFiles(tmp.FullName, "trickplay_small_*.png")
				.OrderBy(f => int.Parse(Path.GetFileNameWithoutExtension(f).Split('_').Last()))
				.ToArray();

			columns = 10;
			rows = 10;
			if (smallImages.Length > 0)
			{
				using Image firstImage = await Image.LoadAsync(smallImages[0], cancellationToken);
				int tileWidth = firstImage.Width;
				int tileHeight = firstImage.Height;
				int mosaicWidth = tileWidth * columns;
				int mosaicHeight = tileHeight * rows;

				using Image<Rgba32> mosaic = new(mosaicWidth, mosaicHeight);

				for (int i = 0; i < Math.Min(smallImages.Length, 100); i++)
				{
					using Image tile = await Image.LoadAsync(smallImages[i], cancellationToken);
					int x = (i % columns) * tileWidth;
					int y = (i / columns) * tileHeight;

					mosaic.Mutate(ctx => ctx.DrawImage(tile, new Point(x, y), 1f));
				}

				await mosaic.SaveAsPngAsync(Path.Join(job.OutputPath, "trickplay", "small.png"), cancellationToken);
			}

			tmp.Delete(true);

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
		}
		catch (Exception)
		{
			tmp.Delete(true);
			throw;
		}
	}

	public class Arguments
	{
		public bool DeleteAfterTranscode { get; set; }
		public Guid VideoId { get; set; }
		public Guid LibraryId { get; set; }
	}
}