namespace AnalyticalSales.Domain.Models;

public record Product(
    long Id,
    double Prediction,
    long Stock
);