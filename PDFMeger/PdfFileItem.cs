namespace PDFMeger;

public class PdfFileItem
{
    public int Index { get; set; }

    public string FilePath { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public int PageCount { get; set; }

    public long FileSizeBytes { get; set; }

    public string FileSize
    {
        get
        {
            if (FileSizeBytes < 1024)
                return $"{FileSizeBytes} B";

            if (FileSizeBytes < 1024 * 1024)
                return $"{FileSizeBytes / 1024d:N1} KB";

            return $"{FileSizeBytes / 1024d / 1024d:N1} MB";
        }
    }
}
