-- =============================================================
-- Artskart3 Integration Test Seed Data Extraction Script
--
-- Run this against your production/staging database to extract
-- a minimal, representative dataset for integration tests.
--
-- Usage (SSMS):
--   1. Connect to production/staging database
--   2. Switch to "Results to Text" mode (Ctrl+T)
--   3. Tools > Options > Query Results > SQL Server > Results to Text
--      Set "Maximum characters per column" to 65535
--   4. Execute the script
--   5. Copy ALL output to:
--      Artskart3.Tests.Integration/SeedData/seed_data.sql
--
-- The output is valid T-SQL INSERT statements ready to be
-- executed against the Testcontainer database after migrations.
--
-- Requires SQL Server 2017+ (uses STRING_AGG).
-- Safe to re-run at any time -- no temp tables used.
-- =============================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;

PRINT 'SET NOCOUNT ON;';
PRINT '';

-- =====================================================================
-- STEP 1: Compute the data subset as comma-separated ID strings.
-- Using variables avoids temp table collisions on re-runs.
-- =====================================================================

-- Tuning knobs -- adjust these to control dataset size.
DECLARE @TaxaPerGroup INT = 2;  -- taxa per (TaxonRankId, ParentTaxonId, TaxonGroupId, ExistsInCountry)
DECLARE @ObsPerGroup  INT = 3;  -- observations per (CategoryId, TaxonId, TaxonGroupId)

DECLARE @TaxonIds       NVARCHAR(MAX);
DECLARE @ObservationIds NVARCHAR(MAX);
DECLARE @LocationIds    NVARCHAR(MAX);

-- Step 1a: pick @TaxaPerGroup taxa from each (TaxonRankId, TaxonGroupId, ExistsInCountry) group.
SELECT @TaxonIds = STRING_AGG(CAST(Id AS NVARCHAR(MAX)), ',')
FROM (
    SELECT Id,
           ROW_NUMBER() OVER (
               PARTITION BY TaxonRankId, TaxonGroupId, ExistsInCountry
               ORDER BY Id
           ) AS rn
    FROM Taxon
    WHERE IsDeleted = 0
) x
WHERE x.rn <= @TaxaPerGroup;

-- Step 1b: pick @ObsPerGroup observations per (CategoryId, TaxonId, TaxonGroupId) for the selected taxa.
SELECT @ObservationIds = STRING_AGG(CAST(Id AS NVARCHAR(MAX)), ',')
FROM (
    SELECT Id,
           ROW_NUMBER() OVER (PARTITION BY CategoryId, TaxonId, TaxonGroupId ORDER BY Id) AS rn
    FROM Observation
    WHERE TaxonId      IN (SELECT CAST(value AS BIGINT) FROM STRING_SPLIT(@TaxonIds, ','))
      AND LocationId   IS NOT NULL
      AND CategoryId   IS NOT NULL
      AND TaxonGroupId IS NOT NULL
) x
WHERE x.rn <= @ObsPerGroup;

-- Step 1c: derive unique LocationIds from the selected observations.
SELECT @LocationIds = STRING_AGG(CAST(LocationId AS NVARCHAR(MAX)), ',')
FROM (
    SELECT DISTINCT LocationId
    FROM Observation
    WHERE Id IN (SELECT CAST(value AS BIGINT) FROM STRING_SPLIT(@ObservationIds, ','))
) x;

PRINT '-- Subset: Taxon count      = ' + CAST((LEN(@TaxonIds)       - LEN(REPLACE(@TaxonIds,       ',', '')) + 1) AS VARCHAR(10));
PRINT '-- Subset: Observation count = ' + CAST((LEN(@ObservationIds) - LEN(REPLACE(@ObservationIds, ',', '')) + 1) AS VARCHAR(10));
PRINT '-- Subset: Location count   = ' + CAST((LEN(@LocationIds)     - LEN(REPLACE(@LocationIds,   ',', '')) + 1) AS VARCHAR(10));
PRINT '';

-- =====================================================================
-- LOOKUP TABLES
-- Each block uses COL_LENGTH to detect BaseEntity columns and
-- builds the INSERT statement accordingly via dynamic SQL.
-- =====================================================================

DECLARE @sql NVARCHAR(MAX);

-- -------  CategoryType  -------
PRINT '-- ============================================================';
PRINT '-- CategoryType';
PRINT '-- ============================================================';

IF COL_LENGTH('CategoryType', 'CreatedAt') IS NOT NULL
    SET @sql = N'
    SELECT ''INSERT INTO [CategoryType] ([Id],[Name],[CreatedAt],[UpdatedAt],[IsDeleted],[DeletedAt]) VALUES (''
        + CAST(Id AS VARCHAR(20)) + '',''
        + '''''''' + REPLACE(Name, '''''''', '''''''''''') + '''''''' + '',''
        + '''''''' + CONVERT(VARCHAR(23), CreatedAt, 126) + '''''''' + '',''
        + '''''''' + CONVERT(VARCHAR(23), UpdatedAt, 126) + '''''''' + '',''
        + CAST(CAST(IsDeleted AS TINYINT) AS VARCHAR(1)) + '',''
        + ISNULL('''''''' + CONVERT(VARCHAR(23), DeletedAt, 126) + '''''''', ''NULL'')
        + '');''
    FROM [CategoryType] ORDER BY Id;';
ELSE
    SET @sql = N'
    SELECT ''INSERT INTO [CategoryType] ([Id],[Name]) VALUES (''
        + CAST(Id AS VARCHAR(20)) + '',''
        + '''''''' + REPLACE(Name, '''''''', '''''''''''') + ''''''''
        + '');''
    FROM [CategoryType] ORDER BY Id;';

EXEC sp_executesql @sql;

-- -------  Category  -------
PRINT '';
PRINT '-- ============================================================';
PRINT '-- Category';
PRINT '-- ============================================================';

