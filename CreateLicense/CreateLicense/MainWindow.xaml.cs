using System;
using System.Linq;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace PDFMeger
{
    public partial class CreateLicense : Window
    {
        // Secret Key dài hơn để tăng cường độ bảo mật cho HMAC-SHA512
        private const string SECRET_SALT = "HT_PDF_MERGER_SUPER_SECRET_SECURITY_SALT_2026_ENTERPRISE_KEY_X98A_V5";

        public CreateLicense()
        {
            InitializeComponent();
            DpExpiry.SelectedDate = new DateTime(2026, 10, 12);
        }

        private void BtnCurrentMachine_Click(object sender, RoutedEventArgs e)
        {
            TxtHwidInput.Text = GetEthernetMacAddress();
        }

        private string GetEthernetMacAddress()
        {
            try
            {
                var ethernetInterface = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(nic => nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet
                               && nic.OperationalStatus == OperationalStatus.Up
                               && !nic.Description.ToLower().Contains("virtual")
                               && !nic.Description.ToLower().Contains("pseudo"))
                    .FirstOrDefault();

                if (ethernetInterface == null)
                {
                    ethernetInterface = NetworkInterface.GetAllNetworkInterfaces()
                        .Where(nic => nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet
                                   && !nic.Description.ToLower().Contains("virtual")
                                   && !nic.Description.ToLower().Contains("pseudo"))
                        .FirstOrDefault();
                }

                if (ethernetInterface != null)
                {
                    byte[] macBytes = ethernetInterface.GetPhysicalAddress().GetAddressBytes();
                    if (macBytes.Length > 0)
                    {
                        return string.Join("-", macBytes.Select(b => b.ToString("X2")));
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi khi lấy MAC Ethernet: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return "NOT-FOUND";
        }

        // Tạo mã License SIÊU DÀI (Gấp 5 lần phiên bản cũ)
        private void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            string macAddress = TxtHwidInput.Text.Trim().ToUpper();
            DateTime? expiry = DpExpiry.SelectedDate;

            if (string.IsNullOrEmpty(macAddress) || macAddress == "NOT-FOUND")
            {
                MessageBox.Show("Vui lòng nhập hoặc kiểm tra lại Địa chỉ MAC của khách hàng!", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!expiry.HasValue)
            {
                MessageBox.Show("Vui lòng chọn ngày hết hạn!", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 1. Nhân bản dữ liệu entropy (Noise/Padding) bằng các chuỗi GUID ngẫu nhiên để kéo dài kích thước
            string paddingBlock1 = Guid.NewGuid().ToString("N").ToUpper() + Guid.NewGuid().ToString("N").ToUpper();
            string paddingBlock2 = Guid.NewGuid().ToString("N").ToUpper() + Guid.NewGuid().ToString("N").ToUpper();

            // 2. Định dạng thông tin chính
            string expiryStr = expiry.Value.ToString("yyyy-MM-dd-HH-mm-ss");
            string issueDateStr = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss-ffff");

            // 3. Ghép Payload dài gấp nhiều lần bao gồm Metadata đệm
            // Cấu trúc: [PAD1]|[MAC]|[EXPIRY]|[ISSUE_DATE]|[PAD2]
            string rawPayload = $"{paddingBlock1}|{macAddress}|{expiryStr}|{issueDateStr}|{paddingBlock2}";

            // 4. Tạo Chữ ký HMAC-SHA512 đầy đủ (128 ký tự Hex)
            string signatureHex = ComputeHmacSha512(rawPayload, SECRET_SALT);

            // 5. Kết hợp toàn bộ Raw Payload + Signature + Additional Hardware Checksum
            string fullData = $"{rawPayload}|SIG:{signatureHex}|APP:HT_PDF_MERGER_V2026_ENTERPRISE_LICENSING_SYSTEM";

            // 6. Mã hóa Base64 cho toàn bộ chuỗi siêu dài
            string base64Token = Convert.ToBase64String(Encoding.UTF8.GetBytes(fullData));

            // 7. Định dạng chuỗi theo từng nhóm 8 ký tự
            string formattedKey = FormatLicenseKey(base64Token);

            TxtOutputKey.Text = formattedKey;
            TxtOutputKey.Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#1E293B");
        }

        /// <summary>
        /// Sử dụng HMAC-SHA512 để có độ dài chữ ký 512 bit (128 ký tự Hex)
        /// </summary>
        private string ComputeHmacSha512(string data, string key)
        {
            using (var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(key)))
            {
                byte[] hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
                return BitConverter.ToString(hashBytes).Replace("-", ""); // Trả về đủ 128 ký tự
            }
        }

        /// <summary>
        /// Chia đoạn chuỗi mã hóa thành dạng Key
        /// </summary>
        private string FormatLicenseKey(string rawBase64)
        {
            string cleanStr = rawBase64.Replace("=", "");

            int chunkSize = 8;
            var chunks = Enumerable.Range(0, (cleanStr.Length + chunkSize - 1) / chunkSize)
                                  .Select(i => cleanStr.Substring(i * chunkSize, Math.Min(chunkSize, cleanStr.Length - i * chunkSize)));

            return "HTLIC-V5-" + string.Join("-", chunks);
        }

        private void BtnAddDays_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && int.TryParse(btn.Tag?.ToString(), out int days))
            {
                DpExpiry.SelectedDate = DateTime.Now.AddDays(days);
            }
        }

        private void BtnCopyOutput_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(TxtOutputKey.Text) && !TxtOutputKey.Text.Contains("Mã License được mã hóa"))
            {
                Clipboard.SetText(TxtOutputKey.Text);
                MessageBox.Show("Đã sao chép mã kích hoạt vào bộ nhớ tạm!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Vui lòng bấm nút 'TẠO MÃ LICENSE BẢN QUYỀN' trước khi sao chép!", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}