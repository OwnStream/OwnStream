using System.Text.Json;
using CoenM.ImageHash;
using CoenM.ImageHash.HashAlgorithms;
using Microsoft.EntityFrameworkCore;
using OwnStream.Database;
using OwnStream.Database.Models;
using Xabe.FFmpeg;

namespace OwnStream.Jobs;

[Job("DetectIntroSections")]
public class DetectIntroSectionsJob : IJob
{
	private DatabaseContext db = null!;
	private const string FingerprintsFileName = "fingerprints.fp";

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
		Arguments? args = JsonSerializer.Deserialize<Arguments>(job.Arguments);

		if (args == null) throw new Exception("Invalid arguments");
		await db.SaveChangesAsync(cancellationToken);

		string fingerprintsPath = Path.Join(job.OutputPath, FingerprintsFileName);
		FingerprintFile fingerprints;
		if (!File.Exists(fingerprintsPath))
		{
			fingerprints = await GenerateFingerprints(job, fingerprintsPath, cancellationToken);
		}
		else
		{
			await using FileStream fs = File.OpenRead(fingerprintsPath);
			fingerprints = FingerprintFile.ReadFromStream(fs);
		}

		job.Message = "Loading other episodes...";
		await db.SaveChangesAsync(cancellationToken);
		DatabaseVideo? thisVideo = db.Videos
			.Include(x => x.Episode)
			.Include(x => x.Episode)
			.ThenInclude(x => x!.ParentContent)
			.ThenInclude(x => x.Episodes)
			.ThenInclude(x => x.Videos)
			.Include(x => x.Episode)
			.ThenInclude(x => x!.ParentContent)
			.ThenInclude(x => x.Library)
			.FirstOrDefault(x => x.Id == args.VideoId);
		OtherEpisode[] otherEpisodes = thisVideo?.Episode?.ParentContent.Episodes
			                               .Where(x => x.Season == thisVideo.Episode?.Season)
			                               .Where(x => x.Id != thisVideo.EpisodeId)
			                               .Select(x => new OtherEpisode
			                               {
				                               VideoId = x.Videos.First().Id,
				                               EpisodeId = x.Id,
				                               LibraryId = x.ParentContent.LibraryId,
				                               SeasonNum = x.Season,
				                               EpisodeNum = x.Episode,
				                               Path = Path.Join(x.ParentContent.Library.Path,
					                               x.Videos.First().Id.ToString())
			                               }).ToArray()
		                               ?? [];
		foreach (OtherEpisode otherEpisode in otherEpisodes) otherEpisode.LoadFile();
		otherEpisodes = otherEpisodes.Where(x => x.File != null).ToArray();

