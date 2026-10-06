using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Persistence;
using Artskart3.Core.Application.Services;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Domain.Entities;
using Artskart3.Core.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Artskart3.Infrastructure.Persistence.QueryBuilders;

/// <summary>
/// Bygger opp IQueryable-filtre for observasjoner basert på ObservationSearchFilterDto.
/// Delt mellom søk-endepunktet og CSV-eksporten.
/// </summary>
public static class ObservationQueryBuilder
{
    public static IQueryable<Observation> ApplyFilters(
        IArtsKartDbContext context,
        ITaxonHierarchyService taxonHierarchy,
        IQueryable<Observation> query,
        ObservationSearchFilterDto filter)
    {
        query = ApplyDirectFilters(query, filter);
        query = ApplyTaxonFilter(context, taxonHierarchy, query, filter);
        query = ApplyIdentifierFilters(context, query, filter);
        query = ApplyAreaFilters(context, query, filter);
        query = ApplyRangeFilters(query, filter);

        return query;
    }

    private static IQueryable<Observation> ApplyTaxonFilter(
        IArtsKartDbContext context,
        ITaxonHierarchyService taxonHierarchy,
        IQueryable<Observation> query,
        ObservationSearchFilterDto filter)
    {
        if (filter.TaxonIds?.Any() != true)
            return query;

        // Samme normalisering som i SearchRepository: eksporten må filtrere identisk
        // med søket. Etterkommere som allerede er dekket av et annet valgt takson
        // fjernes — deres UNION-gren ville bare gjentatt rader grenen over ga.
        IQueryable<int>? combinedQuery = null;
        foreach (var taxonId in taxonHierarchy.RemoveRedundantDescendants(filter.TaxonIds)!)
        {
            var subquery = GetObservationIdsByTaxonHierarchy(context, taxonHierarchy, taxonId);
            combinedQuery = combinedQuery == null ? subquery : combinedQuery.Union(subquery);
        }

        return query.Where(o => combinedQuery!.Contains(o.Id));
    }

    public static IQueryable<int> GetObservationIdsByTaxonHierarchy(
        IArtsKartDbContext context,
        ITaxonHierarchyService taxonHierarchy,
        int taxonId)
    {
        var rankId = taxonHierarchy.GetTaxonRankId(taxonId);
        var h = context.Set<ObservationTaxonHierarchy>();

        return rankId switch
        {
            1  => h.Where(x => x.KingdomTaxonId == taxonId).Select(x => x.ObservationId),
            2  => h.Where(x => x.SubkingdomTaxonId == taxonId).Select(x => x.ObservationId),
            3  => h.Where(x => x.PhylumTaxonId == taxonId).Select(x => x.ObservationId),
            4  => h.Where(x => x.SubphylumTaxonId == taxonId).Select(x => x.ObservationId),
            5  => h.Where(x => x.SuperclassTaxonId == taxonId).Select(x => x.ObservationId),
            6  => h.Where(x => x.ClassTaxonId == taxonId).Select(x => x.ObservationId),
            7  => h.Where(x => x.SubclassTaxonId == taxonId).Select(x => x.ObservationId),
            8  => h.Where(x => x.InfraclassTaxonId == taxonId).Select(x => x.ObservationId),
            9  => h.Where(x => x.CohortTaxonId == taxonId).Select(x => x.ObservationId),
            10 => h.Where(x => x.SuperorderTaxonId == taxonId).Select(x => x.ObservationId),
            11 => h.Where(x => x.OrderTaxonId == taxonId).Select(x => x.ObservationId),
            12 => h.Where(x => x.SuborderTaxonId == taxonId).Select(x => x.ObservationId),
            13 => h.Where(x => x.InfraorderTaxonId == taxonId).Select(x => x.ObservationId),
            14 => h.Where(x => x.SuperfamilyTaxonId == taxonId).Select(x => x.ObservationId),
            15 => h.Where(x => x.FamilyTaxonId == taxonId).Select(x => x.ObservationId),
            16 => h.Where(x => x.SubfamilyTaxonId == taxonId).Select(x => x.ObservationId),
            17 => h.Where(x => x.TribeTaxonId == taxonId).Select(x => x.ObservationId),
            18 => h.Where(x => x.SubtribeTaxonId == taxonId).Select(x => x.ObservationId),
            19 => h.Where(x => x.GenusTaxonId == taxonId).Select(x => x.ObservationId),
            20 => h.Where(x => x.SubgenusTaxonId == taxonId).Select(x => x.ObservationId),
            21 => h.Where(x => x.SectionTaxonId == taxonId).Select(x => x.ObservationId),
            22 => h.Where(x => x.SpeciesTaxonId == taxonId).Select(x => x.ObservationId),
            23 => h.Where(x => x.SubspeciesTaxonId == taxonId).Select(x => x.ObservationId),
            24 => h.Where(x => x.VarietyTaxonId == taxonId).Select(x => x.ObservationId),
            25 => h.Where(x => x.FormTaxonId == taxonId).Select(x => x.ObservationId),
            26 => h.Where(x => x.NotSetTaxonId == taxonId).Select(x => x.ObservationId),
            _  => h.Where(x => x.ObservationId == -1).Select(x => x.ObservationId) // ukjent rang, tomt resultat
        };
    }

