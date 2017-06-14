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

namespace scada_analyst
{
    /// <summary>
    /// Interaction logic for Window_ExportControl.xaml
    /// </summary>
    public partial class Window_ExportControl : Window
    {
        #region Variables



        #endregion

        public Window_ExportControl(Window owner)
        {
            InitializeComponent();

            Owner = owner;
        }

        #region Properties

        public bool ExportPowMaxm { get { return CBox_Pow_Maxm.IsChecked.Value; } }
        public bool ExportPowMinm { get { return CBox_Pow_Minm.IsChecked.Value; } }
        public bool ExportPowMean { get { return CBox_Pow_Mean.IsChecked.Value; } }
        public bool ExportPowStdv { get { return CBox_Pow_Stdv.IsChecked.Value; } }

        public bool ExportAmbMaxm { get { return CBox_Amb_Maxm.IsChecked.Value; } }
        public bool ExportAmbMinm { get { return CBox_Amb_Minm.IsChecked.Value; } }
        public bool ExportAmbMean { get { return CBox_Amb_Mean.IsChecked.Value; } }
        public bool ExportAmbStdv { get { return CBox_Amb_Stdv.IsChecked.Value; } }

        public bool ExportWSpMaxm { get { return CBox_WSp_Maxm.IsChecked.Value; } }
        public bool ExportWSpMinm { get { return CBox_WSp_Minm.IsChecked.Value; } }
        public bool ExportWSpMean { get { return CBox_WSp_Mean.IsChecked.Value; } }
        public bool ExportWSpStdv { get { return CBox_WSp_Stdv.IsChecked.Value; } }

        public bool ExportGenMaxm { get { return CBox_Gen_Maxm.IsChecked.Value; } }
        public bool ExportGenMinm { get { return CBox_Gen_Minm.IsChecked.Value; } }
        public bool ExportGenMean { get { return CBox_Gen_Mean.IsChecked.Value; } }
        public bool ExportGenStdv { get { return CBox_Gen_Stdv.IsChecked.Value; } }

        public bool ExportMBrMaxm { get { return CBox_MBr_Maxm.IsChecked.Value; } }
        public bool ExportMBrMinm { get { return CBox_MBr_Minm.IsChecked.Value; } }
        public bool ExportMBrMean { get { return CBox_MBr_Mean.IsChecked.Value; } }
        public bool ExportMBrStdv { get { return CBox_MBr_Stdv.IsChecked.Value; } }

        #endregion
    }
}
