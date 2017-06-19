using System;
using System.Windows;

using MahApps.Metro.Controls;

using scada_analyst.Controls;

namespace scada_analyst.Shared
{
    /// <summary>
    /// Interaction logic for Window_NumberTwo.xaml
    /// </summary>
    public partial class Window_NumberTwo : MetroWindow
    {
        public Window_NumberTwo(MetroWindow owner, string windowTitle, string inputA, string inputB, bool allowDecs = false,
            bool allowNegatives = false, double input1 = 0, double input2 = 0)
        {
            InitializeComponent();

            Owner = owner;

            Title = windowTitle;

            Label1.Content = inputA;
            Label2.Content = inputB;

            Number1.NumericValue = input1;
            Number2.NumericValue = input2;

            if (allowDecs)
            {
                Number1.AllowDecimalPlaces = true;
                Number2.AllowDecimalPlaces = true;
            }

            if (allowNegatives)
            {
                Number1.AllowNegative = true;
                Number2.AllowNegative = true;
            }
        }
        
        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        #region Properties

        public double NumericValue1 { get { return Number1.NumericValue; } }

        public double NumericValue2 { get { return Number2.NumericValue; } }

        #endregion
    }
}
