using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ozon.AnalyticalSales.Domain;
using Ozon.AnalyticalSales.Domain.Interfaces;
using Ozon.AnalyticalSales.Service;

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange:true)
    .Build();

var builder = Host.CreateApplicationBuilder(args);
builder.Services.Configure<ApplicationOptions>(configuration.GetSection(key: nameof(ApplicationOptions)));
builder.Services.AddTransient<IProductService, ProductService>();

var service = builder.Services.BuildServiceProvider();

Console.WriteLine("To stop the calculation process, press Ctrl + c.");

await service.GetService<IProductService>()!.Run();

Console.WriteLine("The calculation process is finished!");