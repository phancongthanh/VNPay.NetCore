using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Globalization;
using VNPay.NetCore.Web.Services;

namespace VNPay.NetCore.Web.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class VNPayTestController : ControllerBase
    {
        private readonly ILogger<VNPayTestController> _logger;
        private readonly IVNPayService _onePayService;
        private readonly IMemoryCache _cache;

        public VNPayTestController(ILogger<VNPayTestController> logger, IVNPayService onePayService, IMemoryCache cache)
        {
            _logger = logger;
            _onePayService = onePayService;
            _cache = cache;
        }

        [HttpPost]
        public async Task<string> CreatePaymentUrl([FromQuery] VNPayRequest request, [FromQuery] string? returnUrl = null)
        {
            if (string.IsNullOrEmpty(request.RequestCode)) return string.Empty;
            if (string.IsNullOrEmpty(returnUrl))
            {
                returnUrl = Url.Action(nameof(GetResponse), new { code = request.RequestCode });
            }
            var url = await _onePayService.CreatePaymentLink(TestVNPayProcessor.TYPE, request, returnUrl);
            return url;
        }

        [HttpGet]
        public async Task<object> GetResponse(string code)
        {
            var returnurlResponse = _cache.Get<VNPayResponse>($"ReturnURL-{code}");
            var ipnResponse = _cache.Get<VNPayResponse>($"IPN-{code}");
            var paytime = DateTime.ParseExact(returnurlResponse.VNPData["PayDate"], "yyyyMMddHHmmss", CultureInfo.InvariantCulture);
            var (result, vnpData) = await _onePayService.QueryDR(returnurlResponse.RequestCode, returnurlResponse.OrderCode, paytime);
            return new { returnurlResponse, ipnResponse, querydr = new { result, vnpData } };
        }
    }
}
