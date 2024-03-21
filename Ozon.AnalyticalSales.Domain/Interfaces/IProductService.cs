namespace Ozon.AnalyticalSales.Domain.Interfaces;

public interface IProductService
{
    public Task ReadFileProductPrediction(string pathToFileProductData);
    public Task WriteFileProductDemand(string pathProductDemand);
    Task Run();
}