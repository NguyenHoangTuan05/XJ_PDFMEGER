using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PDFMeger;

public partial class MainWindow : Window
{
    public ObservableCollection<PdfFileItem> PdfFiles { get; } = new();

    private Point _dragStartPoint;

    public MainWindow()
    {
        InitializeComponent();

        // Kiểm tra bản quyền ngay khi khởi chạy ứng dụng
        if (!CheckLicenseAndContinue())
        {
            Application.Current.Shutdown();
            return;
        }

        DataContext = this;
        UpdateSummary();
    }

    private bool CheckLicenseAndContinue()
    {
        var (isValid, message) = LicenseManager.ValidateLicense();

        if (!isValid)
        {
            // Bật cửa sổ nhập license lên cho người dùng kích hoạt
            var licenseWin = new ActiveLicense();
            licenseWin.ShowDialog();

            // Kiểm tra lại sau khi họ tắt cửa sổ nhập mã
            var recheck = LicenseManager.ValidateLicense();
            return recheck.IsValid;
        }

        return true;
    }

    // =========================================================
    // ADD FILES
    // =========================================================

    private void AddPdf_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Chọn file PDF",
            Filter = "PDF files (*.pdf)|*.pdf",
            Multiselect = true
        };

        if (dialog.ShowDialog() != true)
            return;

        AddFiles(dialog.FileNames);
    }

    private const long MaxTotalSizeBytes = (long)(2.0 * 1024 * 1024 * 1024); // 2.0 GB

    private void AddFiles(IEnumerable<string> files)
    {
        long currentTotalSize = PdfFiles.Sum(x => x.FileSizeBytes);

        foreach (var file in files)
        {
            if (!File.Exists(file))
                continue;

            if (!string.Equals(
                    Path.GetExtension(file),
                    ".pdf",
                    StringComparison.OrdinalIgnoreCase))
                continue;

            if (PdfFiles.Any(x =>
                    string.Equals(
                        x.FilePath,
                        file,
                        StringComparison.OrdinalIgnoreCase)))
                continue;

            var fileInfo = new FileInfo(file);

            // Kiểm tra giới hạn 1.8 GB
            if (currentTotalSize + fileInfo.Length > MaxTotalSizeBytes)
            {
                MessageBox.Show(
                    $"Không thể thêm file \"{fileInfo.Name}\".\nTổng dung lượng danh sách không được vượt quá 1.8 GB.",
                    "Cảnh báo dung lượng",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                break;
            }

            try
            {
                int pageCount;

                using (var document =
                       PdfReader.Open(file, PdfDocumentOpenMode.Import))
                {
                    pageCount = document.PageCount;
                }

                var item = new PdfFileItem
                {
                    FilePath = file,
                    FileName = Path.GetFileName(file),
                    PageCount = pageCount,
                    FileSizeBytes = fileInfo.Length,
                    Index = PdfFiles.Count + 1
                };

                PdfFiles.Add(item);
                currentTotalSize += fileInfo.Length;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Không thể đọc file:\n\n{file}\n\n{ex.Message}",
                    "PDF Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        UpdateIndexes();
        UpdateSummary();
    }

    // =========================================================
    // DELETE
    // =========================================================

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
            return;

        if (button.DataContext is not PdfFileItem item)
            return;

        RemoveItem(item);
    }

    private void DeleteFooter_Click(object sender, RoutedEventArgs e)
    {
        if (PdfFiles.Count == 0)
            return;

        var result = MessageBox.Show(
            "Bạn có chắc chắn muốn xóa tất cả các file khỏi danh sách không?",
            "Xác nhận xóa tất cả",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            PdfFiles.Clear();
            UpdateIndexes();
            UpdateSummary();
        }
    }

    private void RemoveItem(PdfFileItem item)
    {
        PdfFiles.Remove(item);
        UpdateIndexes();
        UpdateSummary();
    }

    // =========================================================
    // REORDER (UP / DOWN)
    // =========================================================

    private void MoveUp_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
            return;

        if (button.DataContext is not PdfFileItem item)
            return;

        MoveUp(item);
    }

    private void MoveDown_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
            return;

        if (button.DataContext is not PdfFileItem item)
            return;

        MoveDown(item);
    }

    private void MoveUpFooter_Click(object sender, RoutedEventArgs e)
    {
        if (PdfDataGrid.SelectedItem is PdfFileItem item)
        {
            MoveToTop(item);
        }
    }

    private void MoveDownFooter_Click(object sender, RoutedEventArgs e)
    {
        if (PdfDataGrid.SelectedItem is PdfFileItem item)
        {
            MoveToBottom(item);
        }
    }

    private void MoveUp(PdfFileItem item)
    {
        var index = PdfFiles.IndexOf(item);
        if (index <= 0) return;

        PdfFiles.Move(index, index - 1);
        UpdateIndexes();

        PdfDataGrid.SelectedItem = item;
        PdfDataGrid.ScrollIntoView(item);
    }

    private void MoveDown(PdfFileItem item)
    {
        var index = PdfFiles.IndexOf(item);
        if (index < 0 || index >= PdfFiles.Count - 1) return;

        PdfFiles.Move(index, index + 1);
        UpdateIndexes();

        PdfDataGrid.SelectedItem = item;
        PdfDataGrid.ScrollIntoView(item);
    }

    private void MoveToTop(PdfFileItem item)
    {
        var index = PdfFiles.IndexOf(item);
        if (index <= 0) return;

        PdfFiles.Move(index, 0);
        UpdateIndexes();

        PdfDataGrid.SelectedItem = item;
        PdfDataGrid.ScrollIntoView(item);
    }

    private void MoveToBottom(PdfFileItem item)
    {
        var index = PdfFiles.IndexOf(item);
        if (index < 0 || index >= PdfFiles.Count - 1) return;

        PdfFiles.Move(index, PdfFiles.Count - 1);
        UpdateIndexes();

        PdfDataGrid.SelectedItem = item;
        PdfDataGrid.ScrollIntoView(item);
    }

    private void UpdateIndexes()
    {
        for (int i = 0; i < PdfFiles.Count; i++)
        {
            PdfFiles[i].Index = i + 1;
        }
        PdfDataGrid.Items.Refresh();
    }

    private void UpdateSummary()
    {
        var totalSize = PdfFiles.Sum(x => x.FileSizeBytes);
        var mb = totalSize / 1024d / 1024d;
        int totalPages = PdfFiles.Sum(x => x.PageCount);

        // Cập nhật các thông số phía trên
        FileCountText.Text = $"{PdfFiles.Count} file";
        TotalPageCountText.Text = $"Tổng: {totalPages} trang";
        TotalSizeText.Text = $"Tổng dung lượng: {mb:N1} MB";

        // Cập nhật dòng thông tin góc dưới bên trái:
        SelectedInfoText.Text = $"{PdfFiles.Count} file đã chọn    |    {totalPages} trang    |    {mb:N1} MB";
    }

    // =========================================================
    // DRAG FILE FROM WINDOWS EXPLORER
    // =========================================================

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;

        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return;

        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        AddFiles(files);
    }

    private void DropZone_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;

        e.Handled = true;
    }

    private void DropZone_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return;

        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        AddFiles(files);

        e.Handled = true;
    }

    // =========================================================
    // DRAG ROW TO REORDER
    // =========================================================

    private void DataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
    }

    private void DataGrid_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
            return;

        var position = e.GetPosition(null);
        var diff = _dragStartPoint - position;

        if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var originalSource = e.OriginalSource as DependencyObject;
        while (originalSource != null && originalSource is not DataGridRow)
        {
            originalSource = VisualTreeHelper.GetParent(originalSource);
        }

        if (originalSource is not DataGridRow)
            return;

        if (PdfDataGrid.SelectedItem is not PdfFileItem item)
            return;

        DragDrop.DoDragDrop(PdfDataGrid, item, DragDropEffects.Move);
    }

    private void DataGrid_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(PdfFileItem)) is PdfFileItem)
        {
            e.Effects = DragDropEffects.Move;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }

        e.Handled = true;
    }

    private void DataGrid_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(PdfFileItem)) is not PdfFileItem source)
        {
            return;
        }

        var targetElement = e.OriginalSource as DependencyObject;

        while (targetElement != null && targetElement is not DataGridRow)
        {
            targetElement = VisualTreeHelper.GetParent(targetElement);
        }

        if (targetElement is not DataGridRow row)
            return;

        if (row.Item is not PdfFileItem target)
            return;

        var oldIndex = PdfFiles.IndexOf(source);
        var newIndex = PdfFiles.IndexOf(target);

        if (oldIndex == newIndex)
            return;

        PdfFiles.Move(oldIndex, newIndex);
        PdfDataGrid.SelectedItem = source;

        UpdateIndexes();

        e.Handled = true;
    }

    // =========================================================
    // SELECTION
    // =========================================================

    private void PdfDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Reserved for future use
    }

    // =========================================================
    // MERGE
    // =========================================================

    private void MergePdf_Click(object sender, RoutedEventArgs e)
    {
        if (PdfFiles.Count == 0)
        {
            MessageBox.Show(
                "Vui lòng thêm ít nhất một file PDF.",
                "PDF Merge",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Lưu file PDF đã merge",
            Filter = "PDF files (*.pdf)|*.pdf",
            FileName = "Merged.pdf",
            DefaultExt = ".pdf"
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            MergePdf(
                PdfFiles.Select(x => x.FilePath),
                dialog.FileName);

            MessageBox.Show(
                $"Đã merge thành công {PdfFiles.Count} file PDF.",
                "PDF Merge",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Merge PDF thất bại.\n\n{ex.Message}",
                "PDF Merge",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static void MergePdf(IEnumerable<string> files, string outputFile)
    {
        using var outputDocument = new PdfDocument();

        foreach (var file in files)
        {
            using var inputDocument = PdfReader.Open(file, PdfDocumentOpenMode.Import);

            for (int i = 0; i < inputDocument.PageCount; i++)
            {
                outputDocument.AddPage(inputDocument.Pages[i]);
            }
        }

        outputDocument.Save(outputFile);
    }
}