IF COL_LENGTH('Category', 'CreatedAt') IS NOT NULL
    SET @sql = N'
    SELECT ''INSERT INTO [Category] ([Id],[Name],[Code],[CategoryTypeId],[CreatedAt],[UpdatedAt],[IsDeleted],[DeletedAt]) VALUES (''
        + CAST(Id AS VARCHAR(20)) + '',''
        + '''''''' + REPLACE(Name, '''''''', '''''''''''') + '''''''' + '',''
        + ISNULL('''''''' + REPLACE(Code, '''''''', '''''''''''') + '''''''', ''NULL'') + '',''
        + CAST(CategoryTypeId AS VARCHAR(20)) + '',''
        + '''''''' + CONVERT(VARCHAR(23), CreatedAt, 126) + '''''''' + '',''
        + '''''''' + CONVERT(VARCHAR(23), UpdatedAt, 126) + '''''''' + '',''
        + CAST(CAST(IsDeleted AS TINYINT) AS VARCHAR(1)) + '',''
        + ISNULL('''''''' + CONVERT(VARCHAR(23), DeletedAt, 126) + '''''''', ''NULL'')
        + '');''
    FROM [Category] ORDER BY Id;';
ELSE
    SET @sql = N'
    SELECT ''INSERT INTO [Category] ([Id],[Name],[Code],[CategoryTypeId]) VALUES (''
        + CAST(Id AS VARCHAR(20)) + '',''
        + '''''''' + REPLACE(Name, '''''''', '''''''''''') + '''''''' + '',''
        + ISNULL('''''''' + REPLACE(Code, '''''''', '''''''''''') + '''''''', ''NULL'') + '',''
        + CAST(CategoryTypeId AS VARCHAR(20))
        + '');''
    FROM [Category] ORDER BY Id;';

EXEC sp_executesql @sql;

-- -------  AreaType  -------
PRINT '';
PRINT '-- ============================================================';
PRINT '-- AreaType';
PRINT '-- ============================================================';

IF COL_LENGTH('AreaType', 'CreatedAt') IS NOT NULL
    SET @sql = N'
    SELECT ''INSERT INTO [AreaType] ([Id],[Name],[IsRequired],[CategoryName],[CreatedAt],[UpdatedAt],[IsDeleted],[DeletedAt]) VALUES (''
        + CAST(Id AS VARCHAR(20)) + '',''
        + '''''''' + REPLACE(Name, '''''''', '''''''''''') + '''''''' + '',''
        + CAST(CAST(IsRequired AS TINYINT) AS VARCHAR(1)) + '',''
        + ISNULL('''''''' + REPLACE(CategoryName, '''''''', '''''''''''') + '''''''', ''NULL'') + '',''
        + '''''''' + CONVERT(VARCHAR(23), CreatedAt, 126) + '''''''' + '',''
        + '''''''' + CONVERT(VARCHAR(23), UpdatedAt, 126) + '''''''' + '',''
        + CAST(CAST(IsDeleted AS TINYINT) AS VARCHAR(1)) + '',''
        + ISNULL('''''''' + CONVERT(VARCHAR(23), DeletedAt, 126) + '''''''', ''NULL'')
        + '');''
    FROM [AreaType] ORDER BY Id;';
ELSE
    SET @sql = N'
    SELECT ''INSERT INTO [AreaType] ([Id],[Name],[IsRequired],[CategoryName]) VALUES (''
        + CAST(Id AS VARCHAR(20)) + '',''
        + '''''''' + REPLACE(Name, '''''''', '''''''''''') + '''''''' + '',''
        + CAST(CAST(IsRequired AS TINYINT) AS VARCHAR(1)) + '',''
        + ISNULL('''''''' + REPLACE(CategoryName, '''''''', '''''''''''') + '''''''', ''NULL'')
        + '');''
    FROM [AreaType] ORDER BY Id;';

EXEC sp_executesql @sql;

-- -------  BasisOfRecord  -------
PRINT '';
PRINT '-- ============================================================';
PRINT '-- BasisOfRecord';
PRINT '-- ============================================================';

IF COL_LENGTH('BasisOfRecord', 'CreatedAt') IS NOT NULL
    SET @sql = N'
    SELECT ''INSERT INTO [BasisOfRecord] ([Id],[Name],[Description],[Variants],[CreatedAt],[UpdatedAt],[IsDeleted],[DeletedAt]) VALUES (''
        + CAST(Id AS VARCHAR(20)) + '',''
        + '''''''' + REPLACE(Name, '''''''', '''''''''''') + '''''''' + '',''
        + ISNULL('''''''' + REPLACE(Description, '''''''', '''''''''''') + '''''''', ''NULL'') + '',''
        + ISNULL('''''''' + REPLACE(Variants, '''''''', '''''''''''') + '''''''', ''NULL'') + '',''
        + '''''''' + CONVERT(VARCHAR(23), CreatedAt, 126) + '''''''' + '',''
        + '''''''' + CONVERT(VARCHAR(23), UpdatedAt, 126) + '''''''' + '',''
        + CAST(CAST(IsDeleted AS TINYINT) AS VARCHAR(1)) + '',''
        + ISNULL('''''''' + CONVERT(VARCHAR(23), DeletedAt, 126) + '''''''', ''NULL'')
        + '');''
    FROM [BasisOfRecord] ORDER BY Id;';
ELSE
    SET @sql = N'
    SELECT ''INSERT INTO [BasisOfRecord] ([Id],[Name],[Description],[Variants]) VALUES (''
        + CAST(Id AS VARCHAR(20)) + '',''
        + '''''''' + REPLACE(Name, '''''''', '''''''''''') + '''''''' + '',''
        + ISNULL('''''''' + REPLACE(Description, '''''''', '''''''''''') + '''''''', ''NULL'') + '',''
        + ISNULL('''''''' + REPLACE(Variants, '''''''', '''''''''''') + '''''''', ''NULL'')
        + '');''
    FROM [BasisOfRecord] ORDER BY Id;';

EXEC sp_executesql @sql;

-- -------  TaxonRank  -------
PRINT '';
PRINT '-- ============================================================';
PRINT '-- TaxonRank';
PRINT '-- ============================================================';

