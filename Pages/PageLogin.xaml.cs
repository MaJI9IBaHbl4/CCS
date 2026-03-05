using CustomCodeSystem.Dtos;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CustomCodeSystem.Pages
{
    /// <summary>
    /// Логика взаимодействия для PageLogin.xaml
    /// </summary>
    public partial class PageLogin : Page
    {
        private bool _initStarted;
        private readonly string path;

        public PageLogin()
        {
            InitializeComponent();

            Loaded += PageLogin_Loaded;
            loginTextBox.Focus();

            path =
#if DEBUG
                @"C:\AllProjects\ALA";
#else
                @"\\mtc-files\MTC gamybos inzinerija\8. Service Files\ALA440_LISTS";
#endif
        }

        private async void PageLogin_Loaded(object sender, RoutedEventArgs e)
        {
            if (_initStarted) return;
            _initStarted = true;

            textBoxInit.Foreground = Brushes.Black;
            textBoxInit.Text = "Failu inicializacija, GOR ID suvedimas yra uzblokuotas kol nepasibaigs inicializacija";
            loginTextBox.IsEnabled = false;

            try
            {
                var (success, errorMsg) = await InitFilesAsync(timeoutSeconds: 10);

                if (success)
                {
                    textBoxInit.Foreground = Brushes.Green;
                    textBoxInit.Text = "SUCCESS: Failai inicializuoti";
                    loginTextBox.IsEnabled = true;
                }
                else
                {
                    textBoxInit.Foreground = Brushes.Red;
                    textBoxInit.Text = $"ERROR: {errorMsg}\n\nNegalima paleisti programos del nesekmingos inicializacijos";
                    loginTextBox.IsEnabled = false;
                }
            }
            catch (Exception ex)
            {
                textBoxInit.Foreground = Brushes.Red;
                textBoxInit.Text = $"ERROR: {ex.Message}\n\nNegalima paleisti programos del nesekmingos inicializacijos";
                loginTextBox.IsEnabled = false;
            }
        }

        private async Task<(bool success, string errorMsg)> InitFilesAsync(int timeoutSeconds = 10)
        {
            var workTask = Task.Run(() =>
            {
                // 1. Сначала инициализация config файла
                var configPath = Path.Combine(path, "ccs.config");

                string errorMsg;
                string value;

                ConfigTxtParser.Clear();
                bool success = ConfigTxtParser.Load(configPath, out errorMsg);
                if (!success)
                {
                    return (false, $"Config load failed: {errorMsg}");
                }

                success = ConfigTxtParser.TryGetValue("ALA440", "Testavimas", out value, out errorMsg);
                if (!success)
                {
                    return (false, $"Config value read failed: {errorMsg}");
                }

                // Если хочешь, можно value куда-то сохранить:
                // AppState.SetSomeConfigValue(value);

                // 2. Потом всё остальное
                var result = ExcelBlockParser.ParseTxt(path);
                if (result.success)
                {
                    return (true, string.Empty);
                }

                return (false, result.errorText ?? "Unknown error");
            });

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds));
            var completed = await Task.WhenAny(workTask, timeoutTask);

            if (completed == timeoutTask)
            {
                return (
                    false,
                    $"Inicializacija uztruko ilgiau nei {timeoutSeconds} s. " +
                    $"Galimai:\n\nNera prieigos prie {path} arba kokios nors kitos problemos."
                );
            }

            return await workTask;
        }

        private async void loginTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (radioMoletai.IsChecked == true)
                AppState.SetBaseUrlLocation("https://mtc-gor.teltonika.lt", "Moletai");

            if (radioDitva.IsChecked == true)
                AppState.SetBaseUrlLocation("https://factory.teltonika.lt", "Ditva");

            if (radioSvyla.IsChecked == true)
                AppState.SetBaseUrlLocation("https://svy-gor.teltonika.lt", "Svyla");

            if (e.Key != Key.Enter && e.Key != Key.Return)
                return;

            if (loginTextBox.Text == "test")
                goto GO_MAIN;

            if (loginTextBox.Text.Length < 5)
                return;

            e.Handled = true;

            var (ok, sessionDto, err) = await GOR_API.GetSessionAsync(loginTextBox.Text);
            if (!ok)
            {
                msgTextBox.Text = err;
                return;
            }

            AppState.SetSession(sessionDto!);

            if (Application.Current.MainWindow is MainWindow mw)
            {
                mw.SetUser(sessionDto.Name, sessionDto.Surname);
                mw.textBlockLocation.Text = AppState.GetLocation();
                mw.textBlockVersion.Text = AppState.GetVersion();
            }

        GO_MAIN:
            Nav.NavigationUIShow();
            Nav.ClearHistory();
            Nav.Go("PageMain");
        }
    }
}