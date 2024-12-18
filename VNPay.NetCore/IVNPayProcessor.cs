using System.Threading.Tasks;

namespace VNPay.NetCore
{
    /// <summary>
    /// Đăng ký 1 processor xử lý phản hồi của VNPay
    /// </summary>
    public interface IVNPayProcessor
    {
        /// <summary>
        /// Loại giao dịch đăng ký xử lý, string.Empty nếu muốn xử lý tất cả
        /// </summary>
        string Type => string.Empty;

        /// <summary>
        /// Xử lý trong trường hợp VNPay returnUrl
        /// </summary>
        /// <param name="response">Dữ liệu từ phản hồi của VNPay</param>
        Task ProcessReturnURL(VNPayResponse response) => Task.CompletedTask;

        /// <summary>
        /// Xử lý trong trường hợp VNPay IPN
        /// </summary>
        /// <param name="response">Dữ liệu từ phản hồi của VNPay</param>
        Task ProcessIPN(VNPayResponse response) => Task.CompletedTask;
    }
}
