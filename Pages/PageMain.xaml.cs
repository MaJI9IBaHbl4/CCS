using System.Windows;
using System.Windows.Controls;

namespace CustomCodeSystem.Pages
{
    /// <summary>
    /// Логика взаимодействия для PageMain.xaml
    /// </summary>
    public partial class PageMain : Page
    {
        private bool accessToConfig = false;
        private const string PASSWORD = "272380";

        public PageMain()
        {
            InitializeComponent();
        }

        private void btnWriteAction_Click(object sender, RoutedEventArgs e)
        {
            Nav.Go("PageScanOperations");
        }

        private void btnConvertSnList_Click(object sender, RoutedEventArgs e)
        {

            if (accessToConfig)
            {
                Nav.Go("PageConfigSns");
                return;
            }

            var dlg = new PasswordDialog("Reikia slaptažodžio", "Įvesk slaptažodį, kad atidaryti langą.", true)
            {
                Owner = Window.GetWindow(this)
            };

            bool? ok = dlg.ShowDialog();
            if (ok != true)
                return;

            if (dlg.ResultText != PASSWORD)
            {
                MessageBox.Show("Neteisingas slaptažodis", "Klaida",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            accessToConfig = true;
            Nav.Go("PageConfigSns");
        }



        private void btnLinkSn_Click(object sender, RoutedEventArgs e)
        {
            Nav.Go("PageLinkSns");
        }

        private void btnSearchAction_Click(object sender, RoutedEventArgs e)
        {
            Nav.Go("PageSearchActions");
        }

        private void btnTestSns_Click(object sender, RoutedEventArgs e)
        {
            Nav.Go("TestSNS");
        }
    }
}
