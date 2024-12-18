# VNPay.NetCore

VNPay.NetCore là một thư viện được thiết kế để tích hợp cổng thanh toán VNPay vào các ứng dụng .NET Core. Nó giúp đơn giản hóa quá trình tạo liên kết thanh toán, xử lý callback (ReturnURL, IPN) và xử lý phản hồi thanh toán.

## Tính năng

- **Tích hợp Dependency Injection**: Dễ dàng cấu hình `VNPayOptions` và `VNPayService` với Dependency Injection.
- **Dịch vụ Thanh toán Tùy chỉnh**: Cho phép sử dụng các triển khai tùy chỉnh của interface `IVNPayService` để xử lý thanh toán.
- **Xử lý Callback**: Hỗ trợ tự động cấu hình các endpoints để xử lý các callback từ VNPay (ReturnURL, IPN).
- **Tạo Liên kết Thanh toán**: Cho phép tạo liên kết thanh toán cho các giao dịch VNPay.
- **Tích hợp Processor**: Hỗ trợ thêm các processor tùy chỉnh để xử lý dữ liệu trả về từ VNPay.

## Cài đặt

Để cài đặt VNPay.NetCore, sử dụng NuGet Package Manager hoặc .NET CLI:

```bash
dotnet add package VNPay.NetCore
```

## Cấu hình và Thiết lập

### 1. Cấu hình VNPay trong `Startup.cs` hoặc `Program.cs`

Trong `Startup.cs` hoặc `Program.cs` của ứng dụng, đăng ký các dịch vụ của VNPay và cấu hình các tùy chọn.

```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddVNPay(options => Configuration.GetSection("VNPay").Bind(options));
    services.AddTransient<IVNPayProcessor, TestVNPayProcessor>();
}
```

### 2. Định tuyến các Endpoints của VNPay

Trong phương thức `Configure`, cấu hình các endpoint để xử lý các phản hồi ReturnURL và IPN từ VNPay.

```csharp
public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
{
    app.MapVNPay();
}
```

### 3. Tạo Liên kết Thanh toán

Sử dụng `VNPayService` để tạo một liên kết thanh toán cho giao dịch.

```csharp
public class SomeClass
{
    private readonly IVNPayService _VNPayService;

    public SomeClass(IVNPayService VNPayService)
    {
        _VNPayService = VNPayService;
    }

    public async Task CreateLink(VNPayRequest request, string returnUrl)
    {
        var url = await _VNPayService.CreatePaymentLink(TestVNPayProcessor.TYPE, request, returnUrl);
    }
}
```

## Tùy chỉnh Dịch vụ Thanh toán

Bạn có thể ghi đè `VNPayService` hoặc triển khai dịch vụ `IVNPayService` của riêng mình và đăng ký nó trong DI container. Dưới đây là ví dụ:

```csharp
public class CustomVNPayService : VNPayService
{
    // Ghi đè các phương thức xử lý yêu cầu ở đây
}
```

Sau đó, đăng ký dịch vụ trong DI container:

```csharp
services.AddVNPay<CustomVNPayService>(options => Configuration.GetSection("VNPay").Bind(options));
```

## Xử lý Callback từ VNPay

Thư viện tự động cấu hình hai endpoint để xử lý callback URLs từ VNPay, bao gồm ReturnURL và IPN:

1. **ReturnURL**: Xử lý qua endpoint `MapGet(options.ReturnURL)`.
2. **IPN**: Xử lý qua endpoint `MapGet(options.IPNURL)`.

Bạn có thể tùy chỉnh cách xử lý phản hồi bằng cách triển khai interface `IVNPayProcessor` và đăng ký các processor tùy chỉnh.

Ví dụ về một processor đơn giản:

```csharp
public class TestVNPayProcessor : IVNPayProcessor
{
    public const string TYPE = nameof(TestVNPayProcessor);
    private readonly ILogger<TestVNPayProcessor> _logger;

    public string Type => TYPE;

    public TestVNPayProcessor(ILogger<TestVNPayProcessor> logger)
    {
        _logger = logger;
    }

    public Task ProcessReturnURL(VNPayResponse response)
    {
        _logger.LogInformation("Đang xử lý ReturnURL cho mã yêu cầu: {0}", response.RequestCode);
        return Task.CompletedTask;
    }

    public Task ProcessIPN(VNPayResponse response)
    {
        _logger.LogInformation("Đang xử lý IPN cho mã yêu cầu: {0}", response.RequestCode);
        return Task.CompletedTask;
    }
}
```

### Đăng ký Processor Tùy chỉnh

Đăng ký processor tùy chỉnh như sau:

```csharp
services.AddTransient<IVNPayProcessor, TestVNPayProcessor>();
```

## Cấu hình Ví dụ

```json
{
  "VNPay": {
    "ApiUrl": "https://sandbox.vnpayment.vn",
    "TmnCode": "",
    "SecureHash": ""
  }
}
```

## Giấy phép

VNPay.NetCore là một thư viện mã nguồn mở và được cấp phép dưới [Giấy phép MIT](LICENSE). Bạn có thể tự do sử dụng, sửa đổi và phân phối thư viện trong các dự án của mình.

## Cảnh báo

- Để đảm bảo tính bảo mật, tác giả khuyến khích bạn tham khảo/tải về và tùy chỉnh mã nguồn của thư viện này để phù hợp với yêu cầu bảo mật cao hơn cho hệ thống của bạn trong môi trường sản xuất với các giao dịch thanh toán tiền **THẬT**.
- Mặc dù thư viện này đã được thiết kế để hỗ trợ tích hợp VNPay, bạn có thể tùy chỉnh và tối ưu mã nguồn để đáp ứng các yêu cầu bảo mật cao hơn, nếu cần thiết.
- Hướng dẫn chi tiết về quy trình tích hợp dịch vụ thanh toán VNPay có sẵn tại [**đây**](https://sandbox.vnpayment.vn/apis/docs/thanh-toan-pay/pay.html).
