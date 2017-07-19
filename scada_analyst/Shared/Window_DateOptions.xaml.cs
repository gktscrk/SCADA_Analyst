using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

using MahApps.Metro.Controls;

using scada_analyst.Shared;

namespace scada_analyst.Controls
{
    /// <summary>
    /// Interaction logic for Window_DateOptions.xaml
    /// </summary>
    public partial class Window_DateOptions : MetroWindow
    {
        public Window_DateOptions(MetroWindow owner, Common.DateFormat format)
        {
            InitializeComponent();

            if (format == Common.DateFormat.DMY) { RBox_DMY.IsChecked = true; }
            else if (format == Common.DateFormat.MDY) { RBox_MDY.IsChecked = true; }
            else if (format == Common.DateFormat.YMD) { RBox_YMD.IsChecked = true; }
            else if (format == Common.DateFormat.YDM) { RBox_YDM.IsChecked = true; }
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

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
    }
}