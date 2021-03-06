﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

using MahApps.Metro.Controls;

namespace scada_analyst
{
    /// <summary>
    /// Interaction logic for Window_AnalysisSettings.xaml
    /// </summary>
    public partial class Window_AnalysisSettings : MetroWindow
    {
        #region Variables
        


        #endregion 

        public Window_AnalysisSettings(MetroWindow owner, Analysis analyser,
            bool nightTime, bool astdwTime, bool naudwTime, bool civdwTime, 
            bool daytmTime, bool civdsTime, bool naudsTime, bool astdsTime)
        {
            InitializeComponent();

            Owner = owner;

            NBox_Cutin.NumericValue = analyser.CutIn;
            NBox_Ctout.NumericValue = analyser.CutOut;
            NBox_RaPow.NumericValue = analyser.RatedPwr;

            CBox_Mnt_Night.IsChecked = nightTime;
            CBox_Mnt_AstDw.IsChecked = astdwTime;
            CBox_Mnt_NauDw.IsChecked = naudwTime;
            CBox_Mnt_CivDw.IsChecked = civdwTime;
            CBox_Mnt_Daytm.IsChecked = daytmTime;
            CBox_Mnt_CivDs.IsChecked = civdsTime;
            CBox_Mnt_NauDs.IsChecked = naudsTime;
            CBox_Mnt_AstDs.IsChecked = astdsTime;

            TCtrl_MorningH.NumericValue = analyser.WorkHoursMorning.Hours;
            TCtrl_MorningM.NumericValue = analyser.WorkHoursMorning.Minutes;
            TCtrl_EveningH.NumericValue = analyser.WorkHoursEvening.Hours;
            TCtrl_EveningM.NumericValue = analyser.WorkHoursEvening.Minutes;
        }

        private void ApplyClick(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        #region Properties

        public bool Mnt_Night { get { return CBox_Mnt_Night.IsChecked.Value; } set { CBox_Mnt_Night.IsChecked = value; } }
        public bool Mnt_AstDw { get { return CBox_Mnt_AstDw.IsChecked.Value; } set { CBox_Mnt_AstDw.IsChecked = value; } }
        public bool Mnt_NauDw { get { return CBox_Mnt_NauDw.IsChecked.Value; } set { CBox_Mnt_NauDw.IsChecked = value; } }
        public bool Mnt_CivDw { get { return CBox_Mnt_CivDw.IsChecked.Value; } set { CBox_Mnt_CivDw.IsChecked = value; } }
        public bool Mnt_Daytm { get { return CBox_Mnt_Daytm.IsChecked.Value; } set { CBox_Mnt_Daytm.IsChecked = value; } }
        public bool Mnt_CivDs { get { return CBox_Mnt_CivDs.IsChecked.Value; } set { CBox_Mnt_CivDs.IsChecked = value; } }
        public bool Mnt_NauDs { get { return CBox_Mnt_NauDs.IsChecked.Value; } set { CBox_Mnt_NauDs.IsChecked = value; } }
        public bool Mnt_AstDs { get { return CBox_Mnt_AstDs.IsChecked.Value; } set { CBox_Mnt_AstDs.IsChecked = value; } }

        public double SpdIns { get { return NBox_Cutin.NumericValue; } set { NBox_Cutin.NumericValue = value; } }
        public double SpdOut { get { return NBox_Ctout.NumericValue; } set { NBox_Ctout.NumericValue = value; } }
        public double RtdPwr { get { return NBox_RaPow.NumericValue; } set { NBox_RaPow.NumericValue = value; } }

        public TimeSpan WorkHoursMorning
        {
            get
            { return new TimeSpan((int)TCtrl_MorningH.NumericValue, (int)TCtrl_MorningM.NumericValue, 0);
            }
            set
            {
                TCtrl_MorningH.NumericValue = value.Hours;
                TCtrl_MorningM.NumericValue = value.Minutes;
            }
        }

        public TimeSpan WorkHoursEvening
        {
            get
            {
                return new TimeSpan((int)TCtrl_EveningH.NumericValue, (int)TCtrl_EveningM.NumericValue, 0);
            }
            set
            {
                TCtrl_EveningH.NumericValue = value.Hours;
                TCtrl_EveningM.NumericValue = value.Minutes;
            }
        }
        #endregion
    }
}
