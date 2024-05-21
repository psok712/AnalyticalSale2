namespace AnalyticalSales.Domain;

public record ApplicationOptions
{
    public int MaxDegreeOfParallelism { get; init; }
    public required string PathReadFileProduct { get; init; }
    public required string PathWriteFileProduct { get; init; }
}