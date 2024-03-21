namespace Ozon.AnalyticalSales.Domain.Models;

public record Product(
    long Id,
    double Prediction,
    long Stock
);