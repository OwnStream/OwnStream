namespace OwnStream.Jobs;

[AttributeUsage(AttributeTargets.Class)]
public class JobAttribute(string jobName) : Attribute
{
	public string JobName { get; } = jobName;
}