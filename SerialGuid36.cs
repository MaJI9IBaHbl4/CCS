//using System.Numerics;

//namespace CustomCodeSystem;

//public static class SerialGuid36
//{
//    private const byte Marker = 0x36; // просто "подпись" формата

//    public static Guid Encode(string serial)
//    {
//        if (serial is null) throw new ArgumentNullException(nameof(serial));
//        serial = serial.Trim().ToUpperInvariant().Split(';')[0];



//        if (serial.Length is < 1 or > 20)
//            throw new ArgumentException("Leidziamas custom kodo ilgis yra 1..20 simboliu.", nameof(serial));

//        BigInteger value = BigInteger.Zero;
//        foreach (char c in serial)
//        {
//            int d = CharToBase36(c);
//            value = value * 36 + d;
//        }

//        // 14 байт (112 бит) достаточно для 20 символов base36
//        byte[] valueBytes = BigIntegerToFixedBigEndian(value, 14);

//        // RFC bytes (16 bytes)
//        var rfc = new byte[16];
//        rfc[0] = (byte)serial.Length;
//        rfc[1] = Marker;
//        Buffer.BlockCopy(valueBytes, 0, rfc, 2, 14);

//        return new Guid(RfcBytesToDotNetBytes(rfc));
//    }

//    public static string Decode(Guid guid)
//    {
//        var rfc = DotNetBytesToRfcBytes(guid.ToByteArray());

//        int len = rfc[0];
//        if (len is < 1 or > 20) throw new ArgumentException("GUID ne musu formato (bad length).", nameof(guid));
//        if (rfc[1] != Marker) throw new ArgumentException("GUID ne musu formato (marker mismatch).", nameof(guid));

//        var valueBytes = new byte[14];
//        Buffer.BlockCopy(rfc, 2, valueBytes, 0, 14);

//        BigInteger value = new BigInteger(valueBytes, isUnsigned: true, isBigEndian: true);

//        char[] chars = new char[len];
//        for (int i = len - 1; i >= 0; i--)
//        {
//            value = BigInteger.DivRem(value, 36, out var rem);
//            chars[i] = Base36ToChar((int)rem);
//        }

//        if (value != BigInteger.Zero)
//            throw new ArgumentException("GUID ne musu formato (value overflow).", nameof(guid));

//        return new string(chars);
//    }

//    private static int CharToBase36(char c)
//    {
//        if (c >= '0' && c <= '9') return c - '0';
//        if (c >= 'A' && c <= 'Z') return 10 + (c - 'A');
//        throw new ArgumentException($"Neleidziamas simbolis '{c}'. Leidziami tik 0-9 и A-Z.");
//    }

//    private static char Base36ToChar(int d)
//    {
//        if (d < 0 || d >= 36) throw new ArgumentOutOfRangeException(nameof(d));
//        return d < 10 ? (char)('0' + d) : (char)('A' + (d - 10));
//    }

//    private static byte[] BigIntegerToFixedBigEndian(BigInteger value, int size)
//    {
//        if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));

//        var le = value.ToByteArray(isUnsigned: true, isBigEndian: false); // little-endian
//        if (le.Length > size) throw new ArgumentException("Reiksme netelpa i isskirtus baitus.");

//        var be = new byte[size];
//        for (int i = 0; i < le.Length; i++)
//            be[size - 1 - i] = le[i];

//        return be;
//    }

//    // ===== RFC <-> .NET Guid byte order conversion =====
//    private static byte[] DotNetBytesToRfcBytes(byte[] le)
//    {
//        var rfc = new byte[16];
//        rfc[0] = le[3]; rfc[1] = le[2]; rfc[2] = le[1]; rfc[3] = le[0];
//        rfc[4] = le[5]; rfc[5] = le[4];
//        rfc[6] = le[7]; rfc[7] = le[6];
//        Buffer.BlockCopy(le, 8, rfc, 8, 8);
//        return rfc;
//    }

