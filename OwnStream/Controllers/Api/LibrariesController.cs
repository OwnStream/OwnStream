using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using OwnStream.ApiModels.Requests;
using OwnStream.ApiModels.Response;
using OwnStream.Database;
using OwnStream.Database.Models;
using OwnStream.Jobs;
using OwnStream.Services;

namespace OwnStream.Controllers.Api;

[ApiController, Route("/api/manage/libraries/"), EnableCors("Api"),
 Authorize(Roles = nameof(UserPermissions.WriteLibraries))]
public class LibrariesController(DatabaseContext db, IFfmpegJobQueueService queueService) : Controller
{
	[HttpGet("list")]
	public IEnumerable<Library> ListLibraries() => db.Libraries.Select(x => new Library(x));

	[HttpGet("{id:guid}")]
	public Library? GetLibrary(Guid id)
	{
		DatabaseLibrary? library = db.Libraries.Find(id);
		if (library == null)
		{
			Response.StatusCode = 404;
			return null;
		}

		return new Library(library, true);
	}

	[HttpPatch("{id:guid}")]
	public Library? PatchLibrary(Guid id, [FromBody] PatchLibraryRequest request)
	{
		DatabaseLibrary? library = db.Libraries.Find(id);
		if (library == null)
		{
			Response.StatusCode = 404;
			return null;
		}

		if (request.Name != null)
			library.Name = request.Name;

		db.SaveChanges();
		return new Library(library);
	}

	[HttpDelete("{id:guid}")]
	public SuccessResponse DeleteLibrary(Guid id, bool deleteMedia = false)
	{
		DatabaseLibrary? library = db.Libraries.Find(id);
		if (library == null)
		{
			Response.StatusCode = 404;
			return new SuccessResponse(false, "Library not found");
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

		db.Libraries.Remove(library);
		db.SaveChanges();
		return new SuccessResponse(true);
	}

	[HttpPost("{id:guid}/recalculateSize")]
	public SuccessResponse RecalculateLibrarySize(Guid id)
	{
		DatabaseLibrary? library = db.Libraries.Find(id);
		if (library == null)
		{
			Response.StatusCode = 404;
			return new SuccessResponse(false, $"Library with ID {id} not found.");
		}

		queueService.EnqueueAsync("RecalculateVideoSizes", "", "", new RecalculateVideoSizesJob.Arguments
		{
			LibraryId = library.Id,
		});

		return new SuccessResponse(true);
	}

	[HttpPost("new")]
	public SuccessResponse<Library> CreateLibrary([FromBody] CreateLibraryRequest request)
	{
		if (!Directory.Exists(request.Path))
			return new SuccessResponse<Library>(false, "Path does not exist");

		try
		{
			string testFile = Path.Combine(request.Path, $".write_test_{Guid.NewGuid()}");
			System.IO.File.WriteAllText(testFile, "test");
			System.IO.File.Delete(testFile);
			Directory.CreateDirectory(testFile);
			Directory.Delete(testFile);
		}
		catch
		{
			return new SuccessResponse<Library>(false, "Path is not writeable");
		}

		DatabaseLibrary library = new()
		{
			Id = Guid.NewGuid(),
			Name = request.Name,
			Path = request.Path
		};
		db.Add(library);
		db.SaveChanges();

		return new SuccessResponse<Library>(true, null, new Library(library));
	}
}