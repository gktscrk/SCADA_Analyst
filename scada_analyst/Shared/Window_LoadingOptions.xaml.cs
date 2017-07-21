using System;
using System.Windows;

using MahApps.Metro.Controls;

using scada_analyst.Shared;

namespace scada_analyst.Controls
{
    /// <summary>
    /// Interaction logic for Window_LoadingOptions.xaml
    /// </summary>
    public partial class Window_LoadingOptions : MetroWindow
    {
        #region Constructor

        public Window_LoadingOptions(MetroWindow owner, Common.DateFormat format, TimeSpan sampleLength)
        {
            InitializeComponent();

            if (format == Common.DateFormat.DMY) { RBox_DMY.IsChecked = true; }
            else if (format == Common.DateFormat.MDY) { RBox_MDY.IsChecked = true; }
            else if (format == Common.DateFormat.YMD) { RBox_YMD.IsChecked = true; }
            else if (format == Common.DateFormat.YDM) { RBox_YDM.IsChecked = true; }

            NBox_FileTimeStep.NumericValue = sampleLength.TotalMinutes;
        }

        #endregion 

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        #region Properties

        public TimeSpan SampleSeparation
        {
            get { return new TimeSpan(0, (int)NBox_FileTimeStep.NumericValue, 0); }
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

        #endregion 
    }
}