//    private static byte[] RfcBytesToDotNetBytes(byte[] rfc)
//    {
//        var le = new byte[16];
//        le[0] = rfc[3]; le[1] = rfc[2]; le[2] = rfc[1]; le[3] = rfc[0];
//        le[4] = rfc[5]; le[5] = rfc[4];
//        le[6] = rfc[7]; le[7] = rfc[6];
//        Buffer.BlockCopy(rfc, 8, le, 8, 8);
//        return le;
//    }
//}

using System;
using System.Numerics;

namespace CustomCodeSystem;

public static class SerialGuid36
{
    private const byte MarkerPlain = 0x36; // старый формат: "ABC123" (1..20)
    private const byte MarkerDashed = 0x37; // новый формат: "XXXX-XXXX-XXXX-XXXX"

    // =========================
    // PUBLIC API (старый кейс)
    // =========================
    public static Guid Encode(string serial)
    {
        if (serial is null) throw new ArgumentNullException(nameof(serial));
        serial = NormalizePlain(serial);

        if (serial.Length is < 1 or > 20)
            throw new ArgumentException("Leidziamas custom kodo ilgis yra 1..20 simboliu.", nameof(serial));

        return EncodeCore(serial, MarkerPlain);
    }

    public static string Decode(Guid guid)
    {
        var (marker, raw) = DecodeCore(guid);

        if (marker != MarkerPlain)
            throw new ArgumentException("GUID ne musu formato (marker mismatch).", nameof(guid));

        return raw; // plain как есть
    }

    // =========================
    // PUBLIC API (новый кейс)
    // =========================
    public static Guid EncodeDashed(string dashedSerial)
    {
        if (dashedSerial is null) throw new ArgumentNullException(nameof(dashedSerial));
        dashedSerial = dashedSerial.Trim().ToUpperInvariant();

        ValidateDashed19(dashedSerial);

        // убираем дефисы -> 16 base36 символов
        string compact = dashedSerial.Replace("-", "");
        // compact.Length == 16

        return EncodeCore(compact, MarkerDashed);
    }

    public static string DecodeDashed(Guid guid)
    {
        var (marker, raw) = DecodeCore(guid);

        if (marker != MarkerDashed)
            throw new ArgumentException("GUID ne musu formato (marker mismatch).", nameof(guid));

        if (raw.Length != 16)
            throw new ArgumentException("GUID ne musu formato (bad length for dashed).", nameof(guid));

        // возвращаем XXXX-XXXX-XXXX-XXXX
        return $"{raw[..4]}-{raw.Substring(4, 4)}-{raw.Substring(8, 4)}-{raw.Substring(12, 4)}";
    }

    // ==========================================
    // CORE: base36 string <-> Guid payload
    // ==========================================
    private static Guid EncodeCore(string base36Serial, byte marker)
    {
        // base36Serial: только 0-9 A-Z, длина 1..20 (но для dashed будет 16)
        BigInteger value = Base36ToBigInteger(base36Serial);

        // 14 байт (112 бит) достаточно для 20 символов base36
        byte[] valueBytes = BigIntegerToFixedBigEndian(value, 14);

        // RFC bytes (16 bytes)
        var rfc = new byte[16];
        rfc[0] = (byte)base36Serial.Length;
        rfc[1] = marker;
        Buffer.BlockCopy(valueBytes, 0, rfc, 2, 14);

        return new Guid(RfcBytesToDotNetBytes(rfc));
    }

    /// <summary>
    /// Возвращает marker и "сырой" base36 серийник (без дефисов).
    /// Дальше конкретный Decode* решает, как его форматировать.
    /// </summary>
    private static (byte marker, string serial) DecodeCore(Guid guid)
    {
        var rfc = DotNetBytesToRfcBytes(guid.ToByteArray());

        int len = rfc[0];
        byte marker = rfc[1];

        if (len is < 1 or > 20)
            throw new ArgumentException("GUID ne musu formato (bad length).", nameof(guid));

        var valueBytes = new byte[14];
        Buffer.BlockCopy(rfc, 2, valueBytes, 0, 14);

        BigInteger value = new BigInteger(valueBytes, isUnsigned: true, isBigEndian: true);
        string serial = BigIntegerToBase36FixedLength(value, len);

        // если осталось >0 — значит len/данные не согласованы
        // (BigIntegerToBase36FixedLength сам проверяет overflow)
        return (marker, serial);
    }

