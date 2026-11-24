using System.Net.Http.Json;
using System.Text.Json;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.ExternalModels;
using Artskart3.Core.Application.Persistence;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Artskart3.Infrastructure.Services;

/// <summary>
/// Spør annoteringstjenesten med de samme egenskapene som funndetaljene i gamle Artskart
/// sendte, så regelbaserte annoteringer (art, datasett, fylke, polygon) treffer likt.
/// </summary>
public class ObservationAnnotationService(HttpClient httpClient, IArtsKartDbContext context) : IObservationAnnotationService
{
    // «Objekt» (belagt funn), samme ID som gamle Artskart sendte.
    private const int PreservedSpecimenBasisOfRecordId = 1;
    private const int CountyAreaTypeId = 2;
    private const string ScientificNameField = "ScientificName";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<List<ObservationAnnotationDto>?> GetAsync(int observationId, CancellationToken cancellationToken)
    {
        // Bare offentlige koordinater, som i gamle Artskart. Aldri SensitiveObservationData.
        var observation = await context.Set<Observation>().AsNoTracking()
            .Where(o => o.Id == observationId && !o.IsDeleted)
            .Select(o => new
            {
                o.ProxyId,
                GbifDataSetId = context.Set<ImportState>().Where(s => s.Id == o.NodeId)
                    .Select(s => (Guid?)s.GbifDataSetId).FirstOrDefault(),
                ScientificName = o.MatchedScientificName.ScientificName,
                Collected = o.DateTimeCollected ?? o.DateLastModified,
                o.Latitude,
                o.Longitude,
                County = o.Location != null
                    ? o.Location.Areas.Where(a => !a.IsDeleted && a.IsCurrent && a.AreaTypeId == CountyAreaTypeId)
                        .OrderBy(a => a.Name).Select(a => a.Name).FirstOrDefault()
                    : null,
                o.BasisOfRecordId,
                o.HasAnnotations,
                o.Sensitive,
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (observation is null) return null;

        var properties = new AnnotationObservationProperties
        {
            OccurrenceId = observation.ProxyId ?? "",
            GbifDataSetId = observation.GbifDataSetId ?? Guid.Empty,
            ScientificName = observation.ScientificName ?? "",
            ObservationCollected = observation.Collected,
            Latitude = observation.Latitude,
            Longitude = observation.Longitude,
            StateProvince = observation.County ?? "",
            IsPreservedSpecimen = observation.BasisOfRecordId == PreservedSpecimenBasisOfRecordId,
        };
        using var response = await httpClient.PostAsJsonAsync(
            "api/public-annotations/forObservation", properties, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        var annotations = await response.Content.ReadFromJsonAsync<List<PublicAnnotation>>(JsonOptions, cancellationToken);
        return ToDisplayed(annotations ?? [], observation.HasAnnotations, observation.Sensitive);
    }

    /// <summary>
    /// Tjenesten returnerer en navneendring på artsnivå for alle funn som har det nye navnet,
    /// også funn som alltid har hatt det. Som i gamle Artskart vises den bare når en annotering
    /// faktisk har endret funnet (HasAnnotations).
    ///
    /// Feltendringer vises bare når de er godtatt, og uten tom ny verdi, som importen hopper
    /// over. Aldri på sensitive funn: annotøren kan ha skrevet inn nøyaktig sted eller dato,
    /// som maskeringen ellers skjuler.
    /// </summary>
    private static List<ObservationAnnotationDto> ToDisplayed(
        IEnumerable<PublicAnnotation> annotations, bool hasAnnotations, bool sensitive) =>
        annotations
            .Where(a => hasAnnotations || a.Observations is { Count: > 0 } ||
                a.Fields?.Any(f => f.Name == ScientificNameField) != true)
            .Select(a => new ObservationAnnotationDto
            {
                Author = string.IsNullOrWhiteSpace(a.AuthorName) ? null : a.AuthorName.Trim(),
                Created = a.DateCreated,
                Comment = string.IsNullOrWhiteSpace(a.Comment) ? null : a.Comment.Trim(),
                Changes = !sensitive && a.Actions?.AcceptChanges == true && a.Fields is not null
                    ? a.Fields.Where(f => !string.IsNullOrEmpty(f.NewValue))
                        .Select(f => new ObservationAnnotationChangeDto(f.Name, f.OriginalValue, f.NewValue)).ToList()
                    : [],
            })
            .ToList();
}