IF COL_LENGTH('TaxonRank', 'CreatedAt') IS NOT NULL
    SET @sql = N'
    SELECT ''INSERT INTO [TaxonRank] ([Id],[Name],[CreatedAt],[UpdatedAt],[IsDeleted],[DeletedAt]) VALUES (''
        + CAST(Id AS VARCHAR(20)) + '',''
        + '''''''' + REPLACE(Name, '''''''', '''''''''''') + '''''''' + '',''
        + '''''''' + CONVERT(VARCHAR(23), CreatedAt, 126) + '''''''' + '',''
        + '''''''' + CONVERT(VARCHAR(23), UpdatedAt, 126) + '''''''' + '',''
        + CAST(CAST(IsDeleted AS TINYINT) AS VARCHAR(1)) + '',''
        + ISNULL('''''''' + CONVERT(VARCHAR(23), DeletedAt, 126) + '''''''', ''NULL'')
        + '');''
    FROM [TaxonRank] ORDER BY Id;';
ELSE
    SET @sql = N'
    SELECT ''INSERT INTO [TaxonRank] ([Id],[Name]) VALUES (''
        + CAST(Id AS VARCHAR(20)) + '',''
        + '''''''' + REPLACE(Name, '''''''', '''''''''''') + ''''''''
        + '');''
    FROM [TaxonRank] ORDER BY Id;';

EXEC sp_executesql @sql;

-- -------  TaxonGroup (IsDeleted maps to column [Deleted])  -------
PRINT '';
PRINT '-- ============================================================';
PRINT '-- TaxonGroup';
PRINT '-- ============================================================';

IF COL_LENGTH('TaxonGroup', 'CreatedAt') IS NOT NULL
    SET @sql = N'
    SELECT ''INSERT INTO [TaxonGroup] ([Id],[Name],[ObservationCount],[CreatedAt],[UpdatedAt],[IsDeleted],[DeletedAt]) VALUES (''
        + CAST(Id AS VARCHAR(20)) + '',''
        + '''''''' + REPLACE(Name, '''''''', '''''''''''') + '''''''' + '',''
        + ISNULL(CAST(ObservationCount AS VARCHAR(20)), ''NULL'') + '',''
        + '''''''' + CONVERT(VARCHAR(23), CreatedAt, 126) + '''''''' + '',''
        + '''''''' + CONVERT(VARCHAR(23), UpdatedAt, 126) + '''''''' + '',''
        + CAST(CAST(IsDeleted AS TINYINT) AS VARCHAR(1)) + '',''
        + ISNULL('''''''' + CONVERT(VARCHAR(23), DeletedAt, 126) + '''''''', ''NULL'')
        + '');''
    FROM [TaxonGroup] ORDER BY Id;';
ELSE
    SET @sql = N'
    SELECT ''INSERT INTO [TaxonGroup] ([Id],[Name],[ObservationCount]) VALUES (''
        + CAST(Id AS VARCHAR(20)) + '',''
        + '''''''' + REPLACE(Name, '''''''', '''''''''''') + '''''''' + '',''
        + ISNULL(CAST(ObservationCount AS VARCHAR(20)), ''NULL'')
        + '');''
    FROM [TaxonGroup] ORDER BY Id;';

EXEC sp_executesql @sql;

-- =====================================================================
-- TAXON DATA (filtered to @TaxonIds, injected inline)
-- =====================================================================

-- -------  Taxon  -------
-- Id is NOT an identity (ValueGeneratedNever) -- no IDENTITY_INSERT needed.
PRINT '';
PRINT '-- ============================================================';
PRINT '-- Taxon';
PRINT '-- ============================================================';

IF COL_LENGTH('Taxon', 'CreatedAt') IS NOT NULL
    SET @sql = N'
    SELECT ''INSERT INTO [Taxon] ([Id],[ExternalTaxonId],[TaxonRankId],[ParentTaxonId],[ValidScientificNameId],''
        + ''[ValidScientificName],[ValidScientificNameAuthorship],[PreferredPopularname],[TaxonGroupId],''
        + ''[ExistsInCountry],[ScientificNameIdHiarchy],[TaxonIdHiarchy],[ObservationCount],''
        + ''[CumulativeObservationCount],[WeeklyObservationCount],[WeeklyCumulativeObservationCount],''
        + ''[DailyObservationCount],[DailyCumulativeObservationCount],''
        + ''[CreatedAt],[UpdatedAt],[IsDeleted],[DeletedAt]) VALUES (''
        + CAST(t.Id AS VARCHAR(20)) + '',''
        + CAST(t.ExternalTaxonId AS VARCHAR(20)) + '',''
        + CAST(t.TaxonRankId AS VARCHAR(20)) + '',''
        + CAST(t.ParentTaxonId AS VARCHAR(20)) + '',''
        + CAST(t.ValidScientificNameId AS VARCHAR(20)) + '',''
        + ISNULL('''''''' + REPLACE(t.ValidScientificName, '''''''', '''''''''''') + '''''''', ''NULL'') + '',''
        + ISNULL('''''''' + REPLACE(t.ValidScientificNameAuthorship, '''''''', '''''''''''') + '''''''', ''NULL'') + '',''
        + ISNULL('''''''' + REPLACE(t.PreferredPopularname, '''''''', '''''''''''') + '''''''', ''NULL'') + '',''
        + CAST(t.TaxonGroupId AS VARCHAR(20)) + '',''
        + CAST(CAST(t.ExistsInCountry AS TINYINT) AS VARCHAR(1)) + '',''
        + '''''''' + REPLACE(t.ScientificNameIdHiarchy, '''''''', '''''''''''') + '''''''' + '',''
        + '''''''' + REPLACE(t.TaxonIdHiarchy, '''''''', '''''''''''') + '''''''' + '',''
        + ISNULL(CAST(t.ObservationCount AS VARCHAR(20)), ''NULL'') + '',''
        + ISNULL(CAST(t.CumulativeObservationCount AS VARCHAR(20)), ''NULL'') + '',''
        + ISNULL(CAST(t.WeeklyObservationCount AS VARCHAR(20)), ''NULL'') + '',''
        + ISNULL(CAST(t.WeeklyCumulativeObservationCount AS VARCHAR(20)), ''NULL'') + '',''
        + ISNULL(CAST(t.DailyObservationCount AS VARCHAR(20)), ''NULL'') + '',''
        + ISNULL(CAST(t.DailyCumulativeObservationCount AS VARCHAR(20)), ''NULL'') + '',''
        + '''''''' + CONVERT(VARCHAR(23), t.CreatedAt, 126) + '''''''' + '',''
        + '''''''' + CONVERT(VARCHAR(23), t.UpdatedAt, 126) + '''''''' + '',''
        + CAST(CAST(t.IsDeleted AS TINYINT) AS VARCHAR(1)) + '',''
        + ISNULL('''''''' + CONVERT(VARCHAR(23), t.DeletedAt, 126) + '''''''', ''NULL'')
        + '');''
    FROM [Taxon] t WHERE t.Id IN (SELECT CAST(value AS INT) FROM STRING_SPLIT(@ExternalTaxonIds, '','')) ORDER BY t.Id;';
