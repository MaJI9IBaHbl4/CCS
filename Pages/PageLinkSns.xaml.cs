using CustomCodeSystem.Dtos;
using SixLabors.ImageSharp.Formats.Tga;
using System.IO;
using System.Reflection.Emit;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace CustomCodeSystem.Pages
{
    /// <summary>
    /// Логика взаимодействия для PageLinkSns.xaml
    /// </summary>
    public partial class PageLinkSns : Page
    {
        private List<string> _linkSn = new();
        private readonly List<ItemUi> _snItems = new();
        private List<List<string>> _sourceItems = new();
        private BitmapImage bmp = new BitmapImage();
        private int CompletedCount = 0;

        // Удобный контейнер: один "item" = корневой элемент + 2 TextBox
        public sealed class ItemUi
        {
            public required Border Root { get; init; }

            public required TextBox TbOperationalNumber { get; init; }
            public required TextBox TbLinkSerialNumber { get; init; }
            public required string thirdPartySn { get; set; }
            public required TextBlock TbError { get; init; }
        }

        public PageLinkSns()
        {
            InitializeComponent();
#if DEBUG
#else
            boarderScan.Visibility = Visibility.Collapsed;
            borderApply.Visibility = Visibility.Collapsed;

            borderMainArea.Visibility = Visibility.Collapsed;
#endif
            textBoxGorCode.Focus();
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Panel.png");

            bmp.BeginInit();
            bmp.UriSource = new Uri(path, UriKind.Absolute);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();

            gridError.Visibility = Visibility.Visible;
            gridMain.Visibility = Visibility.Collapsed;

            SetGorOp();
        }

        private bool _isLinkSnRunning;

        private async void textBoxLinkSn_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter && e.Key != Key.Return) return;
            e.Handled = true;

            if (_isLinkSnRunning) return; // защита от двойного Enter
            _isLinkSnRunning = true;

            try
            {
                await RunLinkSnFlowAsync();
            }
            finally
            {
                _isLinkSnRunning = false;
            }
        }

        private async Task RunLinkSnFlowAsync()
        {
            try
            {
                var extractResult = MasterCodeHelper.ExtractOperationals(textBoxLinkSn.Text);

                if (extractResult.Success)
                {
                    _linkSn = extractResult.Operationals;
                    textBlockSearchResult.Text = "";
                }
                else
                {
                    textBlockSearchResult.Text =
                        $"Klaida extraktinant operacinius is master kodo: {extractResult.Error}";
                    return;
                }

                if (_linkSn.Count != 1 && _linkSn.Count != 8)
                {
                    MessageBox.Show("Galima atlikti susiejima vienam arba 8 gaminiams", "",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                BuildByOperationalNumbers(_linkSn);
                borderMainArea.Visibility = Visibility.Visible;
                await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);

                while (true)
                {
                    var dlg = new PasswordDialog("IMEI skanavimas", "Skanuok bet kuri IMEI. Programa pati suras likusius", false)
                    {
                        Owner = Window.GetWindow(this)
                    };

                    bool? ok = dlg.ShowDialog();
                    if (ok != true)
                        return;

                    var parts = (dlg.ResultText ?? "").Split(';');
                    if (parts.Length < 2)
                    {
                        MessageBox.Show($"Skenuojate ne IMEI: {dlg.ResultText}");
                        continue;
                    }

                    var serial = parts[1];

                    var parsedDto = ExcelBlockParser.FindBySn(serial);

                    if (parsedDto == null)
                    {
                        MessageBox.Show($"IMEI nerastas: {dlg.ResultText}");
                        continue;
                    }

                    if (_linkSn.Count == 1)
                    {
                        _snItems[0].TbLinkSerialNumber.Text = parsedDto.IMEI + ";" + parsedDto.SN;
                        _snItems[0].thirdPartySn = parsedDto.OperationalNumber;
                        borderApply.Visibility = Visibility.Visible;
                        textBoxApply.Focus();
                        return;
                    }

                    List<ParsedDto> parsedDtos = ExcelBlockParser.FindRowBySn(serial);
                    if (parsedDtos != null)
                    {
                        for (int i = 0; i < 8; i++)
                        {
                            int j = (i < 4) ? (3 - i) : (11 - i);

                            _snItems[i].TbLinkSerialNumber.Text = parsedDtos[j].IMEI + ";" + parsedDtos[j].SN;
                            _snItems[i].thirdPartySn = parsedDtos[j].OperationalNumber;
                        }
                    }

                    borderApply.Visibility = Visibility.Visible;
                    textBoxApply.Focus();
                    return;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Įvyko klaida: {ex.Message}\n\n{ex}",
                    "Klaida",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }


        private void Button_Click(object sender, RoutedEventArgs e)
        {
            ClearAll();
        }

        public List<ItemUi> BuildByOperationalNumbers(List<string> operationalNumbers)
        {
            operationalNumbers ??= new List<string>();

            wrapPanelMultiSns.Children.Clear();
            _snItems.Clear();

            // =========================================================
            // COLORS
            // =========================================================
            Brush cardBorder = (Brush)new BrushConverter().ConvertFromString("#E5E7EB");
            Brush inputBorder = (Brush)new BrushConverter().ConvertFromString("#D1D5DB");
            Brush inputHover = (Brush)new BrushConverter().ConvertFromString("#9CA3AF");
            Brush inputFocus = (Brush)new BrushConverter().ConvertFromString("#2563EB");
            Brush roBg = (Brush)new BrushConverter().ConvertFromString("#F9FAFB");
            Brush editBg = (Brush)new BrushConverter().ConvertFromString("#EEF2FF");

            // =========================================================
            // TextBox template: shadow layer + crisp chrome layer
            // =========================================================
            ControlTemplate CreateCrispTextBoxTemplate()
            {
                var template = new ControlTemplate(typeof(TextBox));
                var grid = new FrameworkElementFactory(typeof(Grid));

                var shadow = new FrameworkElementFactory(typeof(Border));
                shadow.Name = "shadow";
                shadow.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
                shadow.SetValue(Border.BackgroundProperty, Brushes.White);
                shadow.SetValue(Border.BorderThicknessProperty, new Thickness(1));
                shadow.SetValue(Border.BorderBrushProperty, Brushes.Transparent);
                shadow.SetValue(UIElement.IsHitTestVisibleProperty, false);
                shadow.SetValue(UIElement.EffectProperty, new DropShadowEffect
                {
                    BlurRadius = 10,
                    ShadowDepth = 2,
                    Opacity = 0.14
                });

                var chrome = new FrameworkElementFactory(typeof(Border));
                chrome.Name = "bd";
                chrome.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
                chrome.SetValue(Border.BorderThicknessProperty, new Thickness(1));
                chrome.SetValue(Border.BorderBrushProperty, inputBorder);
                chrome.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
                chrome.SetValue(Border.SnapsToDevicePixelsProperty, true);

                var sv = new FrameworkElementFactory(typeof(ScrollViewer));
                sv.Name = "PART_ContentHost";
                sv.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
                chrome.AppendChild(sv);

                grid.AppendChild(shadow);
                grid.AppendChild(chrome);
                template.VisualTree = grid;

                var tHover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
                tHover.Setters.Add(new Setter(Border.BorderBrushProperty, inputHover, "bd"));

                var tFocus = new Trigger { Property = UIElement.IsKeyboardFocusedProperty, Value = true };
                tFocus.Setters.Add(new Setter(Border.BorderBrushProperty, inputFocus, "bd"));

                var tDisabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
                tDisabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.60, "bd"));

                template.Triggers.Add(tHover);
                template.Triggers.Add(tFocus);
                template.Triggers.Add(tDisabled);

                return template;
            }

            var tbTemplate = CreateCrispTextBoxTemplate();

            TextBlock MakeLabel(string text)
            {
                var lbl = new TextBlock
                {
                    Text = text,
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brushes.Black,
                    Margin = new Thickness(0, 6, 0, 3),
                    SnapsToDevicePixels = true
                };
                ApplyCrispText(lbl);
                return lbl;
            }

            TextBox MakeTextBox(bool readOnly, bool highlightEditable = false)
            {
                var tb = new TextBox
                {
                    Width = 235,
                    Height = 28,
                    Padding = new Thickness(10, 5, 10, 5),
                    FontSize = 12,
                    FontWeight = FontWeights.Medium,
                    Foreground = Brushes.Black,
                    Background = readOnly ? roBg : (highlightEditable ? editBg : Brushes.White),
                    BorderThickness = new Thickness(0), // border inside template
                    VerticalContentAlignment = VerticalAlignment.Center,
                    IsReadOnly = readOnly,
                    Margin = new Thickness(0, 0, 0, 2),
                    Template = tbTemplate,
                    SnapsToDevicePixels = true
                };
                ApplyCrispText(tb);
                return tb;
            }

            // =========================================================
            // Card factory:
            // - UIElement to add: Grid (shadow + card + badge)
            // - Border returned separately (real card border) to store in ItemUi.Root
            // =========================================================
            (UIElement ui, Border cardBorderRef) MakeCard(UIElement content, int index)
            {
                var root = new Grid
                {
                    Margin = new Thickness(6),
                    SnapsToDevicePixels = true
                };
                root.SetValue(FrameworkElement.UseLayoutRoundingProperty, true);

                var shadow = new Border
                {
                    CornerRadius = new CornerRadius(12),
                    Background = Brushes.White,
                    BorderBrush = Brushes.Transparent,
                    BorderThickness = new Thickness(1),
                    IsHitTestVisible = false,
                    Effect = new DropShadowEffect
                    {
                        BlurRadius = 14,
                        ShadowDepth = 3,
                        Opacity = 0.14
                    },
                    Margin = new Thickness(0, 1, 0, 0)
                };

                var card = new Border
                {
                    CornerRadius = new CornerRadius(12),
                    Background = Brushes.White,
                    BorderBrush = cardBorder,
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(10, 8, 10, 8),
                    SnapsToDevicePixels = true,
                    Child = content
                };

                // Badge "номер блока" — по центру на рамке
                var badgeBorder = new Border
                {
                    Background = Brushes.White,                 // "перерезаем" линию рамки
                    BorderBrush = cardBorder,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(8, 2, 8, 2),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, -10, 0, 0),      // поднять на рамку
                    SnapsToDevicePixels = true,
                    IsHitTestVisible = false
                };

                var badgeText = new TextBlock
                {
                    Text = index.ToString(),
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brushes.Black,
                    SnapsToDevicePixels = true
                };
                ApplyCrispText(badgeText);
                badgeBorder.Child = badgeText;

                root.Children.Add(shadow);
                root.Children.Add(card);
                root.Children.Add(badgeBorder);

                Panel.SetZIndex(shadow, 0);
                Panel.SetZIndex(card, 1);
                Panel.SetZIndex(badgeBorder, 2);

                return (root, card);
            }

            // =========================================================
            // Build UI
            // =========================================================
            int idx = 0;
            foreach (var op in operationalNumbers)
            {
                var lbl1 = MakeLabel("OperationalNumber");
                var tb1 = MakeTextBox(readOnly: true);
                tb1.Text = op ?? string.Empty;

                var lbl2 = MakeLabel("LinkSerialNumber");
                //var tb2 = MakeTextBox(readOnly: false, highlightEditable: true);
                var tb2 = MakeTextBox(readOnly: true);
                tb2.Tag = idx;
                //tb2.KeyDown += TbLinkSerialNumber_KeyDownAsync;

                var err = new TextBlock
                {
                    Text = "",
                    Margin = new Thickness(0, 8, 0, 0),
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = Brushes.Black,
                    FontSize = 12,
                    FontWeight = FontWeights.Medium,
                    Opacity = 0.85,
                    SnapsToDevicePixels = true
                };
                ApplyCrispText(err);

                var stack = new StackPanel { Orientation = Orientation.Vertical };
                stack.Children.Add(lbl1); stack.Children.Add(tb1);
                stack.Children.Add(lbl2); stack.Children.Add(tb2);
                stack.Children.Add(err);

                // idx+1 — чтобы номера были 1..N
                var (cardUi, cardBorderRef) = MakeCard(stack, idx + 1);

                wrapPanelMultiSns.Children.Add(cardUi);

                _snItems.Add(new ItemUi
                {
                    Root = cardBorderRef,           // ВАЖНО: сохраняем именно Border карточки (как ты раньше ожидал)
                    TbOperationalNumber = tb1,
                    TbLinkSerialNumber = tb2,
                    thirdPartySn = string.Empty,
                    TbError = err
                });

                idx++;
            }

            return _snItems;
        }

        private static void ApplyCrispText(DependencyObject d)
        {
            TextOptions.SetTextFormattingMode(d, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(d, TextRenderingMode.ClearType);
            TextOptions.SetTextHintingMode(d, TextHintingMode.Fixed);
            RenderOptions.SetClearTypeHint(d, ClearTypeHint.Enabled);

            // важно: округление layout лучше включить на Window/root XAML,
            // но тут тоже не мешает
            if (d is UIElement el)
            {
                el.SnapsToDevicePixels = true;
                el.SetValue(FrameworkElement.UseLayoutRoundingProperty, true);
            }
        }


        public void SetError(int index, string errorText)
        {
            if (index < 0 || index >= _linkSn.Count) return;
            _snItems[index].TbError.Text = errorText ?? "";
        }

        public void ClearAll()
        {
            wrapPanelMultiSns.Children.Clear();
            _linkSn.Clear();
            borderApply.Visibility = Visibility.Collapsed;
            textBoxLinkSn.Focus();
            textBoxLinkSn.SelectAll();
        }

        private async void textBoxApply_KeyDownAsync(object sender, KeyEventArgs e)
        {

            if (e.Key != Key.Enter && e.Key != Key.Return)
                return;

            var text = textBoxApply.Text?.Trim().ToUpper();

            if (!string.IsNullOrEmpty(text) && text != "OK")
                return;

            for (int i = 0; i < _snItems.Count; i++)
            {

                var item = _snItems[i];
                var writeMetaResult = await GOR_API.PutMetadataVariationCustomSerialAsync(AppState.GetSessionIdOrThrow(), item.thirdPartySn, item.TbOperationalNumber.Text);
                item.TbError.Text = null;
                if (writeMetaResult.success)
                {
                    SetTextAndColor(item, "Susiejimas: SUCCESS", true);
                }
                else
                {
                    SetTextAndColor(item, writeMetaResult.errorText, false);
                    return;
                }

                var writeMetaToTargetResult = await GOR_API.PutMetadataVariationCustomSerialAsync(AppState.GetSessionIdOrThrow(), item.TbOperationalNumber.Text, item.TbLinkSerialNumber.Text);
                if (writeMetaToTargetResult.success)
                {
                    SetTextAndColor(item, "Priskirimas: SUCCESS", true);
                }
                else
                {
                    SetTextAndColor(item, writeMetaToTargetResult.errorText, false);
                    return;
                }

                var writeActionResult = await GOR_API.CreateActionByOperationalCodeAsync(AppState.GetSessionIdOrThrow(), AppState.GetLinkTaskId(), item.TbOperationalNumber.Text, true, null, $"Priskirta: {item.TbLinkSerialNumber.Text}");
                if (writeActionResult.success)
                {
                    SetTextAndColor(item, "Operacija: SUCCESS", true);
                }
                else
                {
                    SetTextAndColor(item, writeActionResult.errorText, false);
                    return;
                }

            }



            CompletedCount+= _snItems.Count;
            textBlockCombletedCount.Text = CompletedCount.ToString();
            borderApply.Visibility = Visibility.Collapsed;
            textBoxLinkSn.Clear();
            textBoxLinkSn.Focus();

        }

        private void SetTextAndColor(ItemUi item, string text, bool good)
        {
            item.TbError.Foreground = good ? Brushes.Green : Brushes.Red;

            var prev = item.TbError.Text;
            item.TbError.Text = string.IsNullOrEmpty(prev) ? text : prev + "\n" + text;
        }

        private async void SetGorOp()
        {
            try
            {
                string gorOp;
                string errorMsg;
                bool success = ConfigTxtParser.TryGetValue("ALA440", "Susiejimas", out gorOp, out errorMsg);
                if (!success)
                {
                    MessageBox.Show(errorMsg);
                    return;
                }

                var findOpResult = await GOR_API.GetOperationCodeAsync(AppState.GetSessionIdOrThrow(), gorOp);
                if (findOpResult.success)
                {
                    textBoxOperationCode.Text = findOpResult.data.Code;
                    textBoxOperationDiscription.Text = findOpResult.data.Description;
                    textBoxMandatoryOperations.Text = findOpResult.data.MandatoryOperations;
                    checkBoxRepeatable.IsChecked = findOpResult.data.Repeatable;
                    gridErrorErrorText.Text = "";
                }
                else
                {
                    textBlockErrorGorCode.Text = gorOp;
                    gridErrorErrorText.Text = findOpResult.errorText;
                    //MessageBox.Show(findOpResult.errorText);
                    return;
                }

                if (findOpResult.data.Disabled == true)
                {
                    textBlockErrorGorCode.Text = gorOp;
                    gridErrorErrorText.Text = "Operacija isjungta!";
                    return;
                }

                var startTaskResult = await GOR_API.StartTaskAsync(AppState.GetSessionIdOrThrow(), findOpResult.data.Code);

                if (startTaskResult.success)
                {
                    AppState.SetLinkTaskId((int)startTaskResult.taskId);
                    boarderScan.Visibility = Visibility.Visible;
                    borderMainArea.Visibility = Visibility.Visible;
                    textBoxGorCode.Clear();
                    textBoxLinkSn.Focus();
                    textBlockErrorGorCode.Text = "";
                    gridErrorErrorText.Text = "";

                    gridError.Visibility = Visibility.Collapsed;
                    gridMain.Visibility = Visibility.Visible;
                }
                else
                {
                    textBlockErrorGorCode.Text = gorOp;
                    gridErrorErrorText.Text = $"Panasu, kad GOR kodas atjungas, arba neegistuoja\n{startTaskResult.errorText}";
                    
                }

            }
            catch (Exception ex)
            {
                gridErrorErrorText.Text = ex.Message;
            }
        }



        private async void textBoxGorCode_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter || e.Key != Key.Return) { return; }
            string gorCode = textBoxGorCode.Text;
            if (gorCode.Length != 10)
            {
                textBlockGorCodeError.Text = "Operacijos kodas susidaro is 10 simboliu";
                return;
            }

            try
            {
                var findOpResult = await GOR_API.GetOperationCodeAsync(AppState.GetSessionIdOrThrow(), gorCode);
                if (findOpResult.success)
                {
                    textBoxOperationCode.Text = findOpResult.data.Code;
                    textBoxOperationDiscription.Text = findOpResult.data.Description;
                    textBoxMandatoryOperations.Text = findOpResult.data.MandatoryOperations;
                    checkBoxRepeatable.IsChecked = findOpResult.data.Repeatable;
                    textBlockGorCodeError.Text = "";
                }
                else
                {
                    textBlockGorCodeError.Text = findOpResult.errorText;
                    return;
                }

                if (findOpResult.data.Disabled == true)
                {
                    textBlockGorCodeError.Text = "Operacija isjungta!";
                    return;
                }

                var startTaskResult = await GOR_API.StartTaskAsync(AppState.GetSessionIdOrThrow(), findOpResult.data.Code);

                if (startTaskResult.success)
                {
                    AppState.SetLinkTaskId((int)startTaskResult.taskId);
                    boarderScan.Visibility = Visibility.Visible;
                    borderMainArea.Visibility = Visibility.Visible;
                    textBoxGorCode.Clear();
                    textBoxLinkSn.Focus();
                    textBlockGorCodeError.Text = "";
                }
                else
                {
                    textBlockGorCodeError.Text = startTaskResult.errorText;
                }

            }
            catch (Exception ex)
            {

                textBlockGorCodeError.Text = ex.Message;
            }

        }


        private void gridErrorBtn_Click(object sender, RoutedEventArgs e)
        {
            SetGorOp();
        }
    }
}
