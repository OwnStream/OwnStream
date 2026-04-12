using System.Diagnostics;

namespace OwnStream.Database.Models;

public class DatabaseLibrary
{
	public Guid Id { get; set; }
	public string Name { get; set; }
	public string Path { get; set; }
	public ICollection<DatabaseWebhook> Webhooks { get; set; } = new List<DatabaseWebhook>();

	public DiskInfo GetSpaceInfo()
	{
		if (OperatingSystem.IsWindows())
		{
			DriveInfo drive = new(System.IO.Path.GetPathRoot(Path)!);
			return new DiskInfo
			{
				Used = drive.TotalSize - drive.AvailableFreeSpace,
				Available = drive.AvailableFreeSpace,
				Total = drive.TotalSize
			};
		}

		// ReSharper disable once InvertIf
		if (OperatingSystem.IsLinux())
		{
			try
			{
				ProcessStartInfo p = new("df", [Path, "--output=size,used,avail", "-B1"])
				{
					RedirectStandardOutput = true
				};
				Process? process = Process.Start(p);
				if (process == null) throw new Exception();
				process.WaitForExit();
				string data = process.StandardOutput.ReadToEnd();
				long[] sizes = data.Split("\n")[1].Split(" ").Where(x => x.Length > 0).Select(long.Parse).ToArray();
				return new DiskInfo
				{
					Used = sizes[1],
					Available = sizes[2],
					Total = sizes[0]
				};
			}
			catch (Exception _)
			{
				// silently fail
			}
		}

		return new DiskInfo
		{
			Used = 0,
			Available = 0,
			Total = 0
		};
	}
}

public class DiskInfo
{
	public long Used { get; set; }
	public long Available { get; set; }
	public long Total { get; set; }
}