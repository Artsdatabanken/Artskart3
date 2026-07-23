using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Artskart3.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/[controller]")]
public class ObservationController(IObservationService observationService, ILogger<ObservationController> logger)
    : ControllerBase
{
    [HttpGet("{locationId}/{observationId}")]
    [Produces("application/json")]
    //TODO Make ObservationDto
    public async Task<ObservationDto> GetObservationDetails(int locationId, int observationId)
    {
        try
        {
            ObservationDto observation = await observationService.GetObservationDetails(locationId, observationId);
            return observation;
        }
        catch (Exception e)
        {
            logger.LogError(e, "failed to retrieve observation details");
            throw;
        }
    }

    [HttpPost]
    public async Task<IEnumerable<ObservationDto>> GetObservationsByLocations(IEnumerable<int> locationIds)
    {
        try
        {
            IEnumerable<ObservationDto> observations = await observationService.GetObservationsByLocations(locationIds);
            return observations;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to get observation by location.");
            throw;
        }
    }
}
