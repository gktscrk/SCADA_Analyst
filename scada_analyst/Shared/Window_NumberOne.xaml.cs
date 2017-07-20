using System.Windows;

using MahApps.Metro.Controls;

namespace scada_analyst.Controls
{
    /// <summary>
    /// Interaction logic for Window_NumberOne.xaml
    /// </summary>
    public partial class Window_NumberOne : MetroWindow
    {
        #region Constructor

        public Window_NumberOne(MetroWindow owner, string windowTitle, string inputA, bool allowDecs = false,
            bool allowNegatives = false, double input1 = 0)
        {
            InitializeComponent();

            Owner = owner;

            Title = windowTitle;

            Label1.Content = inputA;

            Number1.NumericValue = input1;

            if (allowDecs)
            {
                Number1.AllowDecimalPlaces = true;
            }

            if (allowNegatives)
            {
                Number1.AllowNegative = true;
            }
        }

        #endregion 

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        #region Properties

        public double NumericValue1 { get { return Number1.NumericValue; } }

        #endregion
    }
}
