namespace OwnStream.ApiModels.Requests;

public class CreateJobRequest
{
	public string JobType { get; set; }
	public string InputPath { get; set; }
	public string OutputPath { get; set; }
	public string Arguments { get; set; }
}