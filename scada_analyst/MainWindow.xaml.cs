﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

using Microsoft.Win32;

using LiveCharts;
using LiveCharts.Charts;

using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;

using scada_analyst.Controls;
using scada_analyst.Shared;
using LiveCharts.Wpf;

namespace scada_analyst
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow, INotifyPropertyChanged
    {
        #region Variables

        private bool _geoLoaded = false, _meteoLoaded = false, _scadaLoaded = false;
        private bool positionsAddedToData = false, eventsAreProcessed = false, eventsMatchedAcrossTypes = false;

        private static bool mnt_Night = false;
        private static bool mnt_AstDw = false;
        private static bool mnt_NauDw = false;
        private static bool mnt_CivDw = true;
        private static bool mnt_Daytm = true;
        private static bool mnt_CivDs = true;
        private static bool mnt_NauDs = false;
        private static bool mnt_AstDs = false;

        private bool _averagesComputed = false;
        private bool _usingPreviousWeekForGraphing = true;

        private bool exportPowMaxm = false, exportAmbMaxm = false, exportWSpMaxm = false;
        private bool exportPowMinm = false, exportAmbMinm = false, exportWSpMinm = false;
        private bool exportPowMean = true, exportAmbMean = true, exportWSpMean = true;
        private bool exportPowStdv = false, exportAmbStdv = false, exportWSpStdv = false;

        private bool exportNacMaxm = false, exportGBxMaxm = false, exportGenMaxm = false, exportMBrMaxm = false;
        private bool exportNacMinm = false, exportGBxMinm = false, exportGenMinm = false, exportMBrMinm = false;
        private bool exportNacMean = true, exportGBxMean = true, exportGenMean = true, exportMBrMean = true;
        private bool exportNacStdv = false, exportGBxStdv = false, exportGenStdv = false, exportMBrStdv = false;

        private string[] _labels;

        private List<int> loadedAsset = new List<int>();
        private List<string> _loadedFiles = new List<string>();
        private List<string> _eventDetailsSelection = new List<string>();
        private List<string> _eventSummarySelection = new List<string>();

        private CancellationTokenSource _cts;

        private Common.DateFormat _dateFormat = Common.DateFormat.YMD;
        private DateTime _dataExportStart = new DateTime();
        private DateTime _dataExportEndTm = new DateTime();
        private DateTime _eventExplrStart = new DateTime();
        private DateTime _eventExplrEndTm = new DateTime();
        private TimeSpan _inputSeparation = new TimeSpan(0, 10, 0);

        private Analysis _analyser = new Analysis();
        private GeoData _geoFile;
        private MeteoData _meteoFile = new MeteoData();
        private ScadaData _scadaFile = new ScadaData();
        
        private List<DirectoryItem> _overview = new List<DirectoryItem>();
        
        private ScadaData.ScadaSample.GearBox _gbox = new ScadaData.ScadaSample.GearBox();
        private ScadaData.ScadaSample.Generator _genr = new ScadaData.ScadaSample.Generator();
        private ScadaData.ScadaSample.MainBearing _mbrg = new ScadaData.ScadaSample.MainBearing();

        private ObservableCollection<Structure> _assetsVw = new ObservableCollection<Structure>();
        private ObservableCollection<EventData> _allWtrVw = new ObservableCollection<EventData>();
        private ObservableCollection<EventData> _allPwrVw = new ObservableCollection<EventData>();
        private ObservableCollection<EventData> _loSpView = new ObservableCollection<EventData>();
        private ObservableCollection<EventData> _hiSpView = new ObservableCollection<EventData>();
        private ObservableCollection<EventData> _noPwView = new ObservableCollection<EventData>();
        private ObservableCollection<EventData> _rtPwView = new ObservableCollection<EventData>();

        private ObservableCollection<string> _turbines = new ObservableCollection<string>();

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        #region Constructor

        public MainWindow()
        {
            InitializeComponent();
            this.WindowState = WindowState.Maximized;
            this.DataContext = this;

            //MainWindowViewModel mwVM = new MainWindowViewModel();

            LView_Overview.IsEnabled = false;

            LView_WSpdEvLo.IsEnabled = false;
            LView_WSpdEvHi.IsEnabled = false;
            LView_PowrNone.IsEnabled = false;
            LView_PowrRted.IsEnabled = false;

            ProgressBarInvisible();

            CreateEventDetailsView();
            CreateSummaryComboInfo();
            CreateSummaries();

            LView_LoadedOverview.SelectedIndex = 0;            
        }

        #endregion

        #region File Menu Functions

        /// <summary>
        /// Brings up the About window
        /// </summary>
        /// <param name = "sender" ></ param >
        /// < param name="e"></param>
        private void AboutClick(object sender, RoutedEventArgs e)
        {
            new Window_About(this).ShowDialog();
        }

        /// <summary>
        /// Options for choosing input data date format; default is year-month-day
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DateFormatClick(object sender, RoutedEventArgs e)
        {
            Window_LoadingOptions dO = new Window_LoadingOptions(this, _dateFormat, _inputSeparation);

            if (dO.ShowDialog().Value)
            {
                _dateFormat = dO.Format;
                _inputSeparation = dO.SampleSeparation;
            }
        }

        /// <summary>
        /// Method to display all loaded files' filenames
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LoadedFilesClick(object sender, RoutedEventArgs e)
        {
            new Window_LoadedFiles(this, LoadedFiles).ShowDialog();
        }

        /// <summary>
        /// Quit
        /// </summary>
        /// <param name = "sender" ></ param >
        /// < param name="e"></param>
        private void Exit(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        #endregion

        #region Duration Filter Editing

        /// <summary>
        /// Changes the duration of the filter than can be used to remove events from active consideration
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void EditDurationFilter(object sender, RoutedEventArgs e)
        {
            Window_NumberTwo getTimeDur = new Window_NumberTwo(this, "Duration Filter Settings",
                            "Hours", "Minutes", false, false, _analyser.DuratFilter.TotalHours, _analyser.DuratFilter.Minutes);

            if (getTimeDur.ShowDialog().Value)
            {
                _analyser.DuratFilter = new TimeSpan((int)getTimeDur.NumericValue1, (int)getTimeDur.NumericValue2, 0);
            }
        }

        #endregion

        #region Clear Data Methods

        /// <summary>
        /// Calls other clear events, leaves nothing
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ClearAllData(object sender, RoutedEventArgs e)
        {
            ClearGeoData(sender, e);
            ClearMeteoData(sender, e);
            ClearScadaData(sender, e);

            ClearEvents(sender, e);

            _dataExportStart = new DateTime();
            _dataExportEndTm = new DateTime();
            _loadedFiles.Clear();
        }

        /// <summary>
        /// Clears all processed events
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ClearEvents(object sender, RoutedEventArgs e)
        {
            AllWtrView = new ObservableCollection<EventData>();
            LoSpdViews = new ObservableCollection<EventData>();
            HiSpdViews = new ObservableCollection<EventData>();

            AllPowView = new ObservableCollection<EventData>();
            NoPowViews = new ObservableCollection<EventData>();
            RtdPowView = new ObservableCollection<EventData>();

            LView_PowrNone.ItemsSource = null;
            LView_PowrNone.IsEnabled = false;

            LView_PowrRted.ItemsSource = null;
            LView_PowrRted.IsEnabled = false;

            LView_WSpdEvLo.ItemsSource = null;
            LView_WSpdEvLo.IsEnabled = false;

            LView_WSpdEvHi.ItemsSource = null;
            LView_WSpdEvHi.IsEnabled = false;

            CreateSummaries();

            eventsMatchedAcrossTypes = false;
            eventsAreProcessed = false;
        }

        /// <summary>
        /// Clears all loaded geographic data
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ClearGeoData(object sender, RoutedEventArgs e)
        {
            _geoFile = null; _geoLoaded = false; positionsAddedToData = false;

            _analyser.AddStructureLocations(_geoFile, _meteoFile, _scadaFile, _scadaLoaded, _meteoLoaded, _geoLoaded);
            CreateSummaries();
        }

        /// <summary>
        /// Clears all loaded meteorologic data
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ClearMeteoData(object sender, RoutedEventArgs e)
        {
            for (int i = _analyser.AssetList.Count - 1; i >= 0; i--)
            {
                if (_analyser.AssetList[i].Type == BaseStructure.Types.METMAST)
                {
                    loadedAsset.Remove(_analyser.AssetList[i].UnitID);
                    _analyser.AssetList.RemoveAt(i);
                }
            }

            _loadedFiles.Clear();
            _meteoFile = new MeteoData(); _meteoLoaded = false;

            _analyser.AddStructureLocations(_geoFile, _meteoFile, _scadaFile, _scadaLoaded, _meteoLoaded, _geoLoaded);
            CreateSummaries();
            PopulateOverview();
        }

        /// <summary>
        /// Clears all loaded SCADA data
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ClearScadaData(object sender, RoutedEventArgs e)
        {
            for (int i = _analyser.AssetList.Count - 1; i >= 0; i--)
            {
                if (_analyser.AssetList[i].Type == BaseStructure.Types.TURBINE)
                {
                    loadedAsset.Remove(_analyser.AssetList[i].UnitID);
                    _analyser.AssetList.RemoveAt(i);
                }
            }

            _loadedFiles.Clear();
            _scadaFile = new ScadaData(); _scadaLoaded = false; _averagesComputed = false;

            _analyser.AddStructureLocations(_geoFile, _meteoFile, _scadaFile, _scadaLoaded, _meteoLoaded, _geoLoaded);
            CreateSummaries();
            PopulateOverview();
        }

        #endregion

        #region Event Details View Manipulation

        private void ChangeListViewDataset(object sender, RoutedEventArgs e)
        {
            // checks whether the dataset choice button has been activated and displays the respective
            // -- either all historic data to the end of the event, or only partial data for the duration
            // of the event

            if (CBox_DataSetChoice.IsChecked.Value)
            {
                LView_EventExplorer_Main.ItemsSource = HistEventDataVw;
                LView_EventExplorer_Gearbox.ItemsSource = HistEventDataVw;
                LView_EventExplorer_Generator.ItemsSource = HistEventDataVw;
                LView_EventExplorer_MainBear.ItemsSource = HistEventDataVw;
            }
            else
            {
                LView_EventExplorer_Main.ItemsSource = ThisEventDataVw;
                LView_EventExplorer_Gearbox.ItemsSource = ThisEventDataVw;
                LView_EventExplorer_Generator.ItemsSource = ThisEventDataVw;
                LView_EventExplorer_MainBear.ItemsSource = ThisEventDataVw;
            }
        }

        private void Chart_OnData_Click(object sender, ChartPoint point)
        {
            // bring up a messagebox to show the user the time and value of the datapoint they
            // clicked on

            string time = Int32.TryParse(point.X.ToString(), out int theta) ? Labels[(int)point.X] : "N/A";

            LBL_ClickInfo.Content = "Info: " + point.Y + "° C at " + time;
        }

        private void ChartShowSeries(string input, bool _previousWeekIncluded)
        {
            try
            {
                // create an empty container for graphing to choose which source for the data is required
                ObservableCollection<ScadaData.ScadaSample> _graphingData = new ObservableCollection<ScadaData.ScadaSample>();

                if (_previousWeekIncluded) { _graphingData = WeekEventDataVw; } else { _graphingData = ThisEventDataVw; }

                LChart_Basic.Series.Clear();

                List<double> list1 = new List<double>();
                List<double> list2 = new List<double>();
                List<string> times = new List<string>();

                LineSeries priGraph = new LineSeries();
                LineSeries secGraph = new LineSeries();

                // make automatic counter to suit data length and yet provide utilisable speed
                int counter = 1;
                TimeSpan length = _graphingData[_graphingData.Count - 1].TimeStamp - _graphingData[0].TimeStamp;

                if (length < new TimeSpan(3,0,0)) { counter = 1; }
                else if (length < new TimeSpan(12, 0, 0)) { counter = 3; }
                else if (length < new TimeSpan(24 * 7, 0, 0)) { counter = 6; }
                else if (length < new TimeSpan(24 * 30, 0, 0)) { counter = 12; }
                else if (length < new TimeSpan(24 * 360, 0, 0)) { counter = 60; }
                else if (length >= new TimeSpan(24 * 360, 0, 0)) { counter = 120; }

                for (int i = 0; i < _graphingData.Count; i += counter)
                {
                    double var1 = double.NaN;
                    double var2 = double.NaN;

                    if (input == _eventDetailsSelection[1])
                    {
                        priGraph.Title = _gbox.HsGen.Description;
                        var1 = Math.Round(_graphingData[i].Gearbox.HsGen.Mean, 1);

                        if (_averagesComputed)
                        {
                            var2 = Math.Round(_graphingData[i].Gearbox.HsRot.Mean, 1);
                        }
                    }
                    else if (input == _eventDetailsSelection[2])
                    {
                        priGraph.Title = _genr.BearingG.Description;
                        var1 = Math.Round(_graphingData[i].Genny.BearingG.Mean, 1);

                        if (_averagesComputed)
                        {
                            var2 = Math.Round(_graphingData[i].Genny.BearingR.Mean, 1);
                        }
                    }
                    else if (input == _eventDetailsSelection[3])
                    {
                        priGraph.Title = _mbrg.Main.Description;
                        var1 = Math.Round(_graphingData[i].MainBear.Main.Mean, 1);

                        if (_averagesComputed)
                        {
                            var2 = Math.Round(_graphingData[i].MainBear.Hs.Mean, 1);
                        }
                    }

                    times.Add(_graphingData[i].TimeStamp.ToString("dd/MMM/yy HH:mm"));
                    list1.Add(!double.IsNaN(var1) ? var1 : double.NaN);

                    if (_averagesComputed) { list2.Add(!double.IsNaN(var2) ? var2 : double.NaN); }
                }

                Labels = times.ToArray();

                priGraph.Values = new ChartValues<double>(list1);
                priGraph.Fill = Brushes.Transparent;
                LChart_Basic.Series.Add(priGraph);

                if (_averagesComputed)
                {
                    secGraph.Title = "Fleetwise Average";
                    secGraph.Values = new ChartValues<double>(list2);
                    secGraph.Fill = Brushes.Transparent;
                    LChart_Basic.Series.Add(secGraph);
                }

                LChart_XAxis.Labels = Labels;
            }
            catch { }
        }

        private void DisplayCorrectEventDetails(bool _graphIncludingPreviousWeek)
        {
            if (Comb_DisplayEvDetails.SelectedIndex != -1)
            {
                LChart_Basic.Visibility = Visibility.Visible;

                // this here chooses what to display in the events details view in order to 
                // show the right information based on what is chosen in the combobox
                if ((string)Comb_DisplayEvDetails.SelectedItem == _eventDetailsSelection[0])
                {
                    LView_EventExplorer_Main.Visibility = Visibility.Visible;
                    LView_EventExplorer_Gearbox.Visibility = Visibility.Collapsed;
                    LView_EventExplorer_Generator.Visibility = Visibility.Collapsed;
                    LView_EventExplorer_MainBear.Visibility = Visibility.Collapsed;
                    LChart_Basic.Visibility = Visibility.Collapsed;
                }
                else if ((string)Comb_DisplayEvDetails.SelectedItem == _gbox.Name)
                {
                    LView_EventExplorer_Main.Visibility = Visibility.Collapsed;
                    LView_EventExplorer_Gearbox.Visibility = Visibility.Visible;
                    LView_EventExplorer_Generator.Visibility = Visibility.Collapsed;
                    LView_EventExplorer_MainBear.Visibility = Visibility.Collapsed;
                    ChartShowSeries(_eventDetailsSelection[1], _graphIncludingPreviousWeek);
                }
                else if ((string)Comb_DisplayEvDetails.SelectedItem == _genr.Name)
                {
                    LView_EventExplorer_Main.Visibility = Visibility.Collapsed;
                    LView_EventExplorer_Gearbox.Visibility = Visibility.Collapsed;
                    LView_EventExplorer_Generator.Visibility = Visibility.Visible;
                    LView_EventExplorer_MainBear.Visibility = Visibility.Collapsed;
                    ChartShowSeries(_eventDetailsSelection[2], _graphIncludingPreviousWeek);
                }
                else if ((string)Comb_DisplayEvDetails.SelectedItem == _mbrg.Name)
                {
                    LView_EventExplorer_Main.Visibility = Visibility.Collapsed;
                    LView_EventExplorer_Gearbox.Visibility = Visibility.Collapsed;
                    LView_EventExplorer_Generator.Visibility = Visibility.Collapsed;
                    LView_EventExplorer_MainBear.Visibility = Visibility.Visible;
                    ChartShowSeries(_eventDetailsSelection[3], _graphIncludingPreviousWeek);
                }
            }
        }

        private void PickDetailedBeginTime(object sender, RoutedEventArgs e)
        {
            Window_CalendarChooser startCal = new Window_CalendarChooser(this, "Choose event start date", _eventExplrStart);

            if (startCal.ShowDialog().Value)
            {
                DateTime tempDate = Common.StringToDateTime(startCal.TextBox_Calendar.Text, Common.DateFormat.DMY);

                // this is currently a suboptimal method as it should also take into account
                // what the specific start and end times are for the specific turbine
                if (tempDate < _eventExplrEndTm)
                {
                    if (tempDate > _dataExportStart)
                    {
                        _eventExplrStart = tempDate;
                    }
                    else
                    {
                        _eventExplrStart = _dataExportStart;
                    }
                }
                else
                {
                    _eventExplrStart = _eventExplrEndTm.AddHours(-6);
                }

                LBL_DetailedStartTime.Content = _eventExplrStart.ToString();
            }
        }

        private void PickDetailedEndTime(object sender, RoutedEventArgs e)
        {
            Window_CalendarChooser endCal = new Window_CalendarChooser(this, "Choose event end date", _eventExplrEndTm);
            
            if (endCal.ShowDialog().Value)
            {
                DateTime tempDate = Common.StringToDateTime(endCal.TextBox_Calendar.Text, Common.DateFormat.DMY);

                // this is currently a suboptimal method as it should also take into account
                // what the specific start and end times are for the specific turbine
                if (tempDate > _eventExplrStart)
                {
                    if (tempDate < _dataExportEndTm)
                    {
                        _eventExplrEndTm = tempDate;
                    }
                    else
                    {
                        _eventExplrEndTm = _dataExportEndTm;
                    }
                }
                else
                {
                    _eventExplrEndTm = _eventExplrStart.AddHours(6);                
                }

                LBL_DetailedEndTime.Content = _eventExplrEndTm.ToString();
            }
        }
        
        private void ExploreDetailedEvent(object sender, RoutedEventArgs e)
        {
            // first set the time extent of the explored range
            // and then do everything data
            ExploreEvent_MenuItem_Click(sender, e);
        }

        #endregion

        #region Event Summary View Manipulation

        private void CreateSummaryComboInfo()
        {
            _eventSummarySelection.Add("No Power Production Events");
            _eventSummarySelection.Add("High Power Production Events");
            _eventSummarySelection.Add("Low Wind Speed Events");
            _eventSummarySelection.Add("High Wind Speed Events");

            Comb_SummaryChoose.ItemsSource = _eventSummarySelection;
            Comb_SummaryChoose.SelectedIndex = 0;
        }

        /// <summary>
        /// Controls the event summary view selection range results
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Comb_DisplaySummary_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Comb_SummaryChoose.SelectedIndex != -1)
            {
                if ((string)Comb_SummaryChoose.SelectedItem == _eventSummarySelection[0])
                {
                    LView_EventsSumPwrNone.Visibility = Visibility.Visible;
                    LView_EventsSumPwrHigh.Visibility = Visibility.Collapsed;
                    LView_EventsSumWndLows.Visibility = Visibility.Collapsed;
                    LView_EventsSumWndHigh.Visibility = Visibility.Collapsed;
                }
                else if ((string)Comb_SummaryChoose.SelectedItem == _eventSummarySelection[1])
                {
                    LView_EventsSumPwrNone.Visibility = Visibility.Collapsed;
                    LView_EventsSumPwrHigh.Visibility = Visibility.Visible;
                    LView_EventsSumWndLows.Visibility = Visibility.Collapsed;
                    LView_EventsSumWndHigh.Visibility = Visibility.Collapsed;
                }
                else if ((string)Comb_SummaryChoose.SelectedItem == _eventSummarySelection[2])
                {
                    LView_EventsSumPwrNone.Visibility = Visibility.Collapsed;
                    LView_EventsSumPwrHigh.Visibility = Visibility.Collapsed;
                    LView_EventsSumWndLows.Visibility = Visibility.Visible;
                    LView_EventsSumWndHigh.Visibility = Visibility.Collapsed;
                }
                else if ((string)Comb_SummaryChoose.SelectedItem == _eventSummarySelection[3])
                {
                    LView_EventsSumPwrNone.Visibility = Visibility.Collapsed;
                    LView_EventsSumPwrHigh.Visibility = Visibility.Collapsed;
                    LView_EventsSumWndLows.Visibility = Visibility.Collapsed;
                    LView_EventsSumWndHigh.Visibility = Visibility.Visible;
                }
            }
        }

        /// <summary>
        /// Checks what equipment is chosen from the menu and changes information displayed based on that.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Comb_DisplayEvDetails_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // only go into this event if the below conditions are true; probably will disable the combobox as well though
            DisplayCorrectEventDetails(_usingPreviousWeekForGraphing);
        }

        private void CreateEventDetailsView()
        {
            _eventDetailsSelection.Add("Main Overview");
            _eventDetailsSelection.Add(_gbox.Name);
            _eventDetailsSelection.Add(_genr.Name);
            _eventDetailsSelection.Add(_mbrg.Name);

            Comb_DisplayEvDetails.ItemsSource = _eventDetailsSelection;
            Comb_DisplayEvDetails.SelectedIndex = 0;
        }

        #endregion

        #region Main Functions

        /// <summary>
        /// The function that is called by the button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void AddDaytimesToEvents(object sender, RoutedEventArgs e)
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            var progress = new Progress<int>(value =>
            {
                UpdateProgress(value);
            });

            try
            {
                // these conditions check that the function is not used in a situation
                // where it would cause a nullreference exception or some other bad result

                if (AssetsView == null || AssetsView.Count == 0)
                {
                    await this.ShowMessageAsync("Warning!",
                        "No structures have been loaded. Load structures before using this function.");

                    throw new CancelLoadingException();
                }
                else if ((Tab_NoPower.IsSelected && NoPowViews.Count == 0) ||
                        (Tab_RtPower.IsSelected && RtdPowView.Count == 0))
                {
                    await this.ShowMessageAsync("Warning!",
                        "There are no respective power production events to process.");

                    throw new CancelLoadingException();
                }
                else if (_geoFile == null || !positionsAddedToData)
                {
                    await this.ShowMessageAsync("Warning!",
                        "Geographic details have not been loaded, or the data has not been associated with the loaded structures.");

                    throw new CancelLoadingException();
                }

                // if the above conditions are not fulfilled, the process can continue

                ProgressBarVisible();

                await Task.Run(() => _analyser.AddDaytimesToEvents(progress));

                ProgressBarInvisible();
                RefreshEvents();
            }
            catch (CancelLoadingException) { }
            catch (Exception ex)
            {
                await this.ShowMessageAsync("Warning!", ex.GetType().Name + ": " + ex.Message);
            }
        }

        /// <summary>
        /// Method called by the UI that references the AddStructureLocations bool
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void AddStructureLocationsAsync(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_geoFile == null || _geoFile.GeoInfo.Count == 0)
                {
                    await this.ShowMessageAsync("Warning!",
                        "No geographic data is loaded.");

                    throw new CancelLoadingException();
                }
                else if (_meteoFile.MetMasts.Count == 0 && _scadaFile.WindFarm.Count == 0)
                {
                    await this.ShowMessageAsync("Warning!",
                        "No meteorologic or SCADA data is loaded.");

                    throw new CancelLoadingException();
                }

                positionsAddedToData = _analyser.AddStructureLocations(_geoFile, _meteoFile, _scadaFile, _scadaLoaded, _meteoLoaded, _geoLoaded);
            }
            catch (CancelLoadingException) { }
            catch (Exception ex)
            {
                await this.ShowMessageAsync("Warning!", ex.GetType().Name + ": " + ex.Message);
            }
        }

        /// <summary>
        /// Exports meteorology data
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ExportMeteoDataAsync(object sender, RoutedEventArgs e)
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            var progress = new Progress<int>(value =>
            {
                UpdateProgress(value);
            });

            try
            {
                if (_meteoFile.MetMasts.Count != 0)
                {
                    SaveFileDialog saveFileDialog = new SaveFileDialog();
                    // set a default file name
                    saveFileDialog.FileName = ".csv";
                    // set filters
                    saveFileDialog.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*";

                    if (saveFileDialog.ShowDialog().Value)
                    {
                        ProgressBarVisible();

                        if (CBox_DateRangeExport.IsChecked)
                        {
                            Window_CalendarChooser startCal = new Window_CalendarChooser(this, "Choose export start date", _dataExportStart);
                            Window_CalendarChooser endCal = new Window_CalendarChooser(this, "Choose export end date", _dataExportEndTm);

                            if (startCal.ShowDialog().Value)
                            {
                                _dataExportStart = Common.StringToDateTime(startCal.TextBox_Calendar.Text, Common.DateFormat.DMY);
                            }

                            if (endCal.ShowDialog().Value)
                            {
                                _dataExportEndTm = Common.StringToDateTime(endCal.TextBox_Calendar.Text, Common.DateFormat.DMY);
                            }
                        }

                        await Task.Run(() => _meteoFile.ExportFiles(progress, saveFileDialog.FileName, _dataExportStart, _dataExportEndTm));

                        ProgressBarInvisible();
                    }
                }
                else
                {
                    await this.ShowMessageAsync("Warning!", 
                        "No data of this type has been loaded yet. Please load data before trying to export.");
                }
            }
            catch (Exception ex)
            {
                await this.ShowMessageAsync("Warning!", ex.GetType().Name + ": " + ex.Message);
            }
        }

        /// <summary>
        /// Exports SCADA data
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ExportScadaDataAsync(object sender, RoutedEventArgs e)
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            var progress = new Progress<int>(value =>
            {
                UpdateProgress(value);
            });

            try
            {
                if (_scadaFile.WindFarm.Count != 0)
                {
                    SaveFileDialog saveFileDialog = new SaveFileDialog();
                    // set a default file name
                    saveFileDialog.FileName = ".csv";
                    // set filters
                    saveFileDialog.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*";

                    if (saveFileDialog.ShowDialog().Value)
                    {
                        ProgressBarVisible();

                        if (CBox_DateRangeExport.IsChecked)
                        {
                            Window_CalendarChooser startCal = new Window_CalendarChooser(this, "Choose export start date", _dataExportStart);
                            Window_CalendarChooser endCal = new Window_CalendarChooser(this, "Choose export end date", _dataExportEndTm);

                            if (startCal.ShowDialog().Value)
                            {
                                _dataExportStart = Common.StringToDateTime(startCal.TextBox_Calendar.Text, Common.DateFormat.DMY);
                            }

                            if (endCal.ShowDialog().Value)
                            {
                                _dataExportEndTm = Common.StringToDateTime(endCal.TextBox_Calendar.Text, Common.DateFormat.DMY);
                            }
                        }

                        await Task.Run(() => _scadaFile.ExportFiles(progress, saveFileDialog.FileName,
                            exportPowMaxm, exportPowMinm, exportPowMean, exportPowStdv,
                            exportAmbMaxm, exportAmbMinm, exportAmbMean, exportAmbStdv,
                            exportWSpMaxm, exportWSpMinm, exportWSpMean, exportWSpStdv,
                            exportGBxMaxm, exportGBxMinm, exportGBxMean, exportGBxStdv,
                            exportGenMaxm, exportGenMinm, exportGenMean, exportGenStdv,
                            exportMBrMaxm, exportMBrMinm, exportMBrMean, exportMBrStdv,
                            exportNacMaxm, exportNacMinm, exportNacMean, exportNacStdv,
                            _dataExportStart, _dataExportEndTm));

                        ProgressBarInvisible();
                    }
                }
                else
                {
                    await this.ShowMessageAsync("Warning!", 
                        "No data of this type has been loaded yet. Please load data before trying to export.");
                }
            }
            catch (Exception ex)
            {
                await this.ShowMessageAsync("Warning!", ex.GetType().Name + ": " + ex.Message);
            }
        }

        /// <summary>
        /// Method called by the UI, references the real method
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void FindEventsAsync(object sender, RoutedEventArgs e)
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            var progress = new Progress<int>(value =>
            {
                UpdateProgress(value);
            });

            try
            {
                if (loadedAsset == null || loadedAsset.Count == 0)
                {
                    await this.ShowMessageAsync("Warning!",
                        "No turbines or metmasts are loaded. Load data before trying to process it.");

                    throw new CancelLoadingException();
                }

                ProgressBarVisible();
                ClearEvents(sender, e);

                await Task.Run(() => _analyser.FindEvents(_scadaFile, _meteoFile, progress));

                ProgressBarInvisible();
                RefreshEvents();

                eventsAreProcessed = true;
            }
            catch (CancelLoadingException) { }
            catch (Exception ex)
            {
                await this.ShowMessageAsync("Warning!", ex.GetType().Name + ": " + ex.Message);
            }
        }

        /// <summary>
        /// Calculates distances between various loaded assets
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void GetDistancesBetweenStructuresAsync(object sender, RoutedEventArgs e)
        {
            // this method to invoke the analysis class and do some work there

            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            var progress = new Progress<int>(value =>
            {
                UpdateProgress(value);
            });

            try
            {
                if (_geoFile == null || !positionsAddedToData)
                {
                    await this.ShowMessageAsync("Warning!",
                        "Geographic details have not been loaded, or the data has not been associated with the loaded structures.");

                    throw new CancelLoadingException();
                }
                else if (AssetsView.Count < 0)
                {
                    await this.ShowMessageAsync("Warning!",
                        "No turbines or metmasts are loaded. Load data before trying to analyse it.");

                    throw new CancelLoadingException();
                }

                await Task.Run(() => _analyser.GetDistances(_analyser.AssetList));
            }
            catch (CancelLoadingException) { }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                await this.ShowMessageAsync("Warning!", ex.GetType().Name + ": " + ex.Message);
            }
        }

        /// <summary>
        /// Method that loads the geographic data
        /// </summary>
        /// <param name="filenames"></param>
        /// <param name="progress"></param>
        private void LoadGeoData(string[] filenames, IProgress<int> progress)
        {
            try
            {
                for (int i = 0; i < filenames.Length; i++)
                {
                    if (!_loadedFiles.Contains(filenames[i]))
                    {
                        _geoFile = new GeoData(filenames[i], progress);
                        _loadedFiles.Add(filenames[i]);
                    }
                }
            }
            catch { throw; }
        }

        /// <summary>
        /// UI calls this, references the non async method
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void LoadGeoDataAsync(object sender, RoutedEventArgs e)
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            var progress = new Progress<int>(value =>
            {
                UpdateProgress(value);
            });

            try
            {
                OpenFileDialog openFileDialog = new OpenFileDialog();
                openFileDialog.Filter = "Location files (*.csv)|*.csv|All files (*.*)|*.*";
                openFileDialog.Multiselect = false;

                if (openFileDialog.ShowDialog().Value)
                {
                    ProgressBarVisible();

                    await Task.Run(() => LoadGeoData(openFileDialog.FileNames, progress));

                    ProgressBarInvisible();

                    _geoLoaded = true;
                }
            }
            catch (OperationCanceledException) { }
            catch (LoadingCancelledException)
            {
                await this.ShowMessageAsync("Warning!", "Loading cancelled by user.");
            }            
            catch (WrongFileTypeException)
            {
                await this.ShowMessageAsync("Warning!", "This file cannot be loaded since it is of an incompatible file type for this function.");
            }
            catch (Exception ex)
            {
                await this.ShowMessageAsync("Warning!", ex.GetType().Name + ": " + ex.Message);
            }
        }

        /// <summary>
        /// Method loads the meteorology data, checks for existing files and appends datapoints
        /// </summary>
        /// <param name="existingData"></param>
        /// <param name="filenames"></param>
        /// <param name="isLoaded"></param>
        /// <param name="progress"></param>
        private async Task LoadMeteoData(MeteoData existingData, string[] filenames, bool isLoaded, IProgress<int> progress)
        {
            try
            {
                MeteoData analysis = new MeteoData(existingData);

                await Task.Run(() =>
                {
                    analysis.AppendFiles(filenames, existingData.FileName, _dateFormat, progress);
                    _loadedFiles.AddRange(filenames);
                });

                _meteoFile = analysis;
                _meteoLoaded = true;
            }
            catch { throw; }
        }

        /// <summary>
        /// UI references this, this calls the above sync method
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void LoadMeteoDataAsync(object sender, RoutedEventArgs e)
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            var progress = new Progress<int>(value =>
            {
                UpdateProgress(value);
            });

            try
            {
                OpenFileDialog openFileDialog = new OpenFileDialog();
                openFileDialog.Filter = "Meteorology files (*.csv)|*.csv|All files (*.*)|*.*";
                openFileDialog.Multiselect = true;

                if (openFileDialog.ShowDialog().Value)
                {
                    ProgressBarVisible();

                    await Task.Run(() => LoadMeteoData(_meteoFile, openFileDialog.FileNames, _meteoLoaded,
                        progress));

                    ProgressBarInvisible();

                    PopulateOverview();
                }
            }
            catch (OperationCanceledException) { }
            catch (LoadingCancelledException)
            {
                await this.ShowMessageAsync("Warning!", "Loading cancelled by user.");
            }
            catch (WrongFileTypeException)
            {
                await this.ShowMessageAsync("Warning!", 
                    "This file cannot be loaded since it is of an incompatible file type for this function.");
            }
            catch (Exception ex)
            {
                await this.ShowMessageAsync("Warning!", ex.GetType().Name + ": " + ex.Message);
            }
        }

        /// <summary>
        /// Method loads the SCADA data, checks for existing files and appends datapoints
        /// </summary>
        /// <param name="existingData"></param>
        /// <param name="filenames"></param>
        /// <param name="isLoaded"></param>
        /// <param name="progress"></param>
        /// <returns></returns>
        private async Task LoadScadaData(ScadaData existingData, string[] filenames, int _singleTurbineLoading, TimeSpan _separation, 
            IProgress<int> progress)
        {
            try
            {
                ScadaData analysis = new ScadaData(existingData);

                await Task.Run(() =>
                {
                    analysis.AppendFiles(filenames, existingData.FileName, _dateFormat, _singleTurbineLoading, _separation, progress);
                    _loadedFiles.AddRange(filenames);
                });

                _scadaFile = analysis;
                _scadaLoaded = true;
            }
            catch { throw; }
        }

        /// <summary>
        /// UI calls this method, references the async Task
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void LoadScadaDataAsync(object sender, RoutedEventArgs e)
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            var progress = new Progress<int>(value =>
            {
                UpdateProgress(value);
            });

            try
            {
                OpenFileDialog openFileDialog = new OpenFileDialog();
                openFileDialog.Filter = "SCADA files (*.csv)|*.csv|All files (*.*)|*.*";
                openFileDialog.Multiselect = true;

                if (openFileDialog.ShowDialog().Value)
                {
                    int _singleTurbineLoading = -1;

                    if (CBox_SingleTurbineLoading.IsChecked)
                    {
                        // option to just load one turbine from the entire file if the code goes into here
                        Window_NumberOne _whichTurbine = new Window_NumberOne(this, "Which Turbine Should Be Loaded?", "Turbine ID");

                        if (_whichTurbine.ShowDialog().Value) { _singleTurbineLoading = (int)_whichTurbine.NumericValue1; }
                    }

                    ProgressBarVisible();

                    await Task.Run(() =>
                        LoadScadaData(_scadaFile, openFileDialog.FileNames, _singleTurbineLoading, _inputSeparation, progress), token);

                    ProgressBarInvisible();
                    PopulateOverview();
                }
            }
            catch (OperationCanceledException) { }
            catch (LoadingCancelledException)
            {
                await this.ShowMessageAsync("Warning!", "Loading cancelled by user.");
            }
            catch (WrongDateTimeException wdtE)
            {
                await this.ShowMessageAsync("Warning!",
                    "Try changing the loaded date-time format for the file(s) to load properly. Problem caused at " + 
                    wdtE.DateInfo[0] + " " + wdtE.DateInfo[1] + ".");
            }
            catch (WrongFileTypeException)
            {
                await this.ShowMessageAsync("Warning!", 
                    "This file cannot be loaded since it is of an incompatible file type for this function.");
            }
            catch (Exception ex)
            {
                await this.ShowMessageAsync("Warning!", ex.GetType().Name + ": " + ex.Message);
            }
        }

        /// <summary>
        /// UI calls this, goes to above
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void MatchEventsAsync(object sender, RoutedEventArgs e)
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            var progress = new Progress<int>(value =>
            {
                UpdateProgress(value);
            });

            try
            {
                if (loadedAsset == null || loadedAsset.Count == 0)
                {
                    await this.ShowMessageAsync("Warning!",
                        "No turbines or metmasts are loaded. Load data before trying to analyse it.");

                    throw new CancelLoadingException();
                }
                else if (!eventsAreProcessed)
                {
                    await this.ShowMessageAsync("Warning!",
                        "Events have not been processed.");

                    throw new CancelLoadingException();
                }

                ProgressBarVisible();

                LView_PowrNone.ItemsSource = null;
                LView_PowrRted.ItemsSource = null;

                await Task.Run(() => _analyser.AssociateEvents(progress));

                ProgressBarInvisible();
                RefreshEvents();

                eventsMatchedAcrossTypes = true;
            }
            catch (CancelLoadingException) { }
            catch (Exception ex)
            {
                await this.ShowMessageAsync("Warning!", ex.GetType().Name + ": " + ex.Message);
            }
        }

        /// <summary>
        /// This function populates the DMean field for several variables, allowing fleetwide averages
        /// to be investigated with respect to every timestep.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void PopulateFleetAverages(object sender, RoutedEventArgs e)
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            var progress = new Progress<int>(value =>
            {
                UpdateProgress(value);
            });

            try
            {
                if (loadedAsset == null || loadedAsset.Count == 0)
                {
                    await this.ShowMessageAsync("Warning!",
                        "No turbines or metmasts are loaded. Load data before trying to process it.");

                    throw new CancelLoadingException();
                }

                ProgressBarVisible();

                await Task.Run(() => _scadaFile = _analyser.FleetStats(_scadaFile, progress));

                ProgressBarInvisible();
            }
            catch (CancelLoadingException) { }
            catch (Exception ex)
            {
                await this.ShowMessageAsync("Warning!", ex.GetType().Name + ": " + ex.Message);
            }
        }

        /// <summary>
        /// UI calls this, references above
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ProcessDurationFilterAsync(object sender, RoutedEventArgs e)
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            var progress = new Progress<int>(value =>
            {
                UpdateProgress(value);
            });

            try
            {
                if (_analyser.DuratFilter.TotalSeconds == 0)
                {
                    await this.ShowMessageAsync("Warning!",
                        "The duration filter is set to 0 seconds. Please change the length of this filter.");

                    throw new CancelLoadingException();
                }
                else if (NoPowViews.Count == 0 && RtdPowView.Count == 0)
                {
                    await this.ShowMessageAsync("Warning!",
                        "There are power production related events to filter.");

                    throw new CancelLoadingException();
                }

                ProgressBarVisible();

                await Task.Run(() => _analyser.RemoveByDuration(progress));

                ProgressBarInvisible();
                RefreshEvents();
            }
            catch (CancelLoadingException) { }
            catch (Exception ex)
            {
                await this.ShowMessageAsync("Warning!", ex.GetType().Name + ": " + ex.Message);
            }
        }

        /// <summary>
        /// UI calls this
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void RemoveMatchedEventsAsync(object sender, RoutedEventArgs e)
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            var progress = new Progress<int>(value =>
            {
                UpdateProgress(value);
            });

            try
            {
                if (loadedAsset == null || loadedAsset.Count == 0)
                {
                    await this.ShowMessageAsync("Warning!",
                        "No turbines or metmasts are loaded. Load data before trying to analyse it.");

                    throw new CancelLoadingException();
                }
                else if (!eventsAreProcessed)
                {
                    await this.ShowMessageAsync("Warning!",
                        "Events have not been processed.");

                    throw new CancelLoadingException();
                }
                else if (!eventsMatchedAcrossTypes)
                {
                    await this.ShowMessageAsync("Warning!",
                        "Events have not been matched across types.");

                    throw new CancelLoadingException();
                }

                ProgressBarVisible();

                LView_PowrNone.ItemsSource = null;
                LView_PowrRted.ItemsSource = null;

                await Task.Run(() => _analyser.RemoveMatchedEvents(progress));

                ProgressBarInvisible();
                RefreshEvents();
            }
            catch (CancelLoadingException) { }
            catch (Exception ex)
            {
                await this.ShowMessageAsync("Warning!", ex.GetType().Name + ": " + ex.Message);
            }
        }

        /// <summary>
        /// UI calls this, references the OC return method
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void RemoveProcessedDaytimesAsync(object sender, RoutedEventArgs e)
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            var progress = new Progress<int>(value =>
            {
                UpdateProgress(value);
            });

            try
            {
                // these conditions check that the function is not used in a situation
                // where it would cause a nullreference exception or some other bad result

                if ((Tab_NoPower.IsSelected && NoPowViews.Count == 0) ||
                        (Tab_RtPower.IsSelected && RtdPowView.Count == 0))
                {
                    await this.ShowMessageAsync("Warning!",
                        "There are no respective power production events to process.");

                    throw new CancelLoadingException();
                }
                else if ((Tab_NoPower.IsSelected && (NoPowViews.Count > 0 && NoPowViews[0].DayTime == EventData.TimeOfEvent.UNKNOWN)) ||
                    (Tab_RtPower.IsSelected && (RtdPowView.Count > 0 && RtdPowView[0].DayTime == EventData.TimeOfEvent.UNKNOWN)))
                {
                    await this.ShowMessageAsync("Warning!",
                        "The events cannot be removed before the day-time associations have been created.");

                    throw new CancelLoadingException();
                }

                ProgressBarVisible();

                await Task.Run(() => _analyser.RemoveProcessedDaytimes(progress));

                ProgressBarInvisible();
                RefreshEvents();
            }
            catch (CancelLoadingException) { }
            catch (Exception ex)
            {
                await this.ShowMessageAsync("Warning!", ex.GetType().Name + ": " + ex.Message);
            }
        }

        /// <summary>
        /// Resets power production events to their original state
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ResetPowerProdEvents(object sender, RoutedEventArgs e)
        {
            _analyser.DuratFilter = new TimeSpan(0, 10, 0);

            _analyser.ResetEventList();
            PopulateOverview();
            RefreshEvents();
        }

        /// <summary>
        /// Brings up a dialog window with various analysis settings
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SetAnalysisSettings(object sender, RoutedEventArgs e)
        {
            // new code below with the proper analysis settings window, etc
            // more options for expanding the code if necessary
            Window_AnalysisSettings anaSets = new Window_AnalysisSettings(this, _analyser,
                mnt_Night, mnt_AstDw, mnt_NauDw, mnt_CivDw, mnt_Daytm, mnt_CivDs, mnt_NauDs, mnt_AstDs);

            if (anaSets.ShowDialog().Value)
            {
                _analyser.CutIn = anaSets.SpdIns;
                _analyser.CutOut = anaSets.SpdOut;
                _analyser.RatedPwr = anaSets.RtdPwr;

                mnt_Night = anaSets.Mnt_Night;
                mnt_AstDw = anaSets.Mnt_AstDw;
                mnt_NauDw = anaSets.Mnt_NauDw;
                mnt_CivDw = anaSets.Mnt_CivDw;
                mnt_Daytm = anaSets.Mnt_Daytm;
                mnt_CivDs = anaSets.Mnt_CivDs;
                mnt_NauDs = anaSets.Mnt_NauDs;
                mnt_AstDs = anaSets.Mnt_AstDs;

                _analyser.WorkHoursMorning = anaSets.WorkHoursMorning;
                _analyser.WorkHoursEvening = anaSets.WorkHoursEvening;

                RtdPowView.Clear();
                CreateSummaries();
            }
        }

        /// <summary>
        /// Brings up a dialog window with export settings
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SetExportSettings(object sender, RoutedEventArgs e)
        {
            Window_ExportControl exportOptions = new Window_ExportControl(this);

            exportOptions.ExportAmbMaxm = exportAmbMaxm;
            exportOptions.ExportAmbMinm = exportAmbMinm;
            exportOptions.ExportAmbMean = exportAmbMean;
            exportOptions.ExportAmbStdv = exportAmbStdv;

            exportOptions.ExportPowMaxm = exportPowMaxm;
            exportOptions.ExportPowMinm = exportPowMinm;
            exportOptions.ExportPowMean = exportPowMean;
            exportOptions.ExportPowStdv = exportPowStdv;

            exportOptions.ExportWSpMaxm = exportWSpMaxm;
            exportOptions.ExportWSpMinm = exportWSpMinm;
            exportOptions.ExportWSpMean = exportWSpMean;
            exportOptions.ExportWSpStdv = exportWSpStdv;

            exportOptions.ExportGBxMaxm = exportGBxMaxm;
            exportOptions.ExportGBxMinm = exportGBxMinm;
            exportOptions.ExportGBxMean = exportGBxMean;
            exportOptions.ExportGBxStdv = exportGBxStdv;

            exportOptions.ExportGenMaxm = exportGenMaxm;
            exportOptions.ExportGenMinm = exportGenMinm;
            exportOptions.ExportGenMean = exportGenMean;
            exportOptions.ExportGenStdv = exportGenStdv;

            exportOptions.ExportMBrMaxm = exportMBrMaxm;
            exportOptions.ExportMBrMinm = exportMBrMinm;
            exportOptions.ExportMBrMean = exportMBrMean;
            exportOptions.ExportMBrStdv = exportMBrStdv;

            exportOptions.ExportNacMaxm = exportNacMaxm;
            exportOptions.ExportNacMinm = exportNacMinm;
            exportOptions.ExportNacMean = exportNacMean;
            exportOptions.ExportNacStdv = exportNacStdv;

            if (exportOptions.ShowDialog().Value)
            {
                exportPowMaxm = exportOptions.ExportPowMaxm;
                exportPowMinm = exportOptions.ExportPowMinm;
                exportPowMean = exportOptions.ExportPowMean;
                exportPowStdv = exportOptions.ExportPowStdv;

                exportAmbMaxm = exportOptions.ExportAmbMaxm;
                exportAmbMinm = exportOptions.ExportAmbMinm;
                exportAmbMean = exportOptions.ExportAmbMean;
                exportAmbStdv = exportOptions.ExportAmbStdv;

                exportWSpMaxm = exportOptions.ExportWSpMaxm;
                exportWSpMinm = exportOptions.ExportWSpMinm;
                exportWSpMean = exportOptions.ExportWSpMean;
                exportWSpStdv = exportOptions.ExportWSpStdv;

                exportGBxMaxm = exportOptions.ExportGBxMaxm;
                exportGBxMinm = exportOptions.ExportGBxMinm;
                exportGBxMean = exportOptions.ExportGBxMean;
                exportGBxStdv = exportOptions.ExportGBxStdv;

                exportGenMaxm = exportOptions.ExportGenMaxm;
                exportGenMinm = exportOptions.ExportGenMinm;
                exportGenMean = exportOptions.ExportGenMean;
                exportGenStdv = exportOptions.ExportGenStdv;

                exportMBrMaxm = exportOptions.ExportMBrMaxm;
                exportMBrMinm = exportOptions.ExportMBrMinm;
                exportMBrMean = exportOptions.ExportMBrMean;
                exportMBrStdv = exportOptions.ExportMBrStdv;

                exportNacMaxm = exportOptions.ExportNacMaxm;
                exportNacMinm = exportOptions.ExportNacMinm;
                exportNacMean = exportOptions.ExportNacMean;
                exportNacStdv = exportOptions.ExportNacStdv;

                // these must be used by the async task that doesn't yet exist
                // the async task will lead into the writing method
            }
        }

        #endregion

        #region Background Methods

        #region Thresholds

        private void Threshold_LoseFocus(object sender, RoutedEventArgs e)
        {
            CalculateThresholds(this, new RoutedEventArgs());
        }

        private void Threshold_TextChanged(object sender, TextChangedEventArgs e)
        {
            Controls.NumericTextBox tB = (Controls.NumericTextBox)sender;

            Analysis.AnalyticLimit thresLim = (Analysis.AnalyticLimit)tB.Tag;
            thresLim.MaxVars = tB.NumericValue;
            
            CalculateThresholds(this, new RoutedEventArgs());
        }

        private void CalculateThresholds(object sender, RoutedEventArgs e)
        {
            LView_ThresholdValues.ItemsSource = null;

            if (HistEventDataVw != null) { _analyser.Thresholding(_analyser.HistEventData.ToList()); }

            LView_ThresholdValues.ItemsSource = ThresholdEventsView;
            LView_ThresholdValues.Items.Refresh();
        }

        #endregion

        #region Rates of Changes

        private void ROC_LoseFocus(object sender, RoutedEventArgs e)
        {
            CalculateRatesOfChange(this, new RoutedEventArgs());
        }

        private void ROC_TextChanged(object sender, TextChangedEventArgs e)
        {
            Controls.NumericTextBox tB = (Controls.NumericTextBox)sender;

            Analysis.AnalyticLimit rocLim = (Analysis.AnalyticLimit)tB.Tag;
            rocLim.MaxVars = tB.NumericValue;

            CalculateRatesOfChange(this, new RoutedEventArgs());
        }

        private void CalculateRatesOfChange(object sender, RoutedEventArgs e)
        {
            LView_ROCValues.ItemsSource = null;

            if (HistEventDataVw != null) { _analyser.RatesOfChange(_analyser.HistEventData.ToList()); }

            LView_ROCValues.ItemsSource = RateChangeEventsView;
            LView_ROCValues.Items.Refresh();
        }

        #endregion

        private void LView_LoadedOverview_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LView_LoadedOverview.SelectedItems.Count == 1)
            {
                DirectoryItem thisItem = (DirectoryItem)LView_LoadedOverview.SelectedItem;

                if (thisItem.StringData == Overview[0].StringData)
                {
                    Tab_EventsSummary.IsSelected = true;

                    Comb_SummaryChoose.Visibility = Visibility.Visible;
                    Lbl_TabDescription.Visibility = Visibility.Collapsed;
                    Btn_DurationFilter.Visibility = Visibility.Collapsed;
                }
                else if (thisItem.StringData == Overview[1].StringData)
                {
                    Tab_DetailTimeFrame.IsSelected = true;

                    Comb_SummaryChoose.Visibility = Visibility.Collapsed;
                    Lbl_TabDescription.Visibility = Visibility.Visible;
                    Lbl_TabDescription.Content = Overview[1].StringData;
                    Btn_DurationFilter.Visibility = Visibility.Collapsed;
                }
                else if (thisItem.StringData == Overview[2].StringData)
                {
                    Tab_LoWinds.IsSelected = true;

                    Comb_SummaryChoose.Visibility = Visibility.Collapsed;
                    Lbl_TabDescription.Visibility = Visibility.Visible;
                    Lbl_TabDescription.Content = Overview[2].StringData;
                    Btn_DurationFilter.Visibility = Visibility.Collapsed;
                }
                else if (thisItem.StringData == Overview[3].StringData)
                {
                    Tab_HiWinds.IsSelected = true;

                    Comb_SummaryChoose.Visibility = Visibility.Collapsed;
                    Lbl_TabDescription.Visibility = Visibility.Visible;
                    Lbl_TabDescription.Content = Overview[3].StringData;
                    Btn_DurationFilter.Visibility = Visibility.Collapsed;
                }
                else if (thisItem.StringData == Overview[4].StringData)
                {
                    Tab_NoPower.IsSelected = true;

                    Comb_SummaryChoose.Visibility = Visibility.Collapsed;
                    Lbl_TabDescription.Visibility = Visibility.Visible;
                    Lbl_TabDescription.Content = Overview[4].StringData;
                    Btn_DurationFilter.Visibility = Visibility.Visible;
                }
                else if (thisItem.StringData == Overview[5].StringData)
                {
                    Tab_RtPower.IsSelected = true;

                    Comb_SummaryChoose.Visibility = Visibility.Collapsed;
                    Lbl_TabDescription.Visibility = Visibility.Visible;
                    Lbl_TabDescription.Content = Overview[5].StringData;
                    Btn_DurationFilter.Visibility = Visibility.Visible;
                }
            }
        }

        private void ChangeFaultStatus(bool result)
        {
            foreach (object selectedItem in LView_PowrNone.SelectedItems)
            {
                EventData _event = (EventData)selectedItem;

                // find index and change the fault status at that index 
                // this also needs to check the event is from the same asset to be certain we are editing the correct one
                int index = _analyser.NoPwEvents.FindIndex(x => x.SourceAsset == _event.SourceAsset && x.Start == _event.Start);

                _analyser.NoPwEvents[index].IsFault = result;
            }
        }

        void CancelProgress_Click(object sender, RoutedEventArgs e)
        {
            if (_cts != null)
            {
                _cts.Cancel();
                ProgressBarInvisible();
            }
        }

        void CreateSummaries()
        {
            DataSummary();
            EventsSummary();
            ComboBoxInformationList();
        }

        void ComboBoxInformationList()
        {
            Combo_LoadedAssets.ItemsSource = AssetsView;
            Combo_LoadedAssets.SelectedValue = AssetsView;
            Combo_LoadedAssets.DisplayMemberPath = "UnitID";
            Combo_LoadedAssets.SelectedValuePath = "UnitID";
            Combo_LoadedAssets.Items.Refresh();
        }

        void DataSummary()
        {
            LView_LoadedOverview.ItemsSource = null;

            _overview.Clear();
            _overview.Add(new DirectoryItem("Events Summary", LoSpdViews.Count + HiSpdViews.Count + NoPowViews.Count + RtdPowView.Count));
            _overview.Add(new DirectoryItem("Detailed Timeframe"));
            _overview.Add(new DirectoryItem("Wind Speeds: Low", LoSpdViews.Count));
            _overview.Add(new DirectoryItem("Wind Speeds: High", HiSpdViews.Count));
            _overview.Add(new DirectoryItem("Power Prod: None", NoPowViews.Count));
            _overview.Add(new DirectoryItem(
                "Power Prod: " + Common.GetStringDecimals(_analyser.RatedPwr / 1000.0, 1) + "MW", RtdPowView.Count));

            LView_LoadedOverview.ItemsSource = _overview;
        }

        void EventsSummary()
        {
            LView_EventsSumPwrNone.ItemsSource = null;
            LView_EventsSumPwrHigh.ItemsSource = null;
            LView_EventsSumWndLows.ItemsSource = null;
            LView_EventsSumWndHigh.ItemsSource = null;

            ObservableCollection<Analysis.StructureSmry> sumEvents = new ObservableCollection<Analysis.StructureSmry>(_analyser.Summary());

            LView_EventsSumPwrNone.ItemsSource = sumEvents;
            LView_EventsSumPwrHigh.ItemsSource = sumEvents;
            LView_EventsSumWndLows.ItemsSource = sumEvents;
            LView_EventsSumWndHigh.ItemsSource = sumEvents;
        }

        void PopulateOverview()
        {
            if (_meteoFile.MetMasts.Count != 0)
            {
                for (int i = 0; i < _meteoFile.MetMasts.Count; i++)
                {
                    if (!loadedAsset.Contains(_meteoFile.MetMasts[i].UnitID))
                    {
                        _analyser.AssetList.Add((Structure)_meteoFile.MetMasts[i]);

                        loadedAsset.Add(_meteoFile.MetMasts[i].UnitID);
                    }
                    else
                    {
                        int index = _analyser.AssetList.IndexOf
                            (_analyser.AssetList.Where(x => x.UnitID == _meteoFile.MetMasts[i].UnitID).FirstOrDefault());

                        _analyser.AssetList[index].CheckDataSeriesTimes(_meteoFile.MetMasts[i]);
                    }
                }
            }

            if (_scadaFile.WindFarm.Count != 0)
            {
                for (int i = 0; i < _scadaFile.WindFarm.Count; i++)
                {
                    if (!loadedAsset.Contains(_scadaFile.WindFarm[i].UnitID))
                    {
                        _analyser.AssetList.Add((Structure)_scadaFile.WindFarm[i]);

                        loadedAsset.Add(_scadaFile.WindFarm[i].UnitID);
                    }
                    else
                    {
                        int index = _analyser.AssetList.IndexOf
                            (_analyser.AssetList.Where(x => x.UnitID == _scadaFile.WindFarm[i].UnitID).FirstOrDefault());

                        _analyser.AssetList[index].CheckDataSeriesTimes(_scadaFile.WindFarm[i]);
                    }
                }
            }

            // sort the assetview-source before making it the itemssource
            _analyser.AssetList = _analyser.AssetList.OrderBy(o => o.UnitID).ToList();

            // proceed with a correctly ordered list
            LView_Overview.ItemsSource = AssetsView;
            LView_Overview.Items.Refresh();

            LView_Overview.IsEnabled = AssetsView != null && AssetsView.Count > 0 ? true : false;

            if (AssetsView != null && AssetsView.Count > 0)
            {
                _dataExportStart = _eventExplrStart = AssetsView[0].StartTime;
                _dataExportEndTm = _eventExplrEndTm = AssetsView[0].EndTime;

                // i is 1 below because the first values have already been assigned by the above code
                for (int i = 1; i < AssetsView.Count; i++)
                {
                    if (AssetsView[i].StartTime < _dataExportStart) { _dataExportStart = _eventExplrStart = AssetsView[i].StartTime; }
                    if (AssetsView[i].EndTime > _dataExportEndTm) { _dataExportEndTm = _eventExplrEndTm = AssetsView[i].EndTime; }
                }


                CreateSummaries();
            }
        }

        void ProgressBarVisible()
        {
            progress_ProgrBar.Visibility = Visibility.Visible;
            label_ProgressBar.Visibility = Visibility.Visible;
            cancel_ProgressBar.Visibility = Visibility.Visible;
            //counter_ProgressBar.Visibility = Visibility.Visible;
        }

        void ProgressBarInvisible()
        {
            progress_ProgrBar.Visibility = Visibility.Collapsed;
            progress_ProgrBar.Value = 0;

            label_ProgressBar.Visibility = Visibility.Collapsed;
            label_ProgressBar.Content = "";
            cancel_ProgressBar.Visibility = Visibility.Collapsed;

            //counter_ProgressBar.Content = "";
            //counter_ProgressBar.Visibility = Visibility.Collapsed;
        }

        void RefreshEvents()
        {
            if (NoPowViews.Count != 0)
            {
                LView_PowrNone.IsEnabled = true;
                LView_PowrNone.ItemsSource = NoPowViews;
                LView_PowrNone.Items.Refresh();
            }

            if (RtdPowView.Count != 0)
            {
                LView_PowrRted.IsEnabled = true;
                LView_PowrRted.ItemsSource = RtdPowView;
                LView_PowrRted.Items.Refresh();
            }

            if (LoSpdViews.Count != 0)
            {
                LView_WSpdEvLo.IsEnabled = true;
                LView_WSpdEvLo.ItemsSource = LoSpdViews;
                LView_WSpdEvLo.Items.Refresh();
            }

            if (HiSpdViews.Count != 0)
            {
                LView_WSpdEvHi.IsEnabled = true;
                LView_WSpdEvHi.ItemsSource = HiSpdViews;
                LView_WSpdEvHi.Items.Refresh();
            }

            CreateSummaries();
        }
        
        void OnPropertyChanged(string name)
        {
            var changed = PropertyChanged;
            if (changed != null)
            {
                changed(this, new PropertyChangedEventArgs(name));
            }
        }

        void UpdateProgress(int value)
        {
            label_ProgressBar.Content = value + "%";
            progress_ProgrBar.Value = value;
        }

        #endregion

        #region Asset List ContextMenu

        private void LView_Overview_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SetContextMenuAssets();
        }

        private void SetContextMenuAssets()
        {
            ContextMenu menu = null;
                menu = new ContextMenu();

            if (LView_Overview.SelectedItems.Count == 1)
            {
                MenuItem removeAsset_MenuItem = new MenuItem();
                removeAsset_MenuItem.Header = "Remove Asset";
                removeAsset_MenuItem.Click += RemoveAsset_MenuItem_Click;
                menu.Items.Add(removeAsset_MenuItem);
            }

            if (LView_Overview.SelectedItems.Count > 1)
            {
                MenuItem removeAsset_MenuItem = new MenuItem();
                removeAsset_MenuItem.Header = "Remove Assets";
                removeAsset_MenuItem.Click += RemoveAsset_MenuItem_Click;
                menu.Items.Add(removeAsset_MenuItem);
            }

            LView_Overview.ContextMenu = menu;
        }

        private void RemoveAsset_MenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (LView_Overview.SelectedItems.Count > 0)
            {
                foreach (object selectedItem in LView_Overview.SelectedItems)
                {
                    Structure _struc = (Structure)selectedItem;

                    if (_struc.Type == BaseStructure.Types.METMAST)
                    {
                        int index = _meteoFile.MetMasts.FindIndex(x => x.UnitID == _struc.UnitID);
                        _meteoFile.MetMasts.RemoveAt(index);
                        _meteoFile.InclMetm.Remove(_struc.UnitID);
                    }
                    else if (_struc.Type == BaseStructure.Types.TURBINE)
                    {
                        int index = _scadaFile.WindFarm.FindIndex(x => x.UnitID == _struc.UnitID);
                        _scadaFile.WindFarm.RemoveAt(index);
                        _scadaFile.InclTrbn.Remove(_struc.UnitID);
                    }

                    loadedAsset.Remove(_struc.UnitID);

                    int target = _analyser.AssetList.FindIndex(x => x.UnitID == _struc.UnitID);
                    _analyser.AssetList.RemoveAt(target);
                }

                LView_Overview.ItemsSource = AssetsView;
                LView_Overview.Items.Refresh();
                CreateSummaries();
            }
        }

        #endregion

        #region Event List ContextMenu

        private void LView_Powr_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SetContextMenuPowerEvents();
        }

        private void SetContextMenuPowerEvents()
        {
            ContextMenu menu = null;
            menu = new ContextMenu();

            if (LView_PowrNone.SelectedItems.Count == 1 || LView_PowrRted.SelectedItems.Count == 1)
            {
                MenuItem explorEvent_MenuItem = new MenuItem();
                explorEvent_MenuItem.Header = "Explore Event";
                explorEvent_MenuItem.Click += ExploreEvent_MenuItem_Click;
                menu.Items.Add(explorEvent_MenuItem);
            }

            if (LView_PowrNone.SelectedItems.Count == 1)
            {
                MenuItem removeEvent_MenuItem = new MenuItem();
                removeEvent_MenuItem.Header = "Remove Event";
                removeEvent_MenuItem.Click += RemoveEvent_MenuItem_Click;
                menu.Items.Add(removeEvent_MenuItem);
                MenuItem removeAssetEvents_MenuItem = new MenuItem();
                removeAssetEvents_MenuItem.Header = "Remove Events from This Asset";
                removeAssetEvents_MenuItem.Click += RemoveAssetEvents_MenuItem_Click;
                menu.Items.Add(removeAssetEvents_MenuItem);
                MenuItem makeFault_MenuItem = new MenuItem();
                makeFault_MenuItem.Header = "Change Event to Fault";
                makeFault_MenuItem.Click += MakeFault_MenuItem_Click;
                menu.Items.Add(makeFault_MenuItem);
                MenuItem makeNormal_MenuItem = new MenuItem();
                makeNormal_MenuItem.Header = "Change Event to Not Fault";
                makeNormal_MenuItem.Click += MakeNormal_MenuItem_Click;
                menu.Items.Add(makeNormal_MenuItem);
            }
            else if (LView_PowrNone.SelectedItems.Count > 1)
            {
                MenuItem removeEvent_MenuItem = new MenuItem();
                removeEvent_MenuItem.Header = "Remove Events";
                removeEvent_MenuItem.Click += RemoveEvent_MenuItem_Click;
                menu.Items.Add(removeEvent_MenuItem);
                MenuItem makeFault_MenuItem = new MenuItem();
                makeFault_MenuItem.Header = "Change Events to Faults";
                makeFault_MenuItem.Click += MakeFault_MenuItem_Click;
                menu.Items.Add(makeFault_MenuItem);
                MenuItem makeNormal_MenuItem = new MenuItem();
                makeNormal_MenuItem.Header = "Change Events to Not Faults";
                makeNormal_MenuItem.Click += MakeNormal_MenuItem_Click;
                menu.Items.Add(makeNormal_MenuItem);
            }

            if (LView_PowrNone.SelectedItems.Count >= 1) { LView_PowrNone.ContextMenu = menu; }
            else if (LView_PowrRted.SelectedItems.Count == 1) { LView_PowrRted.ContextMenu = menu; }
        }

        private void ExploreEvent_MenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                EventData thisEv;

                if ((LView_PowrNone.SelectedItems.Count == 1 || LView_PowrRted.SelectedItems.Count == 1) &&
                    !Tab_DetailTimeFrame.IsSelected)
                {
                    _usingPreviousWeekForGraphing = true;

                    thisEv = LView_PowrNone.SelectedItems.Count == 1 ?
                        (EventData)LView_PowrNone.SelectedItem : (EventData)LView_PowrRted.SelectedItem;
                }
                else
                {
                    _usingPreviousWeekForGraphing = false;
                    List<ScadaData.ScadaSample> thisList = new List<ScadaData.ScadaSample>();

                    if (Combo_LoadedAssets.SelectedIndex != -1)
                    {
                        Structure selectedAsset = (Structure)Combo_LoadedAssets.SelectedItem;

                        thisList = _analyser.GetSpecEventDetails
                            (_scadaFile, selectedAsset.UnitID, _eventExplrStart, _eventExplrEndTm);
                    }

                    thisEv = new EventData(thisList, EventData.AnomalySource.USERDEFINED);
                }

                _analyser.EventData(_scadaFile, thisEv);

                // assign the created dataset lists to their global variables as proprties

                // now sent the thisEvScada to the new ListView to populate it
                InitializeEventExploration(sender, e);
            }
            catch
            {
                MessageBox.Show("A problem has come up with the code in loading this event. Have the programmer check the indices.", "Warning!");
            }
        }

        private void InitializeEventExploration(object sender, RoutedEventArgs e)
        {
            Comb_DisplayEvDetails.IsEnabled = true;
            LBL_EquipmentChoice.IsEnabled = true;
            CBox_DataSetChoice.IsEnabled = true;

            // first add it to the gridview on the list
            LView_EventExplorer_Gearbox.ItemsSource =  ThisEventDataVw;
            LView_EventExplorer_Generator.ItemsSource = ThisEventDataVw;
            LView_EventExplorer_MainBear.ItemsSource = ThisEventDataVw;

            ChangeListViewDataset(sender, e);

            DisplayCorrectEventDetails(_usingPreviousWeekForGraphing);

            // lastly also add respective dataviews to the chart and also to the viewmodel

            //ScrollableViewModel sVM = new ScrollableViewModel(histEventData.ToList());

            //ScrollView.Visibility = Visibility.Visible;
            //ScrollView.DataContext = sVM;
        }

        private void MakeFault_MenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (LView_PowrNone.SelectedItems.Count > 0) { ChangeFaultStatus(true); }

            RefreshEvents();
        }

        private void MakeNormal_MenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (LView_PowrNone.SelectedItems.Count > 0) { ChangeFaultStatus(false); }

            RefreshEvents();
        }
        
        private void RemoveEvent_MenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (LView_PowrNone.SelectedItems.Count > 0)
            {
                foreach (object selectedItem in LView_PowrNone.SelectedItems)
                {
                    EventData _event = (EventData)selectedItem;

                    // find index and removeat that index but make certain it is the right asset we're removing from
                    int index = _analyser.NoPwEvents.FindIndex(x => x.SourceAsset == _event.SourceAsset && x.Start == _event.Start);
                    _analyser.NoPwEvents.RemoveAt(index);
                }

                RefreshEvents();
            }
        }

        private void RemoveAssetEvents_MenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (LView_PowrNone.SelectedItems.Count == 1)
            {
                EventData _event = (EventData)LView_PowrNone.SelectedItem;

                // create a reverse loop to go through and remove if asset IDs match
                for (int i = _analyser.NoPwEvents.Count - 1; i >= 0; i--)
                {
                    if (_analyser.NoPwEvents[i].SourceAsset == _event.SourceAsset)
                    {
                        _analyser.NoPwEvents.RemoveAt(i);
                    }
                }
                
                RefreshEvents();
            }
        }

        #endregion

        #region Support Classes

        #endregion

        #region Properties

        public Analysis Analyser { get { return _analyser; } set { _analyser = value; } }
        
        public GeoData GeoFile { get { return _geoFile; } set { _geoFile = value; } }
        public MeteoData MeteoFile { get { return _meteoFile; } set { _meteoFile = value; } }
        public ScadaData ScadaFile { get { return _scadaFile; } set { _scadaFile = value; } }

        public bool GeoLoaded { get { return _geoLoaded; } set { _geoLoaded = value; } }        
        public bool MeteoLoaded { get { return _meteoLoaded; } set { _meteoLoaded = value; } }
        public bool ScadaLoaded { get { return _scadaLoaded; } set { _scadaLoaded = value; } }

        public string EventExploreStartTm
        {
            get { return _eventExplrStart.ToString(); }
            set
            {
                if (EventExploreStartTm != value)
                {
                    EventExploreStartTm = value;
                    OnPropertyChanged(nameof(EventExploreStartTm));
                }
            }
        }

        public string EventExploreEndTime
        {
            get { return _eventExplrEndTm.ToString(); }
            set
            {
                if (EventExploreEndTime != value)
                {
                    EventExploreEndTime = value;
                    OnPropertyChanged(nameof(EventExploreEndTime));
                }
            }
        }

        public string[] Labels
        {
            get { return _labels; }
            set
            {
                if (_labels != value)
                {
                    _labels = value;
                    OnPropertyChanged(nameof(_labels));
                }
            }
        }

        public static bool Mnt_Night { get { return mnt_Night; } set { mnt_Night = value; } }
        public static bool Mnt_AstDw { get { return mnt_AstDw; } set { mnt_AstDw = value; } }
        public static bool Mnt_NauDw { get { return mnt_NauDw; } set { mnt_NauDw = value; } }
        public static bool Mnt_CivDw { get { return mnt_CivDw; } set { mnt_CivDw = value; } }
        public static bool Mnt_Daytm { get { return mnt_Daytm; } set { mnt_Daytm = value; } }
        public static bool Mnt_CivDs { get { return mnt_CivDs; } set { mnt_CivDs = value; } }
        public static bool Mnt_NauDs { get { return mnt_NauDs; } set { mnt_NauDs = value; } }
        public static bool Mnt_AstDs { get { return mnt_AstDs; } set { mnt_AstDs = value; } }

        public List<DirectoryItem> Overview
        {
            get { return _overview; }
            set
            {
                if (_overview != value)
                {
                    _overview = value;
                    OnPropertyChanged(nameof(Overview));
                }
            }
        }

        public ObservableCollection<Analysis.AnalyticLimit> RocVw
        {
            get { return new ObservableCollection<Analysis.AnalyticLimit>(_analyser.RateChange); }
            set
            {
                if (RocVw != value)
                {
                    RocVw = value;
                    OnPropertyChanged(nameof(RocVw));
                }
            }
        }

        public ObservableCollection<Analysis.AnalyticLimit> ThresholdVw
        {
            get { return new ObservableCollection<Analysis.AnalyticLimit>(_analyser.Thresholds); }
            set
            {
                if (ThresholdVw != value)
                {
                    ThresholdVw = value;
                    OnPropertyChanged(nameof(ThresholdVw));
                }
            }
        }

        public ObservableCollection<EventData> RateChangeEventsView
        {
            get { return new ObservableCollection<EventData>(_analyser.RChngEvnts); }
            set
            {
                if (RateChangeEventsView != value)
                {
                    RateChangeEventsView = value;
                    OnPropertyChanged(nameof(RateChangeEventsView));
                }
            }
        }

        public ObservableCollection<EventData> ThresholdEventsView
        {
            get { return new ObservableCollection<EventData>(_analyser.ThresEvnts); }
            set
            {
                if (ThresholdEventsView != value)
                {
                    ThresholdEventsView = value;
                    OnPropertyChanged(nameof(ThresholdEventsView));
                }
            }
        }

        public ObservableCollection<string> LoadedFiles
        {
            get
            {
                ObservableCollection<string> _list = new ObservableCollection<string>();

                foreach(string filename in _loadedFiles)
                {
                    _list.Add(System.IO.Path.GetFileName(filename));
                }

                return new ObservableCollection<string>(_list);
            }
        }
        
        public ObservableCollection<EventData> AllWtrView
        {
            get { return new ObservableCollection<EventData>(_analyser.AllWtrEvts); }
            set { _analyser.AllWtrEvts = value.ToList(); }
        }

        public ObservableCollection<EventData> LoSpdViews
        {
            get { return new ObservableCollection<EventData>(_analyser.LoSpEvents); }
            set { _analyser.LoSpEvents = value.ToList(); }
        }

        public ObservableCollection<EventData> HiSpdViews
        {
            get { return new ObservableCollection<EventData>(_analyser.HiSpEvents); }
            set { _analyser.HiSpEvents = value.ToList(); }
        }

        public ObservableCollection<EventData> NoPowViews
        {
            get { return new ObservableCollection<EventData>(_analyser.NoPwEvents); }
            set { _analyser.NoPwEvents = value.ToList(); }
        }

        public ObservableCollection<EventData> AllPowView
        {
            get { return new ObservableCollection<EventData>(_analyser.AllPwrEvts); }
            set { _analyser.AllPwrEvts = value.ToList(); }
        }

        public ObservableCollection<EventData> RtdPowView
        {
            get { return new ObservableCollection<EventData>(_analyser.RtPwEvents); }
            set { _analyser.RtPwEvents = value.ToList(); }
        }

        public ObservableCollection<Structure> AssetsView
        {
            get { return _assetsVw = new ObservableCollection<Structure>(_analyser.AssetList); }
            set
            {
                if (_assetsVw != value)
                {
                    _assetsVw = value;
                    OnPropertyChanged(nameof(_analyser.AssetList));
                }
            }
        }
        
        public ObservableCollection<ScadaData.ScadaSample> ThisEventDataVw
        {
            get { return new ObservableCollection<ScadaData.ScadaSample>(_analyser.ThisEvScada); }
            set { _analyser.ThisEvScada = value.ToList(); }
        }

        public ObservableCollection<ScadaData.ScadaSample> WeekEventDataVw
        {
            get { return new ObservableCollection<ScadaData.ScadaSample>(_analyser.WeekHistory); }
            set { _analyser.WeekHistory = value.ToList(); }
        }

        public ObservableCollection<ScadaData.ScadaSample> HistEventDataVw
        {
            get { return new ObservableCollection<ScadaData.ScadaSample>(_analyser.HistEventData); }
            set { _analyser.HistEventData = value.ToList(); }
        }

        #endregion
    }

    #region Project Specific Exceptions

    public class CancelLoadingException : Exception { }

    public class DataNotProcessedException : Exception { }

    public class FutureDevelopmentException : Exception { }

    public class LoadingCancelledException : Exception { }

    public class NoFilesInFolderException : Exception { }

    public class UnsuitableTargetFileException : Exception { }

    public class WritingCancelledException : Exception { }

    public class WrongDateTimeException : Exception
    {
        #region Variables

        private string[] dateInfo;

        #endregion

        #region Constructor

        public WrongDateTimeException() { }

        public WrongDateTimeException(string[] dateInfo)
        {
            this.dateInfo = dateInfo;
        }

        #endregion

        #region Properties

        public string[] DateInfo { get { return dateInfo; } set { dateInfo = value; } }

        #endregion
    }

    public class WrongFileTypeException : Exception { }

#endregion
}
