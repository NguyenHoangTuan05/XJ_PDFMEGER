using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Newtonsoft.Json;
using OfficeOpenXml;

namespace CreateTest
{
    public partial class MainWindow : Window
    {
        private ObservableCollection<EmployeeModel> _employeeList;
        private static readonly HttpClient client = new HttpClient();

        public MainWindow()
        {
            InitializeComponent();

            // Đặt cấu hình License cho EPPlus phiên bản mới (EPPlus 5+)
            ExcelPackage.License.SetNonCommercialPersonal("User");

            _employeeList = new ObservableCollection<EmployeeModel>();
            DgEmployees.ItemsSource = _employeeList;

            // Gán ngày hiện tại cho DatePicker
            DpExamDate.SelectedDate = DateTime.Now;

            // Gọi API lấy danh sách bài thi khi khởi động
            _ = LoadExamListFromApiAsync();
        }

        // ================= 1. GỌI API LẤY DANH SÁCH BÀI THI =================
        private async Task LoadExamListFromApiAsync()
        {
            try
            {
                string apiUrl = "https://your-server-api.com/api/exams";

                // Khi có API thật, mở comment đoạn dưới đây và sử dụng apiUrl:
                // var response = await client.GetStringAsync(apiUrl);
                // var exams = JsonConvert.DeserializeObject<List<ExamModel>>(response);

                // Dữ liệu mẫu minh họa giao diện:
                var mockExams = new List<ExamModel>
                {
                    new ExamModel { ExamId = 1, ExamName = "Kiểm tra kiến thức cơ bản" },
                    new ExamModel { ExamId = 2, ExamName = "An toàn lao động & Kaizen" },
                    new ExamModel { ExamId = 3, ExamName = "Quy trình vận hành PLC & Modbus" }
                };

                CbExamList.ItemsSource = mockExams;
                CbExamList.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi tải danh sách bài thi: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ================= 2. TẢI FILE MẪU EXCEL VỀ MÁY =================
        private void BtnDownloadTemplate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                    FileName = "Template.xlsx"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    using (var package = new ExcelPackage())
                    {
                        var worksheet = package.Workbook.Worksheets.Add("DanhSach");

                        // Tạo tiêu đề chuẩn theo đúng yêu cầu
                        worksheet.Cells[1, 1].Value = "Mã nhân viên";
                        worksheet.Cells[1, 2].Value = "Họ tên";
                        worksheet.Cells[1, 3].Value = "Bộ phận";

                        // Dòng dữ liệu mẫu gợi ý
                        worksheet.Cells[2, 1].Value = "NV001";
                        worksheet.Cells[2, 2].Value = "Nguyễn Văn An";
                        worksheet.Cells[2, 3].Value = "Sản xuất";

                        worksheet.Cells.AutoFitColumns();
                        var file = new FileInfo(saveFileDialog.FileName);
                        package.SaveAs(file);
                    }

                    MessageBox.Show("Tải file mẫu thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi tạo file mẫu: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ================= 3. TẢI FILE EXCEL VÀ HIỂN THỊ LÊN BẢNG (TABLE) =================
        private void BtnUploadExcel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                OpenFileDialog openFileDialog = new OpenFileDialog
                {
                    Filter = "Excel Files|*.xls;*.xlsx;*.xlsm"
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    using (var package = new ExcelPackage(new FileInfo(openFileDialog.FileName)))
                    {
                        var worksheet = package.Workbook.Worksheets[0];
                        int rowCount = worksheet.Dimension.Rows;

                        _employeeList.Clear();

                        int index = 1;
                        for (int row = 2; row <= rowCount; row++)
                        {
                            string empCode = worksheet.Cells[row, 1].Text?.Trim() ?? string.Empty;
                            string fullName = worksheet.Cells[row, 2].Text?.Trim() ?? string.Empty;
                            string dept = worksheet.Cells[row, 3].Text?.Trim() ?? string.Empty;

                            if (!string.IsNullOrEmpty(empCode))
                            {
                                _employeeList.Add(new EmployeeModel
                                {
                                    Index = index++,
                                    EmployeeCode = empCode,
                                    FullName = fullName,
                                    Department = dept,
                                    IsSelected = true
                                });
                            }
                        }
                    }

                    UpdateFooterCounts();
                    MessageBox.Show("Đã tải dữ liệu file Excel lên bảng thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi đọc file Excel: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ================= 4. GỬI DỮ LIỆU LÊN SERVER =================
        private async void BtnCreateExam_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedEmployees = new List<EmployeeModel>();
                foreach (var emp in _employeeList)
                {
                    if (emp.IsSelected)
                    {
                        selectedEmployees.Add(emp);
                    }
                }

                if (selectedEmployees.Count == 0)
                {
                    MessageBox.Show("Vui lòng chọn ít nhất một nhân viên để tạo bài thi!", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var examId = CbExamList.SelectedValue;
                var examDate = DpExamDate.SelectedDate;

                var payload = new
                {
                    ExamId = examId,
                    ExamDate = examDate?.ToString("yyyy-MM-dd"),
                    Employees = selectedEmployees
                };

                string jsonPayload = JsonConvert.SerializeObject(payload);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                string apiUrl = "https://your-server-api.com/api/create-exam";

                // Khi kết nối server thật, hãy mở comment dòng dưới và truyền apiUrl vào:
                // HttpResponseMessage response = await client.PostAsync(apiUrl, content);

                await Task.Delay(400);
                _ = apiUrl; // Đánh dấu biến đã được sử dụng để tránh cảnh báo

                MessageBox.Show($"Đã tạo bài thi thành công cho {selectedEmployees.Count} nhân viên!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi gửi dữ liệu lên server: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Employee_CheckedChanged(object sender, RoutedEventArgs e) => UpdateFooterCounts();

        private void ChkAll_Checked(object sender, RoutedEventArgs e)
        {
            foreach (var emp in _employeeList) emp.IsSelected = true;
            DgEmployees.Items.Refresh();
            UpdateFooterCounts();
        }

        private void ChkAll_Unchecked(object sender, RoutedEventArgs e)
        {
            foreach (var emp in _employeeList) emp.IsSelected = false;
            DgEmployees.Items.Refresh();
            UpdateFooterCounts();
        }

        private void UpdateFooterCounts()
        {
            int selectedCount = 0;
            foreach (var emp in _employeeList)
            {
                if (emp.IsSelected) selectedCount++;
            }
            TxtSelectedCount.Text = $"Đã chọn {selectedCount} nhân viên";
            TxtTotalCount.Text = $"Tổng {_employeeList.Count} nhân viên";
        }
    }

    // ================= MODEL DỮ LIỆU =================
    public class EmployeeModel
    {
        public bool IsSelected { get; set; }
        public int Index { get; set; }
        public string EmployeeCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
    }

    public class ExamModel
    {
        public int ExamId { get; set; }
        public string ExamName { get; set; } = string.Empty;
    }
}