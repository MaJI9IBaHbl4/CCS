using ClosedXML.Excel;
using CustomCodeSystem.Dtos;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;

namespace CustomCodeSystem;

public static class ExcelBlockParser
{
    public static List<ParsedDto> snList = new();

    private static ParsedIndex? _index;

    public static List<ParsedDto> Parse(string filePath)
    {
        var result = new List<ParsedDto>();

        using var workbook = new XLWorkbook(filePath);
        var ws = workbook.Worksheet(1);

        int lastRow = ws.LastRowUsed().RowNumber();

        for (int row = 2; row <= lastRow; row++)
        {
            // Один RowId на всю строку Excel
            var rowId = Guid.NewGuid();

            for (int block = 1; block <= 8; block++)
            {
                int snCol = 2 + (block - 1) * 3;
                int imeiCol = 3 + (block - 1) * 3;

                var sn = ws.Cell(row, snCol).GetValue<string>()?.Trim();
                var imei = ws.Cell(row, imeiCol).GetValue<string>()?.Trim();

                result.Add(new ParsedDto
                {
                    RowId = rowId,
                    SN = string.IsNullOrWhiteSpace(sn) ? null : sn,
                    IMEI = string.IsNullOrWhiteSpace(imei) ? null : imei,
                    Block = block
                });
            }
        }

        snList = result;
        RebuildIndexes();

        return result;
    }

    public static void Save(string filePath, List<ParsedDto> items)
    {
        var sb = new StringBuilder();

        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];

            sb.Append(item.RowId);
            sb.Append(';');
            sb.Append(item.Block);
            sb.Append(';');
            sb.Append(item.IMEI);
            sb.Append(';');
            sb.Append(item.SN);
            sb.Append(';');
            sb.Append(item.OperationalNumber);

            if (i < items.Count - 1)
                sb.AppendLine();
        }

        File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
    }

    public static (bool success, string errorText) ParseTxt(string folderPath)
    {
        var result = new List<ParsedDto>();

        try
        {
            if (string.IsNullOrWhiteSpace(folderPath))
                return (false, "folderPath is empty.");

            if (!Directory.Exists(folderPath))
                return (false, $"Folder not found: {folderPath}");

            var files = Directory.GetFiles(folderPath, "*.alalist", SearchOption.TopDirectoryOnly);

            if (files.Length == 0)
                return (false, $"No .alalist files found in folder: {folderPath}");

            foreach (var filePath in files)
            {
                var lines = File.ReadAllLines(filePath, Encoding.UTF8);

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    var parts = line.Split(';');

                    // формат:
                    // RowId;Block;SN;IMEI;OperationalNumber
                    if (parts.Length < 5)
                        continue;

                    var rowIdText = parts[0].Trim();
                    var blockText = parts[1].Trim();
                    var imei = parts[2].Trim();
                    var sn = parts[3].Trim();
                    var op = parts[4].Trim();

                    if (!Guid.TryParse(rowIdText, out var rowId))
                        continue;

                    if (!int.TryParse(blockText, out var block))
                        continue;

                    result.Add(new ParsedDto
                    {
                        RowId = rowId,
                        Block = block,
                        SN = string.IsNullOrWhiteSpace(sn) ? null : sn,
                        IMEI = string.IsNullOrWhiteSpace(imei) ? null : imei,
                        OperationalNumber = string.IsNullOrWhiteSpace(op) ? null : op
                    });
                }
            }

            snList = result;
            RebuildIndexes();

            return (true, "");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public static void RebuildIndexes()
    {
        _index = new ParsedIndex(snList);
    }

    public static ParsedDto? FindBySn(string sn)
    {
        return _index?.FindBySn(sn);
    }

    public static List<ParsedDto> FindRowBySn(string sn)
    {
        return _index?.FindRowBySn(sn) ?? new List<ParsedDto>();
    }

    public static ParsedDto? FindByImei(string imei)
    {
        return _index?.FindByImei(imei);
    }

    public static List<ParsedDto> FindRowByImei(string imei)
    {
        return _index?.FindRowByImei(imei) ?? new List<ParsedDto>();
    }

    public static ParsedDto? FindByOperationalNumber(string operationalNumber)
    {
        return _index?.FindByOperationalNumber(operationalNumber);
    }

    public static List<ParsedDto> FindRowByOperationalNumber(string operationalNumber)
    {
        return _index?.FindRowByOperationalNumber(operationalNumber) ?? new List<ParsedDto>();
    }

    public static (string? min, string? max) GetMinAndMax(List<ParsedDto> items)
    {
        BigInteger? minSn = null;
        BigInteger? maxSn = null;

        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.SN))
                continue;

            if (!BigInteger.TryParse(item.SN, out var snValue))
                continue;

            if (minSn == null || snValue < minSn.Value)
                minSn = snValue;

            if (maxSn == null || snValue > maxSn.Value)
                maxSn = snValue;
        }

        return (minSn?.ToString(), maxSn?.ToString());
    }
}