ELSE
    SET @sql = N'
    SELECT ''INSERT INTO [Taxon] ([Id],[ExternalTaxonId],[TaxonRankId],[ParentTaxonId],[ValidScientificNameId],''
        + ''[ValidScientificName],[ValidScientificNameAuthorship],[PreferredPopularname],[TaxonGroupId],''
        + ''[ExistsInCountry],[ScientificNameIdHiarchy],[TaxonIdHiarchy],[ObservationCount],''
        + ''[CumulativeObservationCount],[WeeklyObservationCount],[WeeklyCumulativeObservationCount],''
        + ''[DailyObservationCount],[DailyCumulativeObservationCount]) VALUES (''
        + CAST(t.Id AS VARCHAR(20)) + '',''
        + CAST(t.ExternalTaxonId AS VARCHAR(20)) + '',''
        + CAST(t.TaxonRankId AS VARCHAR(20)) + '',''
        + CAST(t.ParentTaxonId AS VARCHAR(20)) + '',''
        + CAST(t.ValidScientificNameId AS VARCHAR(20)) + '',''
        + ISNULL('''''''' + REPLACE(t.ValidScientificName, '''''''', '''''''''''') + '''''''', ''NULL'') + '',''
        + ISNULL('''''''' + REPLACE(t.ValidScientificNameAuthorship, '''''''', '''''''''''') + '''''''', ''NULL'') + '',''
        + ISNULL('''''''' + REPLACE(t.PreferredPopularname, '''''''', '''''''''''') + '''''''', ''NULL'') + '',''
        + CAST(t.TaxonGroupId AS VARCHAR(20)) + '',''
        + CAST(CAST(t.ExistsInCountry AS TINYINT) AS VARCHAR(1)) + '',''
        + '''''''' + REPLACE(t.ScientificNameIdHiarchy, '''''''', '''''''''''') + '''''''' + '',''
        + '''''''' + REPLACE(t.TaxonIdHiarchy, '''''''', '''''''''''') + '''''''' + '',''
        + ISNULL(CAST(t.ObservationCount AS VARCHAR(20)), ''NULL'') + '',''
        + ISNULL(CAST(t.CumulativeObservationCount AS VARCHAR(20)), ''NULL'') + '',''
        + ISNULL(CAST(t.WeeklyObservationCount AS VARCHAR(20)), ''NULL'') + '',''
        + ISNULL(CAST(t.WeeklyCumulativeObservationCount AS VARCHAR(20)), ''NULL'') + '',''
        + ISNULL(CAST(t.DailyObservationCount AS VARCHAR(20)), ''NULL'') + '',''
        + ISNULL(CAST(t.DailyCumulativeObservationCount AS VARCHAR(20)), ''NULL'')
        + '');''
    FROM [Taxon] t WHERE t.Id IN (SELECT CAST(value AS INT) FROM STRING_SPLIT(@ExternalTaxonIds, '','')) ORDER BY t.Id;';

EXEC sp_executesql @sql, N'@ExternalTaxonIds NVARCHAR(MAX)', @TaxonIds;

-- -------  TaxonName  -------
-- Id is NOT an identity (ValueGeneratedNever).
PRINT '';
PRINT '-- ============================================================';
PRINT '-- TaxonName';
PRINT '-- ============================================================';

IF COL_LENGTH('TaxonName', 'CreatedAt') IS NOT NULL
    SET @sql = N'
    SELECT ''INSERT INTO [TaxonName] ([Id],[ScientificName],[ScientificNameAuthorship],[TaxonId],[Accepted],''
        + ''[CreatedAt],[UpdatedAt],[IsDeleted],[DeletedAt]) VALUES (''
        + CAST(tn.Id AS VARCHAR(20)) + '',''
        + '''''''' + REPLACE(tn.ScientificName, '''''''', '''''''''''') + '''''''' + '',''
        + ISNULL('''''''' + REPLACE(tn.ScientificNameAuthorship, '''''''', '''''''''''') + '''''''', ''NULL'') + '',''
        + ISNULL(CAST(tn.TaxonId AS VARCHAR(20)), ''NULL'') + '',''
        + CAST(CAST(tn.Accepted AS TINYINT) AS VARCHAR(1)) + '',''
        + '''''''' + CONVERT(VARCHAR(23), tn.CreatedAt, 126) + '''''''' + '',''
        + '''''''' + CONVERT(VARCHAR(23), tn.UpdatedAt, 126) + '''''''' + '',''
        + CAST(CAST(tn.IsDeleted AS TINYINT) AS VARCHAR(1)) + '',''
        + ISNULL('''''''' + CONVERT(VARCHAR(23), tn.DeletedAt, 126) + '''''''', ''NULL'')
        + '');''
    FROM [TaxonName] tn WHERE tn.TaxonId IN (SELECT CAST(value AS INT) FROM STRING_SPLIT(@TaxonIds, '','')) ORDER BY tn.Id;';
