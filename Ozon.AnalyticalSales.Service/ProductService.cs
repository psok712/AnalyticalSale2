using System.Globalization;
using System.Threading.Channels;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Options;
using Ozon.AnalyticalSales.Domain;
using Ozon.AnalyticalSales.Domain.Interfaces;
using Ozon.AnalyticalSales.Domain.Models;

namespace Ozon.AnalyticalSales.Service;

public class ProductService : IProductService
{
    private ApplicationOptions _applicationOptions;
    
    private readonly List<Task> _tasks = new();
    private readonly List<CancellationTokenSource> _tokens = new();
    private readonly CancellationTokenSource _cancelAllToken = new();
    
    private readonly Channel<Product> _channelRead = Channel.CreateBounded<Product>(100);
    private readonly Channel<ProductDemand> _channelWrite = Channel.CreateBounded<ProductDemand>(1000);
    
    private readonly object _lock = new();
    
    private long _amountReadLine;
    private long _amountWriteLine;
    private long _countedLine;

    public ProductService(IOptionsMonitor<ApplicationOptions> optionsMonitor)
    {
        _applicationOptions = optionsMonitor.CurrentValue;

        optionsMonitor.OnChange((options, _) =>
        {
            _applicationOptions = options;
            UpdateParallelism();
        });
        
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            _cancelAllToken.Cancel();
            
            lock (_lock)
            {
                Console.WriteLine("The settlement operation has been cancelled.");
            }

            Environment.Exit(0);
        };
    }

    public async Task ReadFileProductPrediction(string pathProductPrediction)
    {
        pathProductPrediction = pathProductPrediction.SettingSeparatorOs();

        await Task.Run(async () =>
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                PrepareHeaderForMatch = args => args.Header.ToUpperInvariant()
            };
            using var sr = new StreamReader(pathProductPrediction);
            using var csv = new CsvReader(sr, config);
            await csv.ReadAsync();
            csv.ReadHeader();

            while (await csv.ReadAsync())
            {
                var line = csv.GetRecord<Product>();
                await _channelRead.Writer.WriteAsync(line);
                ++_amountReadLine;
                
                lock (_lock)
                {
                    Console.WriteLine($"Product read: {line.Id}. Total read from the {_amountReadLine} product file.");
                }
            }
            
            
            _channelRead.Writer.Complete();
        });
    }

    public async Task Run()
    {
        AddTasksCalculateDemand(_applicationOptions.MaxDegreeOfParallelism);

        await Task.WhenAll(Task.WhenAll(_tasks),
            WriteFileProductDemand(_applicationOptions.PathWriteFileProduct),
            ReadFileProductPrediction(_applicationOptions.PathReadFileProduct));
    }

    public async Task WriteFileProductDemand(string pathProductDemand)
    {
        pathProductDemand = pathProductDemand.SettingSeparatorOs();

        await Task.Run(async () =>
        {
            await using var sw = new StreamWriter(pathProductDemand);
            await sw.WriteLineAsync("id, demand");

            while (await _channelWrite.Reader.WaitToReadAsync())
            {
                var product = await _channelWrite.Reader.ReadAsync();

                await sw.WriteLineAsync($"{product.Id}, {product.Demand}");
                ++_amountWriteLine;

                lock (_lock)
                {
                    Console.WriteLine($"Product recorded: {product}. Total recorded results: {_amountWriteLine}");
                }
            }
        });
    }

    private void UpdateParallelism()
    {
        if (_applicationOptions.MaxDegreeOfParallelism > _tasks.Count)
            AddTasksCalculateDemand(_applicationOptions.MaxDegreeOfParallelism - _tasks.Count);
        else
            RemoveTasks(_tasks.Count - _applicationOptions.MaxDegreeOfParallelism);

        Console.WriteLine(
            $"The degree of parallelism has been updated to be: {_applicationOptions.MaxDegreeOfParallelism}.");
    }

    private void AddTasksCalculateDemand(int amount)
    {
        for (var i = 0; i < amount; ++i)
        {

            CancellationTokenSource cts = new();
            _tokens.Add(cts);
            
            _tasks.Add(Task.Factory.StartNew(async () =>
                {
                    while (!cts.IsCancellationRequested && await _channelRead.Reader.WaitToReadAsync(_cancelAllToken.Token))
                    {
                        var product = await _channelRead.Reader.ReadAsync(_cancelAllToken.Token).ConfigureAwait(false);
                        var demand = product.Prediction - product.Stock > 0
                            ? product.Prediction - product.Stock
                            : 0;
                        var productDemand = new ProductDemand(product.Id, demand);

                        await _channelWrite.Writer.WriteAsync(productDemand, _cancelAllToken.Token).ConfigureAwait(false);

                        lock (_lock)
                        {
                            ++_countedLine;
                            Console.WriteLine($"Product counted: {product.Id}. Total {_countedLine} items counted.");
                        }


                        await Task.Delay(300, _cancelAllToken.Token).ConfigureAwait(false);
                    }

                    if (!cts.IsCancellationRequested)
                    {
                        await _cancelAllToken.CancelAsync();
                        _channelWrite.Writer.Complete();
                    }
                },
                cts.Token,
                TaskCreationOptions.None,
                TaskScheduler.Default)
            );
        }
    }

    private void RemoveTasks(int amount)
    {
        while (amount != 0)
        {
            var taskIndexToCancel = _tasks.Count - 1;
            _tokens[taskIndexToCancel].Cancel();
            _tokens.RemoveAt(taskIndexToCancel);
            _tasks.RemoveAt(taskIndexToCancel);
            --amount;
        }
    }
}