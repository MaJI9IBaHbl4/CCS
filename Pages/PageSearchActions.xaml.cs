using CustomCodeSystem.Dtos;
using DocumentFormat.OpenXml.ExtendedProperties;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

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
            datePickerDateFrom.SelectedDate = DateTime.Today.AddYears(-1);
            Loaded += PageSearchActions_Loaded;

        }

        private void PageSearchActions_Loaded(object sender, RoutedEventArgs e)
        {
            datePickerDateTo.SelectedDate = DateTime.Today;
            datePickerDateFrom.SelectedDate = DateTime.Today.AddYears(-1);
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

                if (textBoxOperationalCustomSn.Text.Length > 11)
                {
                    ParsedDto parsedDto = ExcelBlockParser.FindBySn(operationalCode.Split(';')[1]);
                    //ImeiDto imeiDto = CustomSns.FindBySerialFast(operationalCode.Split(';')[1]);

                    if (parsedDto == null)
                    {
                        gridInfo.Visibility = Visibility.Visible;
                        textBlockInfo.Text = $"IMEI nerastas {operationalCode}";
                        return;
                    }

                    //var result = await GOR_API.FindProductsAsync(AppState.GetSessionIdOrThrow(), new[] { imeiDto.OperationalNumber });
                    var result = await GOR_API.FindProductsAsync(AppState.GetSessionIdOrThrow(), new[] { parsedDto.OperationalNumber });


                    if (result.success == false)
                    {
                        gridInfo.Visibility = Visibility.Visible;
                        textBlockInfo.Text = result.errorText;
                        return;
                    }

                    if (result.items[0].ConfigurationMetadata == null)
                    {
                        gridInfo.Visibility = Visibility.Visible;
                        textBlockInfo.Text = $"IMEI nera susietas";
                        return;
                    }

                    string linkSn = GetCustomSerial(result.items[0].ConfigurationMetadata);

                    if (linkSn == null)
                    {
                        gridInfo.Visibility = Visibility.Visible;
                        textBlockInfo.Text = $"Nepavyko istraukti gaminio operacini is: {result.items[0].ConfigurationMetadata}";
                        return;
                    }
                    operationalCode = linkSn;
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
                return;
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

    public sealed class CompletedActionsVm : INotifyPropertyChanged
    {
        public ObservableCollection<SearchCompletedActionDto> CompletedActions { get; } = new();

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

        public void Clear()
        {
            CompletedActions.Clear();
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
            }
            finally
            {
                IsLoading = false;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
    public sealed class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b ? !b : true;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b ? !b : false;
    }
}
