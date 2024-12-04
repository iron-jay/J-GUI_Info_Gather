using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

namespace J_GUI_Info_Gather
{
    public partial class Test_Dialog : Window
    {
        public string regexOutput;
        public int timeoutOutput;
        public List<string> buildtypesOutput;

        public Test_Dialog()
        {
            InitializeComponent();
        }

        private void Timeout_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Only allow digits
            e.Handled = !Regex.IsMatch(e.Text, @"[0-9]");
        }

        public void TestButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(regex.Text))
            {
                (((App)Application.Current).testingRegex) = regex.Text;
                (((App)Application.Current).regexActive) = true;

            }

            if (!string.IsNullOrWhiteSpace(timeout.Text))
            {
                int.TryParse(timeout.Text, out (((App)Application.Current).testingTimeout));
                (((App)Application.Current).timeoutActive) = true;
            }

            if (!string.IsNullOrWhiteSpace(buildtypes.Text))
            {
                (((App)Application.Current).testingBuildtypes) = buildtypes.Text.Split(',').ToList();
                (((App)Application.Current).buildtypeActive) = true;
            }

            Hide();
        }
    }
}
