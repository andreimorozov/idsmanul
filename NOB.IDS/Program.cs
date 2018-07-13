using System;
using System.IO;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;

// ReSharper disable ArgumentsStyleLiteral

namespace NOB.IDS
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.Title = "IDS.NOB";

            //var seed = args.Any(x => x == "/seed");
            //if (seed) args = args.Except(new[] {"/seed"}).ToArray();

            var host = BuildWebHost(args);

            //if (seed)
            //{
            //    SeedData.EnsureSeedData_AspNetUsers(host.Services);
            //    return;
            //}

            //un-comment to explicitly ensure AspNetUser store
            //SeedData.EnsureSeedData_AspNetUsers(host.Services);
            //return;

            //un-comment to explicitly ensure Persistent store
            //SeedData.EnsureSeedData_PersistentStore(host.Services);
            //return;

            host.Run();
        }

        public static IWebHost BuildWebHost(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}.json",
                    optional: true, reloadOnChange: true)
                .AddCommandLine(args)
                .Build();

            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .CreateLogger();
            
            return WebHost.CreateDefaultBuilder(args)
                .UseConfiguration(configuration)
                .UseStartup<Startup>()
                .ConfigureLogging(builder =>
                {
                    builder.ClearProviders();
                    builder.AddSerilog();
                })
                .Build();
        }
    }
}