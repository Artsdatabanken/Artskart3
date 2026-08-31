using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Persistence;
using Artskart3.Core.Domain.Entities;
using Artskart3.Core.Domain.Enums;
using Artskart3.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Artskart3.Infrastructure.Persistence.QueryBuilders;

/// <summary>
/// Bygger opp IQueryable-filtre for observasjoner basert på ObservationSearchFilterDto.
/// Delt mellom søk-endepunktet og CSV-eksporten.
/// </summary>
public static class ObservationQueryBuilder
{
    private const string SqlWildcard = "%";

    public static IQueryable<Observation> ApplyFilters(
        IArtsKartDbContext context,
        IQueryable<Observation> query,
        ObservationSearchFilterDto filter)
    {
        query = ApplyTextFilters(query, filter);
        query = ApplyDirectFilters(query, filter);
        query = ApplyIdentifierFilters(context, query, filter);
        query = ApplyAreaFilters(context, query, filter);
        query = ApplyRangeFilters(query, filter);

        return query;
    }

    /// <summary>
    /// Samling, prosjekt/datasett og katalognummer.
    ///
    /// Disse manglet i eksportstien. Konsekvensen var ikke en for stor eksport, men
    /// en avvist en: forhåndstellingen i ExportService talte alle 61M observasjoner
    /// og traff radgrensen, så en bruker som hadde filtrert ned til tre treff fikk
    /// «Antall rader overstiger grensen» i stedet for en fil.
    ///
    /// MERK at TaxonIds, RegistrationStatusId og WithImages fortsatt mangler her —
    /// det er en eldre avvikelse mot SearchRepository.ApplyCommonFilters, ikke noe
    /// CompleteFilter innførte, og den er ikke rettet i denne omgang.
    /// </summary>
    private static IQueryable<Observation> ApplyIdentifierFilters(
        IArtsKartDbContext context,
        IQueryable<Observation> query,
        ObservationSearchFilterDto filter)
    {
        if (filter.CollectionOrgId.HasValue)
        {
            var collectionOrgId = filter.CollectionOrgId.Value;
            query = query.Where(o => o.CollectionOrgId == collectionOrgId);
        }

        if (filter.DatasetOrgId.HasValue)
        {
            var datasetOrgId = filter.DatasetOrgId.Value;
            query = query.Where(o => context.Set<ObservationDataset>()
                .Any(d => d.ObservationId == o.Id && d.DatasetOrgId == datasetOrgId));
        }

        if (filter.ObservationIds?.Any() == true)
        {
            var observationIds = filter.ObservationIds;
            query = query.Where(o => observationIds.Contains(o.Id));
        }

        return query;
    }

    private static IQueryable<Observation> ApplyTextFilters(
        IQueryable<Observation> query,
        ObservationSearchFilterDto filter)
    {
        if (!string.IsNullOrEmpty(filter.PreferredPopularName))
        {
            var pattern = SqlWildcard + filter.PreferredPopularName.EscapeSqlLikePattern() + SqlWildcard;
            query = query.Where(o => EF.Functions.Like(o.Taxon.PreferredPopularName, pattern));
        }

        if (!string.IsNullOrEmpty(filter.ScientificName))
        {
            var pattern = SqlWildcard + filter.ScientificName.EscapeSqlLikePattern() + SqlWildcard;
            query = query.Where(o => EF.Functions.Like(o.MatchedScientificName.ScientificName, pattern));
        }

        if (!string.IsNullOrEmpty(filter.Author))
        {
            var pattern = SqlWildcard + filter.Author.EscapeSqlLikePattern() + SqlWildcard;
            query = query.Where(o => EF.Functions.Like(o.MatchedScientificName.ScientificNameAuthorship, pattern));
        }

        return query;
    }

    private static IQueryable<Observation> ApplyDirectFilters(
        IQueryable<Observation> query,
        ObservationSearchFilterDto filter)
    {
        if (filter.TaxonGroupIds?.Any() == true)
            query = query.Where(o => filter.TaxonGroupIds.Contains(o.TaxonGroupId));

        if (filter.CategoryIds?.Any() == true)
            query = query.Where(o => o.CategoryId != null && filter.CategoryIds.Contains(o.CategoryId.Value));

        if (filter.BehaviorIds?.Any() == true)
            query = query.Where(o => o.Behaviors.Any(b => filter.BehaviorIds.Contains(b.Id)));

        if (filter.BasisOfRecordIds?.Any() == true)
            query = query.Where(o => filter.BasisOfRecordIds.Contains(o.BasisOfRecordId));

        return query;
    }

