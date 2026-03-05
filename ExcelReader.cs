using ClosedXML.Excel;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using System.Globalization;
using System.IO;

namespace CustomCodeSystem;

public static class ExcelReader
{

    /// <summary>
    /// Читает первый лист Excel и возвращает значения из указанных столбцов.
    /// columns - 1-based номера столбцов (A=1, B=2, ...).
    /// Возвращает List<List<string>>: каждая внутренняя List<string> — это одна строка,
    /// значения идут в том же порядке, что columns.
    /// Корректно читает большие числа без научной нотации (E+).
    /// </summary>
    public static List<List<string>> ReadColumnsFromFirstSheet(
        string filePath,
        IReadOnlyList<int> columns,
        bool skipHeader = true)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path is empty.", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException("Excel file not found.", filePath);

        if (columns == null || columns.Count == 0)
            throw new ArgumentException("columns is empty.", nameof(columns));

        for (int i = 0; i < columns.Count; i++)
        {
            if (columns[i] <= 0)
                throw new ArgumentOutOfRangeException(nameof(columns), $"Column index must be >= 1. Got {columns[i]}.");
        }

        // На всякий: если передали дубликаты — оставим, но обычно лучше уникализировать
        // columns = columns.Distinct().ToArray();

        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        var result = new List<List<string>>();

        static string? Normalize(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            s = s.Trim();
            return s.Length == 0 ? null : s;
        }

        static string ToPlainIntegerString(double d)
            => d.ToString("0", CultureInfo.InvariantCulture);

        static string? ReadClosedXmlCellAsString(IXLCell cell)
        {
            if (cell == null || cell.IsEmpty()) return null;

            if (cell.DataType == XLDataType.Number)
            {
                if (cell.TryGetValue<decimal>(out var dec))
                    return Normalize(dec.ToString("0", CultureInfo.InvariantCulture));

                var d = cell.GetDouble();
                return Normalize(ToPlainIntegerString(d));
            }

            if (cell.DataType == XLDataType.Text)
                return Normalize(cell.GetString());

            return Normalize(cell.GetValue<string>());
        }

        static string? ReadNpoiCellAsString(ICell? cell, IFormulaEvaluator? evaluator, DataFormatter formatter)
        {
            if (cell == null) return null;

            var cellType = cell.CellType;

            if (cellType == CellType.Formula && evaluator != null)
            {
                var evaluated = evaluator.Evaluate(cell);
                if (evaluated == null) return null;

                return evaluated.CellType switch
                {
                    CellType.String => Normalize(evaluated.StringValue),
                    CellType.Numeric => Normalize(((decimal)evaluated.NumberValue).ToString("0", CultureInfo.InvariantCulture)),
                    CellType.Boolean => Normalize(evaluated.BooleanValue ? "TRUE" : "FALSE"),
                    _ => Normalize(formatter.FormatCellValue(cell, evaluator))
                };
            }

            if (cellType == CellType.String)
                return Normalize(cell.StringCellValue);

            if (cellType == CellType.Numeric)
            {
                if (DateUtil.IsCellDateFormatted(cell))
                    return Normalize(formatter.FormatCellValue(cell));

                var dec = (decimal)cell.NumericCellValue;
                return Normalize(dec.ToString("0", CultureInfo.InvariantCulture));
            }

            return Normalize(formatter.FormatCellValue(cell));
        }

        // Открытие файла так, чтобы оно работало даже если Excel держит файл открытым
        static FileStream OpenSharedRead(string path)
            => new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

        // Если Excel/OneDrive всё равно не даёт — читаем через temp copy
        static string CreateTempCopy(string path)
        {
            var tmp = Path.Combine(
                Path.GetTempPath(),
                $"{Path.GetFileNameWithoutExtension(path)}_{Guid.NewGuid():N}{Path.GetExtension(path)}");

            using (var src = OpenSharedRead(path))
            using (var dst = new FileStream(tmp, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            {
                src.CopyTo(dst);
            }

            return tmp;
        }

        List<List<string>> ReadXlsxLike(string path)
        {
            using var fs = OpenSharedRead(path);
            using var workbook = new XLWorkbook(fs);
            var ws = workbook.Worksheet(1);

            var usedRange = ws.RangeUsed();
            if (usedRange == null) return result;

            int firstRow = usedRange.RangeAddress.FirstAddress.RowNumber;
            int lastRow = usedRange.RangeAddress.LastAddress.RowNumber;
            int startRow = skipHeader ? firstRow + 1 : firstRow;

            for (int row = startRow; row <= lastRow; row++)
            {
                var rowValues = new List<string>(columns.Count);

                for (int i = 0; i < columns.Count; i++)
                {
                    int col = columns[i];
                    var value = ReadClosedXmlCellAsString(ws.Cell(row, col));
                    // сохраняем позицию столбца: null допустим
                    rowValues.Add(value);
                }

                // Если хочешь пропускать полностью пустые строки (все null):
                // if (rowValues.All(v => string.IsNullOrEmpty(v))) continue;

                result.Add(rowValues);
            }

            return result;
        }

        if (ext == ".xlsx" || ext == ".xlsm")
        {
            string? tempPath = null;

            try
            {
                try
                {
                    return ReadXlsxLike(filePath);
                }
                catch (IOException)
                {
                    tempPath = CreateTempCopy(filePath);
                    return ReadXlsxLike(tempPath);
                }
            }
            finally
            {
                if (tempPath != null)
                {
                    try { File.Delete(tempPath); } catch { /* ignore */ }
                }
            }
        }

        if (ext == ".xls")
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var wb = new HSSFWorkbook(fs);
            var sheet = wb.GetSheetAt(0);

            var formatter = new DataFormatter(CultureInfo.InvariantCulture);
            var evaluator = wb.GetCreationHelper().CreateFormulaEvaluator();

            int firstRow = sheet.FirstRowNum;
            int lastRow = sheet.LastRowNum;
            int startRow = skipHeader ? firstRow + 1 : firstRow;

            for (int row = startRow; row <= lastRow; row++)
            {
                var r = sheet.GetRow(row);

                var rowValues = new List<string>(columns.Count);
                for (int i = 0; i < columns.Count; i++)
                {
                    int col = columns[i] - 1; // NPOI: 0-based
                    var c = r?.GetCell(col);
                    var value = ReadNpoiCellAsString(c, evaluator, formatter);
                    rowValues.Add(value);
                }

                result.Add(rowValues);
            }

            return result;
        }

        throw new NotSupportedException($"Unsupported excel extension: {ext}. Only .xls/.xlsx/.xlsm.");
    }

    // Если тебе надо оставить старый API (одна колонка -> List<string>), можно просто обертку:
    public static List<string> ReadFirstColumnFromFirstSheet(string filePath, bool skipHeader = true)
    {
        var rows = ReadColumnsFromFirstSheet(filePath, new[] { 1 }, skipHeader);
        var col = new List<string>(rows.Count);
        foreach (var r in rows)
        {
            var v = r.Count > 0 ? r[0] : null;
            if (!string.IsNullOrEmpty(v))
                col.Add(v);
        }
        return col;
    }
}
