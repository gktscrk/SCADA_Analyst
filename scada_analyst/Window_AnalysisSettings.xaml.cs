using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace scada_analyst
{
    /// <summary>
    /// Interaction logic for Window_AnalysisSettings.xaml
    /// </summary>
    public partial class Window_AnalysisSettings : Window
    {
        #region Variables
        

        #endregion 

        public Window_AnalysisSettings(Window owner, double spdIns, double spdOut)
        {
            InitializeComponent();

            Owner = owner;

            NBox_Cutin.NumericValue = spdIns;
            NBox_Ctout.NumericValue = spdOut;
        }

        private void ApplyClick(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        #region Properties

        public double SpdIns { get { return NBox_Cutin.NumericValue; } set { NBox_Cutin.NumericValue = value; } }
        public double SpdOut { get { return NBox_Ctout.NumericValue; } set { NBox_Ctout.NumericValue = value; } }

        #endregion
    }
}
