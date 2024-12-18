using System;

namespace VNPay.NetCore
{
    /// <summary>
    /// Cấu hình VNPay
    /// </summary>
    public class VNPayOptions
    {
        /// <summary>
        /// Host của VNPay
        /// </summary>
        public string ApiUrl { get; set; } = "https://sandbox.vnpayment.vn";
        public string ReturnURL { get; set; } = "/vnpay/return";
        public string IPNURL { get; set; } = "/vnpay/ipn";

        /// <summary>
        /// TmnCode do VNPay cấp
        /// </summary>
        public string TmnCode { get; set; } = string.Empty;
        /// <summary>
        /// Mã Hash do VNPay cấp
        /// </summary>
        public string SecureHash { get; set; } = string.Empty;

        /// <summary>
        /// Thời gian hết hạn thanh toán
        /// </summary>
        public TimeSpan ExpireTimeSpan { get; set; } = TimeSpan.FromHours(12);
    }
}
