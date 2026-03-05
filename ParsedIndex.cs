using CustomCodeSystem.Dtos;

public sealed class ParsedIndex
{
    private readonly Dictionary<string, ParsedDto> _snIndex;
    private readonly Dictionary<string, ParsedDto> _imeiIndex;
    private readonly Dictionary<string, ParsedDto> _opIndex;
    private readonly Dictionary<Guid, List<ParsedDto>> _rowIndex;

    public ParsedIndex(List<ParsedDto> items)
    {
        _snIndex = new Dictionary<string, ParsedDto>(StringComparer.Ordinal);
        _imeiIndex = new Dictionary<string, ParsedDto>(StringComparer.Ordinal);
        _opIndex = new Dictionary<string, ParsedDto>(StringComparer.Ordinal);
        _rowIndex = new Dictionary<Guid, List<ParsedDto>>();

        Build(items);
    }

    private void Build(List<ParsedDto> items)
    {
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];

            // индекс по RowId -> все элементы строки
            if (!_rowIndex.TryGetValue(item.RowId, out var rowItems))
            {
                rowItems = new List<ParsedDto>(8);
                _rowIndex[item.RowId] = rowItems;
            }

            rowItems.Add(item);

            // индекс по SN
            if (!string.IsNullOrWhiteSpace(item.SN))
            {
                _snIndex[item.SN] = item;
            }

            // индекс по IMEI
            if (!string.IsNullOrWhiteSpace(item.IMEI))
            {
                _imeiIndex[item.IMEI] = item;
            }

            // индекс по OperationalNumber
            if (!string.IsNullOrWhiteSpace(item.OperationalNumber))
            {
                _opIndex[item.OperationalNumber] = item;
            }
        }

        // чтобы full row всегда был в порядке Block 1..8
        foreach (var kv in _rowIndex)
        {
            kv.Value.Sort(static (a, b) => a.Block.CompareTo(b.Block));
        }
    }

    public ParsedDto? FindBySn(string sn)
    {
        if (string.IsNullOrWhiteSpace(sn))
            return null;

        return _snIndex.TryGetValue(sn, out var item) ? item : null;
    }

    public ParsedDto? FindByImei(string imei)
    {
        if (string.IsNullOrWhiteSpace(imei))
            return null;

        return _imeiIndex.TryGetValue(imei, out var item) ? item : null;
    }

    public ParsedDto? FindByOperationalNumber(string operationalNumber)
    {
        if (string.IsNullOrWhiteSpace(operationalNumber))
            return null;

        return _opIndex.TryGetValue(operationalNumber, out var item) ? item : null;
    }

    public List<ParsedDto> FindRowBySn(string sn)
    {
        var item = FindBySn(sn);
        if (item == null)
            return new List<ParsedDto>();

        return FindRowByRowId(item.RowId);
    }

    public List<ParsedDto> FindRowByImei(string imei)
    {
        var item = FindByImei(imei);
        if (item == null)
            return new List<ParsedDto>();

        return FindRowByRowId(item.RowId);
    }

    public List<ParsedDto> FindRowByOperationalNumber(string operationalNumber)
    {
        var item = FindByOperationalNumber(operationalNumber);
        if (item == null)
            return new List<ParsedDto>();

        return FindRowByRowId(item.RowId);
    }

    public List<ParsedDto> FindRowByRowId(Guid rowId)
    {
        if (_rowIndex.TryGetValue(rowId, out var items))
            return items;

        return new List<ParsedDto>();
    }
}