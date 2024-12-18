using Microsoft.Extensions.Caching.Memory;
using VNPay.NetCore.Web.Controllers;
using System.Text.Json;

namespace VNPay.NetCore.Web.Services
{
    public class TestVNPayProcessor : IVNPayProcessor
    {
        public const string TYPE = nameof(TestVNPayProcessor);

        private readonly ILogger<VNPayTestController> _logger;
        private readonly IMemoryCache _cache;

        public string Type => TYPE;

        public TestVNPayProcessor(ILogger<VNPayTestController> logger, IMemoryCache cache)
        {
            _logger = logger;
            _cache = cache;
        }

        public Task ProcessReturnURL(VNPayResponse response)
        {
            _cache.Set($"ReturnURL-{response.RequestCode}", response);

            var data = JsonSerializer.Serialize(response);
            _logger.LogInformation("Process ReturnURL, response: {0}", data);
            return Task.CompletedTask;
        }

        public Task ProcessIPN(VNPayResponse response)
        {
            _cache.Set($"IPN-{response.RequestCode}", response);

            var data = JsonSerializer.Serialize(response);
            _logger.LogInformation("Process IPN, response: {0}", data);
            return Task.CompletedTask;
        }
    }
}
