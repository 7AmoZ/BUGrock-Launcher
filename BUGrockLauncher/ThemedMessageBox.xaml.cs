using System;
using System.Windows;
using System.Windows.Input;

namespace BUGrockLauncher {
    public partial class ThemedMessageBox : Window {
        public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;
        private readonly MessageBoxButton _buttons;

        public ThemedMessageBox(string message, string title, MessageBoxButton buttons, MessageBoxImage icon) {
            InitializeComponent();
            _buttons = buttons;
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

                case MessageBoxButton.YesNoCancel:
                    var cancelBtn3 = new System.Windows.Controls.Button {
                        Content = "Cancel",
                        Style = (Style)FindResource("DialogSecondaryButtonStyle"),
                        Margin = new Thickness(0, 0, 10, 0),
                        IsCancel = true
                    };
                    cancelBtn3.Click += (s, e) => { Result = MessageBoxResult.Cancel; Close(); };
                    ButtonsPanel.Children.Add(cancelBtn3);

                    var noBtn3 = new System.Windows.Controls.Button {
                        Content = "No",
                        Style = (Style)FindResource("DialogSecondaryButtonStyle"),
                        Margin = new Thickness(0, 0, 10, 0)
                    };
                    noBtn3.Click += (s, e) => { Result = MessageBoxResult.No; Close(); };
                    ButtonsPanel.Children.Add(noBtn3);

                    var yesBtn3 = new System.Windows.Controls.Button {
                        Content = "Yes",
                        Style = (Style)FindResource("DialogPrimaryButtonStyle"),
                        IsDefault = true
                    };
                    yesBtn3.Click += (s, e) => { Result = MessageBoxResult.Yes; Close(); };
                    ButtonsPanel.Children.Add(yesBtn3);
                    break;
            }
        }

        private void Header_MouseDown(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e) {
            Result = _buttons switch {
                MessageBoxButton.OK => MessageBoxResult.OK,
                MessageBoxButton.YesNo => MessageBoxResult.No,
                _ => MessageBoxResult.Cancel
            };
            Close();
        }

        public static MessageBoxResult Show(string message, string title = "BUGrock Launcher", MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.None, Window? owner = null) {
            var app = Application.Current;
            if (app != null && !app.Dispatcher.CheckAccess()) {
                return (MessageBoxResult)app.Dispatcher.Invoke(new Func<MessageBoxResult>(() => Show(message, title, buttons, icon, owner)));
            }

            owner ??= app?.MainWindow;

            var dialog = new ThemedMessageBox(message, title, buttons, icon);
            if (owner != null && owner.IsVisible) {
                dialog.Owner = owner;
            }
            dialog.ShowDialog();
            if (dialog.Result == MessageBoxResult.None) {
                return buttons switch {
                    MessageBoxButton.OK => MessageBoxResult.OK,
                    MessageBoxButton.YesNo => MessageBoxResult.No,
                    _ => MessageBoxResult.Cancel
                };
            }
            return dialog.Result;
        }
    }
}
