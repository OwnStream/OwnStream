using System.Collections.Concurrent;

namespace OwnStream.Services;

public class JobCancellationService
{
	private readonly ConcurrentDictionary<Guid, CancellationTokenSource> runningJobs = new();

	public bool Register(Guid jobId, CancellationTokenSource cancellationTokenSource) =>
		runningJobs.TryAdd(jobId, cancellationTokenSource);

	public bool Unregister(Guid jobId, CancellationTokenSource cancellationTokenSource) =>
		runningJobs.TryRemove(new KeyValuePair<Guid, CancellationTokenSource>(jobId, cancellationTokenSource));

	public bool TryCancel(Guid jobId)
	{
		if (!runningJobs.TryGetValue(jobId, out CancellationTokenSource? cancellationTokenSource))
			return false;

		cancellationTokenSource.Cancel();
		return true;
	}
}
