using System.Collections.Generic;

namespace VNPay.NetCore
{
    public class VNPayRequest
    {
        /// <summary>
        /// Mã giao dịch là duy nhất
        /// </summary>
        public string RequestCode { get; set; } = VNPayExtensions.GenerateRandomString();

        /// <summary>
        /// Mã sản phẩm
        /// </summary>
        public string OrderCode { get; set; } = string.Empty;

        /// <summary>
        /// Số tiền
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// Các thông tin của vnp (không bao gồm vnp_ trong key)
        /// </summary>
        public IDictionary<string, string> VNPData { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Các thông tin tuy chỉnh
        /// </summary>
        public IDictionary<string, string> Data { get; set; } = new Dictionary<string, string>();
    }
}
