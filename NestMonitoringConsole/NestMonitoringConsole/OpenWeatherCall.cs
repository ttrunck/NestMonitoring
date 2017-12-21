using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;

namespace NestMonitoringConsole
{
    public class OpenWeatherCall
    {
        private readonly string token;
        private readonly string cityId;
        private TelemetryClient tc;
        private IMemoryCache cache;
        private HttpClient httpClient;

        public OpenWeatherCall(IConfigurationRoot configuration, TelemetryClient telemetryClient)
        {
            tc = telemetryClient;
            token = configuration["OPENWEATHERTOKEN"];
            cityId = configuration["CITYID"];
            cache = new MemoryCache(new MemoryCacheOptions());
            httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(10)
            };
        }

        public async Task<string> GetOpenWeatherMetric()
        {
            try
            {
                string prometheusMetric;
                if (!cache.TryGetValue(nameof(GetOpenWeatherMetric), out prometheusMetric))
                {
                    var success = false;
                    string content;
                    var startTime = DateTime.UtcNow;
                    var timer = Stopwatch.StartNew();
                    try
                    {
                        var response = await httpClient.GetAsync($"http://api.openweathermap.org/data/2.5/weather?id={cityId}&&units=imperial&appid={token}");
                        content = await response.Content.ReadAsStringAsync();
                        success = response.IsSuccessStatusCode;
                        if (!success)
                        {
                            tc.TrackTrace($"Call to OpenWeather failed. Status code: {response.StatusCode}, Content: {content}", SeverityLevel.Critical);
                            return "";
                        }
                    }
                    finally
                    {
                        timer.Stop();
                        tc.TrackDependency("OPENWEATHER_API", "Get Json weather", startTime, timer.Elapsed, success);
                    }
                    var json = JsonConvert.DeserializeObject<OPENWEATHERJSON>(content);
                    prometheusMetric = json.ToMetric();
                    cache.Set(nameof(GetOpenWeatherMetric), prometheusMetric, TimeSpan.FromSeconds(359));
                    tc.TrackTrace($"Received content: {content}" + Environment.NewLine + $"Prometheus metric: {prometheusMetric}", SeverityLevel.Verbose);
                }
                return prometheusMetric;
            }
            catch (Exception e)
            {
                tc.TrackException(e);
                return "";
            }
        }

        private class OPENWEATHERJSON
        {
            public Main main { get; set; }

            public class Main
            {
                public double temp { get; set; }
                public double pressure { get; set; }
                public double humidity { get; set; }
                public double temp_min { get; set; }
                public double temp_max { get; set; }
            }

            public string ToMetric()
            {
                var res = new List<string>
                {
                    "# HELP openweather_temperature Temperature, Fahrenheit",
                    "# TYPE openweather_temperature gauge",
                    $"openweather_temperature {main.temp}",

                    "# HELP openweather_pressure Atmospheric pressure (on the sea level, if there is no sea_level or grnd_level data), hPa",
                    "# TYPE openweather_pressure gauge",
                    $"openweather_pressure {main.pressure}",

                    "# HELP openweather_humidity Humidity, %",
                    "# TYPE openweather_humidity gauge",
                    $"openweather_humidity {main.humidity}",

                    "# HELP openweather_temperature_min Minimum temperature at the moment. This is deviation from current temp that is possible for large cities and megalopolises geographically expanded (use these parameter optionally). Fahrenheit",
                    "# TYPE openweather_temperature_min gauge",
                    $"openweather_temperature_min {main.temp_min}",

                    "# HELP openweather_temperature_max Maximum temperature at the moment. This is deviation from current temp that is possible for large cities and megalopolises geographically expanded (use these parameter optionally). Fahrenheit.",
                    "# TYPE openweather_temperature_max gauge",
                    $"openweather_temperature_max {main.temp_max}",
                };

                return string.Join("\n", res);
            }
        }
    }
}
