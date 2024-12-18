using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Reflection.Emit;
using System.Text.Json;

namespace VNPay.NetCore
{
    public static class VNPayExtensions
    {
        /// <summary>
        /// Cấu hình VNPayOptions và VNPayService vào Dependency Injection container.
        /// </summary>
        /// <param name="services">IServiceCollection của ứng dụng.</param>
        /// <param name="configureOptions">Hàm cấu hình VNPayOptions.</param>
        /// <returns>IServiceCollection sau khi thêm cấu hình.</returns>
        public static IServiceCollection AddVNPay(this IServiceCollection services, Action<VNPayOptions> configureOptions)
        {
            // Đăng ký memcache để lưu custom data
            services.AddMemoryCache();

            services.AddVNPay<VNPayService>(configureOptions);

            return services;
        }

        /// <summary>
        /// Cấu hình VNPayOptions và VNPayService vào Dependency Injection container.
        /// </summary>
        /// <typeparam name="T">Dịch vụ xử lý tùy chỉnh triển khai IVNPayService</typeparam>
        /// <param name="services">IServiceCollection của ứng dụng.</param>
        /// <param name="configureOptions">Hàm cấu hình VNPayOptions.</param>
        /// <returns>IServiceCollection sau khi thêm cấu hình.</returns>
        public static IServiceCollection AddVNPay<T>(this IServiceCollection services, Action<VNPayOptions> configureOptions)
            where T : class, IVNPayService
        {
            if (configureOptions == null)
            {
                throw new ArgumentNullException(nameof(configureOptions), "ConfigureOptions cannot be null.");
            }

            services.AddHttpContextAccessor();
            services.AddHttpClient<IVNPayService, T>();

            // Đăng ký VNPayOptions từ hàm cấu hình
            services.Configure(configureOptions);

            // Đăng ký IVNPayService với VNPayService
            services.AddScoped<IVNPayService, T>();

            return services;
        }

        /// <summary>
        /// Cấu hình 2 endpoints để xử lý phản hồi của VNPay
        /// </summary>
        public static IEndpointRouteBuilder MapVNPay(this IEndpointRouteBuilder endpoints)
        {
            // Lấy thông tin cấu hình từ VNPayOptions
            var options = endpoints.ServiceProvider.GetRequiredService<IOptions<VNPayOptions>>().Value;

            // Endpoint đầu tiên để xử lý callback (ReturnURL) từ VNPay
            endpoints.MapGet(options.ReturnURL, async context =>
            {
                // Lấy dịch vụ VNPayService từ container DI
                var onePayService = context.RequestServices.GetRequiredService<IVNPayService>();

                // Lấy danh sách các processor đã được đăng ký
                var onePayProcessors = context.RequestServices.GetServices<IVNPayProcessor>().ToArray();

                // Xử lý callback và lấy thông tin trả về
                var (type, response, returnUrl) = await onePayService.ProcessCallBack();

                // Lọc các processor theo loại (nếu có loại phù hợp)
                var processors = onePayProcessors.Where(x => string.IsNullOrEmpty(x.Type) || x.Type == type).ToArray();

                // Gọi phương thức ProcessURL của các processor phù hợp
                foreach (var processor in processors) await processor.ProcessReturnURL(response);

                // Chuyển hướng người dùng đến URL đã đăng ký trong CreatePaymentLink
                context.Response.Redirect(returnUrl);
            });

            // Endpoint thứ hai để xử lý IPN từ VNPay
            endpoints.MapGet(options.IPNURL, async context =>
            {
                // Lấy dịch vụ VNPayService từ container DI
                var onePayService = context.RequestServices.GetRequiredService<IVNPayService>();

                // Lấy danh sách các processor đã được đăng ký
                var onePayProcessors = context.RequestServices.GetServices<IVNPayProcessor>().ToArray();

                try
                {
                    // Xử lý callback và lấy thông tin trả về
                    var (type, response, returnUrl) = await onePayService.ProcessCallBack();

                    // Lọc các processor theo loại (nếu có loại phù hợp)
                    var processors = onePayProcessors.Where(x => string.IsNullOrEmpty(x.Type) || x.Type == type).ToArray();

                    // Gọi phương thức ProcessIPN của các processor phù hợp
                    foreach (var processor in processors) await processor.ProcessIPN(response);

                    // Trả về phản hồi cho VNPay xác nhận đã xử lý
                    await context.Response.WriteAsync(JsonSerializer.Serialize(new { RspCode = "00", Message = string.Empty }));
                }
                catch (Exception ex)
                {
                    // Trả về phản hồi cho VNPay xác nhận đã xử lý
                    await context.Response.WriteAsync(JsonSerializer.Serialize(new { RspCode = "02", Message = ex.Message }));
                }
            });

            // Trả về danh sách endpoints đã cấu hình
            return endpoints;
        }

        /// <summary>
        /// Sinh mã ngẫu nhiên cho giao dịch
        /// </summary>
        /// <param name="length">Độ dài chuỗi</param>
        /// <param name="chars">Các ký tự trong mã</param>
        /// <returns>Mã</returns>
        public static string GenerateRandomString(int length = 12, string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789")
        {
            var random = new Random(Guid.NewGuid().GetHashCode());
            char[] stringChars = new char[length];

            for (int i = 0; i < length; i++)
                stringChars[i] = chars[random.Next(chars.Length)];

            return new string(stringChars);
        }
    }
}
