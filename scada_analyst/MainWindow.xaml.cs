using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

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

        private bool geoLoaded = false, meteoLoaded = false, scadaLoaded = false;
        private bool positionsAddedToData = false, eventsAreProcessed = false, eventsMatchedAcrossTypes = false;

        private static bool mnt_Night = false;
        private static bool mnt_AstDw = false;
        private static bool mnt_NauDw = false;
        private static bool mnt_CivDw = true;
        private static bool mnt_Daytm = true;
        private static bool mnt_CivDs = true;
        private static bool mnt_NauDs = false;
        private static bool mnt_AstDs = false;

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
        private List<string> loadedFiles = new List<string>();
        private List<string> _eventDetailsSelection = new List<string>();
        private List<string> _eventSummarySelection = new List<string>();

        private CancellationTokenSource _cts;

        private Common.DateFormat _dateFormat = Common.DateFormat.YMD;
        private DateTime _dataExportStart = new DateTime();
        private DateTime _dataExportEndTm = new DateTime();

        private Analysis analyser = new Analysis();
        private GeoData geoFile;
        private MeteoData meteoFile = new MeteoData();
        private ScadaData scadaFile = new ScadaData();
        
        private List<DirectoryItem> _overview = new List<DirectoryItem>();

        private ObservableCollection<Structure> _assetsVw = new ObservableCollection<Structure>();
        private ObservableCollection<EventData> _allWtrVw = new ObservableCollection<EventData>();
        private ObservableCollection<EventData> _allPwrVw = new ObservableCollection<EventData>();
        private ObservableCollection<EventData> _loSpView = new ObservableCollection<EventData>();
        private ObservableCollection<EventData> _hiSpView = new ObservableCollection<EventData>();
        private ObservableCollection<EventData> _noPwView = new ObservableCollection<EventData>();
        private ObservableCollection<EventData> _rtPwView = new ObservableCollection<EventData>();
        
        private ObservableCollection<ScadaData.ScadaSample> _thisEventData;
        private ObservableCollection<ScadaData.ScadaSample> _weekEventData;
        private ObservableCollection<ScadaData.ScadaSample> _histEventData;

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

            //LBL_EquipmentChoice.IsEnabled = false;
            //Comb_DisplayEvDetails.IsEnabled = false;
            //LBL_EquipmentChoice.IsEnabled = false;
            //CBox_DataSetChoice.IsEnabled = false;
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
            Window_DateOptions dO = new Window_DateOptions(this, _dateFormat);

            if (dO.ShowDialog().Value) { _dateFormat = dO.Format; }
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
                            "Hours", "Minutes", false, false, analyser.DuratFilter.TotalHours, analyser.DuratFilter.Minutes);

            if (getTimeDur.ShowDialog().Value)
            {
                analyser.DuratFilter = new TimeSpan((int)getTimeDur.NumericValue1, (int)getTimeDur.NumericValue2, 0);
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
            loadedFiles.Clear();
            geoFile = null; geoLoaded = false; positionsAddedToData = false;

            analyser.AddStructureLocations(geoFile, meteoFile, scadaFile, scadaLoaded, meteoLoaded, geoLoaded);
            CreateSummaries();
        }

        /// <summary>
        /// Clears all loaded meteorologic data
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ClearMeteoData(object sender, RoutedEventArgs e)
        {
            for (int i = analyser.AssetList.Count - 1; i >= 0; i--)
            {
                if (analyser.AssetList[i].Type == BaseStructure.Types.METMAST)
                {
                    loadedAsset.Remove(analyser.AssetList[i].UnitID);
                    analyser.AssetList.RemoveAt(i);
                }
            }

            loadedFiles.Clear();
            meteoFile = new MeteoData(); meteoLoaded = false;

            analyser.AddStructureLocations(geoFile, meteoFile, scadaFile, scadaLoaded, meteoLoaded, geoLoaded);
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
            for (int i = analyser.AssetList.Count - 1; i >= 0; i--)
            {
                if (analyser.AssetList[i].Type == BaseStructure.Types.TURBINE)
                {
                    loadedAsset.Remove(analyser.AssetList[i].UnitID);
                    analyser.AssetList.RemoveAt(i);
                }
            }

            loadedFiles.Clear();
            scadaFile = new ScadaData(); scadaLoaded = false;

            analyser.AddStructureLocations(geoFile, meteoFile, scadaFile, scadaLoaded, meteoLoaded, geoLoaded);
            CreateSummaries();
            PopulateOverview();
        }

        #endregion

        #region ComboBox Navigations

        private void CreateSummaryComboInfo()
        {
            _eventSummarySelection.Add("No Power Production Events");
            _eventSummarySelection.Add("High Power Production Events");
            _eventSummarySelection.Add("Low Wind Speed Events");
            _eventSummarySelection.Add("High Wind Speed Events");

            Comb_SummaryChoose.ItemsSource = _eventSummarySelection;
            Comb_SummaryChoose.SelectedIndex = 0;
        }

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

        private void CreateEventDetailsView()
        {
            _eventDetailsSelection.Add(" ");
            _eventDetailsSelection.Add("Gearbox");
            _eventDetailsSelection.Add("Generator");
            _eventDetailsSelection.Add("Main bearing");

            Comb_DisplayEvDetails.ItemsSource = _eventDetailsSelection;
            Comb_DisplayEvDetails.SelectedIndex = 0;
        }

        /// <summary>
        /// Checks what equipment is chosen from the menu and changes information displayed based on that.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Comb_DisplayEvDetails_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // only go into this event if the below conditions are true; probably will disable the combobox as well though
            DisplayCorrectEventDetails();
        }

        #endregion

        #region Event Details View Manipulation

        private void Chart_OnData_Click(object sender, ChartPoint point)
        {
            // bring up a messagebox to show the user the time and value of the datapoint they
            // clicked on

            string time = Int32.TryParse(point.X.ToString(), out int theta) ? Labels[(int)point.X] : "N/A";

            LBL_ClickInfo.Content = "Info: " + point.Y + "° C at " + time;
        }

        private void ChangeListViewDataset(object sender, RoutedEventArgs e)
        {
            // checks whether the dataset choice button has been activated and displays the respective
            // -- either all historic data to the end of the event, or only partial data for the duration
            // of the event

            if (CBox_DataSetChoice.IsChecked.Value)
            {
                LView_EventExplorer_Gearbox.ItemsSource = _histEventData;
                LView_EventExplorer_Generator.ItemsSource = _histEventData;
                LView_EventExplorer_MainBear.ItemsSource = _histEventData;
            }
            else
            {
                LView_EventExplorer_Gearbox.ItemsSource = _thisEventData;
                LView_EventExplorer_Generator.ItemsSource = _thisEventData;
                LView_EventExplorer_MainBear.ItemsSource = _thisEventData;
            }
        }

        private void ChartShowSeries(string input)
        {
            try
            {
                LChart_Basic.Series.Clear();

                List<double> list1 = new List<double>();
                List<double> list2 = new List<double>();
                List<string> times = new List<string>();

                LineSeries priGraph = new LineSeries();
                LineSeries secGraph = new LineSeries();

                for (int i = 0; i < _weekEventData.Count; i++)
                {
                    double var1 = double.NaN;
                    double var2 = double.NaN;

                    if (input == _eventDetailsSelection[1])
                    {
                        priGraph.Title = "HS Gens.";
                        var1 = Math.Round(_weekEventData[i].Gearbox.Hs.Gens.Mean, 1);

                        secGraph.Title = "HS Rots.";
                        var2 = Math.Round(_weekEventData[i].Gearbox.Hs.Rots.Mean, 1);
                    }
                    else if (input == _eventDetailsSelection[2])
                    {
                        priGraph.Title = "G-Bearing";
                        var1 = Math.Round(_weekEventData[i].Genny.BearingG.Mean, 1);

                        secGraph.Title = "R-Bearing";
                        var2 = Math.Round(_weekEventData[i].Genny.BearingR.Mean, 1);
                    }
                    else if (input == _eventDetailsSelection[3])
                    {
                        priGraph.Title = "Main Bearing";
                        var1 = Math.Round(_weekEventData[i].MainBear.Main.Mean, 1);

                        priGraph.Title = "HS Bearing";
                        var2 = Math.Round(_weekEventData[i].MainBear.Hs.Mean, 1);
                    }

                    list1.Add(!double.IsNaN(var1) ? var1 : double.NaN);
                    list2.Add(!double.IsNaN(var2) ? var2 : double.NaN);

                    times.Add(_weekEventData[i].TimeStamp.ToString("HH:mm dd-MMM"));
                }

                priGraph.Values = new ChartValues<double>(list1);
                priGraph.Fill = Brushes.Transparent;

                secGraph.Values = new ChartValues<double>(list2);
                secGraph.Fill = Brushes.Transparent;

                Labels = times.ToArray();

                LChart_Basic.Series.Add(priGraph);
                LChart_Basic.Series.Add(secGraph);

                LChart_XAxis.Labels = Labels;
            }
            catch { }
        }

        private void DisplayCorrectEventDetails()
        {
            if (Comb_DisplayEvDetails.SelectedIndex != -1)
            {
                LChart_Basic.Visibility = Visibility.Visible;

                if ((string)Comb_DisplayEvDetails.SelectedItem == _eventDetailsSelection[0])
                {
                    LView_EventExplorer_Gearbox.Visibility = Visibility.Collapsed;
                    LView_EventExplorer_Generator.Visibility = Visibility.Collapsed;
                    LView_EventExplorer_MainBear.Visibility = Visibility.Collapsed;
                    LChart_Basic.Visibility = Visibility.Collapsed;
                    //analyser.CreateThresholdLimits(_eventDetailsSelection[0]);
                }
                else if ((string)Comb_DisplayEvDetails.SelectedItem == _eventDetailsSelection[1])
                {
                    LView_EventExplorer_Gearbox.Visibility = Visibility.Visible;
                    LView_EventExplorer_Generator.Visibility = Visibility.Collapsed;
                    LView_EventExplorer_MainBear.Visibility = Visibility.Collapsed;
                    ChartShowSeries(_eventDetailsSelection[1]);
                    //analyser.CreateThresholdLimits(_eventDetailsSelection[1]);
                }
                else if ((string)Comb_DisplayEvDetails.SelectedItem == _eventDetailsSelection[2])
                {
                    LView_EventExplorer_Gearbox.Visibility = Visibility.Collapsed;
                    LView_EventExplorer_Generator.Visibility = Visibility.Visible;
                    LView_EventExplorer_MainBear.Visibility = Visibility.Collapsed;
                    ChartShowSeries(_eventDetailsSelection[2]);
                    //analyser.CreateThresholdLimits(_eventDetailsSelection[2]);
                }
                else if ((string)Comb_DisplayEvDetails.SelectedItem == _eventDetailsSelection[3])
                {
                    LView_EventExplorer_Gearbox.Visibility = Visibility.Collapsed;
                    LView_EventExplorer_Generator.Visibility = Visibility.Collapsed;
                    LView_EventExplorer_MainBear.Visibility = Visibility.Visible;
                    ChartShowSeries(_eventDetailsSelection[3]);
                    //analyser.Thresholds = analyser.CreateThresholdLimits(_eventDetailsSelection[3]);
                }
            }
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
                else if (geoFile == null || !positionsAddedToData)
                {
                    await this.ShowMessageAsync("Warning!",
                        "Geographic details have not been loaded, or the data has not been associated with the loaded structures.");

                    throw new CancelLoadingException();
                }

                // if the above conditions are not fulfilled, the process can continue

                ProgressBarVisible();

                await Task.Run(() => analyser.AddDaytimesToEvents(progress));

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
                if (geoFile == null || geoFile.GeoInfo.Count == 0)
                {
                    await this.ShowMessageAsync("Warning!",
                        "No geographic data is loaded.");

                    throw new CancelLoadingException();
                }
                else if (meteoFile.MetMasts.Count == 0 && scadaFile.WindFarm.Count == 0)
                {
                    await this.ShowMessageAsync("Warning!",
                        "No meteorologic or SCADA data is loaded.");

                    throw new CancelLoadingException();
                }

                positionsAddedToData = analyser.AddStructureLocations(geoFile, meteoFile, scadaFile, scadaLoaded, meteoLoaded, geoLoaded);
            }
            catch (CancelLoadingException) { }
            catch { throw new Exception(); }
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
                if (meteoFile.MetMasts.Count != 0)
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
                                _dataExportStart = Common.StringToDateTime(startCal.TextBox_Calendar.Text, false);
                            }

                            if (endCal.ShowDialog().Value)
                            {
                                _dataExportEndTm = Common.StringToDateTime(endCal.TextBox_Calendar.Text, false);
                            }
                        }

                        await Task.Run(() => meteoFile.ExportFiles(progress, saveFileDialog.FileName, _dataExportStart, _dataExportEndTm));

                        ProgressBarInvisible();
                    }
                }
                else
                {
                    MessageBox.Show(this, "No data of this type has been loaded yet. Please load data before trying to export.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.GetType().Name + ": " + ex.Message);
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
                if (scadaFile.WindFarm.Count != 0)
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
                                _dataExportStart = Common.StringToDateTime(startCal.TextBox_Calendar.Text, false);
                            }

                            if (endCal.ShowDialog().Value)
                            {
                                _dataExportEndTm = Common.StringToDateTime(endCal.TextBox_Calendar.Text, false);
                            }
                        }

                        await Task.Run(() => scadaFile.ExportFiles(progress, saveFileDialog.FileName,
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
                    MessageBox.Show(this, "No data of this type has been loaded yet. Please load data before trying to export.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.GetType().Name + ": " + ex.Message);
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

                await Task.Run(() => analyser.FindEvents(scadaFile, meteoFile, progress));

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
                if (geoFile == null || !positionsAddedToData)
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

                await Task.Run(() => analyser.GetDistances(analyser.AssetList));
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
                    if (!loadedFiles.Contains(filenames[i]))
                    {
                        geoFile = new GeoData(filenames[i], progress);

                        loadedFiles.Add(filenames[i]);
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
                openFileDialog.Multiselect = true;

                if (openFileDialog.ShowDialog().Value)
                {
                    ProgressBarVisible();

                    await Task.Run(() => LoadGeoData(openFileDialog.FileNames, progress));

                    ProgressBarInvisible();

                    geoLoaded = true;
                }
            }
            catch (LoadingCancelledException)
            {
                MessageBox.Show("Loading cancelled by user.");
            }
            catch (OperationCanceledException)
            {

            }
            catch (WrongFileTypeException)
            {
                MessageBox.Show("This file cannot be loaded since it is of an incompatible file type for this function.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.GetType().Name + ": " + ex.Message);
            }
        }

        /// <summary>
        /// Method loads the meteorology data, checks for existing files and appends datapoints
        /// </summary>
        /// <param name="existingData"></param>
        /// <param name="filenames"></param>
        /// <param name="isLoaded"></param>
        /// <param name="progress"></param>
        private void LoadMeteoData(MeteoData existingData, string[] filenames, bool isLoaded, IProgress<int> progress)
        {
            try
            {
                MeteoData analysis = new MeteoData(existingData);

                if (!isLoaded)
                {
                    analysis = new MeteoData(filenames, progress);
                }
                else
                {
                    analysis.AppendFiles(filenames, progress);
                }

                meteoFile = analysis;
                meteoLoaded = true;
            }
            catch
            {
                throw;
            }
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

                    await Task.Run(() => LoadMeteoData(meteoFile, openFileDialog.FileNames, meteoLoaded,
                        progress));

                    ProgressBarInvisible();

                    PopulateOverview();
                }
            }
            catch (LoadingCancelledException)
            {
                MessageBox.Show("Loading cancelled by user.");
            }
            catch (OperationCanceledException)
            {

            }
            catch (WrongFileTypeException)
            {
                MessageBox.Show("This file cannot be loaded since it is of an incompatible file type for this function.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.GetType().Name + ": " + ex.Message);
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
        private async Task LoadScadaData(ScadaData existingData, string[] filenames, bool isLoaded, IProgress<int> progress)
        {
            try
            {
                ScadaData analysis = new ScadaData(existingData);

                await Task.Run(() =>
                {
                    if (!isLoaded)
                    {
                        analysis = new ScadaData(filenames, _dateFormat, progress);
                    }
                    else
                    {
                        analysis.AppendFiles(filenames, _dateFormat, progress);
                    }
                });

                scadaFile = analysis;
                scadaLoaded = true;
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
                    ProgressBarVisible();

                    await Task.Run(() =>
                        LoadScadaData(scadaFile, openFileDialog.FileNames, scadaLoaded, progress), token);

                    ProgressBarInvisible();
                    PopulateOverview();
                }
            }
            catch (LoadingCancelledException)
            {
                await this.ShowMessageAsync("Warning!", "Loading cancelled by user.");
            }
            catch (OperationCanceledException)
            {
                await this.ShowMessageAsync("Warning!", "Loading cancelled by user.");
            }
            catch (WrongDateTimeException)
            {
                await this.ShowMessageAsync("Warning!",
                    "Try changing the loaded date-time format for the file(s) to load properly.");
            }
            catch (WrongFileTypeException)
            {
                await this.ShowMessageAsync("Warning!", "This file cannot be loaded since it is of an incompatible file type for this function.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.GetType().Name + ": " + ex.Message);
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

                await Task.Run(() => analyser.AssociateEvents(progress));

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

                await Task.Run(() => scadaFile = analyser.FleetStats(scadaFile, progress));

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
                if (analyser.DuratFilter.TotalSeconds == 0)
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

                await Task.Run(() => analyser.RemoveByDuration(progress));

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

                await Task.Run(() => analyser.RemoveMatchedEvents(progress));

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

                await Task.Run(() => analyser.RemoveProcessedDaytimes(progress));

                ProgressBarInvisible();
                RefreshEvents();
            }
            catch (CancelLoadingException) { }
            catch (Exception ex)
            {
                MessageBox.Show(ex.GetType().Name + ": " + ex.Message);
            }
        }

        /// <summary>
        /// Resets power production events to their original state
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ResetPowerProdEvents(object sender, RoutedEventArgs e)
        {
            analyser.DuratFilter = new TimeSpan(0, 10, 0);

            analyser.ResetEventList();
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
            // old method here before took the use of the proper analysis settings window
            // I'll leave the old code here in its commented form

            //Window_NumberTwo setSpdLims = new Window_NumberTwo(this, "Turbine Operating Limits",
            //    "Cut In", "Cut Out", true, false, cutIn, cutOut);

            //if (setSpdLims.ShowDialog().Value)
            //{
            //    cutIn = setSpdLims.NumericValue1;
            //    cutOut = setSpdLims.NumericValue2;
            //}

            // new code below with the proper analysis settings window, etc
            // more options for expanding the code if necessary (which it no doubt will be)

            Window_AnalysisSettings anaSets = new Window_AnalysisSettings(this, analyser,
                mnt_Night, mnt_AstDw, mnt_NauDw, mnt_CivDw, mnt_Daytm, mnt_CivDs, mnt_NauDs, mnt_AstDs);

            if (anaSets.ShowDialog().Value)
            {
                analyser.CutIn = anaSets.SpdIns;
                analyser.CutOut = anaSets.SpdOut;
                analyser.RatedPwr = anaSets.RtdPwr;

                mnt_Night = anaSets.Mnt_Night;
                mnt_AstDw = anaSets.Mnt_AstDw;
                mnt_NauDw = anaSets.Mnt_NauDw;
                mnt_CivDw = anaSets.Mnt_CivDw;
                mnt_Daytm = anaSets.Mnt_Daytm;
                mnt_CivDs = anaSets.Mnt_CivDs;
                mnt_NauDs = anaSets.Mnt_NauDs;
                mnt_AstDs = anaSets.Mnt_AstDs;

                analyser.WorkHoursMorning = anaSets.WorkHoursMorning;
                analyser.WorkHoursEvening = anaSets.WorkHoursEvening;

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
                    Tab_LoWinds.IsSelected = true;

                    Comb_SummaryChoose.Visibility = Visibility.Collapsed;
                    Lbl_TabDescription.Visibility = Visibility.Visible;
                    Lbl_TabDescription.Content = Overview[1].StringData;
                    Btn_DurationFilter.Visibility = Visibility.Collapsed;
                }
                else if (thisItem.StringData == Overview[2].StringData)
                {
                    Tab_HiWinds.IsSelected = true;

                    Comb_SummaryChoose.Visibility = Visibility.Collapsed;
                    Lbl_TabDescription.Visibility = Visibility.Visible;
                    Lbl_TabDescription.Content = Overview[2].StringData;
                    Btn_DurationFilter.Visibility = Visibility.Collapsed;
                }
                else if (thisItem.StringData == Overview[3].StringData)
                {
                    Tab_NoPower.IsSelected = true;

                    Comb_SummaryChoose.Visibility = Visibility.Collapsed;
                    Lbl_TabDescription.Visibility = Visibility.Visible;
                    Lbl_TabDescription.Content = Overview[3].StringData;
                    Btn_DurationFilter.Visibility = Visibility.Visible;
                }
                else if (thisItem.StringData == Overview[4].StringData)
                {
                    Tab_RtPower.IsSelected = true;

                    Comb_SummaryChoose.Visibility = Visibility.Collapsed;
                    Lbl_TabDescription.Visibility = Visibility.Visible;
                    Lbl_TabDescription.Content = Overview[4].StringData;
                    Btn_DurationFilter.Visibility = Visibility.Visible;
                }
            }
        }

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

            if (_histEventData != null) { analyser.Thresholding(_histEventData.ToList()); }

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

            if (_histEventData != null) { analyser.RatesOfChange(_histEventData.ToList()); }

            LView_ROCValues.ItemsSource = RateChangeEventsView;
            LView_ROCValues.Items.Refresh();
        }

        #endregion

        private void ChangeFaultStatus(bool result)
        {
            foreach (object selectedItem in LView_PowrNone.SelectedItems)
            {
                EventData _event = (EventData)selectedItem;

                // find index and change the fault status at that index 
                // this also needs to check the event is from the same asset to be certain we are editing the correct one
                int triggeringAsset = _event.FromAsset;

                int index = analyser.NoPwEvents.FindIndex(x => x.FromAsset == triggeringAsset && x.Start == _event.Start);

                analyser.NoPwEvents[index].IsFault = result;
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
        }

        void DataSummary()
        {
            LView_LoadedOverview.ItemsSource = null;

            _overview.Clear();
            _overview.Add(new DirectoryItem("Events Summary", AllWtrView.Count + AllPowView.Count));
            _overview.Add(new DirectoryItem("Wind Speeds: Low", LoSpdViews.Count));
            _overview.Add(new DirectoryItem("Wind Speeds: High", HiSpdViews.Count));
            _overview.Add(new DirectoryItem("Power Prod: None", NoPowViews.Count));
            _overview.Add(new DirectoryItem("Power Prod: " + Common.GetStringDecimals(analyser.RatedPwr / 1000.0, 1) + "MW", RtdPowView.Count));

            LView_LoadedOverview.ItemsSource = _overview;
        }

        void EventsSummary()
        {
            LView_EventsSumPwrNone.ItemsSource = null;
            LView_EventsSumPwrHigh.ItemsSource = null;
            LView_EventsSumWndLows.ItemsSource = null;
            LView_EventsSumWndHigh.ItemsSource = null;

            ObservableCollection<Analysis.StructureSmry> sumEvents = new ObservableCollection<Analysis.StructureSmry>(analyser.Summary());

            LView_EventsSumPwrNone.ItemsSource = sumEvents;
            LView_EventsSumPwrHigh.ItemsSource = sumEvents;
            LView_EventsSumWndLows.ItemsSource = sumEvents;
            LView_EventsSumWndHigh.ItemsSource = sumEvents;
        }

        void PopulateOverview()
        {
            if (meteoFile.MetMasts.Count != 0)
            {
                for (int i = 0; i < meteoFile.MetMasts.Count; i++)
                {
                    if (!loadedAsset.Contains(meteoFile.MetMasts[i].UnitID))
                    {
                        analyser.AssetList.Add((Structure)meteoFile.MetMasts[i]);

                        loadedAsset.Add(meteoFile.MetMasts[i].UnitID);
                    }
                    else
                    {
                        int index = analyser.AssetList.IndexOf
                            (analyser.AssetList.Where(x => x.UnitID == meteoFile.MetMasts[i].UnitID).FirstOrDefault());

                        analyser.AssetList[index].CheckDataSeriesTimes(meteoFile.MetMasts[i]);
                    }
                }
            }

            if (scadaFile.WindFarm.Count != 0)
            {
                for (int i = 0; i < scadaFile.WindFarm.Count; i++)
                {
                    if (!loadedAsset.Contains(scadaFile.WindFarm[i].UnitID))
                    {
                        analyser.AssetList.Add((Structure)scadaFile.WindFarm[i]);

                        loadedAsset.Add(scadaFile.WindFarm[i].UnitID);
                    }
                    else
                    {
                        int index = analyser.AssetList.IndexOf
                            (analyser.AssetList.Where(x => x.UnitID == scadaFile.WindFarm[i].UnitID).FirstOrDefault());

                        analyser.AssetList[index].CheckDataSeriesTimes(scadaFile.WindFarm[i]);
                    }
                }
            }

            LView_Overview.ItemsSource = AssetsView;
            LView_Overview.Items.Refresh();

            LView_Overview.IsEnabled = AssetsView != null && AssetsView.Count > 0 ? true : false;

            if (AssetsView != null && AssetsView.Count > 0)
            {
                _dataExportStart = AssetsView[0].StartTime;
                _dataExportEndTm = AssetsView[0].EndTime;

                // i is 1 below because the first values have already been assigned by the above code
                for (int i = 1; i < AssetsView.Count; i++)
                {
                    if (AssetsView[i].StartTime < _dataExportStart) { _dataExportStart = AssetsView[i].StartTime; }
                    if (AssetsView[i].EndTime > _dataExportEndTm) { _dataExportEndTm = AssetsView[i].EndTime; }
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

        void RemoveSingleAsset(int toRemove)
        {
            if (analyser.AssetList.Count != 0)
            {
                for (int i = analyser.AssetList.Count - 1; i >= 0; i--)
                {
                    if (analyser.AssetList[i].UnitID == toRemove)
                    {
                        analyser.AssetList.RemoveAt(i);

                        break;
                    }
                }
            }

            LView_Overview.ItemsSource = AssetsView;
            LView_Overview.Items.Refresh();
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

            if (LView_Overview.SelectedItems.Count == 1)
            {
                menu = new ContextMenu();

                MenuItem removeAsset_MenuItem = new MenuItem();
                removeAsset_MenuItem.Header = "Remove Asset";
                removeAsset_MenuItem.Click += RemoveAsset_MenuItem_Click;
                menu.Items.Add(removeAsset_MenuItem);
            }

            LView_Overview.ContextMenu = menu;
        }

        private void RemoveAsset_MenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (LView_Overview.SelectedItems.Count == 1)
            {
                Structure struc = (Structure)LView_Overview.SelectedItem;

                if (struc.Type == BaseStructure.Types.METMAST)
                {
                    int index = meteoFile.MetMasts.FindIndex(x => x.UnitID == struc.UnitID);
                    meteoFile.MetMasts.RemoveAt(index);
                    meteoFile.InclMetm.Remove(struc.UnitID);
                }
                else if (struc.Type == BaseStructure.Types.TURBINE)
                {
                    int index = scadaFile.WindFarm.FindIndex(x => x.UnitID == struc.UnitID);
                    scadaFile.WindFarm.RemoveAt(index);
                    scadaFile.InclTrbn.Remove(struc.UnitID);
                }

                loadedAsset.Remove(struc.UnitID);
                RemoveSingleAsset(struc.UnitID);
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
                MenuItem removeEvent_MenuItem = new MenuItem();
                removeEvent_MenuItem.Header = "Remove Event";
                removeEvent_MenuItem.Click += RemoveEvent_MenuItem_Click;
                menu.Items.Add(removeEvent_MenuItem);
            }

            if (LView_PowrNone.SelectedItems.Count == 1)
            {
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
                if (LView_PowrNone.SelectedItems.Count == 1 || LView_PowrRted.SelectedItems.Count == 1)
                {
                    EventData thisEv = LView_PowrNone.SelectedItems.Count == 1 ?
                        (EventData)LView_PowrNone.SelectedItem : (EventData)LView_PowrRted.SelectedItem;

                    // do something part of the method
                    // this one needs to take the event details and send it to another listbox plus graph

                    // thisEvent has the event data only -- for the actual data to display, we'll need to find 
                    // the datapoints from the source data

                    List<ScadaData.ScadaSample> thisEvScada = new List<ScadaData.ScadaSample>();
                    List<ScadaData.ScadaSample> weekHistory = new List<ScadaData.ScadaSample>();
                    List<ScadaData.ScadaSample> dataHistory = new List<ScadaData.ScadaSample>();

                    // get index of the asset and get index of the event time in the asset
                    // the index of the asset to be used below
                    int assetIndex = scadaFile.WindFarm.FindIndex(x => x.UnitID == thisEv.FromAsset);
                    // the index of the timestamp a week before the event began or otherwise the first timestamp in the series - long conditional but should work
                    TimeSpan stepBack = new TimeSpan(0, -60 * 24 * 7, 0);
                    int weekIndex = scadaFile.WindFarm[assetIndex].DataSorted.FindIndex(x => x.TimeStamp == thisEv.EvTimes[0].Add(stepBack)) != -1 ? scadaFile.WindFarm[assetIndex].DataSorted.FindIndex(x => x.TimeStamp == thisEv.EvTimes[0].Add(stepBack)) : 0;

                    int timeIndex = scadaFile.WindFarm[assetIndex].DataSorted.FindIndex(x => x.TimeStamp == thisEv.EvTimes[0]);

                    for (int i = 0; i < thisEv.EvTimes.Count; i++)
                    {
                        thisEvScada.Add(scadaFile.WindFarm[assetIndex].DataSorted[timeIndex + i]);
                    }

                    for (int i = weekIndex; i < (timeIndex + thisEv.EvTimes.Count); i++)
                    {
                        weekHistory.Add(scadaFile.WindFarm[assetIndex].DataSorted[i]);
                    }

                    for (int j = 0; j < (timeIndex + thisEv.EvTimes.Count); j++)
                    {
                        dataHistory.Add(scadaFile.WindFarm[assetIndex].DataSorted[j]);
                    }

                    // assign the created dataset lists to their global variables
                    _thisEventData = new ObservableCollection<ScadaData.ScadaSample>(thisEvScada);
                    _weekEventData = new ObservableCollection<ScadaData.ScadaSample>(weekHistory);
                    _histEventData = new ObservableCollection<ScadaData.ScadaSample>(dataHistory);

                    // now sent the thisEvScada to the new ListView to populate it
                    InitializeEventExploration(sender, e);
                }
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
            LView_EventExplorer_Gearbox.ItemsSource = _thisEventData;
            LView_EventExplorer_Generator.ItemsSource = _thisEventData;
            LView_EventExplorer_MainBear.ItemsSource = _thisEventData;

            ChangeListViewDataset(sender, e);
            DisplayCorrectEventDetails();

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
            if (LView_PowrNone.SelectedItems.Count == 1 || LView_PowrRted.SelectedItems.Count == 1)
            {
                EventData _event;

                if (LView_PowrNone.SelectedItems.Count == 1)
                {
                    _event = (EventData)LView_PowrNone.SelectedItem;

                    // find index and removeat that index
                    analyser.NoPwEvents.RemoveAt(analyser.NoPwEvents.FindIndex(x => x.Start == _event.Start));
                }
                else
                {
                    _event = (EventData)LView_PowrRted.SelectedItem;

                    // find index and removeat that index
                    analyser.RtPwEvents.RemoveAt(analyser.RtPwEvents.FindIndex(x => x.Start == _event.Start));
                }

                RefreshEvents();
            }
        }

        #endregion

        #region Support Classes

        #endregion

        #region Properties

        public Analysis Analyser { get { return analyser; } set { analyser = value; } }
        
        public GeoData GeoFile { get { return geoFile; } set { geoFile = value; } }
        public MeteoData MeteoFile { get { return meteoFile; } set { meteoFile = value; } }
        public ScadaData ScadaFile { get { return scadaFile; } set { scadaFile = value; } }

        public bool GeoLoaded { get { return geoLoaded; } set { geoLoaded = value; } }
        
        public bool MeteoLoaded { get { return meteoLoaded; } set { meteoLoaded = value; } }
        public bool ScadaLoaded { get { return scadaLoaded; } set { scadaLoaded = value; } }

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
            get { return new ObservableCollection<Analysis.AnalyticLimit>(analyser.RateChange); }
            set
            {
                if (ThresholdVw != value)
                {
                    ThresholdVw = value;
                    OnPropertyChanged(nameof(ThresholdVw));
                }
            }
        }

        public ObservableCollection<Analysis.AnalyticLimit> ThresholdVw
        {
            get { return new ObservableCollection<Analysis.AnalyticLimit>(analyser.Thresholds); }
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
            get { return new ObservableCollection<EventData>(analyser.RChngEvnts); }
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
            get { return new ObservableCollection<EventData>(analyser.ThresEvnts); }
            set
            {
                if (ThresholdEventsView != value)
                {
                    ThresholdEventsView = value;
                    OnPropertyChanged(nameof(ThresholdEventsView));
                }
            }
        }

        public ObservableCollection<EventData> AllWtrView
        {
            get { return new ObservableCollection<EventData>(analyser.AllWtrEvts); }
            set { analyser.AllWtrEvts = value.ToList(); }
        }

        public ObservableCollection<EventData> LoSpdViews
        {
            get { return new ObservableCollection<EventData>(analyser.LoSpEvents); }
            set { analyser.LoSpEvents = value.ToList(); }
        }

        public ObservableCollection<EventData> HiSpdViews
        {
            get { return new ObservableCollection<EventData>(analyser.HiSpEvents); }
            set { analyser.HiSpEvents = value.ToList(); }
        }

        public ObservableCollection<EventData> NoPowViews
        {
            get { return new ObservableCollection<EventData>(analyser.NoPwEvents); }
            set { analyser.NoPwEvents = value.ToList(); }
        }

        public ObservableCollection<EventData> AllPowView
        {
            get { return new ObservableCollection<EventData>(analyser.AllPwrEvts); }
            set { analyser.AllPwrEvts = value.ToList(); }
        }

        public ObservableCollection<EventData> RtdPowView
        {
            get { return new ObservableCollection<EventData>(analyser.RtPwEvents); }
            set { analyser.RtPwEvents = value.ToList(); }
        }

        public ObservableCollection<Structure> AssetsView
        {
            get { return _assetsVw = new ObservableCollection<Structure>(analyser.AssetList); }
            set
            {
                if (_assetsVw != value)
                {
                    _assetsVw = value;
                    OnPropertyChanged(nameof(analyser.AssetList));
                }
            }
        }

        public ObservableCollection<Structure> ThrsEventData
        {
            get { return _assetsVw = new ObservableCollection<Structure>(analyser.AssetList); }
            set
            {
                if (_assetsVw != value)
                {
                    _assetsVw = value;
                    OnPropertyChanged(nameof(analyser.AssetList));
                }
            }
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

    public class WrongDateTimeException : Exception { }

    public class WrongFileTypeException : Exception { }

#endregion
}
