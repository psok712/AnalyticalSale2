namespace Ozon.AnalyticalSales.Domain;

public class ApplicationOptions
{
    public int MaxDegreeOfParallelism { get; init; }
    public string PathReadFileProduct { get; init; }
    public string PathWriteFileProduct { get; init; }
}