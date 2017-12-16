using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace NestMonitoringConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("config.json", optional: true, reloadOnChange: true);

            IConfigurationRoot configuration = builder.Build();

            TelemetryConfiguration.Active.InstrumentationKey = configuration["APPINSIGHTS_INSTRUMENTATIONKEY"];
            TelemetryClient tc = new TelemetryClient();
            tc.TrackTrace("This is a test for real real", SeverityLevel.Critical);

            var webServer = new WebServer(configuration, tc);
            webServer.Run();
        }
    }
}

