# Integration Test Seed Data

This folder contains two files:

| File | Purpose |
|---|---|
| `extract_seed_data.sql` | Run against production/staging to generate seed data |
| `seed_data.sql` | The generated INSERT statements used by tests *(not committed — generate locally)* |

---

## How to generate `seed_data.sql`

### Step 1 — Configure SSMS output

1. Open **SSMS** and connect to the **production** or **staging** database
2. Switch to **Results to Text** mode: **`Ctrl+T`**
3. Go to **Tools → Options → Query Results → SQL Server → Results to Text**
   - Set **"Maximum characters per column"** to **`65535`**
   - Click OK *(you may need to open a new query window for the setting to take effect)*

### Step 2 — Run the extraction script

1. Open `SeedData/extract_seed_data.sql` in SSMS
2. Execute it (**`F5`**)
3. The **Results** pane will contain valid T-SQL `INSERT` statements

### Step 3 — Save the output

1. In the Results pane, select all output (**`Ctrl+A`**)
2. Copy and paste into a new file: `SeedData/seed_data.sql`
3. Make sure the file starts with `SET NOCOUNT ON;` and ends with `PRINT 'Seed data loaded successfully.';`

### Step 4 — Run the tests

```bash
dotnet test Artskart3.Tests.Integration
```

The `DatabaseFixture` will automatically:
- Start a SQL Server Docker container (requires Docker Desktop)
- Apply EF Core migrations
- Execute `seed_data.sql` in batches split on `GO` separators
- Tear down the container after the test run

---

## What is extracted

The script extracts the minimum data needed to exercise all three API endpoints:

| Table(s) | Rows extracted | Used by |
|---|---|---|
| `CategoryType`, `Category` | All rows | `GetLocations` filter |
| `AreaType` | All rows | `GetAreas` |
| `BasisOfRecord` | All rows | `GetLocations` filter |
| `TaxonRank`, `TaxonGroup` | All rows | `SearchTaxons` |
| `Taxon` + `TaxonName` + `TaxonPopularName` | Top 10 taxa by observation count | `SearchTaxons` (exact / starts-with / contains) |
| `Location` | Top 50 by observation count | `GetLocations` |
| `Observation` | Linked to the above 50 locations + 10 taxa | `GetLocations` filters |
| `Area` (AreaTypeId 1 & 2) | All counties and municipalities | `GetAreas` centroid/grouping |
| `LocationAreas` | Junction rows for selected locations | `GetAreas` |

**Total: typically a few hundred rows** — fast to insert, deterministic, no production-scale data in the test container.

---

## Without seed data

If `seed_data.sql` is absent, the `DatabaseFixture` will log a warning and continue with an empty database. Integration tests are designed to **still pass** in this state — they verify HTTP response shapes and status codes. Tests that assert specific data content will receive empty results (which are valid responses).

---

## Refreshing the seed data

Re-run Steps 1–3 whenever:
- The database schema changes significantly (new required columns, new FKs)
- You need different taxa or locations to cover a new test scenario
- The lookup tables (`BasisOfRecord`, `TaxonGroup`, etc.) are updated in production