ELSE
    SET @sql = N'
    SELECT ''INSERT INTO [TaxonName] ([Id],[ScientificName],[ScientificNameAuthorship],[TaxonId],[Accepted]) VALUES (''
        + CAST(tn.Id AS VARCHAR(20)) + '',''
        + '''''''' + REPLACE(tn.ScientificName, '''''''', '''''''''''') + '''''''' + '',''
        + ISNULL('''''''' + REPLACE(tn.ScientificNameAuthorship, '''''''', '''''''''''') + '''''''', ''NULL'') + '',''
        + ISNULL(CAST(tn.TaxonId AS VARCHAR(20)), ''NULL'') + '',''
        + CAST(CAST(tn.Accepted AS TINYINT) AS VARCHAR(1))
        + '');''
    FROM [TaxonName] tn WHERE tn.TaxonId IN (SELECT CAST(value AS INT) FROM STRING_SPLIT(@TaxonIds, '','')) ORDER BY tn.Id;';

EXEC sp_executesql @sql, N'@TaxonIds NVARCHAR(MAX)', @TaxonIds;

-- -------  TaxonPopularName  -------
-- Id IS an identity -> needs SET IDENTITY_INSERT.
PRINT '';
PRINT '-- ============================================================';
PRINT '-- TaxonPopularName';
PRINT '-- ============================================================';
PRINT 'SET IDENTITY_INSERT [TaxonPopularName] ON;';

IF COL_LENGTH('TaxonPopularName', 'CreatedAt') IS NOT NULL
    SET @sql = N'
    SELECT ''INSERT INTO [TaxonPopularName] ([Id],[SourceId],[Name],[Language],[Preferred],[TaxonId],''
        + ''[CreatedAt],[UpdatedAt],[IsDeleted],[DeletedAt]) VALUES (''
        + CAST(tpn.Id AS VARCHAR(20)) + '',''
        + ISNULL(CAST(tpn.SourceId AS VARCHAR(20)), ''0'') + '',''
        + '''''''' + REPLACE(tpn.Name, '''''''', '''''''''''') + '''''''' + '',''
        + ISNULL('''''''' + REPLACE(tpn.Language, '''''''', '''''''''''') + '''''''', ''NULL'') + '',''
        + CAST(CAST(tpn.Preferred AS TINYINT) AS VARCHAR(1)) + '',''
        + ISNULL(CAST(tpn.TaxonId AS VARCHAR(20)), ''NULL'') + '',''
        + '''''''' + CONVERT(VARCHAR(23), tpn.CreatedAt, 126) + '''''''' + '',''
        + '''''''' + CONVERT(VARCHAR(23), tpn.UpdatedAt, 126) + '''''''' + '',''
        + CAST(CAST(tpn.IsDeleted AS TINYINT) AS VARCHAR(1)) + '',''
        + ISNULL('''''''' + CONVERT(VARCHAR(23), tpn.DeletedAt, 126) + '''''''', ''NULL'')
        + '');''
    FROM [TaxonPopularName] tpn WHERE tpn.TaxonId IN (SELECT CAST(value AS INT) FROM STRING_SPLIT(@TaxonIds, '','')) ORDER BY tpn.Id;';
ELSE
    SET @sql = N'
    SELECT ''INSERT INTO [TaxonPopularName] ([Id],[SourceId],[Name],[Language],[Preferred],[TaxonId]) VALUES (''
        + CAST(tpn.Id AS VARCHAR(20)) + '',''
        + ISNULL(CAST(tpn.SourceId AS VARCHAR(20)), ''0'') + '',''
        + '''''''' + REPLACE(tpn.Name, '''''''', '''''''''''') + '''''''' + '',''
        + ISNULL('''''''' + REPLACE(tpn.Language, '''''''', '''''''''''') + '''''''', ''NULL'') + '',''
        + CAST(CAST(tpn.Preferred AS TINYINT) AS VARCHAR(1)) + '',''
        + ISNULL(CAST(tpn.TaxonId AS VARCHAR(20)), ''NULL'')
        + '');''
    FROM [TaxonPopularName] tpn WHERE tpn.TaxonId IN (SELECT CAST(value AS INT) FROM STRING_SPLIT(@TaxonIds, '','')) ORDER BY tpn.Id;';

EXEC sp_executesql @sql, N'@TaxonIds NVARCHAR(MAX)', @TaxonIds;
PRINT 'SET IDENTITY_INSERT [TaxonPopularName] OFF;';

-- =====================================================================
-- LOCATION + OBSERVATION DATA
-- =====================================================================

-- -------  Location  -------
-- Id IS an identity -> needs SET IDENTITY_INSERT.
PRINT '';
PRINT '-- ============================================================';
PRINT '-- Location';
PRINT '-- ============================================================';
PRINT 'SET IDENTITY_INSERT [Location] ON;';

IF COL_LENGTH('Location', 'CreatedAt') IS NOT NULL
    SET @sql = N'
    SELECT ''INSERT INTO [Location] ([Id],[LookupId],[Latitude],[Longitude],[CoordinatePrecision],''
        + ''[East],[North],[Locality],[TimeStamp],[NodeId],[LocationId],''
        + ''[CreatedAt],[UpdatedAt],[IsDeleted],[DeletedAt]) VALUES (''
        + CAST(l.Id AS VARCHAR(20)) + '',''
        + ISNULL('''''''' + REPLACE(l.LookupId, '''''''', '''''''''''') + '''''''', ''NULL'') + '',''
        + ISNULL(CAST(l.Latitude AS VARCHAR(30)), ''NULL'') + '',''
        + ISNULL(CAST(l.Longitude AS VARCHAR(30)), ''NULL'') + '',''
        + ISNULL(CAST(l.CoordinatePrecision AS VARCHAR(20)), ''NULL'') + '',''
        + CAST(l.East AS VARCHAR(20)) + '',''
        + CAST(l.North AS VARCHAR(20)) + '',''
        + ISNULL('''''''' + REPLACE(l.Locality, '''''''', '''''''''''') + '''''''', ''NULL'') + '',''
        + '''''''' + CONVERT(VARCHAR(23), l.TimeStamp, 126) + '''''''' + '',''
        + CAST(l.NodeId AS VARCHAR(20)) + '',''
        + ISNULL('''''''' + REPLACE(l.LocationId, '''''''', '''''''''''') + '''''''', ''NULL'') + '',''
        + '''''''' + CONVERT(VARCHAR(23), l.CreatedAt, 126) + '''''''' + '',''
        + '''''''' + CONVERT(VARCHAR(23), l.UpdatedAt, 126) + '''''''' + '',''
        + CAST(CAST(l.IsDeleted AS TINYINT) AS VARCHAR(1)) + '',''
        + ISNULL('''''''' + CONVERT(VARCHAR(23), l.DeletedAt, 126) + '''''''', ''NULL'')
        + '');''
    FROM [Location] l WHERE l.Id IN (SELECT CAST(value AS INT) FROM STRING_SPLIT(@LocationIds, '','')) ORDER BY l.Id;';
