using System;
using System.Linq;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace PDFMeger
{
    public partial class ActiveLicense : Window
    {
        public bool IsActivated { get; private set; } = false;
        private const string PLACEHOLDER_TEXT = "Dán mã bản quyền (License Token) đã tạo vào đây...";

        public ActiveLicense()
        {
            InitializeComponent();
            SetPlaceholder();
            LoadHardwareId();
            UpdateVersionDisplay(); // Hiển thị phiên bản phần mềm lên UI

            // Tự động focus vào ô nhập liệu khi mở form
            this.Loaded += (s, e) =>
            {
                TxtLicenseKey.Focus();
                Keyboard.Focus(TxtLicenseKey);
            };
        }

        /// <summary>
        /// Cập nhật hiển thị phiên bản phần mềm lên giao diện
        /// </summary>
        private void UpdateVersionDisplay()
        {
            if (TxtVersion != null)
            {
                TxtVersion.Text = LicenseManager.GetCurrentVersionString();
            }
        }

        /// <summary>
        /// Lấy Địa chỉ MAC Ethernet làm Mã máy
        /// </summary>
        private void LoadHardwareId()
        {
            try
            {
                string macAddress = GetEthernetMacAddress();
                TxtHwid.Text = macAddress;
            }
            catch
            {
                TxtHwid.Text = "NOT-AVAILABLE";
            }
        }

        /// <summary>
        /// Lấy địa chỉ MAC của card mạng Ethernet (Card mạng dây)
        /// </summary>
        private string GetEthernetMacAddress()
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

            return "NOT-FOUND";
        }

        private void SetPlaceholder()
        {
            TxtLicenseKey.Text = PLACEHOLDER_TEXT;
            TxtLicenseKey.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8"));
        }

        private void TxtLicenseKey_GotFocus(object sender, RoutedEventArgs e)
        {
            if (TxtLicenseKey.Text == PLACEHOLDER_TEXT)
            {
                TxtLicenseKey.Text = "";
                TxtLicenseKey.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F172A"));
            }
        }

        private void BtnCopyHwid_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(TxtHwid.Text);
            MessageBox.Show("Đã sao chép MAC Ethernet vào bộ nhớ tạm!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnPaste_Click(object sender, RoutedEventArgs e)
        {
            if (Clipboard.ContainsText())
            {
                TxtLicenseKey.Text = Clipboard.GetText().Trim();
                TxtLicenseKey.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F172A"));
            }
        }

        private void BtnActivate_Click(object sender, RoutedEventArgs e)
        {
            ProcessActivation();
        }

        private void TxtLicenseKey_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ProcessActivation();
                e.Handled = true;
            }
        }

        private void ProcessActivation()
        {
            string code = TxtLicenseKey.Text.Trim();

            if (string.IsNullOrEmpty(code) || code == PLACEHOLDER_TEXT)
            {
                MessageBox.Show("Vui lòng nhập mã bản quyền!", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = LicenseManager.SaveAndActivateLicense(code);

            if (result.IsSuccess)
            {
                UpdateVersionDisplay(); // Cập nhật lại giao diện (giữ nguyên v2.5.0 không đổi)
                MessageBox.Show(result.Message, "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                IsActivated = true;
                this.Close();
            }
            else
            {
                MessageBox.Show(result.Message, "Lỗi kích hoạt", MessageBoxButton.OK, MessageBoxImage.Error);
                TxtLicenseKey.SelectAll();
                TxtLicenseKey.Focus();
            }
        }
    }
}