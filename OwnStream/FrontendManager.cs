using System.IO.Compression;

namespace OwnStream;

public class FrontendManager(ILogger<FrontendManager> logger)
{
	private readonly HttpClient httpClient = new();

	public async Task<string?> InstallFrontend()
	{
		string source = Utils.GetEnvironmentVariable("OWNSTREAM_FRONTEND_SOURCE") ??
		                "github://github.com/ownstream/ownstream-web";
		Uri sourceUri = new(source);
		switch (sourceUri.Scheme)
		{
			case "http":
			case "https":
			{
				return await InstallFrontendFromUrl(sourceUri);
			}

			//case "github":
			//{
			//	await InstallFrontendFromGithubReleases(sourceUri);
			//	break;
			//}

			default:
			{
				logger.LogCritical("Unknown frontend source: {Source}, a web frontend will not be available.", source);
				return null;
			}
		}
	}

	private async Task<string> InstallFrontendFromUrl(Uri sourceUri)
	{
		logger.LogInformation("Downloading frontend from {Url}", sourceUri);

		HttpResponseMessage response = await httpClient.GetAsync(sourceUri);
		response.EnsureSuccessStatusCode();

		string tmpDir = Directory.CreateTempSubdirectory("os_frontend_").FullName;
		Directory.CreateDirectory(tmpDir);

		string tmpFile = Path.Combine(tmpDir, "frontend.download");
		await using (FileStream fs = new(tmpFile, FileMode.Create))
		{
			await response.Content.CopyToAsync(fs);
		}

		try
		{
			string? contentType = response.Content.Headers.ContentType?.MediaType;
			string fileName = sourceUri.AbsolutePath.Split('/').Last();

			if (contentType == "application/zip" || fileName.EndsWith(".zip"))
			{
				logger.LogInformation("Extracting ZIP archive");
				await ZipFile.ExtractToDirectoryAsync(tmpFile, tmpDir);
			}
			else
			{
				logger.LogError("Unknown archive format: {ContentType}, file: {FileName}", contentType, fileName);
				throw new NotSupportedException($"Unknown archive format: {contentType}");
			}

			logger.LogInformation("Frontend extracted to {TmpDir}", tmpDir);
			return tmpDir;
		}
		finally
		{
			if (File.Exists(tmpFile))
				File.Delete(tmpFile);
		}
	}
}