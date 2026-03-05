using CustomCodeSystem.Dtos;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CustomCodeSystem.Pages
{
    /// <summary>
    /// Логика взаимодействия для PageScanOperations.xaml
    /// </summary>
    public partial class PageScanOperations : Page
    {
        private int _successCount;
        private readonly ObservableCollection<ScanResultRow> _scanRows = new();

        private const string StatusWaiting = "Waiting...";
        private const string StatusLoading = "Loading...";
        private const string StatusOk = "OK";
        private const string StatusSkipped = "Skipped";

        public PageScanOperations()
        {
            InitializeComponent();

            scanResultsGrid.ItemsSource = _scanRows;

            gridMain.Visibility = Visibility.Collapsed;
            gridError.Visibility = Visibility.Visible;
            boarderScan.Visibility = Visibility.Collapsed;

            radioGood.IsChecked = true;
            StartGorOp();
        }

        private async void StartGorOp()
        {
            try
            {
                string gorCode;
                string errorText;

                bool success = ConfigTxtParser.TryGetValue("ALA440", "Testavimas", out gorCode, out errorText);
                if (!success)
                {
                    textBlockErrorGorCode.Text = "";
                    gridErrorErrorText.Text = errorText;
                    return;
                }

                var findOpResult = await GOR_API.GetOperationCodeAsync(AppState.GetSessionIdOrThrow(), gorCode);
                if (!findOpResult.success)
                {
                    textBlockErrorGorCode.Text = gorCode;
                    gridErrorErrorText.Text = findOpResult.errorText;
                    return;
                }

                textBoxOperationCode.Text = findOpResult.data.Code;
                textBoxOperationDiscription.Text = findOpResult.data.Description;
                textBoxMandatoryOperations.Text = findOpResult.data.MandatoryOperations;
                checkBoxRepeatable.IsChecked = findOpResult.data.Repeatable;

                if (findOpResult.data.Disabled == true)
                {
                    textBlockErrorGorCode.Text = gorCode;
                    gridErrorErrorText.Text = "Operacija isjungta!";
                    return;
                }

                var startTaskResult = await GOR_API.StartTaskAsync(AppState.GetSessionIdOrThrow(), findOpResult.data.Code);
                if (!startTaskResult.success)
                {
                    textBlockErrorGorCode.Text = gorCode;
                    gridErrorErrorText.Text = startTaskResult.errorText;
                    return;
                }

                AppState.SetActionTaskId((int)startTaskResult.taskId);

                gridMain.Visibility = Visibility.Visible;
                gridError.Visibility = Visibility.Collapsed;
                boarderScan.Visibility = Visibility.Visible;
                gridErrorErrorText.Text = "";
                textBoxInput.Focus();
            }
            catch (Exception ex)
            {
                gridErrorErrorText.Text = ex.Message;
            }
        }

        private async void textBoxGorCode_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter && e.Key != Key.Return)
            {
                return;
            }

            string gorCode = textBoxGorCode.Text?.Trim() ?? "";

            if (gorCode.Length != 10)
            {
                textBlockGorCodeError.Text = "Operacijos kodas susidaro is 10 simboliu";
                return;
            }

            try
            {
                var findOpResult = await GOR_API.GetOperationCodeAsync(AppState.GetSessionIdOrThrow(), gorCode);
                if (!findOpResult.success)
                {
                    textBlockGorCodeError.Text = findOpResult.errorText;
                    return;
                }

                textBoxOperationCode.Text = findOpResult.data.Code;
                textBoxOperationDiscription.Text = findOpResult.data.Description;
                textBoxMandatoryOperations.Text = findOpResult.data.MandatoryOperations;
                checkBoxRepeatable.IsChecked = findOpResult.data.Repeatable;

                if (findOpResult.data.Disabled == true)
                {
                    textBlockGorCodeError.Text = "Operacija isjungta!";
                    return;
                }

                var startTaskResult = await GOR_API.StartTaskAsync(AppState.GetSessionIdOrThrow(), findOpResult.data.Code);
                if (!startTaskResult.success)
                {
                    textBlockGorCodeError.Text = startTaskResult.errorText;
                    return;
                }

                AppState.SetActionTaskId((int)startTaskResult.taskId);
                boarderScan.Visibility = Visibility.Visible;
                textBlockGorCodeError.Text = "";
                textBoxInput.Focus();
            }
            catch (Exception ex)
            {
                textBlockGorCodeError.Text = ex.Message;
            }
        }

        private void textBoxInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter && e.Key != Key.Return)
            {
                return;
            }

            string rawInput = textBoxInput.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(rawInput))
            {
                return;
            }

            bool pass = radioGood.IsChecked == true;

            var row = new ScanResultRow
            {
                ScannedData = rawInput,
                FindProductsStatus = StatusLoading,
                CreateActionStatus = StatusWaiting
            };

            _scanRows.Insert(0, row);

            textBlockScanError.Text = "";
            textBoxInput.Clear();
            textBoxInput.Focus();

            _ = ProcessScanAsync(rawInput, row, pass);
        }

        private async Task ProcessScanAsync(string rawInput, ScanResultRow row, bool pass)
        {
            try
            {
                string[] inputSn = rawInput.Split(';');

                if (inputSn.Length < 2 || string.IsNullOrWhiteSpace(inputSn[1]))
                {
                    row.FindProductsStatus = "Neteisingas skenavimo formatas";
                    row.CreateActionStatus = StatusSkipped;
                    return;
                }

                ParsedDto parsedDto = ExcelBlockParser.FindBySn(inputSn[1]);
                if (parsedDto == null)
                {
                    row.FindProductsStatus = $"IMEI nerastas: {inputSn[1]}";
                    row.CreateActionStatus = StatusSkipped;
                    return;
                }

                if (string.IsNullOrWhiteSpace(parsedDto.OperationalNumber))
                {
                    row.FindProductsStatus = "OperationalNumber tuscias";
                    row.CreateActionStatus = StatusSkipped;
                    return;
                }

                var result = await GOR_API.FindProductsAsync(
                    AppState.GetSessionIdOrThrow(),
                    new[] { parsedDto.OperationalNumber });

                if (!result.success)
                {
                    row.FindProductsStatus = SafeError(result.errorText, "FindProductsAsync klaida");
                    row.CreateActionStatus = StatusSkipped;
                    return;
                }

                if (result.items == null || result.items.Count == 0)
                {
                    row.FindProductsStatus = "Produktas nerastas";
                    row.CreateActionStatus = StatusSkipped;
                    return;
                }

                var product = result.items[0];

                if (product.ConfigurationMetadata == null)
                {
                    row.FindProductsStatus = "ConfigurationMetadata yra null";
                    row.CreateActionStatus = StatusSkipped;
                    return;
                }

                string linkSn = GetCustomSerial(product.ConfigurationMetadata);
                if (string.IsNullOrWhiteSpace(linkSn))
                {
                    row.FindProductsStatus = "CustomSerial nerastas ConfigurationMetadata";
                    row.CreateActionStatus = StatusSkipped;
                    return;
                }

                row.FindProductsStatus = StatusOk;
                row.CreateActionStatus = StatusLoading;

                var createActionResult = await GOR_API.CreateActionByOperationalCodeAsync(
                    AppState.GetSessionIdOrThrow(),
                    AppState.GetActionTaskId(),
                    linkSn,
                    pass);

                if (!createActionResult.success)
                {
                    row.CreateActionStatus = SafeError(createActionResult.errorText, "CreateActionByOperationalCodeAsync klaida");
                    return;
                }

                row.CreateActionStatus = StatusOk;

                _successCount++;
                textBlockCompletedCount.Text = "Atlikta operaciju: " + _successCount;
            }
            catch (Exception ex)
            {
                if (row.FindProductsStatus == StatusLoading)
                {
                    row.FindProductsStatus = ex.Message;
                    row.CreateActionStatus = StatusSkipped;
                    return;
                }

                if (row.CreateActionStatus == StatusLoading || row.CreateActionStatus == StatusWaiting)
                {
                    row.CreateActionStatus = ex.Message;
                }
            }
        }

        private static string SafeError(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        public static string GetCustomSerial(string json)
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

        private void gridErrorBtn_Click(object sender, RoutedEventArgs e)
        {
            StartGorOp();
        }

        public class ScanResultRow : INotifyPropertyChanged
        {
            private string _scannedData = string.Empty;
            private string _findProductsStatus = string.Empty;
            private string _createActionStatus = string.Empty;

            public string ScannedData
            {
                get => _scannedData;
                set
                {
                    _scannedData = value;
                    OnPropertyChanged();
                }
            }

            public string FindProductsStatus
            {
                get => _findProductsStatus;
                set
                {
                    _findProductsStatus = value;
                    OnPropertyChanged();
                }
            }

            public string CreateActionStatus
            {
                get => _createActionStatus;
                set
                {
                    _createActionStatus = value;
                    OnPropertyChanged();
                }
            }

            public event PropertyChangedEventHandler? PropertyChanged;

            protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}