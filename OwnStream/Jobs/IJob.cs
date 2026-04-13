using OwnStream.Database.Models;

namespace OwnStream.Jobs;

public interface IJob
{
	public void Initialize(IServiceProvider serviceProvider);
	public Task ExecuteJob(Guid jobId, CancellationToken cancellationToken);
}