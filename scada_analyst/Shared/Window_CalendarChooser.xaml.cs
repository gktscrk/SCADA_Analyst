﻿using System;
using System.Windows;

using MahApps.Metro.Controls;

using scada_analyst.Controls;

namespace scada_analyst.Shared
{
    /// <summary>
    /// Interaction logic for Window_CalendarChooser.xaml
    /// </summary>
    public partial class Window_CalendarChooser : MetroWindow
    {
        #region Constuctor 

        public Window_CalendarChooser(MetroWindow owner, string windowTitle, DateTime inputDate)
        {
            InitializeComponent();

            Owner = owner;

            Title = windowTitle;
            
            Calendar.SelectedDate = inputDate;
            Calendar.DisplayDate = inputDate;
        }

        #endregion

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void Calendar_DisplayDateChanged(object sender, System.Windows.Controls.CalendarDateChangedEventArgs e)
        {

        }

        private void Calendar_DisplayModeChanged(object sender, System.Windows.Controls.CalendarModeChangedEventArgs e)
        {

        }

        private void Calendar_SelectedDatesChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            TextBox_Calendar.Text = Calendar.SelectedDate.ToString();
        }

        #region Properties

        public string CalendarDate { get { return TextBox_Calendar.Text; } }

        #endregion
    }
}
