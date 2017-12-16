using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace NestMonitoringConsole
{
    public class NestCall
    {
        private readonly string token;
        private readonly string deviceId;
        private TelemetryClient tc;
        private IMemoryCache cache;

        public NestCall(IConfigurationRoot configuration, TelemetryClient telemetryClient)
        {
            tc = telemetryClient;
            token = configuration["NESTTOKEN"];
            deviceId = configuration["DEVICEID"];
            cache = new MemoryCache(new MemoryCacheOptions());
        }

        public async Task<string> GetNestMetric()
        {
            try
            {
                var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                string prometheusMetric;
                if (!cache.TryGetValue(nameof(GetNestMetric), out prometheusMetric))
                {
                    var success = false;
                    string content;
                    var startTime = DateTime.UtcNow;
                    var timer = Stopwatch.StartNew();
                    try
                    {
                        var response = await httpClient.GetAsync($"https://developer-api.nest.com/devices/thermostats/{deviceId}");
                        content = await response.Content.ReadAsStringAsync();
                        success = response.IsSuccessStatusCode;
                        if (!success)
                        {
                            tc.TrackTrace($"Call to Nest failed. Status code: {response.StatusCode}, Content: {content}", SeverityLevel.Critical);
                        }
                    }
                    finally
                    {
                        timer.Stop();
                        tc.TrackDependency("NEST_API", "Get Json Thermostat data", startTime, timer.Elapsed, success);
                    }
                    var json = JsonConvert.DeserializeObject<NESTJSON>(content);
                    prometheusMetric = json.ToMetric();
                    cache.Set(nameof(GetNestMetric), prometheusMetric, TimeSpan.FromSeconds(59));
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


        private class NESTJSON
        {
            public DateTime last_connection { get; set; }
            public bool is_online { get; set; }
            public bool can_cool { get; set; }
            public bool can_heat { get; set; }
            public bool has_fan { get; set; }
            public bool has_leaf { get; set; }
            public int target_temperature_f { get; set; }
            public int target_temperature_high_f { get; set; }
            public int target_temperature_low_f { get; set; }
            public int eco_temperature_high_f { get; set; }
            public int eco_temperature_low_f { get; set; }
            public HvacMode hvac_mode { get; set; }
            public int ambient_temperature_f { get; set; }
            public int humidity { get; set; }
            public HvacState hvac_state { get; set; }

            [JsonConverter(typeof(StringEnumConverter))]
            public enum HvacMode
            {
                heat = 3,
                cool = 2,
                [EnumMember(Value = "heat-cool")]
                heatcool = 4,
                eco = 1,
                off = 0
            }

            public enum HvacState
            {
                heating = 1,
                cooling = -1,
                off = 0
            }

            public string ToMetric()
            {
                var res = new List<string>
                {
                    "# HELP last_connection Number of second from the last successful interaction with the Nest service,",
                    "# TYPE last_connection gauge",
                    $"last_connection {(DateTime.UtcNow - last_connection).TotalSeconds}",

                    "# HELP is_online Device connection status with the Nest service.",
                    "# TYPE is_online gauge",
                    $"is_online {(is_online ? 1 : 0)}",

                    "# HELP can_cool System ability to cool (has AC).",
                    "# TYPE can_cool gauge",
                    $"can_cool {(can_cool ? 1 : 0)}",

                    "# HELP can_heat System ability to heat.",
                    "# TYPE can_heat gauge",
                    $"can_heat {(can_heat ? 1 : 0)}",

                    "# HELP has_fan System ability to control the fan independently from heating or cooling.",
                    "# TYPE has_fan gauge",
                    $"has_fan {(has_fan ? 1 : 0)}",

                    "# HELP has_leaf Displayed when the thermostat is set to an energy-saving temperature.",
                    "# TYPE has_leaf gauge",
                    $"has_leaf {(has_leaf ? 1 : 0)}",

                    "# HELP target_temperature_f Desired temperature, in full degrees Fahrenheit (1F). Used when hvac_mode = heat or cool.",
                    "# TYPE target_temperature_f gauge",
                    $"target_temperature_f {target_temperature_f}",

                    "# HELP target_temperature_high_f Maximum target temperature, displayed in whole degrees Fahrenheit (1F). Used when hvac_mode = heat-cool (HeatCool mode).",
                    "# TYPE target_temperature_high_f gauge",
                    $"target_temperature_high_f {target_temperature_high_f}",

                    "# HELP target_temperature_low_f Minimum target temperature, displayed in whole degrees Fahrenheit (1F). Used when hvac_mode = heat-cool (HeatCool mode).",
                    "# TYPE target_temperature_low_f gauge",
                    $"target_temperature_low_f {target_temperature_low_f}",

                    "# HELP eco_temperature_high_f Maximum Eco Temperature, displayed in whole degrees Fahrenheit (1F). Used when hvac_mode = eco.",
                    "# TYPE eco_temperature_high_f gauge",
                    $"eco_temperature_high_f {eco_temperature_high_f}",

                    "# HELP eco_temperature_low_f Minimum Eco Temperature, displayed in whole degrees Fahrenheit (1F). Used when hvac_mode = eco",
                    "# TYPE eco_temperature_low_f gauge",
                    $"eco_temperature_low_f {eco_temperature_low_f}",

                    "# HELP hvac_mode Indicates HVAC system heating/cooling modes, like HeatCool for systems with heating and cooling capacity, or Eco Temperatures for energy savings. (0=off, 1=eco, 2=cool, 3=heat, 4=heat-cool)",
                    "# TYPE hvac_mode gauge",
                    $"hvac_mode {(int)hvac_mode}",

                    "# HELP ambient_temperature_f Temperature, measured at the device, in whole degrees Fahrenheit (1F).",
                    "# TYPE ambient_temperature_f gauge",
                    $"ambient_temperature_f {ambient_temperature_f}",

                    "# HELP humidity Humidity, in percent (%) format, measured at the device, rounded to the nearest 5%.",
                    "# TYPE humidity gauge",
                    $"humidity {humidity}",

                    "# HELP Indicates whether HVAC system is actively heating, cooling or is off. Use this value to indicate HVAC activity state. (0=off, 1=heating, -1=cooling)",
                    "# TYPE hvac_state gauge",
                    $"hvac_state {(int)hvac_state}"
                };

                return string.Join("\n", res);
            }
        }
    }
}