ELSE
    SET @sql = N'
    SELECT ''INSERT INTO [Location] ([Id],[LookupId],[Latitude],[Longitude],[CoordinatePrecision],''
        + ''[East],[North],[Locality],[TimeStamp],[NodeId],[LocationId]) VALUES (''
        + CAST(l.Id AS VARCHAR(20)) + '',''
        + ISNULL('''''''' + REPLACE(l.LookupId, '''''''', '''''''''''') + '''''''', ''NULL'') + '',''
        + ISNULL(CAST(l.Latitude AS VARCHAR(30)), ''NULL'') + '',''
        + ISNULL(CAST(l.Longitude AS VARCHAR(30)), ''NULL'') + '',''
        + ISNULL(CAST(l.CoordinatePrecision AS VARCHAR(20)), ''NULL'') + '',''
        + CAST(l.East AS VARCHAR(20)) + '',''
        + CAST(l.North AS VARCHAR(20)) + '',''
        + ISNULL('''''''' + REPLACE(l.Locality, '''''''', '''''''''''') + '''''''', ''NULL'') + '',''
        + '''''''' + CONVERT(VARCHAR(23), l.TimeStamp, 126) + '''''''' + '',''
        + CAST(l.NodeId AS VARCHAR(20)) + '',''
        + ISNULL('''''''' + REPLACE(l.LocationId, '''''''', '''''''''''') + '''''''', ''NULL'')
        + '');''
    FROM [Location] l WHERE l.Id IN (SELECT CAST(value AS INT) FROM STRING_SPLIT(@LocationIds, '','')) ORDER BY l.Id;';

EXEC sp_executesql @sql, N'@LocationIds NVARCHAR(MAX)', @LocationIds;
PRINT 'SET IDENTITY_INSERT [Location] OFF;';

-- -------  Observation  -------
-- Id IS an identity -> needs SET IDENTITY_INSERT.
-- EXCLUDE computed columns: YearCollected, MonthCollected.
PRINT '';
PRINT '-- ============================================================';
PRINT '-- Observation';
PRINT '-- ============================================================';
PRINT 'SET IDENTITY_INSERT [Observation] ON;';

IF COL_LENGTH('Observation', 'CreatedAt') IS NOT NULL
    SET @sql = N'
    SELECT ''INSERT INTO [Observation] ([Id],[ProxyId],[DateLastModified],[DateTimeCollected],''
        + ''[DateTimeRecordImported],[DateTimeRecordProcessed],[NodeId],[InstitutionId],[InstitutionCode],''
        + ''[CollectionCode],[CatalogNumber],[BasisOfRecordId],[DatetimeIdentified],[TaxonId],''
        + ''[MatchedScientificNameId],[TaxonGroupId],[CategoryId],[Latitude],[Longitude],''
        + ''[CoordinatePrecisionInMeters],[East],[North],[LocationId],[OccurrenceId],[HashCode],''
        + ''[ProcessEngineId],[HasErrors],[HasAnnotations],[ObservationQualityTypeId],''
        + ''[CreatedAt],[UpdatedAt],[IsDeleted],[DeletedAt]) VALUES (''
        + CAST(o.Id AS VARCHAR(20)) + '',''
        + ISNULL('''''''' + REPLACE(o.ProxyId, '''''''', '''''''''''') + '''''''', ''NULL'') + '',''
        + '''''''' + CONVERT(VARCHAR(23), o.DateLastModified, 126) + '''''''' + '',''
        + ISNULL('''''''' + CONVERT(VARCHAR(23), o.DateTimeCollected, 126) + '''''''', ''NULL'') + '',''
        + '''''''' + CONVERT(VARCHAR(23), o.DateTimeRecordImported, 126) + '''''''' + '',''
        + '''''''' + CONVERT(VARCHAR(23), o.DateTimeRecordProcessed, 126) + '''''''' + '',''
        + CAST(o.NodeId AS VARCHAR(20)) + '',''
        + ISNULL('''''''' + REPLACE(o.InstitutionId, '''''''', '''''''''''') + '''''''', ''NULL'') + '',''
        + ISNULL('''''''' + REPLACE(o.InstitutionCode, '''''''', '''''''''''') + '''''''', ''NULL'') + '',''
        + ISNULL('''''''' + REPLACE(o.CollectionCode, '''''''', '''''''''''') + '''''''', ''NULL'') + '',''
        + ISNULL('''''''' + REPLACE(o.CatalogNumber, '''''''', '''''''''''') + '''''''', ''NULL'') + '',''
        + CAST(o.BasisOfRecordId AS VARCHAR(20)) + '',''
        + ISNULL('''''''' + CONVERT(VARCHAR(23), o.DatetimeIdentified, 126) + '''''''', ''NULL'') + '',''
        + CAST(o.TaxonId AS VARCHAR(20)) + '',''
        + CAST(o.MatchedScientificNameId AS VARCHAR(20)) + '',''
        + CAST(o.TaxonGroupId AS VARCHAR(20)) + '',''
        + ISNULL(CAST(o.CategoryId AS VARCHAR(20)), ''NULL'') + '',''
        + CAST(o.Latitude AS VARCHAR(30)) + '',''
        + CAST(o.Longitude AS VARCHAR(30)) + '',''
        + ISNULL(CAST(o.CoordinatePrecisionInMeters AS VARCHAR(20)), ''NULL'') + '',''
        + CAST(o.East AS VARCHAR(20)) + '',''
        + CAST(o.North AS VARCHAR(20)) + '',''
        + ISNULL(CAST(o.LocationId AS VARCHAR(20)), ''NULL'') + '',''
        + ISNULL('''''''' + REPLACE(o.OccurrenceId, '''''''', '''''''''''') + '''''''', ''NULL'') + '',''
        + CAST(o.HashCode AS VARCHAR(20)) + '',''
        + CAST(o.ProcessEngineId AS VARCHAR(20)) + '',''
        + CAST(CAST(o.HasErrors AS TINYINT) AS VARCHAR(1)) + '',''
        + CAST(CAST(o.HasAnnotations AS TINYINT) AS VARCHAR(1)) + '',''
        + ISNULL(CAST(o.ObservationQualityTypeId AS VARCHAR(20)), ''NULL'') + '',''
        + '''''''' + CONVERT(VARCHAR(23), o.CreatedAt, 126) + '''''''' + '',''
        + '''''''' + CONVERT(VARCHAR(23), o.UpdatedAt, 126) + '''''''' + '',''
        + CAST(CAST(o.IsDeleted AS TINYINT) AS VARCHAR(1)) + '',''
        + ISNULL('''''''' + CONVERT(VARCHAR(23), o.DeletedAt, 126) + '''''''', ''NULL'')
        + '');''
    FROM [Observation] o
    WHERE o.Id IN (SELECT CAST(value AS BIGINT) FROM STRING_SPLIT(@ObservationIds, '',''))
    ORDER BY o.TaxonId, o.Id;';