    private static IQueryable<Observation> ApplyAreaFilters(
        IArtsKartDbContext context,
        IQueryable<Observation> query,
        ObservationSearchFilterDto filter)
    {
        // Område- og organisasjonsfiltre via ObservationEntityIndex-tabellen.
        // Alle entiteter bruker int EntityId — string-Fid-er konverteres til int før spørring.
        // Speiler logikken i SearchRepository slik at CSV-eksporten filtrerer identisk med søk.
        var hasMunicipality = filter.MunicipalityIds?.Any() == true;
        var hasCounty = filter.CountyIds?.Any() == true;
        var hasRestricted = filter.RestrictedAreaIds?.Any() == true;
        var hasOcean = filter.OceanAreaIds?.Any() == true;

        if (hasMunicipality || hasCounty || hasRestricted || hasOcean)
        {
            var municipalityIds = ConvertFidsToInt(filter.MunicipalityIds);
            var countyIds = ConvertFidsToInt(filter.CountyIds);
            var restrictedIds = ConvertRestrictedAreaFidsToInt(filter.RestrictedAreaIds);
            var oceanIds = ConvertFidsToInt(filter.OceanAreaIds);

            query = query.Where(o => context.Set<ObservationEntityIndex>().Any(idx =>
                idx.ObservationId == o.Id && (
                    (idx.EntityTypeId == (int)ObservationIndexEntityType.Municipality && municipalityIds.Contains(idx.EntityId)) ||
                    (idx.EntityTypeId == (int)ObservationIndexEntityType.County && countyIds.Contains(idx.EntityId)) ||
                    // Svalbard/Bjørnøya/Jan Mayen slås opp med fylkes-IDene, som i
                    // SearchRepository. Grenen manglet her, så et fylkesvalg på
                    // Svalbard ga treff på kartet og en tom CSV.
                    (idx.EntityTypeId == (int)ObservationIndexEntityType.SvalbardBjørnøyaAndJanMayen && countyIds.Contains(idx.EntityId)) ||
                    (idx.EntityTypeId == (int)ObservationIndexEntityType.RestrictedArea && restrictedIds.Contains(idx.EntityId)) ||
                    (idx.EntityTypeId == (int)ObservationIndexEntityType.OceanArea && oceanIds.Contains(idx.EntityId))
                )));
        }

        // Institusjonsfilteret er flyttet ut av OR-blokken over, til den
        // denormaliserte kolonnen.
        //
        // SEMANTIKK: institusjon AND-es med områdefiltrene, men flere valgte
        // institusjoner OR-es seg imellom — Contains blir IN (A, B). Velger man
        // Oslo pluss NHM og NINA, betyr det «i Oslo, og fra enten NHM eller NINA».
        // Samme regel gjelder i SearchRepository, både for observasjonssøk og
        // områdetellinger.
        //
        // MERK — DETTE ER EN OPPFØRSELSENDRING. Institusjon lå tidligere som ett
        // av leddene i den samme OR-en, så «Oslo ELLER NHM» ga treff på alt i Oslo
        // pluss alt fra NHM. Søket (SearchRepository.ApplyCommonFilters) har alltid
        // behandlet institusjon som et eget AND-vilkår, altså «i Oslo OG fra NHM».
        // Kommentaren over hevdet at eksporten speilet søket; det gjorde den ikke.
        //
        // Semantikken kunne ikke bevares uansett: institusjon ligger nå i en kolonne
        // på Observation, ikke som rader i indekstabellen, og de radene fjernes i
        // oppryddingssteget. Valget står derfor mellom AND og å beholde en
        // avvikende OR — og AND er det eksporten hele tiden var ment å gjøre.
        if (filter.OrganizationIds?.Any() == true)
        {
            var orgIds = filter.OrganizationIds;
            query = query.Where(o => o.InstitutionOrgId.HasValue && orgIds.Contains(o.InstitutionOrgId.Value));
        }

        return query;
    }

    /// <summary>
    /// Konverterer string-Fid-er til int ved å fjerne "_" (for historiske fylkes-Fid-er som "15_2017").
    /// </summary>
    private static int[] ConvertFidsToInt(string[]? fids)
    {
        if (fids == null || fids.Length == 0) return [];
        return fids
            .Select(fid => int.TryParse(fid.Replace("_", ""), out var id) ? id : (int?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToArray();
    }

    /// <summary>
    /// Konverterer verneområde-Fid-er til int ved å fjerne "Naturbase VV"-prefiks.
    /// </summary>
    private static int[] ConvertRestrictedAreaFidsToInt(string[]? fids)
    {
        if (fids == null || fids.Length == 0) return [];
        return fids
            .Select(fid => int.TryParse(fid.Replace("Naturbase VV", ""), out var id) ? id : (int?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToArray();
    }

    private static IQueryable<Observation> ApplyRangeFilters(
        IQueryable<Observation> query,
        ObservationSearchFilterDto filter)
    {
        if (filter.CoordinatePrecision?.From.HasValue == true)
            query = query.Where(o => o.CoordinatePrecisionInMeters >= filter.CoordinatePrecision.From.Value);

        if (filter.CoordinatePrecision?.To.HasValue == true)
            query = query.Where(o => o.CoordinatePrecisionInMeters <= filter.CoordinatePrecision.To.Value);

        if (filter.Period?.From.HasValue == true)
        {
            var fromDate = new DateTime(filter.Period.From.Value, 1, 1);
            query = query.Where(o => o.DateTimeCollected >= fromDate);
        }

        if (filter.Period?.To.HasValue == true)
        {
            var toDate = new DateTime(filter.Period.To.Value, 12, 31, 23, 59, 59);
            query = query.Where(o => o.DateTimeCollected <= toDate);
        }

        if (filter.Period?.Months?.Any() == true)
        {
            var months = filter.Period.Months;
            query = query.Where(o => o.DateTimeCollected.HasValue && months.Contains(o.DateTimeCollected.Value.Month));
        }

        return query;
    }
}
