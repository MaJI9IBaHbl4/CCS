
using System.Globalization;


namespace CustomCodeSystem;

public static class MasterCodeHelper
{

    public static (bool Success, List<string> Operationals, string Error) ExtractOperationals(string masterCode)
    {
        var result = new List<string>();

        if (string.IsNullOrWhiteSpace(masterCode))
            return (false, result, "masterCode is empty.");

        masterCode = masterCode.Trim().ToUpper();
        // 1) Проверка: никаких букв, кроме 'M'
        for (int i = 0; i < masterCode.Length; i++)
        {
            char ch = masterCode[i];
            if (char.IsLetter(ch) && ch != 'M')
                return (false, new List<string>(), $"Invalid letter '{ch}' found. Only 'M' is allowed.");
        }

        int mIndex = masterCode.IndexOf('M');

        // 2) Если M нет — возвращаем весь masterCode
        if (mIndex < 0)
        {
            result.Add(masterCode);
            return (true, result, "");
        }

        // 3) Формат: <multDigits>M<startDigits>
        if (mIndex == 0)
            return (false, new List<string>(), "Invalid format: multiplier is missing before 'M'.");

        if (mIndex == masterCode.Length - 1)
            return (false, new List<string>(), "Invalid format: start number is missing after 'M'.");

        string multPart = masterCode.Substring(0, mIndex);
        string startPart = masterCode.Substring(mIndex + 1);

        if (!IsAllDigits(multPart))
            return (false, new List<string>(), $"Invalid format: multiplier '{multPart}' must contain only digits.");

        if (!IsAllDigits(startPart))
            return (false, new List<string>(), $"Invalid format: start number '{startPart}' must contain only digits.");

        // NEW: лимит длины после M
        if (startPart.Length > 11)
            return (false, new List<string>(), $"Invalid format: start number length is {startPart.Length}, max allowed is 11.");

        if (!int.TryParse(multPart, NumberStyles.None, CultureInfo.InvariantCulture, out int count))
            return (false, new List<string>(), $"Invalid multiplier '{multPart}'.");

        if (count <= 0)
            return (false, new List<string>(), $"Invalid multiplier '{count}'. Must be > 0.");

        if (!long.TryParse(startPart, NumberStyles.None, CultureInfo.InvariantCulture, out long start))
            return (false, new List<string>(), $"Invalid start number '{startPart}'.");

        // 4) Генерация (с сохранением лидирующих нулей по ширине startPart)
        int width = startPart.Length;

        checked
        {
            for (int i = 0; i < count; i++)
            {
                long val = start + i;
                result.Add(val.ToString("D" + width, CultureInfo.InvariantCulture));
            }
        }

        return (true, result, "");
    }

    private static bool IsAllDigits(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        for (int i = 0; i < s.Length; i++)
            if (!char.IsDigit(s[i]))
                return false;
        return true;
    }
}
