using System.Windows;
using System.Windows.Input;

namespace MCLauncher {
    public partial class ThemedMessageBox : Window {
        public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;

        public ThemedMessageBox(string message, string title, MessageBoxButton buttons, MessageBoxImage icon) {
            InitializeComponent();
            TitleLabel.Text = string.IsNullOrEmpty(title) ? "BUGrock Launcher" : title;
            MessageText.Text = message;

            switch (icon) {
                case MessageBoxImage.Error:
                    IconLabel.Text = "❌";
                    break;
                case MessageBoxImage.Warning:
                    IconLabel.Text = "⚠️";
                    break;
                case MessageBoxImage.Question:
                    IconLabel.Text = "❓";
                    break;
                default:
                    IconLabel.Text = "ℹ️";
                    break;
            }

            SetupButtons(buttons);
        }

        private void SetupButtons(MessageBoxButton buttons) {
            ButtonsPanel.Children.Clear();

            switch (buttons) {
                case MessageBoxButton.OK:
                    var okBtn = new System.Windows.Controls.Button {
                        Content = "OK",
                        Style = (Style)FindResource("DialogPrimaryButtonStyle"),
                        IsDefault = true
                    };
                    okBtn.Click += (s, e) => { Result = MessageBoxResult.OK; Close(); };
                    ButtonsPanel.Children.Add(okBtn);
                    break;

                case MessageBoxButton.OKCancel:
                    var cancelBtn = new System.Windows.Controls.Button {
                        Content = "Cancel",
                        Style = (Style)FindResource("DialogSecondaryButtonStyle"),
                        Margin = new Thickness(0, 0, 10, 0),
                        IsCancel = true
                    };
                    cancelBtn.Click += (s, e) => { Result = MessageBoxResult.Cancel; Close(); };
                    ButtonsPanel.Children.Add(cancelBtn);

                    var okBtn2 = new System.Windows.Controls.Button {
                        Content = "OK",
                        Style = (Style)FindResource("DialogPrimaryButtonStyle"),
                        IsDefault = true
                    };
                    okBtn2.Click += (s, e) => { Result = MessageBoxResult.OK; Close(); };
                    ButtonsPanel.Children.Add(okBtn2);
                    break;

                case MessageBoxButton.YesNo:
                    var noBtn = new System.Windows.Controls.Button {
                        Content = "No",
                        Style = (Style)FindResource("DialogSecondaryButtonStyle"),
                        Margin = new Thickness(0, 0, 10, 0),
                        IsCancel = true
                    };
                    noBtn.Click += (s, e) => { Result = MessageBoxResult.No; Close(); };
                    ButtonsPanel.Children.Add(noBtn);

                    var yesBtn = new System.Windows.Controls.Button {
                        Content = "Yes",
                        Style = (Style)FindResource("DialogPrimaryButtonStyle"),
                        IsDefault = true
                    };
                    yesBtn.Click += (s, e) => { Result = MessageBoxResult.Yes; Close(); };
                    ButtonsPanel.Children.Add(yesBtn);
                    break;
            }
        }

        private void Header_MouseDown(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e) {
            Result = MessageBoxResult.Cancel;
            Close();
        }

        public static MessageBoxResult Show(string message, string title = "BUGrock Launcher", MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.None, Window? owner = null) {
            owner ??= Application.Current?.MainWindow;
            if (owner != null && !owner.Dispatcher.CheckAccess()) {
                return owner.Dispatcher.Invoke(() => Show(message, title, buttons, icon, owner));
            }

            var dialog = new ThemedMessageBox(message, title, buttons, icon);
            if (owner != null && owner.IsVisible) {
                dialog.Owner = owner;
            }
            dialog.ShowDialog();
            return dialog.Result == MessageBoxResult.None ? (buttons == MessageBoxButton.OK ? MessageBoxResult.OK : MessageBoxResult.Cancel) : dialog.Result;
        }
    }
}
