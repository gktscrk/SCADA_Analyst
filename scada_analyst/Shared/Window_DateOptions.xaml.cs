using System.Windows;
using MahApps.Metro.Controls;

using scada_analyst.Shared;

namespace scada_analyst.Controls
{
    /// <summary>
    /// Interaction logic for Window_DateOptions.xaml
    /// </summary>
    public partial class Window_DateOptions : MetroWindow
    {
        #region Constructor

        public Window_DateOptions(MetroWindow owner, Common.DateFormat format)
        {
            InitializeComponent();

            if (format == Common.DateFormat.DMY) { RBox_DMY.IsChecked = true; }
            else if (format == Common.DateFormat.MDY) { RBox_MDY.IsChecked = true; }
            else if (format == Common.DateFormat.YMD) { RBox_YMD.IsChecked = true; }
            else if (format == Common.DateFormat.YDM) { RBox_YDM.IsChecked = true; }
        }

        #endregion 

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        #region Properties

        public Common.DateFormat Format
        {
            get
            {
                if (RBox_DMY.IsChecked.Value) { return Common.DateFormat.DMY; }
                else if (RBox_MDY.IsChecked.Value) { return Common.DateFormat.MDY; }
                else if (RBox_YMD.IsChecked.Value) { return Common.DateFormat.YMD; }
                else if (RBox_YDM.IsChecked.Value) { return Common.DateFormat.YDM; }
                else { return Common.DateFormat.EMPTY; }
            }
        }

        #endregion 
    }
}