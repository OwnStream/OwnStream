using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using OwnStream.ApiModels.Response;
using OwnStream.Database.Models;
using OwnStream.Jobs;
using OwnStream.Services;

namespace OwnStream.Controllers.Api;

[ApiController, Route("/api/maintenance/"), EnableCors("Api"), Authorize(Roles = nameof(UserPermissions.Admin))]
public class MaintenanceController(IFfmpegJobQueueService queueService) : Controller
{
	[HttpGet("recreateFingerprints")]
	public async Task<SuccessResponse> RecreateFingerprints([FromQuery] ScanMissingFingerprintsJob.RecreateType? type,
		[FromQuery] Guid libraryId)
	{
		if (type == null) return new SuccessResponse(false, "RecreateType must be one of: All, IfDoesntExist");

		DatabaseFfmpegJob job = await queueService.EnqueueAsync("ScanMissingFingerprints", "", "",
			new ScanMissingFingerprintsJob.Arguments
			{
				Type = type.Value,
				LibraryId = libraryId
			});
		return new SuccessResponse(true, job.Id.ToString());
	}
}