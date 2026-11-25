using OwnStream.Database;
using OwnStream.Database.Models;

namespace OwnStream.Managers;

public class TagManager(DatabaseContext db)
{
	public void RecalculateSystemTagsFor(DatabaseContent content)
	{
		DatabaseContentTag[] tags = db.ContentTags.Where(x => x.ContentId == content.Id).ToArray();
		db.ContentTags.RemoveRange(tags.Where(x => x.IsSystem && x.Tag.StartsWith("rating")));
		foreach ((string locale, string rating) in content.AgeRatings)
			AddTag(content.Id, $"rating:{locale}:{rating}");

		Dictionary<string, string>.KeyCollection[] localeKeyLists =
			[content.TranslatedTitle.Keys, content.TranslatedTagline.Keys, content.TranslatedDescription.Keys];
		string[] localeKeys = localeKeyLists.SelectMany(x => x).Distinct().ToArray();
		db.ContentTags.RemoveRange(tags.Where(x => x.IsSystem && x.Tag.StartsWith("hasmetalang")));
		foreach (string localeKey in localeKeys)
			AddTag(content.Id, $"hasmetalang:{localeKey}");
	}

	public void RecalculateSystemTagsFor(DatabaseVideo video, bool clearAll = false, Guid? contentIdOverride = null)
	{
		Guid? contentId = contentIdOverride ?? video.Episode?.ParentContentId;
		if (contentId == null) return;
		DatabaseContentTag[] tags = db.ContentTags.Where(x => x.ContentId == contentId).ToArray();

		if (clearAll)
		{
			db.RemoveRange(tags.Where(x => x.IsSystem && (x.Tag.StartsWith("hasdub:") || x.Tag.StartsWith("hassub:"))));
			tags = [];
		}

		foreach (string lang in video.Language.Split(","))
		{
			string tag = $"hasdub:{lang}";
			if (tags.All(x => x.Tag != tag))
				AddTag(contentId.Value, tag);
		}

		foreach (string lang in video.SubtitleLanguages.Split(","))
		{
			string tag = $"hassub:{lang}";
			if (tags.All(x => x.Tag != tag))
				AddTag(contentId.Value, tag);
		}
	}

	private void AddTag(Guid contentId, string tag)
	{
		db.ContentTags.Add(new DatabaseContentTag
		{
			Id = Guid.NewGuid(),
			ContentId = contentId,
			IsSystem = true,
			Tag = tag
		});
	}
}