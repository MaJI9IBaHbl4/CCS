using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;


namespace CustomCodeSystem
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Nav.Init(MainFrame);
            Nav.Go("PageLogin");
            textBlockVersion.Text = AppState.GetVersion();
        }

        public void SetUser(string Name, string Surname)
        {
            textBlockUser.Text = Name + " " + Surname;
        }

        public void SetLocation(string location)
        {
            textBlockLocation.Text = location;
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            textBlockUser.Text = "";
            textBlockLocation.Text = "—";
            AppState.Clear();
            Nav.NavigationUIHide();
            Nav.ClearCache();
            Nav.ClearHistory();
            Nav.Go("PageLogin");
        }

    }
}