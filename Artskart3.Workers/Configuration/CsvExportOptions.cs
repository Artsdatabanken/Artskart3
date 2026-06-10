namespace Artskart3.Workers.Configuration;

public class CsvExportOptions
{
    public BlobStorageOptions BlobStorage { get; set; } = new();
    public ExportLimitsOptions Limits { get; set; } = new();
    public ExportWorkerOptions Worker { get; set; } = new();
}

public class BlobStorageOptions
{
    public string ConnectionString { get; set; } = "UseDevelopmentStorage=true";
    public string ContainerName { get; set; } = "csv-exports";
}

public class ExportLimitsOptions
{
    public int SoftRowLimit { get; set; } = 50_000;
    public int HardRowLimit { get; set; } = 100_000;
    public int MaxConcurrentPerUser { get; set; } = 3;
}

public class ExportWorkerOptions
{
    public int BatchSize { get; set; } = 5000;
    public int InterBatchDelayMs { get; set; } = 100;
}
