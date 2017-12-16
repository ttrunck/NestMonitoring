using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace NestMonitoringConsole
{
    public class WebServer
    {
        private static SemaphoreSlim semaphore;
        private static HttpListener listener;
        private static NestCall nestCall;
        private static OpenWeatherCall openWeatherCall;
        private static TelemetryClient tc;

        public WebServer(IConfigurationRoot configuration, TelemetryClient telemetryClient)
        {
            tc = telemetryClient;
            nestCall = new NestCall(configuration, tc);
            openWeatherCall = new OpenWeatherCall(configuration, tc);
            semaphore = new SemaphoreSlim(Environment.ProcessorCount);
            listener = new HttpListener();
            listener.Prefixes.Add(configuration["PREFIX"]);
            listener.IgnoreWriteExceptions = true;
            listener.Start();
        }

        public void Run()
        {
            Task.Run(Loop).Wait();
        }

        private static async Task Loop()
        {
            do
            {
                await semaphore.WaitAsync();
                try
                {
                    var context = await listener.GetContextAsync();
                    string responseString = await GetResponseAsync();
                    var resp = context.Response;
                    var req = context.Request;
                    resp.ContentEncoding = req.ContentEncoding;
                    resp.ContentLength64 = resp.ContentEncoding.GetByteCount(responseString);
                    using (var writer = new StreamWriter(resp.OutputStream, resp.ContentEncoding))
                    {
                        await writer.WriteAsync(responseString);
                    }
                    resp.Close();
                }
                finally
                {
                    semaphore.Release();
                }
            } while (true);
        }

        private static async Task<string> GetResponseAsync()
        {
            var nest = await nestCall.GetNestMetric();
            var openWeather = await openWeatherCall.GetOpenWeatherMetric();
            return string.Join("\n", new[] { nest, openWeather});
        }
    }
}
