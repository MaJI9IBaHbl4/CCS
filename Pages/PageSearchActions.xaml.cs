using CustomCodeSystem.Dtos;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace CustomCodeSystem.Pages
{
    /// <summary>
    /// Логика взаимодействия для PageSearchActions.xaml
    /// </summary>
    public partial class PageSearchActions : Page
    {
        private readonly CompletedActionsVm _vm = new();

        public PageSearchActions()
        {
            InitializeComponent();
            DataContext = _vm;

            datePickerDateTo.SelectedDate = DateTime.Today;
            datePickerDateFrom.SelectedDate = DateTime.Today.AddDays(-7);
            Loaded += PageSearchActions_Loaded;
        }

        private void PageSearchActions_Loaded(object sender, RoutedEventArgs e)
        {
            // на случай, если страница пересоздаётся/переоткрывается
            datePickerDateTo.SelectedDate ??= DateTime.Today;
            datePickerDateFrom.SelectedDate ??= DateTime.Today.AddYears(-1);
        }

        private async void btnSearch_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string workerId = textBoxWorkerId.Text;
                string workerName = textBoxWorkerName.Text;
                string workerSurname = textBoWorkerSurname.Text;
                string operationCode = textBoxOperation.Text;
                string operationalCode = textBoxOperationalCustomSn.Text;

                DateTime? fromOpt = datePickerDateFrom.SelectedDate;
                DateTime? toOpt = datePickerDateTo.SelectedDate;

                if (fromOpt is null || toOpt is null)
                {
                    MessageBox.Show("Pasirinkite Date from / Date to");
                    return;
                }

                // Если в поле ввели "xxx;SN" или что-то подобное — пытаемся достать SN, найти его,
                // и заменить operationalCode на CustomSerial из метадаты.
                if (!string.IsNullOrWhiteSpace(operationalCode) && operationalCode.Length > 11)
                {
                    var parts = operationalCode.Split(';');
                    if (parts.Length >= 2)
                    {
                        ParsedDto? parsedDto = ExcelBlockParser.FindBySn(parts[1]);
                        if (parsedDto is null)
                        {
                            gridInfo.Visibility = Visibility.Visible;
                            textBlockInfo.Text = $"IMEI nerastas {operationalCode}";
                            return;
                        }

                        var result = await GOR_API.FindProductsAsync(
                            AppState.GetSessionIdOrThrow(),
                            new[] { parsedDto.OperationalNumber });

                        if (result.success == false)
                        {
                            gridInfo.Visibility = Visibility.Visible;
                            textBlockInfo.Text = result.errorText;
                            return;
                        }

                        if (result.items == null || result.items.Count() == 0)
                        {
                            gridInfo.Visibility = Visibility.Visible;
                            textBlockInfo.Text = "No products found.";
                            return;
                        }

                        if (string.IsNullOrWhiteSpace(result.items[0].ConfigurationMetadata))
                        {
                            gridInfo.Visibility = Visibility.Visible;
                            textBlockInfo.Text = $"IMEI nera susietas";
                            return;
                        }

                        string? linkSn = GetCustomSerial(result.items[0].ConfigurationMetadata);
                        if (string.IsNullOrWhiteSpace(linkSn))
                        {
                            gridInfo.Visibility = Visibility.Visible;
                            textBlockInfo.Text = $"Nepavyko istraukti gaminio operacini is: {result.items[0].ConfigurationMetadata}";
                            return;
                        }

                        operationalCode = linkSn;
                    }
                }

                DateTime from = fromOpt.Value.Date;
                DateTime to = toOpt.Value.Date;

                string sessionId = AppState.GetSessionIdOrThrow();

                await _vm.LoadAsync(
                    sessionId: sessionId,
                    dateFrom: from,
                    dateTo: to,
                    workerId: workerId,
                    workerName: workerName,
                    workerSurname: workerSurname,
                    operationCode: operationCode,
                    operationalCode: operationalCode);

                gridInfo.Visibility = Visibility.Collapsed;
                textBlockInfo.Text = string.Empty;
            }
            catch (Exception ex)
            {
                gridInfo.Visibility = Visibility.Visible;
                textBlockInfo.Text = ex.Message;
            }
        }

        public static string? GetCustomSerial(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.ValueKind != JsonValueKind.Object)
                    return null;

                if (doc.RootElement.TryGetProperty("CustomSerial", out var prop) &&
                    prop.ValueKind == JsonValueKind.String)
                {
                    return prop.GetString();
                }

                return null;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            datePickerDateTo.SelectedDate = DateTime.Today;
            datePickerDateFrom.SelectedDate = DateTime.Today.AddYears(-1);
            textBoxWorkerId.Text = "";
            textBoxWorkerName.Text = "";
            textBoWorkerSurname.Text = "";
            textBoxOperation.Text = "";
            textBoxOperationalCustomSn.Text = "";
            _vm.Clear();
        }
    }

    // -----------------------------
    // ViewModel + analytics models
    // -----------------------------

    public sealed class CompletedActionsVm : INotifyPropertyChanged
    {
        public ObservableCollection<SearchCompletedActionDto> CompletedActions { get; } = new();

        // Big analysis tables
        public ObservableCollection<OperationCodeStatRow> OperationCodeStats { get; } = new();
        public ObservableCollection<WorkerStatRow> WorkerStats { get; } = new();
        public ObservableCollection<DayStatRow> DayStats { get; } = new();

        public CompletedActionsVm()
        {
            // сортировка по времени (возрастание)
            var view = CollectionViewSource.GetDefaultView(CompletedActions);
            view.SortDescriptions.Clear();
            view.SortDescriptions.Add(
                new SortDescription(nameof(SearchCompletedActionDto.ConnectDateTime), ListSortDirection.Ascending)
            );
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); }
        }

        private string _statusText = "";
        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        // Quick stats
        private int _totalRows;
        public int TotalRows
        {
            get => _totalRows;
            set { _totalRows = value; OnPropertyChanged(); }
        }

        private int _uniqueOperationalNumbers;
        public int UniqueOperationalNumbers
        {
            get => _uniqueOperationalNumbers;
            set { _uniqueOperationalNumbers = value; OnPropertyChanged(); }
        }

        private int _uniqueOperationCodes;
        public int UniqueOperationCodes
        {
            get => _uniqueOperationCodes;
            set { _uniqueOperationCodes = value; OnPropertyChanged(); }
        }

        private int _uniqueWorkers;
        public int UniqueWorkers
        {
            get => _uniqueWorkers;
            set { _uniqueWorkers = value; OnPropertyChanged(); }
        }

        private double _passRate; // 0..1
        public string PassRateText => TotalRows == 0 ? "—" : $"{_passRate:P1}";

        private string _timeSpanText = "—";
        public string TimeSpanText
        {
            get => _timeSpanText;
            set { _timeSpanText = value; OnPropertyChanged(); }
        }

        private string _extraInsights = "";
        public string ExtraInsights
        {
            get => _extraInsights;
            set { _extraInsights = value; OnPropertyChanged(); }
        }

        public void Clear()
        {
            CompletedActions.Clear();

            OperationCodeStats.Clear();
            WorkerStats.Clear();
            DayStats.Clear();

            TotalRows = 0;
            UniqueOperationalNumbers = 0;
            UniqueOperationCodes = 0;
            UniqueWorkers = 0;

            _passRate = 0;
            OnPropertyChanged(nameof(PassRateText));

            TimeSpanText = "—";
            ExtraInsights = "";
            StatusText = "";
        }

        public async Task LoadAsync(
            string sessionId,
            DateTime dateFrom,
            DateTime dateTo,
            string? workerId = null,
            string? workerName = null,
            string? workerSurname = null,
            string? operationCode = null,
            string? operationalCode = null,
            CancellationToken ct = default)
        {
            IsLoading = true;
            StatusText = "Loading...";

            try
            {
                var (ok, items, err) = await GOR_API.SearchCompletedActionsAsync(
                    sessionId,
                    dateFrom,
                    dateTo,
                    workerId: workerId,
                    workerName: workerName,
                    workerSurname: workerSurname,
                    operationCode: operationCode,
                    operationalCode: operationalCode,
                    ct: ct);

                if (!ok)
                {
                    StatusText = "Error: " + err;
                    return;
                }

                CompletedActions.Clear();
                if (items != null)
                {
                    foreach (var it in items)
                        CompletedActions.Add(it);
                }

                StatusText = $"Loaded: {CompletedActions.Count}";
                RebuildStats();
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void RebuildStats()
        {
            var data = CompletedActions.ToList();

            TotalRows = data.Count;

            static bool Has(string? s) => !string.IsNullOrWhiteSpace(s);

            UniqueOperationalNumbers = data
                .Select(x => x.OperationalNumber)
                .Where(Has)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            UniqueOperationCodes = data
                .Select(x => x.OperationCode)
                .Where(Has)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            UniqueWorkers = data
                .Select(x => x.WorkerId)
                .Where(Has)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            int passCount = data.Count(x => x.Pass == true);
            _passRate = TotalRows == 0 ? 0 : (double)passCount / TotalRows;
            OnPropertyChanged(nameof(PassRateText));

            // time span (ConnectDateTime может быть DateTime?)
            var times = data
                .Select(x => x.ConnectDateTime)
                .Where(dt => dt.HasValue)
                .Select(dt => dt!.Value)
                .ToList();

            if (times.Count == 0)
            {
                TimeSpanText = "—";
            }
            else
            {
                var minT = times.Min();
                var maxT = times.Max();
                TimeSpanText = $"{minT:yyyy-MM-dd HH:mm} → {maxT:yyyy-MM-dd HH:mm}";
            }

            BuildOperationStats(data);
            BuildWorkerStats(data);
            BuildDayStats(data);
            BuildExtraInsights(data);
        }

        private void BuildOperationStats(List<SearchCompletedActionDto> data)
        {
            OperationCodeStats.Clear();

            var groups = data
                .GroupBy(
                    x => string.IsNullOrWhiteSpace(x.OperationCode) ? "(empty)" : x.OperationCode.Trim(),
                    StringComparer.OrdinalIgnoreCase)
                .Select(g =>
                {
                    var ops = g.ToList();

                    var uniqueOperational = ops
                        .Select(x => x.OperationalNumber)
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Count();

                    int pass = ops.Count(x => x.Pass == true);
                    int fail = ops.Count - pass;

                    var topOperational = ops
                        .Select(x => x.OperationalNumber)
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .GroupBy(s => s!, StringComparer.OrdinalIgnoreCase)
                        .OrderByDescending(gg => gg.Count())
                        .ThenBy(gg => gg.Key, StringComparer.OrdinalIgnoreCase)
                        .Take(5)
                        .Select(gg => $"{gg.Key}({gg.Count()})");

                    return new OperationCodeStatRow
                    {
                        OperationCode = g.Key,
                        Count = ops.Count,
                        UniqueOperational = uniqueOperational,
                        PassCount = pass,
                        FailCount = fail,
                        PassRate = ops.Count == 0 ? 0 : (double)pass / ops.Count,
                        TopOperationalSample = string.Join(", ", topOperational)
                    };
                })
                .OrderByDescending(x => x.Count)
                .ThenBy(x => x.OperationCode, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var row in groups)
                OperationCodeStats.Add(row);
        }

        private void BuildWorkerStats(List<SearchCompletedActionDto> data)
        {
            WorkerStats.Clear();

            var groups = data
                .GroupBy(x => new
                {
                    WorkerId = string.IsNullOrWhiteSpace(x.WorkerId) ? "(empty)" : x.WorkerId.Trim(),
                    WorkerName = string.IsNullOrWhiteSpace(x.WorkerName) ? "" : x.WorkerName.Trim(),
                    WorkerSurname = string.IsNullOrWhiteSpace(x.WorkerSurname) ? "" : x.WorkerSurname.Trim()
                })
                .Select(g =>
                {
                    var list = g.ToList();

                    int pass = list.Count(x => x.Pass == true);

                    int uniqueOpCodes = list
                        .Select(x => x.OperationCode)
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Count();

                    int uniqueOperational = list
                        .Select(x => x.OperationalNumber)
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Count();

                    //var topOp = list
                    //    .Select(x => x.OperationCode)
                    //    .Where(s => !string.IsNullOrWhiteSpace(s))
                    //    .GroupBy(s => s!, StringComparer.OrdinalIgnoreCase)
                    //    .OrderByDescending(gg => gg.Count())
                    //    .ThenBy(gg => gg.Key, StringComparer.OrdinalIgnoreCase)
                    //    .Select(gg => gg.Key)
                    //    .FirstOrDefault() ?? "(none)";

                    return new WorkerStatRow
                    {
                        WorkerId = g.Key.WorkerId,
                        WorkerName = g.Key.WorkerName,
                        WorkerSurname = g.Key.WorkerSurname,
                        Count = list.Count,
                        UniqueOperationCodes = uniqueOpCodes,
                        UniqueOperational = uniqueOperational,
                        PassRate = list.Count == 0 ? 0 : (double)pass / list.Count,
                        //TopOperationCode = topOp
                    };
                })
                .OrderByDescending(x => x.Count)
                .ThenBy(x => x.WorkerId, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var row in groups)
                WorkerStats.Add(row);
        }

        private void BuildDayStats(List<SearchCompletedActionDto> data)
        {
            DayStats.Clear();

            // ConnectDateTime может быть null, поэтому фильтруем
            var groups = data
                .Where(x => x.ConnectDateTime.HasValue)
                .GroupBy(x => x.ConnectDateTime!.Value.Date)
                .Select(g =>
                {
                    var list = g.ToList();
                    int pass = list.Count(x => x.Pass == true);

                    int uniqueWorkers = list
                        .Select(x => x.WorkerId)
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Count();

                    int uniqueOperational = list
                        .Select(x => x.OperationalNumber)
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Count();

                    return new DayStatRow
                    {
                        Date = g.Key,
                        Count = list.Count,
                        UniqueWorkers = uniqueWorkers,
                        UniqueOperational = uniqueOperational,
                        PassRate = list.Count == 0 ? 0 : (double)pass / list.Count
                    };
                })
                .OrderByDescending(x => x.Date)
                .ToList();

            foreach (var row in groups)
                DayStats.Add(row);
        }

        private void BuildExtraInsights(List<SearchCompletedActionDto> data)
        {
            if (data.Count == 0)
            {
                ExtraInsights = "No data.";
                return;
            }

            int total = data.Count;
            int pass = data.Count(x => x.Pass == true);
            int fail = total - pass;

            var topOperation = data
                .Select(x => x.OperationCode)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .GroupBy(s => s!, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();

            var topWorker = data
                .Where(x => !string.IsNullOrWhiteSpace(x.WorkerId))
                .GroupBy(x => x.WorkerId!.Trim(), StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();

            var busiestHour = data
                .Where(x => x.ConnectDateTime.HasValue)
                .GroupBy(x => x.ConnectDateTime!.Value.Hour)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();

            int uniqueSerial = data
                .Select(x => x.SerialNumber)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            int uniqueImei = data
                .Select(x => x.Imei)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            var sb = new StringBuilder();
            sb.AppendLine($"• Total rows: {total} (Pass: {pass}, Fail: {fail})");
            sb.AppendLine($"• Unique OperationalNumber: {UniqueOperationalNumbers}");
            sb.AppendLine($"• Unique SerialNumber: {uniqueSerial}");
            sb.AppendLine($"• Unique IMEI: {uniqueImei}");
            sb.AppendLine($"• Unique OperationCode: {UniqueOperationCodes}");
            sb.AppendLine($"• Unique Workers: {UniqueWorkers}");

            if (topOperation != null)
                sb.AppendLine($"• Top OperationCode: {topOperation.Key} ({topOperation.Count()} rows)");

            if (topWorker != null)
                sb.AppendLine($"• Top WorkerId: {topWorker.Key} ({topWorker.Count()} rows)");

            if (busiestHour != null)
                sb.AppendLine($"• Busiest hour: {busiestHour.Key:00}:00 ({busiestHour.Count()} rows)");

            var top3 = data
                .Select(x => x.OperationCode)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .GroupBy(s => s!, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count())
                .Take(3)
                .ToList();

            if (top3.Count > 0)
            {
                int top3Count = top3.Sum(g => g.Count());
                sb.AppendLine($"• Top-3 OperationCode share: {(double)top3Count / total:P1} ({top3Count}/{total})");
            }

            ExtraInsights = sb.ToString().TrimEnd();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // таблица: по operation code
    public sealed class OperationCodeStatRow
    {
        public string OperationCode { get; set; } = "";
        public int Count { get; set; }
        public int UniqueOperational { get; set; }
        public int PassCount { get; set; }
        public int FailCount { get; set; }
        public double PassRate { get; set; } // 0..1
        public string PassRateText => Count == 0 ? "—" : $"{PassRate:P1}";
        public string TopOperationalSample { get; set; } = "";
    }

    // таблица: по воркерам
    public sealed class WorkerStatRow
    {
        public string WorkerId { get; set; } = "";
        public string WorkerName { get; set; } = "";
        public string WorkerSurname { get; set; } = "";
        public int Count { get; set; }
        public int UniqueOperationCodes { get; set; }
        public int UniqueOperational { get; set; }
        public double PassRate { get; set; }
        public string PassRateText => Count == 0 ? "—" : $"{PassRate:P1}";
        //public string TopOperationCode { get; set; } = "";
    }

    // таблица: по дням
    public sealed class DayStatRow
    {
        public DateTime Date { get; set; }
        public int Count { get; set; }
        public int UniqueWorkers { get; set; }
        public int UniqueOperational { get; set; }
        public double PassRate { get; set; }
        public string PassRateText => Count == 0 ? "—" : $"{PassRate:P1}";
    }

    public sealed class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b ? !b : true;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b ? !b : false;
    }
}