ELSE
    SET @sql = N'
    SELECT ''INSERT INTO [Observation] ([Id],[ProxyId],[DateLastModified],[DateTimeCollected],''
        + ''[DateTimeRecordImported],[DateTimeRecordProcessed],[NodeId],[InstitutionId],[InstitutionCode],''
        + ''[CollectionCode],[CatalogNumber],[BasisOfRecordId],[DatetimeIdentified],[TaxonId],''
        + ''[MatchedScientificNameId],[TaxonGroupId],[CategoryId],[Latitude],[Longitude],''
        + ''[CoordinatePrecisionInMeters],[East],[North],[LocationId],[OccurrenceId],[HashCode],''
        + ''[ProcessEngineId],[HasErrors],[HasAnnotations],[ObservationQualityTypeId],[CreatedAt],[UpdatedAt],[IsDeleted]) VALUES (''
        + CAST(o.Id AS VARCHAR(20)) + '',''
        + ISNULL('''''''' + REPLACE(o.ProxyId, '''''''', '''''''''''') + '''''''', ''NULL'') + '',''
        + '''''''' + CONVERT(VARCHAR(23), o.DateLastModified, 126) + '''''''' + '',''
        + ISNULL('''''''' + CONVERT(VARCHAR(23), o.DateTimeCollected, 126) + '''''''', ''NULL'') + '',''
        + '''''''' + CONVERT(VARCHAR(23), o.DateTimeRecordImported, 126) + '''''''' + '',''
        + '''''''' + CONVERT(VARCHAR(23), o.DateTimeRecordProcessed, 126) + '''''''' + '',''
        + CAST(o.NodeId AS VARCHAR(20)) + '',''
        + ISNULL('''''''' + REPLACE(o.InstitutionId, '''''''', '''''''''''') + '''''''', ''NULL'') + '',''
        + ISNULL('''''''' + REPLACE(o.InstitutionCode, '''''''', '''''''''''') + '''''''', ''NULL'') + '',''
        + ISNULL('''''''' + REPLACE(o.CollectionCode, '''''''', '''''''''''') + '''''''', ''NULL'') + '',''
        + ISNULL('''''''' + REPLACE(o.CatalogNumber, '''''''', '''''''''''') + '''''''', ''NULL'') + '',''
        + CAST(o.BasisOfRecordId AS VARCHAR(20)) + '',''
        + ISNULL('''''''' + CONVERT(VARCHAR(23), o.DatetimeIdentified, 126) + '''''''', ''NULL'') + '',''
        + CAST(o.TaxonId AS VARCHAR(20)) + '',''
        + CAST(o.MatchedScientificNameId AS VARCHAR(20)) + '',''
        + CAST(o.TaxonGroupId AS VARCHAR(20)) + '',''
        + ISNULL(CAST(o.CategoryId AS VARCHAR(20)), ''NULL'') + '',''
        + CAST(o.Latitude AS VARCHAR(30)) + '',''
        + CAST(o.Longitude AS VARCHAR(30)) + '',''
        + ISNULL(CAST(o.CoordinatePrecisionInMeters AS VARCHAR(20)), ''NULL'') + '',''
        + CAST(o.East AS VARCHAR(20)) + '',''
        + CAST(o.North AS VARCHAR(20)) + '',''
        + ISNULL(CAST(o.LocationId AS VARCHAR(20)), ''NULL'') + '',''
        + ISNULL('''''''' + REPLACE(o.OccurrenceId, '''''''', '''''''''''') + '''''''', ''NULL'') + '',''
        + CAST(o.HashCode AS VARCHAR(20)) + '',''
        + CAST(o.ProcessEngineId AS VARCHAR(20)) + '',''
        + CAST(CAST(o.HasErrors AS TINYINT) AS VARCHAR(1)) + '',''
        + CAST(CAST(o.HasAnnotations AS TINYINT) AS VARCHAR(1)) + '',''
        + ISNULL(CAST(o.ObservationQualityTypeId AS VARCHAR(20)), ''NULL'') + '',''''''''2024-01-01T00:00:00'''''''',''''''''2024-01-01T00:00:00'''''''',0''
        + '');''
    FROM [Observation] o
    WHERE o.Id IN (SELECT CAST(value AS BIGINT) FROM STRING_SPLIT(@ObservationIds, '',''))
    ORDER BY o.TaxonId, o.Id;';

