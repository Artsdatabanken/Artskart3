namespace Artskart3.Workers.Configuration;

public class CsvExportOptions
{
    public BlobStorageOptions BlobStorage { get; set; } = new();
    public ExportWorkerOptions Worker { get; set; } = new();
}

public class BlobStorageOptions
{
    public string ConnectionString { get; set; } = "UseDevelopmentStorage=true";
    public string ContainerName { get; set; } = "csv-exports";
}

public class ExportWorkerOptions
{
    public int BatchSize { get; set; } = 5000;
    public int InterBatchDelayMs { get; set; } = 100;
    public int StuckJobTimeoutMinutes { get; set; } = 10;
}
