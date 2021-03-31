using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;
using TwinCAT.Ads;
using TwinCAT.Ads.TcpRouter;

namespace TwinCAT.Ads.AdsRouterService
{
    class Program
    {
        public static void Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("ADS Router Service is Starting!");
            host.Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseWindowsService()
                .ConfigureLogging((_, logging) => logging.AddEventLog())
                .ConfigureAppConfiguration((hostingContext, configuration) =>
                {
                    //configuration.Sources.Clear();

                    IHostEnvironment env = hostingContext.HostingEnvironment;

                    configuration
                        .AddJsonFile("AMSConfiguration.json", optional: false, reloadOnChange: true);

                    IConfigurationRoot configurationRoot = configuration.Build();

                    AMSConfigurationOptions options = new AMSConfigurationOptions();
                    configurationRoot.GetSection(nameof(AMSConfigurationOptions))
                                     .Bind(options);





                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHostedService<RouterWorker>();
                    services.AddOptions();
                    var _settings = hostContext.Configuration.GetSection("AMSConfigurationOptions");
                    services.Configure<AMSConfigurationOptions>(_settings);


                })
                .ConfigureLogging(logging =>
                {
                    // Uncomment to overwrite logging
                    // Microsoft.Extensions.Logging.Console Nuget package
                    // Namespace Microsoft.Extensions.Logging;
                    logging.ClearProviders();
                    logging.AddConsole();
                    logging.AddEventLog();
                });
    }
}

public class AMSConfigurationOptions
{
    public string LocalAMSNetID { get; set; }
    public string TargetAMSNetID { get; set; }
    public string TargetIPAddress { get; set; }
}

public class RouterWorker : BackgroundService
{
    private readonly ILogger<RouterWorker> _logger;
    private readonly IOptions<AMSConfigurationOptions> _settings;



    public RouterWorker(ILogger<RouterWorker> logger, IOptions<AMSConfigurationOptions> settings)
    {
        _logger = logger;
        _settings = settings;
    }

    protected override async Task ExecuteAsync(CancellationToken cancel)
    {
        try
        {


            String LocalAMSNetID = _settings.Value.LocalAMSNetID;
            String TargetAMSNetID = _settings.Value.TargetAMSNetID;
            String TargetIPAddress = _settings.Value.TargetIPAddress;
            //_logger.LogInformation("Local AMS Net ID: {0}", LocalAMSNetID);
            //_logger.LogInformation("Target AMS Net ID: {0}", TargetAMSNetID);
            //_logger.LogInformation("Target IP Address: {0}", TargetIPAddress);

            //Use this overload to instantiate a Router without support of StaticRoutes.xml and parametrize by code
            AmsTcpIpRouter router = new AmsTcpIpRouter(new AmsNetId(LocalAMSNetID),AmsTcpIpRouter.DEFAULT_TCP_PORT,System.Net.IPAddress.Loopback, AmsTcpIpRouter.DEFAULT_TCP_PORT, _logger);
            router.RouterStatusChanged += Router_RouterStatusChanged;

            Route route = new Route("Target", new AmsNetId(TargetAMSNetID), TargetIPAddress);
            //_logger.LogInformation("Route Resolved?: {0}", route.IsResolved);
            

            router.AddRoute(route);

            await router.StartAsync(cancel); // Start the router

        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
        }
    }

    private void Router_RouterStatusChanged(object sender, RouterStatusChangedEventArgs e)
    {
        _logger.LogInformation(e.RouterStatus.ToString());
    }
}
