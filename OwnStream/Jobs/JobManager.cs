using System.Reflection;

namespace OwnStream.Jobs;

public class JobManager
{
	private Dictionary<string, Type> jobs = [];

	public void Init()
	{
		IEnumerable<(Type type, JobAttribute? attr)> jobTypes = Assembly.GetExecutingAssembly()
			.GetTypes()
			.Where(x => x.IsAssignableTo(typeof(IJob)))
			.Select(x => (x, x.GetCustomAttribute<JobAttribute>()));

		foreach ((Type type, JobAttribute? attr) in jobTypes)
		{
			if (attr == null) continue;
			jobs.Add(attr.JobName, type);
		}
	}

	public IJob? GetJobInstance(string jobName) =>
		jobs.TryGetValue(jobName, out Type? type) ? Activator.CreateInstance(type) as IJob : null;
}