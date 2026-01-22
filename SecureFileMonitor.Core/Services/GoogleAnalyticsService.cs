using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.IO;

namespace SecureFileMonitor.Core.Services
{
    public class GoogleAnalyticsService : IAnalyticsService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<GoogleAnalyticsService> _logger;
        private const string MeasurementId = "G-P3FJP55E0E";
        private const string ApiSecret = "q_VVWm8GRpGKIxvqxmZr7g";
        private const string BaseUrl = "https://www.google-analytics.com/mp/collect";
        
        private string _clientId;

        public GoogleAnalyticsService(ILogger<GoogleAnalyticsService> logger)
        {
            _logger = logger;
            _httpClient = new HttpClient();
            _clientId = GetOrCreateClientId();
        }

        private string GetOrCreateClientId()
        {
            var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SecureFileMonitor");
            Directory.CreateDirectory(appData);
            var idFile = Path.Combine(appData, "client_id.txt");

            if (File.Exists(idFile))
            {
                return File.ReadAllText(idFile).Trim();
            }
            else
            {
                var id = Guid.NewGuid().ToString();
                File.WriteAllText(idFile, id);
                return id;
            }
        }

        public async Task LogEventAsync(string eventName, Dictionary<string, object>? parameters = null)
        {
            try
            {
                var url = $"{BaseUrl}?measurement_id={MeasurementId}&api_secret={ApiSecret}";

                var payload = new
                {
                    client_id = _clientId,
                    events = new[]
                    {
                        new
                        {
                            name = eventName,
                            @params = parameters ?? new Dictionary<string, object>()
                        }
                    }
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                // Fire and forget - don't block the app for analytics
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var response = await _httpClient.PostAsync(url, content);
                        if (!response.IsSuccessStatusCode)
                        {
                            var error = await response.Content.ReadAsStringAsync();
                            _logger.LogWarning($"Analytics failed: {response.StatusCode} - {error}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Analytics error: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Analytics preparation error: {ex.Message}");
            }
        }
    }
}
