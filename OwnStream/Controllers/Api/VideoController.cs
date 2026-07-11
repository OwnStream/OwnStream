using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OwnStream.ApiModels.Response;
using OwnStream.Database;
using OwnStream.Database.Models;

namespace OwnStream.Controllers.Api;

[ApiController, Route("/api/video/"), Authorize, EnableCors("Api")]
public class VideoController(DatabaseContext db) : Controller
{
	[HttpGet("{id:guid}")]
	public Video? Get(Guid id)
	{
		DatabaseVideo? video = db.Videos
			.Include(x => x.Library)
			.Include(x => x.VideoSegments)
			.FirstOrDefault(x => x.Id == id);

		if (video == null)
		{
			Response.StatusCode = 404;
			return null;
		}

		DatabaseEpisode? ep = db.Episode
			.Include(x => x.ParentContent)
			.FirstOrDefault(x => x.Id == video.EpisodeId);
		return new Video(video)
		{
			Subtitles = GetSubtitles(video),
			PreviewFiles = GetPreviewFiles(video),
			Attachments = GetAttachmentFiles(video),
			Episode = ep != null ? new Episode(ep, HttpContext) : null,
			Content = ep?.ParentContent != null ? new Content(ep.ParentContent, HttpContext) : null,
			Segments = video.VideoSegments.Select(x => new VideoSegment(x))
		};
	}

	[HttpDelete("{id:guid}"), Authorize(Roles = nameof(UserPermissions.WriteVideos))]
	public SuccessResponse DeleteVideo(Guid id, bool deleteEpisode = false)
	{
		DatabaseVideo? video = db.Videos
			.Include(x => x.Library)
			.Include(x => x.VideoSegments)
			.FirstOrDefault(x => x.Id == id);

		if (video == null)
		{
			Response.StatusCode = 404;
			return new SuccessResponse(false, $"Video with ID {id} not found");
		}

		DirectoryInfo dir = new(Path.Join(video.Library.Path, video.Id.ToString()));
		dir.Delete(true);

		if (deleteEpisode)
		{
			DatabaseEpisode? ep = db.Episode
				.Include(x => x.ParentContent)
				.FirstOrDefault(x => x.Id == video.EpisodeId);
			if (ep != null) db.Episode.Remove(ep);
		}

		db.Videos.Remove(video);
		db.SaveChanges();

		return new SuccessResponse(true);
	}

	[HttpGet("orphaned"), Authorize(Roles = nameof(UserPermissions.Admin))]
	public IEnumerable<Video> GetOrphaned()
	{
		return db.Videos
			.Include(x => x.Library)
			.Where(x => x.EpisodeId == null)
			.ToArray()
			.Select(x => new Video(x)
			{
				Subtitles = GetSubtitles(x),
				PreviewFiles = GetPreviewFiles(x),
				Attachments = GetAttachmentFiles(x)
			});
	}

	private SubtitleFile[]? GetSubtitles(DatabaseVideo video)
	{
		string subtitlesDir = Path.Join(video.Library.Path, video.Id.ToString(), "captions");
		if (!Directory.Exists(subtitlesDir)) return null;
		return Directory.GetFiles(subtitlesDir)
			.Select(x => x.Split("/").Last())
			.GroupBy(x => string.Join("", x.Split('.').SkipLast(1)))
			.Select(x =>
			{
				// eng.English.default.3.vtt
				List<string> parts = x.First().Split(".").ToList();
				string lang = parts[0];
				string title = parts[1];
				parts = parts.Skip(2).ToList();
				return new SubtitleFile
				{
					Id = int.Parse(parts[^2]),
					Files = x.ToDictionary(e => e.Split('.')[^1], e => e),
					Default = parts.Contains("default"),
					Forced = parts.Contains("forced"),
					Language = lang,
					Title = title
				};
			}).ToArray();
	}

	private PreviewFile[]? GetPreviewFiles(DatabaseVideo video)
	{
		string subtitlesDir = Path.Join(video.Library.Path, video.Id.ToString(), "trickplay");
		if (!Directory.Exists(subtitlesDir)) return null;
		string[] files = Directory.GetFiles(subtitlesDir).Select(x => Path.GetFileName(x)).ToArray();
		string[] mediumFiles = files.Where(x => x.StartsWith("medium_")).ToArray();
		List<PreviewFile> r = [];
		if (files.Contains("small.png"))
		{
			r.Add(new PreviewFile
			{
				Template = "small.png",
				FrameCount = 1,
				Rows = 10,
				Columns = 10,
				Period = null
			});
		}

		if (mediumFiles.Length > 0)
		{
			r.Add(new PreviewFile
			{
				Template = "medium_%d.png",
				FrameCount = mediumFiles.Length,
				Rows = 5,
				Columns = 5,
				Period = 5
			});
		}

		return r.ToArray();
	}

	private string[]? GetAttachmentFiles(DatabaseVideo video)
	{
		string attachmentsDir = Path.Join(video.Library.Path, video.Id.ToString(), "attachments");
		return Directory.Exists(attachmentsDir)
			? Directory.GetFiles(attachmentsDir).Select(x => Path.GetFileName(x)).ToArray()
			: null;
	}
}