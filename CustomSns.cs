//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Numerics;

//namespace CustomCodeSystem
//{
//    public static class CustomSns
//    {
//        static List<SnsList> snsLists = new List<SnsList>();

//        static void ClearLists() => snsLists.Clear();

//        public static (bool success, string errorMsg) LoadSns(string folderPath)
//        {
//            try
//            {
//                ClearLists();

//                if (string.IsNullOrWhiteSpace(folderPath))
//                    return (false, "folderPath is empty.");

//                if (!Directory.Exists(folderPath))
//                    return (false, $"Folder not found: '{folderPath}'");

//                var files = Directory.GetFiles(folderPath, "*.txt");

//                for (int f = 0; f < files.Length; f++)
//                {
//                    var filePath = files[f];

//                    var list = new SnsList
//                    {
//                        name = Path.GetFileNameWithoutExtension(filePath),
//                        imeis = new List<List<ImeiDto>>()
//                    };

//                    var lines = File.ReadAllLines(filePath);

//                    for (int i = 0; i < lines.Length; i++)
//                    {
//                        // если бывают пустые строки — пропустим
//                        if (string.IsNullOrWhiteSpace(lines[i]))
//                            continue;

//                        var parts = lines[i].Split(';');

//                        var dto = new ImeiDto
//                        {
//                            OperationalNumber = parts.Length > 0 ? parts[0] : null,
//                            Imei = parts.Length > 1 ? parts[1] : null,
//                            Serial = parts.Length > 2 ? parts[2] : null,
//                        };

//                        list.imeis.Add(new List<ImeiDto>(1) { dto });
//                    }

//                    // start = первый Serial, end = последний Serial
//                    if (list.imeis.Count > 0)
//                    {
//                        var firstSerial = list.imeis[0][0].Serial;
//                        var lastSerial = list.imeis[list.imeis.Count - 1][0].Serial;

//                        if (string.IsNullOrWhiteSpace(firstSerial) || string.IsNullOrWhiteSpace(lastSerial))
//                            return (false, $"Serial is empty in file '{filePath}'.");

//                        list.start = BigInteger.Parse(firstSerial);
//                        list.end = BigInteger.Parse(lastSerial);
//                    }
//                    else
//                    {
//                        // файл пустой/только пустые строки — можно решить: ошибка или просто пропуск
//                        // Если хочешь ошибкой — раскомментируй:
//                        // return (false, $"File '{filePath}' has no valid lines.");
//                        list.start = BigInteger.Zero;
//                        list.end = BigInteger.Zero;
//                    }

//                    snsLists.Add(list);
//                }

//                // сортировку лучше делать один раз после цикла, а не на каждой итерации
//                snsLists.Sort((a, b) => a.start.CompareTo(b.start));

//                return (true, "");
//            }
//            catch (Exception ex)
//            {
//                // максимально полезно: тип + сообщение + где упало (stacktrace)
//                return (false, $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
//            }
//        }


//        public static ImeiDto? FindBySerialFast(string serial)
//        {
//            if (string.IsNullOrWhiteSpace(serial))
//                return null;

//            serial = serial.Trim();

//            if (!BigInteger.TryParse(serial, out var s))
//                return null;

//            int lo = 0;
//            int hi = snsLists.Count - 1;

//            while (lo <= hi)
//            {
//                int mid = (lo + hi) / 2;
//                var l = snsLists[mid];

//                if (s < l.start) { hi = mid - 1; continue; }
//                if (s > l.end) { lo = mid + 1; continue; }

//                int index = (int)(s - l.start);

//                // защита от выхода за границы (на всякий)
//                if ((uint)index >= (uint)l.imeis.Count)
//                    return null;

//                return l.imeis[index][0];
//            }

//            return null;
//        }


//        public static List<ImeiDto>? FindBySerialFast(string serial, int previousCount, bool searchForward)
//        {
//            if (string.IsNullOrWhiteSpace(serial))
//                return null;

//            serial = serial.Trim();

//            if (!BigInteger.TryParse(serial, out var s))
//                return null;

//            previousCount = Math.Max(0, previousCount);

//            int lo = 0;
//            int hi = snsLists.Count - 1;

//            while (lo <= hi)
//            {
//                int mid = (lo + hi) / 2;
//                var l = snsLists[mid];

//                if (s < l.start) { hi = mid - 1; continue; }
//                if (s > l.end) { lo = mid + 1; continue; }

//                int idx = (int)(s - l.start);
//                if ((uint)idx >= (uint)l.imeis.Count)
//                    return null;

//                int from, to;

//                if (searchForward)
//                {
//                    from = idx;
//                    to = Math.Min(l.imeis.Count - 1, idx + previousCount);
//                }
//                else
//                {
//                    from = Math.Max(0, idx - previousCount);
//                    to = idx;
//                }

//                var res = new List<ImeiDto>(Math.Max(0, to - from + 1));
//                for (int i = from; i <= to; i++)
//                {
//                    var dto = l.imeis[i][0];
//                    if (dto != null) res.Add(dto);
//                }

//                return res;
//            }

//            return new List<ImeiDto>(); // распарсили ок, но не нашли диапазон
//        }



//        public static SnsList? GetByNameIgnoreCase(string name)
//        {
//            for (int i = 0; i < snsLists.Count; i++)
//            {
//                if (string.Equals(snsLists[i].name, name, StringComparison.OrdinalIgnoreCase))
//                    return snsLists[i];
//            }
//            return null;
//        }

//        public static SnsList? GetByValue(BigInteger value)
//        {
//            for (int i = 0; i < snsLists.Count; i++)
//            {
//                var l = snsLists[i];
//                if (value >= l.start && value <= l.end)
//                    return l;
//            }
//            return null;
//        }

//        public static SnsList? GetByValue(string value)
//        {
//            return GetByValue(BigInteger.Parse(value));
//        }

//    }

//    public class SnsList
//    {
//        public BigInteger start;
//        public BigInteger end;
//        public string name;
//        public List<List<ImeiDto>> imeis;
//    }

//    public sealed class ImeiDto
//    {
//        public string OperationalNumber { get; set; }
//        public string Imei { get; set; }
//        public string Serial { get; set; }
//    }
//}