		job.Message = $"Found {otherEpisodes.Length} other episodes to compare to.";
		await db.SaveChangesAsync(cancellationToken);
	}

	public static byte[,] CompareFiles(FingerprintFile file1, FingerprintFile file2)
	{
		byte[,] map = new byte[file1.FrameCount, file2.FrameCount];
		for (int x = 0; x < file1.FrameCount; x++)
		for (int y = 0; y < file2.FrameCount; y++)
			map[x, y] = (byte)Math.Round(CompareHash.Similarity(file1.Fingerprints[x], file2.Fingerprints[y]) * 2.55);
		return map;
	}

	public static SimilarRange[] GetSimilarRanges(byte[,] map, byte threshold, int minimumTime)
	{
		List<SimilarRange> ranges = [];
		int lastX = 0;
		int lastY = 0;

		for (int x = 0; x < map.GetLength(0); x++)
		{
			for (int y = 0; y < map.GetLength(1); y++)
			{
				byte val = map[x, y];
				if (val < threshold)
					continue;

				int continuous = 0;
				while (true)
				{
					continuous++;
					//Console.WriteLine($"[x={x}, y={y}]Checking x={x + continuous}, y={y + continuous} (continuous={continuous})");
					try
					{
						if (map[x + continuous, y + continuous] < threshold)
						{
							bool found = false;
							// check next n frames to reduce the amount of "holes"
							for (int i = 1; i <= 3; i++)
							{
								if (map[x + continuous + i, y + continuous + i] > threshold)
								{
									// if matched, increase continuous to not check again
									continuous += i;
									found = true;
								}
							}

							if (found)
								continue;

							// next frame not in threshold, break out
							break;
						}
					}
					catch (IndexOutOfRangeException)
					{
						// cba to check for bounds
						break;
					}
				}

				// take only if > 20 seconds
				if (continuous <= minimumTime) continue;
				int dx = x - lastX;
				int dy = y - lastY;
				int d = Math.Max(Math.Abs(dx), Math.Abs(dy));
				if (d <= 10)
					ranges.LastOrDefault()?.MergeWithNext = true;

				Console.WriteLine();

				lastX = x + continuous;
				lastY = y + continuous;
				ranges.Add(new SimilarRange
				{
					LeftStart = x,
					RightStart = y,
					LeftDuration = map.GetLength(0),
					RightDuration = map.GetLength(0),
					Duration = continuous,
					Delta = d
				});

				x += continuous - 1;
				y += continuous - 1;
			}
		}

		return ranges.ToArray();
	}

	public class SimilarRange
	{
		public int LeftStart { get; set; }
		public int RightStart { get; set; }
		public int LeftDuration { get; set; }
		public int RightDuration { get; set; }
		public int Duration { get; set; }
		public int Delta { get; set; }
		public bool MergeWithNext { get; set; }
	}

	private async Task<FingerprintFile> GenerateFingerprints(DatabaseFfmpegJob job, string outputDir,
		CancellationToken cancellationToken)
	{
		Conversion conv = new();
		DirectoryInfo tmpDir = Directory.CreateTempSubdirectory("os_fingerprint_");

		conv.AddParameter("-hide_banner", ParameterPosition.PreInput);
		conv.AddParameter("-an", ParameterPosition.PreInput);
		conv.AddParameter("-dn", ParameterPosition.PreInput);
		conv.AddParameter("-sn", ParameterPosition.PreInput);
		conv.AddParameter($"-i \"{job.InputPath}\"");
		conv.AddParameter("-vf scale=128x128,fps=1");
		conv.SetOutput(tmpDir.FullName + "/%07d.png");

		DateTimeOffset lastProgressUpdate = DateTimeOffset.MinValue;
		job.Message = "Extracting frames...";
		conv.OnProgress += async (_, eventArgs) =>
		{
			DateTimeOffset now = DateTimeOffset.UtcNow;
			if (!((now - lastProgressUpdate).TotalSeconds >= 5)) return;
			lastProgressUpdate = now;
			job.Progress = eventArgs.Percent;
			job.ProgressMax = 100;
			job.Status = DatabaseFfmpegJob.JobStatus.Processing;
			await db.SaveChangesAsync(cancellationToken);
		};
		await conv.Start(cancellationToken);
		job.Progress = 100;
		job.Message = "Calculating fingerprints...";
		await db.SaveChangesAsync(cancellationToken);

		IImageHash hashAlgo = new AverageHash();
		string[] files = tmpDir.GetFiles().Select(x => x.FullName).ToArray();

		ulong[] fingerprints = new ulong[files.Length];
		job.ProgressMax = files.Length;
		job.Status = DatabaseFfmpegJob.JobStatus.Processing;
		for (int i = 0; i < files.Length; i++)
		{
			job.Progress = i + 1;
			await db.SaveChangesAsync(cancellationToken);
			string f = files[i];
			int index = int.Parse(Path.GetFileNameWithoutExtension(f)) - 1;
			await using FileStream fs = File.Open(f, FileMode.Open, FileAccess.Read);
			fingerprints[index] = hashAlgo.Hash(fs);
			fs.Close();
			File.Delete(f);
		}

		job.Message = "Saving fingerprints...";
		await db.SaveChangesAsync(cancellationToken);

		FingerprintFile file = new()
		{
			Version = 1,
			// We don't have settings yet, just leave like this for now
			HashSettings = [0xDE, 0xAD, 0xBE, 0xEF],
			FrameCount = (uint)fingerprints.Length,
			Fingerprints = fingerprints
		};
		await File.WriteAllBytesAsync(outputDir, file.EncodeToBytes(), cancellationToken);

		tmpDir.Delete(true);

		return file;
	}

	public class Arguments
	{
		public Guid VideoId { get; set; }
	}

	public class FingerprintFile
	{
		public byte Version { get; set; }
		public byte[] HashSettings { get; set; }
		public uint FrameCount { get; set; }
		public ulong[] Fingerprints { get; set; }

		public byte[] EncodeToBytes()
		{
			using MemoryStream ms = new();
			using BinaryWriter bw = new(ms);
			bw.Write("OSFP"u8.ToArray());
			bw.Write(Version);
			bw.Write(HashSettings.Length);
			bw.Write(HashSettings);
			bw.Write((uint)Fingerprints.Length);
			foreach (ulong fp in Fingerprints)
				bw.Write(fp);
			bw.Flush();
			return ms.ToArray();
		}

		public static FingerprintFile ReadFromStream(Stream str)
		{
			using BinaryReader br = new(str);
			byte[] magic = br.ReadBytes(4);
			if (!magic.SequenceEqual("OSFP"u8.ToArray()))
				throw new InvalidDataException("Invalid fingerprint file magic header");

			byte version = br.ReadByte();
			int hashSettingsLength = br.ReadInt32();
			byte[] hashSettings = br.ReadBytes(hashSettingsLength);
			uint frameCount = br.ReadUInt32();
			ulong[] fingerprints = new ulong[frameCount];
			for (int i = 0; i < frameCount; i++)
				fingerprints[i] = br.ReadUInt64();

			return new FingerprintFile
			{
				Version = version,
				HashSettings = hashSettings,
				FrameCount = frameCount,
				Fingerprints = fingerprints
			};
		}
	}

	public class OtherEpisode
	{
		public Guid VideoId { get; set; }
		public Guid EpisodeId { get; set; }
		public Guid LibraryId { get; set; }
		public int SeasonNum { get; set; }
		public int EpisodeNum { get; set; }
		public string Path { get; set; }
		public FingerprintFile? File { get; set; }

		public void LoadFile()
		{
			if (File != null) return;
			try
			{
				using FileStream fs = System.IO.File.OpenRead(System.IO.Path.Join(Path, FingerprintsFileName));
				File = FingerprintFile.ReadFromStream(fs);
			}
			catch (Exception)
			{
				// ignored
			}
		}
	}

	public static List<SimilarRange> MergeRanges(List<SimilarRange> allRanges)
	{
		if (allRanges.Count == 0)
			return [];

		// Sort by LeftStart
		List<SimilarRange> sorted = allRanges.OrderBy(x => x.LeftStart).ToList();
		List<SimilarRange> merged = [];

		SimilarRange current = new()
		{
			LeftStart = sorted[0].LeftStart,
			LeftDuration = sorted[0].LeftDuration,
			Duration = sorted[0].Duration,
			RightStart = 0,
			RightDuration = 0,
			Delta = 0,
			MergeWithNext = false
		};

		for (int i = 1; i < sorted.Count; i++)
		{
			SimilarRange next = sorted[i];
			int currentEnd = current.LeftStart + current.Duration;
			int nextEnd = next.LeftStart + next.Duration;

			if (next.LeftStart <= currentEnd)
			{
				current.Duration = Math.Max(currentEnd, nextEnd) - current.LeftStart;
			}
			else
			{
				merged.Add(current);
				current = new SimilarRange
				{
					LeftStart = next.LeftStart,
					LeftDuration = next.LeftDuration,
					Duration = next.Duration,
					RightStart = 0,
					RightDuration = 0,
					Delta = 0,
					MergeWithNext = false
				};
			}
		}

		merged.Add(current);
		return merged;
	}
}