    private static IQueryable<Observation> ApplyIdentifierFilters(
        IArtsKartDbContext context,
        IQueryable<Observation> query,
        ObservationSearchFilterDto filter)
    {
        if (filter.DatasetOrgId.HasValue)
        {
            var datasetOrgId = filter.DatasetOrgId.Value;
            query = query.Where(o => o.DatasetOrgId == datasetOrgId);
        }

        if (filter.ProjectOrgId.HasValue)
        {
            var projectOrgId = filter.ProjectOrgId.Value;
            query = query.Where(o => context.Set<ObservationProject>()
                .Any(d => d.ObservationId == o.Id && d.ProjectOrgId == projectOrgId));
        }

        if (filter.ObservationIds?.Any() == true)
        {
            var observationIds = filter.ObservationIds;
            query = query.Where(o => observationIds.Contains(o.Id));
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

        // Atferd — denormalisert kolonne. Verdiene filtreres til tinyint-området
        // FØR castet: C# caster unchecked, så (byte)257 ville blitt 1.
        if (filter.BehaviorIds?.Any() == true)
        {
            var behaviorIds = filter.BehaviorIds
                .Where(id => id is >= byte.MinValue and <= byte.MaxValue)
                .Select(id => (byte)id)
                .ToList();

            query = behaviorIds.Count == 0
                ? query.Where(o => false)
                : query.Where(o => o.BehaviorId.HasValue && behaviorIds.Contains(o.BehaviorId.Value));
        }

        if (filter.BasisOfRecordIds?.Any() == true)
            query = query.Where(o => filter.BasisOfRecordIds.Contains(o.BasisOfRecordId));

        // Registreringsstatus er nå en denormalisert kolonne, ikke utledet av
        // tagger. Verdiene er uendret og speiler SearchRepository én-til-én:
        //   1 = funnet, 2 = ikke registrert (Absent), 3 = ikke gjenfunnet
        // Kolonnen fylles av samme CASE som ObservationEntityIndex bruker.
        //
        // At 1 tidligere var et NEGATIVT vilkår er nettopp poenget med kolonnen:
        // manglet filteret i eksporten, fikk man med de radene brukeren hadde
        // filtrert bort. Nå er alle tre likestilte likhetspredikater.
        if (filter.RegistrationStatusId.HasValue)
        {
            var registrationStatusId = (byte)filter.RegistrationStatusId.Value;
            query = query.Where(o => o.RegistrationStatusId == registrationStatusId);
        }

        if (filter.WithImages.HasValue)
        {
            var withImages = filter.WithImages.Value;
            query = query.Where(o => o.HasMediaFiles == withImages);
        }

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
                    //
                    // MERK EN FORSKJELL FRA SearchRepository: der er ID-ene
                    // forhåndsfiltrert til dem som faktisk finnes som
                    // Svalbard-områder, slik at grenen forsvinner helt for et
                    // vanlig fylke. Det er en ren ytelsesforskjell — målt 2 mot
                    // 719 ms på største fylke — og RESULTATET er identisk, siden
                    // en fylkes-ID uansett ikke finnes som Svalbard-område.
                    //
                    // Filtreringen krever IAreaHierarchyService, som denne
                    // byggeren ikke har. Eksport er en bakgrunnsjobb, så den
                    // kostnaden er tålt framfor å tre tjenesten gjennom to
                    // ExportService-konstruktører.
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
