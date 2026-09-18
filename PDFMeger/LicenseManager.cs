using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace PDFMeger
{
    public static class LicenseManager
    {
        private const string SECRET_SALT = "HT_PDF_MERGER_SUPER_SECRET_SECURITY_SALT_2026_ENTERPRISE_KEY_X98A_V5";

        private static readonly string FolderPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PDFMeger"
        );

        private static readonly string LicenseFileName = "license.dat";

        /// <summary>
        /// Lấy phiên bản cố định của App (Tự động lấy theo Assembly Version hoặc trả về v2.5.0)
        /// </summary>
        public static string GetCurrentVersionString()
        {
            try
            {
                Version version = Assembly.GetExecutingAssembly().GetName().Version;
                if (version != null)
                {
                    return $"v{version.Major}.{version.Minor}.{version.Build}";
                }
            }
            catch { }

            return "v2.5.0"; // Cố định Version giao diện
        }

        public static (bool IsValid, string Message) ValidateLicense()
        {
            string filePath = Path.Combine(FolderPath, LicenseFileName);

            if (!File.Exists(filePath))
            {
                return (false, "Chưa kích hoạt bản quyền.");
            }

            try
            {
                string savedCode = File.ReadAllText(filePath).Trim();
                return ProcessAndValidateKey(savedCode);
            }
            catch
            {
                return (false, "Lỗi đọc dữ liệu bản quyền.");
            }
        }

        public static (bool IsSuccess, string Message) SaveAndActivateLicense(string inputCode)
        {
            try
            {
                inputCode = inputCode.Trim();

                var result = ProcessAndValidateKey(inputCode);
                if (!result.IsValid)
                {
                    return (false, result.Message);
                }

                if (!Directory.Exists(FolderPath))
                {
                    Directory.CreateDirectory(FolderPath);
                }

                // Lưu Key License (Không tăng đếm số lần active nữa)
                string filePath = Path.Combine(FolderPath, LicenseFileName);
                File.WriteAllText(filePath, inputCode);

                return (true, result.Message);
            }
            catch
            {
                return (false, "Có lỗi xảy ra khi xử lý mã kích hoạt!");
            }
        }

        private static (bool IsValid, string Message) ProcessAndValidateKey(string licenseKey)
        {
            try
            {
                string cleanKey = licenseKey.Replace("HTLIC-V5-", "").Replace("-", "").Trim();

                switch (cleanKey.Length % 4)
                {
                    case 2: cleanKey += "=="; break;
                    case 3: cleanKey += "="; break;
                }

                byte[] dataBytes = Convert.FromBase64String(cleanKey);
                string fullData = Encoding.UTF8.GetString(dataBytes);

                string[] parts = fullData.Split('|');
                if (parts.Length < 7) return (false, "Mã kích hoạt không đúng định dạng!");

                string macInKey = parts[1];
                string expiryStr = parts[2];
                string signatureInKey = parts[5].Replace("SIG:", "");

                string rawPayload = $"{parts[0]}|{parts[1]}|{parts[2]}|{parts[3]}|{parts[4]}";
                string computedSignature = ComputeHmacSha512(rawPayload, SECRET_SALT);

                if (!string.Equals(signatureInKey, computedSignature, StringComparison.OrdinalIgnoreCase))
                {
                    return (false, "Mã kích hoạt không hợp lệ!");
                }

                string currentMac = GetEthernetMacAddress();
                if (!string.Equals(macInKey, currentMac, StringComparison.OrdinalIgnoreCase))
                {
                    return (false, $"Mã này dành cho MAC: {macInKey}, không dùng được trên máy này ({currentMac})!");
                }

                if (DateTime.TryParseExact(expiryStr, "yyyy-MM-dd-HH-mm-ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime expiryDate))
                {
                    if (DateTime.Now > expiryDate)
                    {
                        return (false, $"Phần mềm đã hết hạn vào {expiryDate:dd/MM/yyyy HH:mm}!");
                    }

                    return (true, $"Kích hoạt thành công! Hạn dùng đến {expiryDate:dd/MM/yyyy}");
                }
                else
                {
                    return (false, "Định dạng ngày tháng không hợp lệ!");
                }
            }
            catch
            {
                return (false, "Mã kích hoạt bị lỗi!");
            }
        }

        private static string ComputeHmacSha512(string data, string key)
        {
            using (var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(key)))
            {
                byte[] hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
                return BitConverter.ToString(hashBytes).Replace("-", "");
            }
        }

        private static string GetEthernetMacAddress()
        {
            try
            {
                var ethernetInterface = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(nic => nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet
                               && nic.OperationalStatus == OperationalStatus.Up
                               && !nic.Description.ToLower().Contains("virtual")
                               && !nic.Description.ToLower().Contains("pseudo"))
                    .FirstOrDefault() ?? NetworkInterface.GetAllNetworkInterfaces()
                    .Where(nic => nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet
                               && !nic.Description.ToLower().Contains("virtual")
                               && !nic.Description.ToLower().Contains("pseudo"))
                    .FirstOrDefault();

                if (ethernetInterface != null)
                {
                    byte[] macBytes = ethernetInterface.GetPhysicalAddress().GetAddressBytes();
                    if (macBytes.Length > 0)
                    {
                        return string.Join("-", macBytes.Select(b => b.ToString("X2")));
                    }
                }
            }
            catch { }
            return "NOT-FOUND";
        }
    }
}