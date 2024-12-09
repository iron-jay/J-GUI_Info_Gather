using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Serilog;

namespace J_GUI_Info_Gather
{
    public partial class MainWindow : Window
    {
        TS_Integration TS = new TS_Integration();
        DispatcherTimer timer;
        int countdownSeconds = (((App)Application.Current).timeout);

        public MainWindow()
        {
            InitializeComponent();
            FillTheForm();

            if (!((App)Application.Current).timeoutActive)
            {
                TimeeoutLabel.Visibility = Visibility.Hidden;
            }
            else
            {
                if (((App)Application.Current).regexActive)
                {
                    if (((App)Application.Current).IsValidHostname())
                    {
                        StartTimer();
                    }
                    else
                    {
                        timerDisplay.Text = "Asset # Invalid.\nTimer Suspended.";
                        timerDisplay.Margin = new Thickness(105, 230, 0, 0);
                    }
                }
                else
                {
                    StartTimer();
                }


            }
        }

        private void FillTheForm()
        {

            hostname.Text = (((App)Application.Current).hostname);
            if (((App)Application.Current).regexActive)
            {
                if (((App)Application.Current).IsValidHostname())
                {
                    hostname.IsEnabled = false;

                }
            }

            model.Text = (((App)Application.Current).model);
            make.Text = (((App)Application.Current).make);
            form.Text = (((App)Application.Current).enclosure);

            if ((((App)Application.Current).buildtypes) != null && (((App)Application.Current).buildtypes[0] != ""))
            {
                foreach (string option in (((App)Application.Current).buildtypes))
                {
                    RadioButton radioButton = new RadioButton
                    {
                        Content = option,
                        Margin = new Thickness(5, 0, 5, 0),
                    };
                    radioButton.Checked += RadioButton_Checked;
                    radioButtonContainer.Children.Add(radioButton);

                }
                if (radioButtonContainer.Children.Count > 0 && radioButtonContainer.Children[0] is RadioButton firstRadioButton)
                {
                    firstRadioButton.IsChecked = true;
                }
            }
            else
            {
                BuildtypeLabel.Visibility = Visibility.Collapsed;
            }
        }

        private void RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            (((App)Application.Current).submittedBuildtype) = (sender as RadioButton)?.Content?.ToString();
        }

        public void StartTimer()
        {

            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += Timer_Tick;

            timerDisplay.Text = $"{(countdownSeconds / 3600).ToString("D2")}:{((countdownSeconds % 3600) / 60).ToString("D2")}:{((countdownSeconds % 3600) % 60).ToString("D2")}";
            timer.Start();
        }
        //defines the timer tick
        public void Timer_Tick(object sender, EventArgs e)
        {
            countdownSeconds--;

            timerDisplay.Text = $"{(countdownSeconds / 3600).ToString("D2")}:{((countdownSeconds % 3600) / 60).ToString("D2")}:{((countdownSeconds % 3600) % 60).ToString("D2")}";

            if (countdownSeconds == 0)
            {
                timerDisplay.Text = $"00:00:00";
            }
            else if (countdownSeconds < 0)
            {
                FinishThings();
            }
        }

        public void FinishThings()
        {
            if (((App)Application.Current).timeoutActive)
            {
                timer.Stop();
            }

            if (((App)Application.Current).regexActive)
            {
                if (!((App)Application.Current).IsValidHostname() && !((App)Application.Current).isVM)
                {
                    hostname.Foreground = System.Windows.Media.Brushes.Red;
                    MessageBox.Show("Hostname does not meet standards", "Invalid", MessageBoxButton.OK, MessageBoxImage.Error);
                    Log.Debug("Hostname does not meet standards");
                }
                else
                {
                    (((App)Application.Current).submittedHostname) = hostname.Text.ToUpper();
                    Log.Information("Hostname Validation: Hostname = {Hostname}, Valid = {IsValid}", ((App)Application.Current).hostname, ((App)Application.Current).IsValidHostname());

                    if (Keyboard.IsKeyDown(Key.LeftCtrl) && Keyboard.IsKeyDown(Key.LeftShift))
                    {
                        Log.Information("Override set to true");

                        if (TS.IsTSEnv())
                        {
                            TS.SetTSVar("Override", "true");

                        }
                        else
                        {
                            MessageBox.Show("Supported Model Overridden.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                    if (Keyboard.IsKeyDown(Key.Escape))
                    {
                        Log.Information("Forced Failed");

                        Environment.Exit(4);
                    }

                    Hide();
                }
            }
            else
            {
                (((App)Application.Current).submittedHostname) = hostname.Text.ToUpper();

                if (Keyboard.IsKeyDown(Key.LeftCtrl) && Keyboard.IsKeyDown(Key.LeftShift))
                {
                    if (TS.IsTSEnv())
                    {
                        TS.SetTSVar("Override", "true");
                    }
                    else
                    {
                        MessageBox.Show("Supported Model Overridden.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                if (Keyboard.IsKeyDown(Key.Escape))
                {
                    Environment.Exit(4);
                }

                Hide();
            }
        }
        public void SubmitButton_Click(object sender, RoutedEventArgs e)
        {
            FinishThings();
        }   
    }
}