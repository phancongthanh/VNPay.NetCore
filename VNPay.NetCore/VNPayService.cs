using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace VNPay.NetCore
{
    public class VNPayService : IVNPayService
    {
        protected readonly VNPayOptions _options;
        protected readonly IHttpContextAccessor _httpContextAccessor;
        protected readonly HttpClient _httpClient;

        protected string CacheKey => nameof(VNPayService);

        public VNPayService(IOptions<VNPayOptions> options, IHttpContextAccessor httpContextAccessor, HttpClient httpClient)
        {
            _httpContextAccessor = httpContextAccessor;
            _options = options.Value;
            _httpClient = httpClient;
        }

        protected Task SetCustomData(string requestCode, IDictionary<string, string> customData)
        {
            var httpContext = _httpContextAccessor.HttpContext
                ?? throw new NullReferenceException("httpContext is null!");

            var memoryCache = httpContext.RequestServices.GetRequiredService<IMemoryCache>();
            // Lưu trữ custome data trong memory cache
            memoryCache.Set($"{CacheKey}-{requestCode}", customData, TimeSpan.FromMinutes(10));
            return Task.CompletedTask;
        }

        protected Task<IDictionary<string, string>?> GetCustomData(string requestCode)
        {
            var httpContext = _httpContextAccessor.HttpContext
                ?? throw new NullReferenceException("httpContext is null!");

            var memoryCache = httpContext.RequestServices.GetRequiredService<IMemoryCache>();
            // Lưu trữ custome data trong memory cache
            var customData = memoryCache.Get<IDictionary<string, string>>($"{CacheKey}-{requestCode}");
            return Task.FromResult<IDictionary<string, string>?>(customData);
        }

        public async Task<string> CreatePaymentLink(string type, VNPayRequest request, string returnUrl)
        {
            // Tạo đối tượng VnPayLibrary để xây dựng URL
            var vnpay = new VnPayLibrary();

            // Thêm các trường bắt buộc vào yêu cầu thanh toán
            vnpay.AddRequestData("vnp_Version", VnPayLibrary.VERSION);
            vnpay.AddRequestData("vnp_Command", "pay");
            vnpay.AddRequestData("vnp_CurrCode", "VND"); // Loại tiền tệ, ở đây là VNĐ
            vnpay.AddRequestData("vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss"));
            vnpay.AddRequestData("vnp_TmnCode", _options.TmnCode); // TmnCode cung cấp bởi VNPay
            vnpay.AddRequestData("vnp_Locale", "vn"); // Ngôn ngữ giao diện thanh toán, ở đây là tiếng Việt
            vnpay.AddRequestData("vnp_OrderType", "other"); //default value: other

            // Thêm URL callback để nhận kết quả trả về từ VNPay sau khi thanh toán
            vnpay.AddRequestData("vnp_ReturnUrl", await GetAbsoluteUrl(_options.ReturnURL));
            vnpay.AddRequestData("vnp_ExpireDate", DateTime.Now.Add(_options.ExpireTimeSpan).ToString("yyyyMMddHHmmss"));

            // Thêm các thông tin giao dịch
            vnpay.AddRequestData("vnp_TxnRef", request.RequestCode); // Mã giao dịch của TmnCode
            vnpay.AddRequestData("vnp_OrderInfo", request.OrderCode); // Mã đơn hàng
            vnpay.AddRequestData("vnp_Amount", request.Amount.ToString("0", CultureInfo.InvariantCulture) + "00"); // Số tiền (thêm hai chữ số thập phân)

            // Thêm địa chỉ IP của khách hàng
            var ip = await GetClientIp(); // Lấy địa chỉ IP của client
            vnpay.AddRequestData("vnp_IpAddr", ip);

            // Thêm thông tin xử lý
            var customData = new Dictionary<string, string>();
            customData.Add("user_Type", type); // Loại request
            customData.Add("user_returnUrl", returnUrl); // URL tùy chỉnh để quay lại sau khi xử lý

            // Thêm các dữ liệu VNP tuỳ chỉnh, nếu có
            if (request.VNPData != null)
                foreach (var item in request.Data)
                    vnpay.AddRequestData($"vnp_{item.Key}", item.Value);

            // Thêm các dữ liệu tuỳ chỉnh của người dùng, nếu có
            if (request.Data != null)
                foreach (var item in request.Data)
                    customData.Add($"user_{item.Key}", item.Value);

            // Tạo chuỗi truy vấn URL chứa toàn bộ thông tin giao dịch và chữ ký bảo mật
            var url = vnpay.CreateRequestUrl($"{_options.ApiUrl}/paymentv2/vpcpay.html", _options.SecureHash);

            // Lưu trữ custome data
            await SetCustomData(request.RequestCode, customData);

            // Trả về đường dẫn thanh toán
            return url;
        }

        /// <summary>
        /// Lấy đường dẫn tuyệt đối cho returnUrl
        /// </summary>
        protected virtual Task<string> GetAbsoluteUrl(string relativePath)
        {
            var httpContext = _httpContextAccessor.HttpContext
                ?? throw new NullReferenceException("httpContext is null!");

            var request = httpContext.Request;
            var host = request.Host.HasValue ? request.Host.Value : throw new InvalidOperationException("Request host is missing.");
            var scheme = request.Scheme;

            return Task.FromResult($"{scheme}://{host}{relativePath}");
        }

        /// <summary>
        /// Lấy Ip người dùng hiện tại
        /// </summary>
        protected virtual Task<string?> GetClientIp()
        {
            var httpContext = _httpContextAccessor.HttpContext
                ?? throw new NullReferenceException("httpContext is null!");

            // Kiểm tra header X-Forwarded-For nếu ứng dụng chạy phía sau proxy hoặc load balancer
            var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].ToString();

            // Nếu header có giá trị, trả về địa chỉ IP đầu tiên (client thực sự)
            if (!string.IsNullOrEmpty(forwardedFor))
            {
                return Task.FromResult(forwardedFor.Split(',').FirstOrDefault());
            }

            // Nếu không có header X-Forwarded-For, lấy IP từ RemoteIpAddress
            return Task.FromResult(httpContext.Connection.RemoteIpAddress?.ToString());
        }

        public async Task<(string type, VNPayResponse response, string returnUrl)> ProcessCallBack()
        {
            // Lấy thông tin HTTP context từ accessor
            var httpContext = _httpContextAccessor.HttpContext
                ?? throw new NullReferenceException("httpContext is null!"); // Kiểm tra null để đảm bảo context tồn tại

            // Tạo VnPayLibrary để xử lý các tham số từ query string
            var vnpay = new VnPayLibrary();
            // Duyệt qua tất cả các key trong query string
            foreach (var item in httpContext.Request.Query.Where(x => !string.IsNullOrEmpty(x.Key) && x.Key.StartsWith("vnp_")))
            {
                vnpay.AddResponseData(item.Key, item.Value.ToString()); // Thêm key-value vào VnPayLibrary
            }
            
            var vnp_ResponseCode = vnpay.GetResponseData("vnp_ResponseCode");
            var vnp_TransactionStatus = vnpay.GetResponseData("vnp_TransactionStatus");
            var vnp_SecureHash = httpContext.Request.Query["vnp_SecureHash"].ToString();
            var checkSignature = vnpay.ValidateSignature(vnp_SecureHash, _options.SecureHash);

            // Xác định trạng thái giao dịch dựa vào kết quả kiểm tra hash và mã phản hồi
            bool? result = checkSignature && vnp_ResponseCode == "00" && vnp_TransactionStatus == "00" ? (bool?)true : // Giao dịch thành công
                           !checkSignature ? (bool?)null : // Giao dịch đang xử lý
                           false; // Giao dịch thất bại

            // Lấy các thông tin quan trọng từ phản hồi
            var requestCode = vnpay.GetResponseData("vnp_TxnRef");
            var code = vnpay.GetResponseData("vnp_OrderInfo"); // Mã đơn hàng
            // Chuyển đổi số tiền từ chuỗi sang kiểu decimal, nếu không chuyển được thì gán giá trị mặc định
            if (!decimal.TryParse(vnpay.GetResponseData("vnp_Amount"), out var amount))
                amount = 0;
            amount /= 100; // Chia 100 để lấy giá trị thực (do API trả về số tiền nhân với 100)

            var customData = await GetCustomData(requestCode) ?? new Dictionary<string,string>();

            var type = customData["user_Type"]; // Loại giao dịch (do user cung cấp)
            var returnUrl = customData["user_returnUrl"]; // URL để người dùng quay lại

            // Lấy các trường dữ liệu bắt đầu bằng "vnp_" và lưu vào dictionary
            var fields = vnpay.GetResult();
            var vnpData = fields
                .Where(x => x.Key.StartsWith("vnp_")) // Chỉ lấy các trường bắt đầu bằng "vnp_"
                .ToDictionary(x => x.Key.Substring(4), x => x.Value.ToString()); // Bỏ tiền tố "vnp_" trong key

            // Lấy các trường dữ liệu bắt đầu bằng "user_" (trừ một số trường cố định) và lưu vào dictionary
            var data = customData
                .Where(x => x.Key.StartsWith("user_")) // Chỉ lấy các trường bắt đầu bằng "user_"
                .Where(x => x.Key != "user_Type" && x.Key != "user_returnUrl") // Loại bỏ các trường "user_Type" và "user_returnUrl"
                .ToDictionary(x => x.Key.Substring(5), x => x.Value.ToString()); // Bỏ tiền tố "user_" trong key

            // Tạo đối tượng VNPayResponse chứa thông tin phản hồi
            var response = new VNPayResponse
            {
                Result = result, // Kết quả giao dịch
                RequestCode = requestCode, // Mã giao dịch
                OrderCode = code, // Mã đơn hàng
                Amount = amount, // Số tiền giao dịch
                VNPData = vnpData, // Các dữ liệu từ trường "vnp_"
                Data = data // Các dữ liệu từ trường "user_"
            };

            // Trả về tuple gồm loại giao dịch (type), phản hồi (response) và URL quay lại (returnUrl)
            return (type, response, returnUrl);
        }

        public async Task<(bool? result, IDictionary<string, string> data)> QueryDR(string requestCode, string orderCode, DateTime transactionDate)
        {
            // URL endpoint của API VNPay
            var url = $"{_options.ApiUrl}/merchant_webapi/api/transaction";

            // Dữ liệu gửi trong body của yêu cầu POST
            var requestData = new Dictionary<string, string>
            {
                { "vnp_RequestId", Guid.NewGuid().ToString() },
                { "vnp_Version", VnPayLibrary.VERSION }, // Phiên bản API
                { "vnp_Command", "querydr" }, // Lệnh gọi API, mặc định là "querydr"
                { "vnp_TmnCode", _options.TmnCode }, // TmnCode ID cung cấp bởi VNPay
                { "vnp_TxnRef", requestCode }, // Mã giao dịch cần truy vấn
                { "vnp_TransactionDate", transactionDate.ToString("yyyyMMddHHmmss") }, // Thời gian giao dịch
                { "vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss") },
                { "vnp_IpAddr", await GetClientIp() },
                { "vnp_OrderInfo", orderCode }, // Mã sản phẩm cần truy vấn
            };

            // Tạo chuỗi signData từ requestData
            var signData = string.Join("|", new[]
            {
                requestData["vnp_RequestId"],
                requestData["vnp_Version"],
                requestData["vnp_Command"],
                requestData["vnp_TmnCode"],
                requestData["vnp_TxnRef"],
                requestData["vnp_TransactionDate"],
                requestData["vnp_CreateDate"],
                requestData["vnp_IpAddr"],
                requestData["vnp_OrderInfo"]
            });

            // Tạo chữ ký bảo mật (vnp_SecureHash) bằng thuật toán HmacSHA512
            var secureHash = Utils.HmacSHA512(_options.SecureHash, signData);
            requestData.Add("vnp_SecureHash", secureHash); // Thêm chữ ký vào requestData

            // Gửi yêu cầu POST đến API
            var response = await _httpClient.PostAsJsonAsync(url, requestData);

            // Kiểm tra nếu phản hồi thành công (HTTP 200)
            response.EnsureSuccessStatusCode();

            // Đọc phản hồi từ API dưới dạng Dictionary<string, string>
            var data = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>() ?? new Dictionary<string, string>();

            // Tạo chuỗi signData từ data
            var keys = new[]
            {
                "vnp_ResponseId",
                "vnp_Command",
                "vnp_ResponseCode",
                "vnp_Message",
                "vnp_TmnCode",
                "vnp_TxnRef",
                "vnp_Amount",
                "vnp_BankCode",
                "vnp_PayDate",
                "vnp_TransactionNo",
                "vnp_TransactionType",
                "vnp_TransactionStatus",
                "vnp_OrderInfo",
                "vnp_PromotionCode",
                "vnp_PromotionAmount"
            };
            signData = string.Join("|", keys.Select(key => data.ContainsKey(key) ? data[key] : ""));

            // Kiểm tra tính toàn vẹn của hash trong phản hồi (vnp_SecureHash)
            var checkSignature = Utils.HmacSHA512(_options.SecureHash, signData) == data["vnp_SecureHash"];
            var vnp_ResponseCode = data["vnp_ResponseCode"];
            var vnp_TransactionStatus = data["vnp_TransactionStatus"];

            // Xác định trạng thái giao dịch dựa vào kết quả kiểm tra hash và mã phản hồi
            bool? result = checkSignature && vnp_ResponseCode == "00" && vnp_TransactionStatus == "00" ? (bool?)true : // Giao dịch thành công
                           !checkSignature ? (bool?)null : // Giao dịch đang xử lý
                           false; // Giao dịch thất bại

            // Lấy danh sách các trường bắt đầu với "vnp_" từ kết quả trả về
            var vnpData = data
                .Where(x => x.Key.StartsWith("vnp_")) // Lọc các trường có tiền tố "vnp_"
                .ToDictionary(x => x.Key.Substring(4), x => x.Value.ToString()); // Loại bỏ tiền tố "vnp_" khi tạo dictionary

            // Trả về kết quả giao dịch và dữ liệu vnp_
            return (result, vnpData);
        }
    }
}
