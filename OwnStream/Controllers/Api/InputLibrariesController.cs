using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OwnStream.ApiModels.Requests;
using OwnStream.ApiModels.Response;
using OwnStream.Database;
using OwnStream.Database.Models;
using OwnStream.Jobs;
using OwnStream.Services;

namespace OwnStream.Controllers.Api;

[ApiController, Route("/api/manage/inputLibraries/"), EnableCors("Api"),
 Authorize(Roles = nameof(UserPermissions.WriteLibraries))]
public class InputLibrariesController(DatabaseContext db, IFfmpegJobQueueService queueService) : Controller
{
	[HttpGet("list")]
	public IEnumerable<InputLibrary> ListInputLibraries() => db.InputLibraries.Select(x => new InputLibrary(x));

	[HttpGet("{id:guid}")]
	public InputLibrary? GetInputLibrary(Guid id)
	{
		DatabaseInputLibrary? library = db.InputLibraries.Find(id);
		if (library == null)
		{
			Response.StatusCode = 404;
			return null;
		}

		return new InputLibrary(library);
	}

	[HttpGet("{id:guid}/scan")]
	public SuccessResponse ScanInputLibrary(Guid id)
	{
		DatabaseInputLibrary? library = db.InputLibraries.Find(id);
		if (library == null)
		{
			Response.StatusCode = 404;
			return new SuccessResponse(false, "InputLibrary not found");
		}

		queueService.EnqueueAsync("ScanLibrary", "", "", new ScanLibraryJob.Arguments
		{
			InputLibraryId = library.Id,
		});

		return new SuccessResponse(true);
	}

	[HttpPatch("{id:guid}")]
	public InputLibrary? PatchInputLibrary(Guid id, [FromBody] PatchInputLibraryRequest request)
	{
		DatabaseInputLibrary? library = db.InputLibraries.Find(id);
		if (library == null)
		{
			Response.StatusCode = 404;
			return null;
		}

		if (request.Name != null)
			library.Name = request.Name;

		if (request.Type != null)
			library.Type = ToLibraryType(request.Type);

		if (request.TranscodeLibraryId != null)
		{
			if (!Guid.TryParse(request.TranscodeLibraryId, out Guid transcodeLibraryId))
			{
				Response.StatusCode = 400;
				return null;
			}

			DatabaseLibrary? transcodeLibrary = db.Libraries.FirstOrDefault(l => l.Id == transcodeLibraryId);
			if (transcodeLibrary == null)
			{
				Response.StatusCode = 400;
				return null;
			}
			
			library.TranscodeLibraryId = transcodeLibraryId;
		}

		db.SaveChanges();
		return new InputLibrary(library);
	}

	[HttpDelete("{id:guid}")]
	public SuccessResponse DeleteInputLibrary(Guid id, bool deleteMedia = false)
	{
		DatabaseInputLibrary? library = db.InputLibraries.Find(id);
		if (library == null)
		{
			Response.StatusCode = 404;
			return new SuccessResponse(false, "InputLibrary not found");
		}

		if (deleteMedia)
		{
			try
			{
				Directory.Delete(library.Path);
			}
			catch (Exception e)
			{
				return new SuccessResponse(false, "Failed to delete library: " + e.Message);
			}
		}

		db.InputLibraries.Remove(library);
		db.SaveChanges();
		return new SuccessResponse(true);
	}

	[HttpPost("new")]
	public SuccessResponse<InputLibrary> CreateInputLibrary([FromBody] CreateInputLibraryRequest request)
	{
		if (!Directory.Exists(request.Path))
			return new SuccessResponse<InputLibrary>(false, "Path does not exist");

		try
		{
			DirectoryInfo info = new(request.Path);
			info.GetDirectories();
			info.GetFiles();
		}
		catch
		{
			return new SuccessResponse<InputLibrary>(false, "Path is not readable");
		}

		if (!Guid.TryParse(request.TranscodeLibraryId, out Guid transcodeLibraryId))
			return new SuccessResponse<InputLibrary>(false, "Transcode library ID is not a valid UUID");

		DatabaseLibrary? transcodeLibrary = db.Libraries.FirstOrDefault(l => l.Id == transcodeLibraryId);
		if (transcodeLibrary == null)
			return new SuccessResponse<InputLibrary>(false, "Transcode library does not exist");

		DatabaseInputLibrary library = new()
		{
			Id = Guid.NewGuid(),
			Name = request.Name,
			Path = request.Path,
			Type = ToLibraryType(request.Type),
			TranscodeLibraryId = transcodeLibraryId
		};
		db.Add(library);
		db.SaveChanges();

		return new SuccessResponse<InputLibrary>(true, null, new InputLibrary(library));
	}

	private static DatabaseInputLibrary.InputLibraryType ToLibraryType(string value) => value.ToLower() switch
	{
		"tv" => DatabaseInputLibrary.InputLibraryType.Tv,
		"movie" => DatabaseInputLibrary.InputLibraryType.Movie,
		_ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
	};
}