EXEC sp_executesql @sql, N'@ObservationIds NVARCHAR(MAX)', @ObservationIds;
PRINT 'SET IDENTITY_INSERT [Observation] OFF;';

-- =====================================================================
-- AREA DATA (counties and municipalities, AreaTypeId 1 and 2)
-- WktPolygon is excluded (geometry values are large and not needed for
-- integration tests). Output as NULL to avoid SSMS column truncation issues.
-- =====================================================================

-- -------  Area  -------
-- Id IS an identity -> needs SET IDENTITY_INSERT.
PRINT '';
PRINT '-- ============================================================';
PRINT '-- Area (AreaTypeId 1 and 2)';
PRINT '-- ============================================================';
PRINT 'SET IDENTITY_INSERT [Area] ON;';

IF COL_LENGTH('Area', 'CreatedAt') IS NOT NULL
    SET @sql = N'
    SELECT ''INSERT INTO [Area] ([Id],[DocumentId],[Fid],[Name],[AreaTypeId],[ZoomLevel],[ParentFid],[SyncDateTime],''
        + ''[ObservationCount],[Bbox],[TimeStamp],[IsCurrent],[WktPolygon],''
        + ''[CreatedAt],[UpdatedAt],[IsDeleted],[DeletedAt]) VALUES (''
        + CAST(a.Id AS VARCHAR(20)) + '',''
        + '''''''' + REPLACE(a.DocumentId, '''''''', '''''''''''') + '''''''' + '',''
        + '''''''' + REPLACE(a.Fid, '''''''', '''''''''''') + '''''''' + '',''
        + '''''''' + REPLACE(a.Name, '''''''', '''''''''''') + '''''''' + '',''
        + CAST(a.AreaTypeId AS VARCHAR(20)) + '',''
        + CAST(a.ZoomLevel AS VARCHAR(20)) + '',''
        + '''''''' + REPLACE(a.ParentFid, '''''''', '''''''''''') + '''''''' + '',''
        + '''''''' + CONVERT(VARCHAR(23), a.SyncDateTime, 126) + '''''''' + '',''
        + ISNULL(CAST(a.ObservationCount AS VARCHAR(20)), ''NULL'') + '',''
        + '''''''' + REPLACE(a.Bbox, '''''''', '''''''''''') + '''''''' + '',''
        + '''''''' + CONVERT(VARCHAR(23), a.TimeStamp, 126) + '''''''' + '',''
        + CAST(CAST(a.IsCurrent AS TINYINT) AS VARCHAR(1)) + '',''
        + ''NULL'' + '',''
        + '''''''' + CONVERT(VARCHAR(23), a.CreatedAt, 126) + '''''''' + '',''
        + '''''''' + CONVERT(VARCHAR(23), a.UpdatedAt, 126) + '''''''' + '',''
        + CAST(CAST(a.IsDeleted AS TINYINT) AS VARCHAR(1)) + '',''
        + ISNULL('''''''' + CONVERT(VARCHAR(23), a.DeletedAt, 126) + '''''''', ''NULL'')
        + '');''
    FROM [Area] a WHERE a.AreaTypeId IN (1, 2) ORDER BY a.AreaTypeId, a.Id;';
ELSE
    SET @sql = N'
    SELECT ''INSERT INTO [Area] ([Id],[DocumentId],[Fid],[Name],[AreaTypeId],[ZoomLevel],[ParentFid],[SyncDateTime],''
        + ''[ObservationCount],[Bbox],[TimeStamp],[IsCurrent],[WktPolygon]) VALUES (''
        + CAST(a.Id AS VARCHAR(20)) + '',''
        + '''''''' + REPLACE(a.DocumentId, '''''''', '''''''''''') + '''''''' + '',''
        + '''''''' + REPLACE(a.Fid, '''''''', '''''''''''') + '''''''' + '',''
        + '''''''' + REPLACE(a.Name, '''''''', '''''''''''') + '''''''' + '',''
        + CAST(a.AreaTypeId AS VARCHAR(20)) + '',''
        + CAST(a.ZoomLevel AS VARCHAR(20)) + '',''
        + '''''''' + REPLACE(a.ParentFid, '''''''', '''''''''''') + '''''''' + '',''
        + '''''''' + CONVERT(VARCHAR(23), a.SyncDateTime, 126) + '''''''' + '',''
        + ISNULL(CAST(a.ObservationCount AS VARCHAR(20)), ''NULL'') + '',''
        + '''''''' + REPLACE(a.Bbox, '''''''', '''''''''''') + '''''''' + '',''
        + '''''''' + CONVERT(VARCHAR(23), a.TimeStamp, 126) + '''''''' + '',''
        + CAST(CAST(a.IsCurrent AS TINYINT) AS VARCHAR(1)) + '',''
        + ''NULL''
        + '');''
    FROM [Area] a WHERE a.AreaTypeId IN (1, 2) ORDER BY a.AreaTypeId, a.Id;';

EXEC sp_executesql @sql;
PRINT 'SET IDENTITY_INSERT [Area] OFF;';

-- -------  LocationAreas junction table  -------
-- Uses STRING_SPLIT to filter without a temp table.
PRINT '';
PRINT '-- ============================================================';
PRINT '-- LocationAreas';
PRINT '-- ============================================================';

SET @sql = N'
SELECT
    ''INSERT INTO [LocationAreas] ([LocationId],[AreaId]) VALUES (''
    + CAST(la.LocationId AS VARCHAR(20)) + '',''
    + CAST(la.AreaId AS VARCHAR(20))
    + '');''
FROM [LocationAreas] la
WHERE la.LocationId IN (SELECT CAST(value AS INT) FROM STRING_SPLIT(@LocationIds, '',''))
  AND la.AreaId IN (SELECT Id FROM Area WHERE AreaTypeId IN (1, 2))
ORDER BY la.LocationId, la.AreaId;';

EXEC sp_executesql @sql, N'@LocationIds NVARCHAR(MAX)', @LocationIds;

PRINT '';
PRINT 'PRINT ''Seed data loaded successfully.'';';
