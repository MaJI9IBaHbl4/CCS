using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace CustomCodeSystem.Pages
{
    public partial class PasswordDialog : Window
    {
        private readonly bool _isPassword;

        public string ResultText => _isPassword ? pbInput.Password : tbInput.Text;

        public PasswordDialog(
            string header,
            string text,
            bool password,
            ImageSource? image = null,
            double imageWidth = 80)
        {
            InitializeComponent();

            _isPassword = password;
            this.Width = Math.Max(this.Width, imageWidth + 50);
            TextBlockHeader.Text = header;
            TextBlockDescription.Text = text;

            // Переключаем режим (PasswordBox vs TextBox)
            pbInput.Visibility = password ? Visibility.Visible : Visibility.Collapsed;
            tbInput.Visibility = password ? Visibility.Collapsed : Visibility.Visible;

            // Картинка (опционально)
            if (image != null)
            {
                DialogImage.Source = image;
                DialogImage.Width = Math.Max(1, imageWidth);
                DialogImage.Visibility = Visibility.Visible;
            }
            else
            {
                DialogImage.Source = null;
                DialogImage.Visibility = Visibility.Collapsed;
            }

            Loaded += (_, __) =>
            {
                if (_isPassword) pbInput.Focus();
                else tbInput.Focus();
            };
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Input_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                DialogResult = true;
                Close();
            }
            else if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
            }
        }

        private void Card_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }
    }
}