    // ==========================================
    // BASE36 helpers
    // ==========================================
    private static BigInteger Base36ToBigInteger(string s)
    {
        BigInteger value = BigInteger.Zero;
        foreach (char c in s)
        {
            int d = CharToBase36(c);
            value = value * 36 + d;
        }
        return value;
    }

    private static string BigIntegerToBase36FixedLength(BigInteger value, int len)
    {
        if (len <= 0) throw new ArgumentOutOfRangeException(nameof(len));

        char[] chars = new char[len];
        for (int i = len - 1; i >= 0; i--)
        {
            value = BigInteger.DivRem(value, 36, out var rem);
            chars[i] = Base36ToChar((int)rem);
        }

        if (value != BigInteger.Zero)
            throw new ArgumentException("GUID ne musu formato (value overflow).");

        return new string(chars);
    }

    private static int CharToBase36(char c)
    {
        if (c >= '0' && c <= '9') return c - '0';
        if (c >= 'A' && c <= 'Z') return 10 + (c - 'A');
        throw new ArgumentException($"Neleidziamas simbolis '{c}'. Leidziami tik 0-9 ir A-Z.");
    }

    private static char Base36ToChar(int d)
    {
        if (d < 0 || d >= 36) throw new ArgumentOutOfRangeException(nameof(d));
        return d < 10 ? (char)('0' + d) : (char)('A' + (d - 10));
    }

    // ==========================================
    // Normalization + validation
    // ==========================================
    private static string NormalizePlain(string serial)
    {
        // оставляем твою логику: Trim/Upper + до ';'
        serial = serial.Trim().ToUpperInvariant().Split(';')[0];

        // можно тут ещё проверить что только 0-9A-Z,
        // но EncodeCore/CharToBase36 всё равно упадёт на плохом символе
        return serial;
    }

    private static void ValidateDashed19(string s)
    {
        if (s.Length != 19)
            throw new ArgumentException("Leidziamas formatas: XXXX-XXXX-XXXX-XXXX (19 simboliu).");

        // 1-based позиции 5,10,15 => 0-based индексы 4,9,14
        if (s[4] != '-' || s[9] != '-' || s[14] != '-')
            throw new ArgumentException("Leidziamas formatas: XXXX-XXXX-XXXX-XXXX (bruksniai 5/10/15 pozicijose).");

        for (int i = 0; i < 19; i++)
        {
            if (i is 4 or 9 or 14) continue;
            _ = CharToBase36(s[i]); // валидируем как base36
        }
    }

    // ==========================================
    // BigInteger -> fixed bytes
    // ==========================================
    private static byte[] BigIntegerToFixedBigEndian(BigInteger value, int size)
    {
        if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));

        var le = value.ToByteArray(isUnsigned: true, isBigEndian: false); // little-endian
        if (le.Length > size) throw new ArgumentException("Reiksme netelpa i isskirtus baitus.");

        var be = new byte[size];
        for (int i = 0; i < le.Length; i++)
            be[size - 1 - i] = le[i];

        return be;
    }

    // ==========================================
    // RFC <-> .NET Guid byte order conversion
    // ==========================================
    private static byte[] DotNetBytesToRfcBytes(byte[] le)
    {
        var rfc = new byte[16];
        rfc[0] = le[3]; rfc[1] = le[2]; rfc[2] = le[1]; rfc[3] = le[0];
        rfc[4] = le[5]; rfc[5] = le[4];
        rfc[6] = le[7]; rfc[7] = le[6];
        Buffer.BlockCopy(le, 8, rfc, 8, 8);
        return rfc;
    }

    private static byte[] RfcBytesToDotNetBytes(byte[] rfc)
    {
        var le = new byte[16];
        le[0] = rfc[3]; le[1] = rfc[2]; le[2] = rfc[1]; le[3] = rfc[0];
        le[4] = rfc[5]; le[5] = rfc[4];
        le[6] = rfc[7]; le[7] = rfc[6];
        Buffer.BlockCopy(rfc, 8, le, 8, 8);
        return le;
    }
}
