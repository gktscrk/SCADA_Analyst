using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

using MahApps.Metro.Controls;

namespace scada_analyst
{
    /// <summary>
    /// Interaction logic for Window_ExportControl.xaml
    /// </summary>
    public partial class Window_ExportControl : MetroWindow
    {
        #region Variables



        #endregion

        public Window_ExportControl(MetroWindow owner)
        {
            InitializeComponent();

            Owner = owner;
        }

        private void ApplyClick(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        #region Properties

        public bool ExportPowMaxm { get { return CBox_Pow_Maxm.IsChecked.Value; } set { CBox_Pow_Maxm.IsChecked = value; } }
        public bool ExportPowMinm { get { return CBox_Pow_Minm.IsChecked.Value; } set { CBox_Pow_Minm.IsChecked = value; } }
        public bool ExportPowMean { get { return CBox_Pow_Mean.IsChecked.Value; } set { CBox_Pow_Mean.IsChecked = value; } }
        public bool ExportPowStdv { get { return CBox_Pow_Stdv.IsChecked.Value; } set { CBox_Pow_Stdv.IsChecked = value; } }

        public bool ExportAmbMaxm { get { return CBox_Amb_Maxm.IsChecked.Value; } set { CBox_Amb_Maxm.IsChecked = value; } }
        public bool ExportAmbMinm { get { return CBox_Amb_Minm.IsChecked.Value; } set { CBox_Amb_Minm.IsChecked = value; } }
        public bool ExportAmbMean { get { return CBox_Amb_Mean.IsChecked.Value; } set { CBox_Amb_Mean.IsChecked = value; } }
        public bool ExportAmbStdv { get { return CBox_Amb_Stdv.IsChecked.Value; } set { CBox_Amb_Stdv.IsChecked = value; } }

        public bool ExportWSpMaxm { get { return CBox_WSp_Maxm.IsChecked.Value; } set { CBox_WSp_Maxm.IsChecked = value; } }
        public bool ExportWSpMinm { get { return CBox_WSp_Minm.IsChecked.Value; } set { CBox_WSp_Minm.IsChecked = value; } }
        public bool ExportWSpMean { get { return CBox_WSp_Mean.IsChecked.Value; } set { CBox_WSp_Mean.IsChecked = value; } }
        public bool ExportWSpStdv { get { return CBox_WSp_Stdv.IsChecked.Value; } set { CBox_WSp_Stdv.IsChecked = value; } }

        public bool ExportGBxMaxm { get { return CBox_GBx_Maxm.IsChecked.Value; } set { CBox_GBx_Maxm.IsChecked = value; } }
        public bool ExportGBxMinm { get { return CBox_GBx_Minm.IsChecked.Value; } set { CBox_GBx_Minm.IsChecked = value; } }
        public bool ExportGBxMean { get { return CBox_GBx_Mean.IsChecked.Value; } set { CBox_GBx_Mean.IsChecked = value; } }
        public bool ExportGBxStdv { get { return CBox_GBx_Stdv.IsChecked.Value; } set { CBox_GBx_Stdv.IsChecked = value; } }

        public bool ExportGenMaxm { get { return CBox_Gen_Maxm.IsChecked.Value; } set { CBox_Gen_Maxm.IsChecked = value; } }
        public bool ExportGenMinm { get { return CBox_Gen_Minm.IsChecked.Value; } set { CBox_Gen_Minm.IsChecked = value; } }
        public bool ExportGenMean { get { return CBox_Gen_Mean.IsChecked.Value; } set { CBox_Gen_Mean.IsChecked = value; } }
        public bool ExportGenStdv { get { return CBox_Gen_Stdv.IsChecked.Value; } set { CBox_Gen_Stdv.IsChecked = value; } }

        public bool ExportMBrMaxm { get { return CBox_MBr_Maxm.IsChecked.Value; } set { CBox_MBr_Maxm.IsChecked = value; } }
        public bool ExportMBrMinm { get { return CBox_MBr_Minm.IsChecked.Value; } set { CBox_MBr_Minm.IsChecked = value; } }
        public bool ExportMBrMean { get { return CBox_MBr_Mean.IsChecked.Value; } set { CBox_MBr_Mean.IsChecked = value; } }
        public bool ExportMBrStdv { get { return CBox_MBr_Stdv.IsChecked.Value; } set { CBox_MBr_Stdv.IsChecked = value; } }

        public bool ExportNacMaxm { get { return CBox_Nac_Maxm.IsChecked.Value; } set { CBox_Nac_Maxm.IsChecked = value; } }
        public bool ExportNacMinm { get { return CBox_Nac_Minm.IsChecked.Value; } set { CBox_Nac_Minm.IsChecked = value; } }
        public bool ExportNacMean { get { return CBox_Nac_Mean.IsChecked.Value; } set { CBox_Nac_Mean.IsChecked = value; } }
        public bool ExportNacStdv { get { return CBox_Nac_Stdv.IsChecked.Value; } set { CBox_Nac_Stdv.IsChecked = value; } }

        #endregion
    }
}
