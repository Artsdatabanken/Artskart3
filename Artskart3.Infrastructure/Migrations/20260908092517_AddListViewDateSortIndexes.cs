using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <summary>
/// Legger DateTimeCollected som nøkkelkolonne nummer to på filterindeksene, slik
/// at listevisningen kan sortere nyeste først uten å sortere treffmengden.
/// </summary>
public partial class AddListViewDateSortIndexes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
CREATE NONCLUSTERED INDEX IX_Observation_CategoryId
ON dbo.Observation (CategoryId, DateTimeCollected)
WITH (DROP_EXISTING = ON, DATA_COMPRESSION = PAGE, MAXDOP = 4);
", suppressTransaction: true);

        migrationBuilder.Sql(@"
CREATE NONCLUSTERED INDEX IX_Observation_TaxonGroupId
ON dbo.Observation (TaxonGroupId, DateTimeCollected)
WITH (DROP_EXISTING = ON, DATA_COMPRESSION = PAGE, MAXDOP = 4);
", suppressTransaction: true);

        migrationBuilder.Sql(@"
CREATE NONCLUSTERED INDEX IX_Observation_BasisOfRecordId
ON dbo.Observation (BasisOfRecordId, DateTimeCollected)
WITH (DROP_EXISTING = ON, DATA_COMPRESSION = PAGE, MAXDOP = 4);
", suppressTransaction: true);

        migrationBuilder.Sql(@"
CREATE NONCLUSTERED INDEX IX_Observation_MonthCollected
ON dbo.Observation (MonthCollected, DateTimeCollected)
WITH (DROP_EXISTING = ON, DATA_COMPRESSION = PAGE, MAXDOP = 4);
", suppressTransaction: true);

        migrationBuilder.Sql(@"
CREATE NONCLUSTERED INDEX IX_Observation_TaxonId
ON dbo.Observation (TaxonId, DateTimeCollected)
WITH (DROP_EXISTING = ON, DATA_COMPRESSION = PAGE, MAXDOP = 4);
", suppressTransaction: true);

    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
CREATE NONCLUSTERED INDEX IX_Observation_CategoryId
ON dbo.Observation (CategoryId)
WITH (DROP_EXISTING = ON, DATA_COMPRESSION = PAGE, MAXDOP = 4);
", suppressTransaction: true);

        migrationBuilder.Sql(@"
CREATE NONCLUSTERED INDEX IX_Observation_TaxonGroupId
ON dbo.Observation (TaxonGroupId)
WITH (DROP_EXISTING = ON, DATA_COMPRESSION = PAGE, MAXDOP = 4);
", suppressTransaction: true);

        migrationBuilder.Sql(@"
CREATE NONCLUSTERED INDEX IX_Observation_BasisOfRecordId
ON dbo.Observation (BasisOfRecordId)
WITH (DROP_EXISTING = ON, DATA_COMPRESSION = PAGE, MAXDOP = 4);
", suppressTransaction: true);

        migrationBuilder.Sql(@"
CREATE NONCLUSTERED INDEX IX_Observation_MonthCollected
ON dbo.Observation (MonthCollected)
WITH (DROP_EXISTING = ON, DATA_COMPRESSION = PAGE, MAXDOP = 4);
", suppressTransaction: true);

        migrationBuilder.Sql(@"
CREATE NONCLUSTERED INDEX IX_Observation_TaxonId
ON dbo.Observation (TaxonId)
WITH (DROP_EXISTING = ON, DATA_COMPRESSION = PAGE, MAXDOP = 4);
", suppressTransaction: true);

    }
}
