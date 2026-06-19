using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Domain.Entities;
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
    public async Task<Observation> GetObservationDetails(int locationId, int observationId)
    {
        try
        {
            Observation observation = await observationService.GetObservationDetails(locationId, observationId);
            return observation;
        }
        catch (Exception e)
        {
            logger.LogError(e, "failed to retrieve observation details");
            throw;
        }
    }

    [HttpGet("{locationId}")]
    public async Task<IEnumerable<Observation>> GetObservationsByLocation(int locationId)
    {
        try
        {
            Console.WriteLine("Hello", locationId);
            IEnumerable<Observation> observations = await observationService.GetObservationsByLocation(locationId);
            return observations;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to get observation by location.");
            throw;
        }
    }
}
