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

using MahApps.Metro.Controls;

using scada_analyst.Shared;

namespace scada_analyst
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow, INotifyPropertyChanged
    {
        #region Variables

        private bool geoLoaded = false;
        private bool meteoLoaded = false;
        private bool posnsCombnd = false;
        private bool scadaLoaded = false;

        private bool exportPowMaxm = false, exportAmbMaxm = false, exportWSpMaxm = false;
        private bool exportPowMinm = false, exportAmbMinm = false, exportWSpMinm = false;
        private bool exportPowMean = false, exportAmbMean = false, exportWSpMean = false;
        private bool exportPowStdv = false, exportAmbStdv = false, exportWSpStdv = false;

        private bool exportGBxMaxm = false, exportGenMaxm = false, exportMBrMaxm = false;
        private bool exportGBxMinm = false, exportGenMinm = false, exportMBrMinm = false;
        private bool exportGBxMean = false, exportGenMean = false, exportMBrMean = false;
        private bool exportGBxStdv = false, exportGenStdv = false, exportMBrStdv = false;

        private double cutIn = 4, cutOut = 25, powerLim = 0;

        private List<int> loadedAsset = new List<int>();
        private List<string> loadedFiles = new List<string>();

        private CancellationTokenSource cts;
        private DateTime expStart = new DateTime();
        private DateTime expEnd = new DateTime();
        private TimeSpan duratFilter = new TimeSpan(0,10,0);

        private GeoData geoFile;
        private MeteoData meteoFile = new MeteoData();
        private ScadaData scadaFile = new ScadaData();

        private List<DataOverview> overview = new List<DataOverview>();

        private ObservableCollection<Event> allEvents = new ObservableCollection<Event>();
        private ObservableCollection<Event> loSpEvents = new ObservableCollection<Event>();
        private ObservableCollection<Event> hiSpEvents = new ObservableCollection<Event>();
        private ObservableCollection<Event> noPwEvents = new ObservableCollection<Event>();
        private ObservableCollection<Event> alPwEvents = new ObservableCollection<Event>();

        private ObservableCollection<Structure> assetList = new ObservableCollection<Structure>();

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        public MainWindow()
        {
            InitializeComponent();

            progress_ProgressBar.Visibility = Visibility.Collapsed;
            label_ProgressBar.Visibility = Visibility.Collapsed;
            cancel_ProgressBar.Visibility = Visibility.Collapsed;
            //counter_ProgressBar.Visibility = Visibility.Collapsed;

            LView_Overview.IsEnabled = false;

            LView_WSpdEvLo.IsEnabled = false;
            LView_WSpdEvHi.IsEnabled = false;
            LView_PowrNone.IsEnabled = false;

            BTN_ProcessFilter.IsEnabled = false;
            LBL_DurationFilter.Content = duratFilter.ToString();

            CreateDataOverview();
        }

        private void AboutClick(object sender, RoutedEventArgs e)
        {
            new Window_About(this).ShowDialog();
        }

        private void ClearAllData(object sender, RoutedEventArgs e)
        {
            ClearGeoData(sender, e);
            ClearMeteoData(sender, e);
            ClearScadaData(sender, e);

            ClearEvents(sender, e);

            expStart = new DateTime();
            expEnd = new DateTime();
        }
        
        private void ClearEvents(object sender, RoutedEventArgs e)
        {
            allEvents = new ObservableCollection<Event>();
            loSpEvents = new ObservableCollection<Event>();
            hiSpEvents = new ObservableCollection<Event>();
            noPwEvents = new ObservableCollection<Event>();

            LView_PowrNone.ItemsSource = null;
            LView_PowrNone.IsEnabled = false;

            LView_WSpdEvLo.ItemsSource = null;
            LView_WSpdEvLo.IsEnabled = false;

            LView_WSpdEvHi.ItemsSource = null;
            LView_WSpdEvHi.IsEnabled = false;

            UpdateDataOverview();
        }

        private void ClearGeoData(object sender, RoutedEventArgs e)
        {
            geoFile = null; geoLoaded = false;

            StructureLocations();

            UpdateDataOverview();
        }

        private void ClearMeteoData(object sender, RoutedEventArgs e)
        {
            for (int i = assetList.Count - 1; i >= 0; i--)
            {
                if (assetList[i].Type == BaseStructure.Types.METMAST)
                {
                    loadedAsset.Remove(assetList[i].UnitID);
                    assetList.RemoveAt(i);
                }
            }

            meteoFile = null; meteoLoaded = false;

            meteoFile = new MeteoData();

            StructureLocations();

            UpdateDataOverview();
        }

        private void ClearScadaData(object sender, RoutedEventArgs e)
        {
            for (int i = assetList.Count - 1; i >= 0; i--)
            {
                if (assetList[i].Type == BaseStructure.Types.TURBINE)
                {
                    loadedAsset.Remove(assetList[i].UnitID);
                    assetList.RemoveAt(i);
                }
            }

            scadaFile = null; scadaLoaded = false;

            scadaFile = new ScadaData();

            StructureLocations();

            UpdateDataOverview();
        }

        private void CreateEventAssociations(IProgress<int> progress)
        {
            try
            {
                int count = 0;

                var currentEvents = noPwEvents;

                this.NoPwEvents = null;

                for (int i = 0; i < currentEvents.Count; i++)
                {
                    bool goIntoHiSpEvents = true;

                    for (int j = 0; j < loSpEvents.Count; j++)
                    {
                        if (currentEvents[i].EvTimes.Intersect(loSpEvents[j].EvTimes).Any())
                        {
                            currentEvents[i].AssocEv = Event.Association.LO_SP;

                            goIntoHiSpEvents = false;
                            break;
                        }
                    }

                    if (goIntoHiSpEvents)
                    {
                        for (int k = 0; k < hiSpEvents.Count; k++)
                        {
                            if (currentEvents[i].EvTimes.Intersect(hiSpEvents[k].EvTimes).Any())
                            {
                                currentEvents[i].AssocEv = Event.Association.HI_SP;

                                break;
                            }
                        }
                    }

                    if (currentEvents[i].AssocEv == Event.Association.NONE)
                    { currentEvents[i].AssocEv = Event.Association.OTHER; }

                    count++;

                    if (count % 10 == 0)
                    {
                        if (progress != null)
                        {
                            progress.Report((int)(0.5 * i / currentEvents.Count * 100.0));
                        }
                    }
                }

                this.NoPwEvents = currentEvents;
            }
            catch
            {
                throw;
            }
        }

        private void EditDurationFilter(object sender, RoutedEventArgs e)
        {
            Window_NumberTwo getTimeDur = new Window_NumberTwo(this, "Duration Filter Settings",
                "Hours", "Minutes", false, false, duratFilter.TotalHours, duratFilter.Minutes);

            if (getTimeDur.ShowDialog().Value)
            {
                duratFilter = new TimeSpan((int)getTimeDur.NumericValue1, (int)getTimeDur.NumericValue2, 0);
                LBL_DurationFilter.Content = duratFilter.ToString();
            }
        }

        private void Exit(object sender, RoutedEventArgs e)
        {
            this.Close();            
        }

        private async void ExportMeteoDataAsync(object sender, RoutedEventArgs e)
        {
            cts = new CancellationTokenSource();
            var token = cts.Token;

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
                            Window_CalendarChooser startCal = new Window_CalendarChooser(this, "Choose export start date", expStart);
                            Window_CalendarChooser endCal = new Window_CalendarChooser(this, "Choose export end date", expEnd);

                            if (startCal.ShowDialog().Value)
                            {
                                expStart = Common.StringToDateTime(startCal.TextBox_Calendar.Text, false);
                            }

                            if (endCal.ShowDialog().Value)
                            {
                                expEnd = Common.StringToDateTime(endCal.TextBox_Calendar.Text, false);
                            }
                        }

                        await Task.Run(() => meteoFile.ExportFiles(progress, saveFileDialog.FileName,expStart,expEnd));

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

        private async void ExportScadaDataAsync(object sender, RoutedEventArgs e)
        {
            cts = new CancellationTokenSource();
            var token = cts.Token;

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
                            Window_CalendarChooser startCal = new Window_CalendarChooser(this, "Choose export start date", expStart);
                            Window_CalendarChooser endCal = new Window_CalendarChooser(this, "Choose export end date", expEnd);

                            if (startCal.ShowDialog().Value)
                            {
                                expStart = Common.StringToDateTime(startCal.TextBox_Calendar.Text, false);
                            }

                            if (endCal.ShowDialog().Value)
                            {
                                expEnd = Common.StringToDateTime(endCal.TextBox_Calendar.Text, false);
                            }
                        }

                        await Task.Run(() => scadaFile.ExportFiles(progress, saveFileDialog.FileName,
                            exportPowMaxm, exportPowMinm, exportPowMean, exportPowStdv,
                            exportAmbMaxm, exportAmbMinm, exportAmbMean, exportAmbStdv,
                            exportWSpMaxm, exportWSpMinm, exportWSpMean, exportWSpStdv,
                            exportGBxMaxm, exportGBxMinm, exportGBxMean, exportGBxStdv,
                            exportGenMaxm, exportGenMinm, exportGenMean, exportGenStdv,
                            exportMBrMaxm, exportMBrMinm, exportMBrMean, exportMBrStdv,
                            expStart, expEnd));

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

        private void FindEvents(IProgress<int> progress)
        {
            // all of the find events methods follow a similar methodology
            //
            // firstly the full set of applicable data is taken to investigate
            // the criteria these are tested against are defined based on wind speed or power output
            // and a certain number of if-conditions need to be passed for the testing to take place
            //
            // namely whether the dataset is continuous in time, whether it doesn't end with the file,
            // and whether all of the tested samples meet the same conditions

            try
            {
                FindNoPowerEvents(progress);
                FindWeatherFromMeteo(progress);
                FindWeatherFromScada(progress);
            }
            catch
            {
                throw;
            }
        }

        private async void FindEventsAsync(object sender, RoutedEventArgs e)
        {
            cts = new CancellationTokenSource();
            var token = cts.Token;

            var progress = new Progress<int>(value =>
            {
                UpdateProgress(value);
            });

            try
            {
                ProgressBarVisible();

                ClearEvents(sender, e);

                await Task.Run(() => FindEvents(progress));

                ProgressBarInvisible();

                RefreshEvents();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.GetType().Name + ": " + ex.Message);
            }
        }

        private void FindNoPowerEvents(IProgress<int> progress)
        {
            // this method investigates all loaded scada files for low power (defined as below 0)
            // times in order to determine when the turbine may have been inactive --
            // which will be carried out in a different method later on

            // purpose of this is to find all suitable events

            try
            {
                if (scadaFile.WindFarm != null)
                {
                    int count = 0;

                    for (int i = 0; i < scadaFile.WindFarm.Count; i++)
                    {
                        for (int j = 0; j < scadaFile.WindFarm[i].DataSorted.Count; j++)
                        {
                            if (scadaFile.WindFarm[i].DataSorted[j].Powers.Mean < powerLim &&
                                scadaFile.WindFarm[i].DataSorted[j].Powers.Mean != -9999)
                            {
                                List<ScadaData.ScadaSample> thisEvent = new List<ScadaData.ScadaSample>();
                                thisEvent.Add(scadaFile.WindFarm[i].DataSorted[j]);

                                for (int k = j + 1; k < scadaFile.WindFarm[i].DataSorted.Count; k++)
                                {
                                    if (scadaFile.WindFarm[i].DataSorted[k].DeltaTime > new TimeSpan(0, 10, 0))
                                    {
                                        j = k; break;
                                    }

                                    if (scadaFile.WindFarm[i].DataSorted[k].Powers.Mean > powerLim)
                                    {
                                        j = k; break;
                                    }
                                    else if (k == scadaFile.WindFarm[i].DataSorted.Count - 1) { j = k; }

                                    thisEvent.Add(scadaFile.WindFarm[i].DataSorted[k]);
                                }

                                noPwEvents.Add(new Event(thisEvent));
                            }

                            count++;

                            if (count % 1000 == 0)
                            {
                                if (progress != null)
                                {
                                    progress.Report((int)(((double)i / scadaFile.WindFarm.Count +
                                        (double)j / scadaFile.WindFarm[i].DataSorted.Count / scadaFile.WindFarm.Count) * 100));
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                throw;
            }
        }

        private void FindWeatherFromMeteo(IProgress<int> progress)
        {
            // this method investigates all loaded meteorologic files for low and high wind speed
            // times in order to determine when the turbine may have been inactive due to that --
            // which will be carried out in a different method later on

            // purpose of this is to find all suitable events

            try
            {
                if (meteoFile.MetMasts != null)
                {
                    int count = 0;

                    for (int i = 0; i < meteoFile.MetMasts.Count; i++)
                    {
                        for (int j = 0; j < meteoFile.MetMasts[i].MetDataSorted.Count; j++)
                        {
                            if (meteoFile.MetMasts[i].MetDataSorted[j].WSpdR.Mean < cutIn &&
                                meteoFile.MetMasts[i].MetDataSorted[j].WSpdR.Mean >= 0)
                            {
                                List<MeteoData.MeteoSample> thisEvent = new List<MeteoData.MeteoSample>();
                                thisEvent.Add(meteoFile.MetMasts[i].MetDataSorted[j]);

                                for (int k = j + 1; k < meteoFile.MetMasts[i].MetDataSorted.Count; k++)
                                {
                                    if (meteoFile.MetMasts[i].MetDataSorted[k].DeltaTime > new TimeSpan(0, 10, 0))
                                    {
                                        j = k; break;
                                    }

                                    if (meteoFile.MetMasts[i].MetDataSorted[k].WSpdR.Mean > cutIn)
                                    {
                                        j = k; break;
                                    }
                                    else if (k == meteoFile.MetMasts[i].MetDataSorted.Count - 1) { j = k; }

                                    thisEvent.Add(meteoFile.MetMasts[i].MetDataSorted[k]);
                                }

                                loSpEvents.Add(new Event(thisEvent, Event.WeatherType.LOW_SP));
                                allEvents.Add(new Event(thisEvent, Event.WeatherType.LOW_SP));
                            }
                            else if (meteoFile.MetMasts[i].MetDataSorted[j].WSpdR.Mean > cutOut)
                            {
                                List<MeteoData.MeteoSample> thisEvent = new List<MeteoData.MeteoSample>();
                                thisEvent.Add(meteoFile.MetMasts[i].MetDataSorted[j]);

                                for (int k = j + 1; k < meteoFile.MetMasts[i].MetDataSorted.Count; k++)
                                {
                                    if (meteoFile.MetMasts[i].MetDataSorted[k].DeltaTime > new TimeSpan(0, 10, 0))
                                    {
                                        j = k; break;
                                    }

                                    if (meteoFile.MetMasts[i].MetDataSorted[k].WSpdR.Mean < cutOut)
                                    {
                                        j = k; break;
                                    }
                                    else if (k == meteoFile.MetMasts[i].MetDataSorted.Count - 1) { j = k; }

                                    thisEvent.Add(meteoFile.MetMasts[i].MetDataSorted[k]);
                                }

                                hiSpEvents.Add(new Event(thisEvent, Event.WeatherType.HI_SPD));
                                allEvents.Add(new Event(thisEvent, Event.WeatherType.HI_SPD));
                            }

                            count++;

                            if (count % 1000 == 0)
                            {
                                if (progress != null)
                                {
                                    progress.Report((int)(((double)i / meteoFile.MetMasts.Count +
                                        (double)j / meteoFile.MetMasts[i].MetDataSorted.Count / meteoFile.MetMasts.Count) * 100));
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                throw;
            }
        }

        private void FindWeatherFromScada(IProgress<int> progress)
        {
            // this method investigates all loaded scada files for low and high wind speed
            // times in order to determine when the turbine may have been inactive due to that --
            // which will be carried out in a different method later on

            // purpose of this is to find all suitable events

            try
            {
                if (scadaFile.WindFarm != null)
                {
                    int count = 0;

                    for (int i = 0; i < scadaFile.WindFarm.Count; i++)
                    {
                        for (int j = 0; j < scadaFile.WindFarm[i].DataSorted.Count; j++)
                        {
                            if (scadaFile.WindFarm[i].DataSorted[j].AnemoM.ActWinds.Mean < cutIn &&
                                scadaFile.WindFarm[i].DataSorted[j].AnemoM.ActWinds.Mean >= 0)
                            {
                                List<ScadaData.ScadaSample> thisEvent = new List<ScadaData.ScadaSample>();
                                thisEvent.Add(scadaFile.WindFarm[i].DataSorted[j]);

                                for (int k = j + 1; k < scadaFile.WindFarm[i].DataSorted.Count; k++)
                                {
                                    if (scadaFile.WindFarm[i].DataSorted[k].DeltaTime > new TimeSpan(0, 10, 0))
                                    {
                                        j = k; break;
                                    }

                                    if (scadaFile.WindFarm[i].DataSorted[k].AnemoM.ActWinds.Mean > cutIn)
                                    {
                                        j = k; break;
                                    }
                                    else if (k == scadaFile.WindFarm[i].DataSorted.Count - 1) { j = k; }

                                    thisEvent.Add(scadaFile.WindFarm[i].DataSorted[k]);
                                }

                                loSpEvents.Add(new Event(thisEvent, Event.WeatherType.LOW_SP));
                                allEvents.Add(new Event(thisEvent, Event.WeatherType.LOW_SP));
                            }
                            else if (scadaFile.WindFarm[i].DataSorted[j].AnemoM.ActWinds.Mean > cutOut)
                            {
                                List<ScadaData.ScadaSample> thisEvent = new List<ScadaData.ScadaSample>();
                                thisEvent.Add(scadaFile.WindFarm[i].DataSorted[j]);

                                for (int k = j + 1; k < scadaFile.WindFarm[i].DataSorted.Count; k++)
                                {
                                    if (scadaFile.WindFarm[i].DataSorted[k].DeltaTime > new TimeSpan(0, 10, 0))
                                    {
                                        j = k; break;
                                    }

                                    if (scadaFile.WindFarm[i].DataSorted[k].AnemoM.ActWinds.Mean < cutOut)
                                    {
                                        j = k; break;
                                    }
                                    else if (k == scadaFile.WindFarm[i].DataSorted.Count - 1) { j = k; }

                                    thisEvent.Add(scadaFile.WindFarm[i].DataSorted[k]);
                                }

                                hiSpEvents.Add(new Event(thisEvent, Event.WeatherType.HI_SPD));
                                allEvents.Add(new Event(thisEvent, Event.WeatherType.HI_SPD));
                            }

                            count++;

                            if (count % 1000 == 0)
                            {
                                if (progress != null)
                                {
                                    progress.Report((int)(((double)i / scadaFile.WindFarm.Count +
                                        (double)j / scadaFile.WindFarm[i].DataSorted.Count / scadaFile.WindFarm.Count) * 100));
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                throw;
            }
        }

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

                geoLoaded = true;
            }
            catch
            {
                throw;
            }
        }

        private async void LoadGeoDataAsync(object sender, RoutedEventArgs e)
        {
            cts = new CancellationTokenSource();
            var token = cts.Token;

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

        private async void LoadMeteoDataAsync(object sender, RoutedEventArgs e)
        {
            cts = new CancellationTokenSource();
            var token = cts.Token;

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

        private void LoadScadaData(ScadaData existingData, string[] filenames, bool isLoaded, IProgress<int> progress)
        {
            try
            {
                ScadaData analysis = new ScadaData(existingData);

                if (!isLoaded)
                {
                    analysis = new ScadaData(filenames, progress);
                }
                else
                {
                    analysis.AppendFiles(filenames, progress);
                }

                scadaFile = analysis;
                scadaLoaded = true;
            }
            catch
            {
                throw;
            }
        }

        private async void LoadScadaDataAsync(object sender, RoutedEventArgs e)
        {
            cts = new CancellationTokenSource();
            var token = cts.Token;    

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
                    
                    await Task.Run(() => LoadScadaData(scadaFile, openFileDialog.FileNames, scadaLoaded, progress));

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

        private void MatchEvents(IProgress<int> progress)
        {
            try
            {
                CreateEventAssociations(progress);

                foreach (Event singleEvent in noPwEvents)
                {
                    alPwEvents.Add(singleEvent);
                }

                RemovedMatchedEvents(progress);
            }
            catch
            {
                throw;
            }
        }

        private async void MatchEventsAsync(object sender, RoutedEventArgs e)
        {
            cts = new CancellationTokenSource();
            var token = cts.Token;

            var progress = new Progress<int>(value =>
            {
                UpdateProgress(value);
            });

            try
            {
                ProgressBarVisible();

                LView_PowrNone.ItemsSource = null;

                await Task.Run(() => MatchEvents(progress));

                ProgressBarInvisible();

                RefreshEvents();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.GetType().Name + ": " + ex.Message);
            }
        }

        private void ProcessDurationFilter(IProgress<int> progress)
        {
            try
            {
                int count = 0;

                var currentEvents = noPwEvents;

                this.NoPwEvents = null;

                for (int i = currentEvents.Count - 1; i >= 0; i--)
                {
                    if (currentEvents[i].Durat < duratFilter)
                    {
                        App.Current.Dispatcher.Invoke((Action)delegate // 
                        {
                            currentEvents.RemoveAt(i);
                        });
                    }

                    count++;

                    if (count % 10 == 0)
                    {
                        if (progress != null)
                        {
                            progress.Report((int)(i / currentEvents.Count * 100.0));
                        }
                    }
                }

                this.NoPwEvents = currentEvents;
            }
            catch
            {
                throw;
            }
        }

        private async void ProcessDurationFilterAsync(object sender, RoutedEventArgs e)
        {
            cts = new CancellationTokenSource();
            var token = cts.Token;

            var progress = new Progress<int>(value =>
            {
                UpdateProgress(value);
            });

            try
            {
                if (duratFilter.TotalSeconds != 0)
                {
                    ProgressBarVisible();
                    
                    await Task.Run(() => ProcessDurationFilter(progress));

                    ProgressBarInvisible();

                    RefreshEvents();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.GetType().Name + ": " + ex.Message);
            }
        }

        private void RemovedMatchedEvents(IProgress<int> progress)
        {
            try
            {
                int count = 0;

                var currentEvents = noPwEvents;

                this.NoPwEvents = null;

                for (int i = currentEvents.Count - 1; i >= 0; i--)
                {
                    if (currentEvents[i].AssocEv == Event.Association.LO_SP ||
                        currentEvents[i].AssocEv == Event.Association.HI_SP)
                    {
                        App.Current.Dispatcher.Invoke((Action)delegate // 
                        {
                            currentEvents.RemoveAt(i);
                        });
                    }

                    count++;

                    if (count % 10 == 0)
                    {
                        if (progress != null)
                        {
                            progress.Report((int)(50 + 0.5 * i / currentEvents.Count * 100.0));
                        }
                    }
                }

                this.NoPwEvents = currentEvents;
            }
            catch { throw; }
        }

        private void ResetPowerEvents(object sender, RoutedEventArgs e)
        {
            noPwEvents.Clear();

            for (int i = 0; i < alPwEvents.Count; i++)
            {
                noPwEvents.Add(alPwEvents[i]);
            }

            duratFilter = new TimeSpan(0, 10, 0);
            LBL_DurationFilter.Content = duratFilter.ToString();

            UpdateDataOverview();
        }

        private void SetAnalysisSets(object sender, RoutedEventArgs e)
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

            Window_AnalysisSettings anaSets = new Window_AnalysisSettings(this, cutIn, cutOut);

            if (anaSets.ShowDialog().Value)
            {
                cutIn = anaSets.SpdIns;
                cutOut = anaSets.SpdOut;
            }
        }
        
        private void SetExportVars(object sender, RoutedEventArgs e)
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

                // these must be used by the async task that doesn't yet exist
                // the async task will lead into the writing method
            }
        }

        private bool StructureLocations()
        {
            if (geoLoaded && (meteoLoaded || scadaLoaded))
            {
                for (int i = 0; i < geoFile.GeoInfo.Count; i++)
                {
                    if (meteoLoaded)
                    {
                        for (int ij = 0; ij < meteoFile.MetMasts.Count; ij++)
                        {
                            if (geoFile.GeoInfo[i].AssetID == meteoFile.MetMasts[ij].UnitID)
                            {
                                meteoFile.MetMasts[ij].Position = geoFile.GeoInfo[i].Position;
                                break;
                            }
                        }
                    }

                    if (scadaLoaded)
                    {
                        for (int ik = 0; ik < scadaFile.WindFarm.Count; ik++)
                        {
                            if (geoFile.GeoInfo[i].AssetID == scadaFile.WindFarm[ik].UnitID)
                            {
                                scadaFile.WindFarm[ik].Position = geoFile.GeoInfo[i].Position;
                                break;
                            }
                        }
                    }
                }

                return posnsCombnd = true;
            }
            else { return posnsCombnd = false; }
        }

        private void StructureLocations(object sender, RoutedEventArgs e)
        {
            StructureLocations();
        }

        #region Background Processes

        void CancelProgress_Click(object sender, RoutedEventArgs e)
        {
            if (cts != null)
            {
                cts.Cancel();

                ProgressBarInvisible();
            }
        }

        void CreateDataOverview()
        {
            overview.Add(new DataOverview("Structures", 0));
            //overview.Add(new DataOverview("Total Events", 0));
            overview.Add(new DataOverview("Low Winds", 0));
            overview.Add(new DataOverview("High Winds", 0));
            overview.Add(new DataOverview("No Power", 0));

            LView_LoadedOverview.ItemsSource = overview;
        }

        void PopulateOverview()
        {
            LView_Overview.IsEnabled = true;

            if (meteoFile.MetMasts.Count != 0)
            {
                for (int i = 0; i < meteoFile.MetMasts.Count; i++)
                {
                    if (!loadedAsset.Contains(meteoFile.MetMasts[i].UnitID))
                    {
                        assetList.Add((Structure)meteoFile.MetMasts[i]);

                        loadedAsset.Add(meteoFile.MetMasts[i].UnitID);
                    }
                }
            }

            if (scadaFile.WindFarm.Count != 0)
            {
                for (int i = 0; i < scadaFile.WindFarm.Count; i++)
                {
                    if (!loadedAsset.Contains(scadaFile.WindFarm[i].UnitID))
                    {
                        assetList.Add((Structure)scadaFile.WindFarm[i]);

                        loadedAsset.Add(scadaFile.WindFarm[i].UnitID);
                    }
                }
            }

            LView_Overview.ItemsSource = assetList;
            LView_Overview.Items.Refresh();

            if (assetList != null && assetList.Count > 0)
            {
                expStart = assetList[0].StartTime;
                expEnd = assetList[0].EndTime;

                for (int i = 1; i < assetList.Count; i++)
                {
                    if (assetList[i].StartTime < expStart) { expStart = assetList[i].StartTime; }
                    if (assetList[i].EndTime > expEnd) { expEnd = assetList[i].EndTime; }
                }

                UpdateDataOverview();
            }
        }

        void ProgressBarVisible()
        {
            progress_ProgressBar.Visibility = Visibility.Visible;
            label_ProgressBar.Visibility = Visibility.Visible;
            cancel_ProgressBar.Visibility = Visibility.Visible;
            //counter_ProgressBar.Visibility = Visibility.Visible;
        }
        
        void ProgressBarInvisible()
        {
            progress_ProgressBar.Visibility = Visibility.Collapsed;
            progress_ProgressBar.Value = 0;

            label_ProgressBar.Visibility = Visibility.Collapsed;
            label_ProgressBar.Content = "";
            cancel_ProgressBar.Visibility = Visibility.Collapsed;

            //counter_ProgressBar.Content = "";
            //counter_ProgressBar.Visibility = Visibility.Collapsed;
        }

        void RefreshEvents()
        {
            if (noPwEvents.Count != 0)
            {
                LView_PowrNone.IsEnabled = true;
                LView_PowrNone.ItemsSource = NoPwEvents;
                LView_PowrNone.Items.Refresh();

                BTN_ProcessFilter.IsEnabled = true;
            }

            if (loSpEvents.Count != 0)
            {
                LView_WSpdEvLo.IsEnabled = true;
                LView_WSpdEvLo.ItemsSource = LoSpEvents;
                LView_WSpdEvLo.Items.Refresh();
            }

            if (hiSpEvents.Count != 0)
            {
                LView_WSpdEvHi.IsEnabled = true;
                LView_WSpdEvHi.ItemsSource = HiSpEvents;
                LView_WSpdEvHi.Items.Refresh();
            }

            UpdateDataOverview();
        }
        
        void RemoveAllAssets()
        {
            LView_Overview.ItemsSource = null;
            LView_Overview.IsEnabled = false;

            assetList.Clear();
        }

        void RemoveSingleAsset(int toRemove)
        {
            if (assetList.Count != 0)
            {
                for (int i = assetList.Count - 1; i >= 0; i--)
                {
                    if (assetList[i].UnitID == toRemove)
                    {
                        App.Current.Dispatcher.Invoke((Action)delegate // 
                        {
                            assetList.RemoveAt(i);
                        });

                        break;
                    }
                }
            }

            LView_Overview.Items.Refresh();
        }

        void OnPropertyChanged(string name)
        {
            var changed = PropertyChanged;
            if (changed != null)
            {
                changed(this, new PropertyChangedEventArgs(name));
            }
        }

        void UpdateDataOverview()
        {
            LView_LoadedOverview.ItemsSource = null;

            overview[0].IntegerData = assetList.Count;
            overview[1].IntegerData = loSpEvents.Count;
            overview[2].IntegerData = hiSpEvents.Count;
            overview[3].IntegerData = noPwEvents.Count;

            LView_LoadedOverview.ItemsSource = overview;
        }

        void UpdateProgress(int value)
        {
            label_ProgressBar.Content = value + "%";
            progress_ProgressBar.Value = value;
        }

        #endregion

        #region ContextMenu

        private void LView_Overview_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SetContextMenuLocationBox();
        }

        private void SetContextMenuLocationBox()
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

        #region Treeview Controls

        private void TWI_Structures_Click(object sender, MouseButtonEventArgs e)
        {
            Tab_Assets.IsSelected = true;
        }

        private void TWI_Ev_LoSp_Click(object sender, MouseButtonEventArgs e)
        {
            Tab_LoWinds.IsSelected = true;
        }

        private void TWI_Ev_HiSp_Click(object sender, MouseButtonEventArgs e)
        {
            Tab_HiWinds.IsSelected = true;
        }

        private void TWI_Ev_NoPw_Click(object sender, MouseButtonEventArgs e)
        {
            Tab_NoPower.IsSelected = true;
        }

        private void TWI_Structures_Click(object sender, RoutedEventArgs e)
        {
            Tab_Assets.IsSelected = true;
        }

        private void TWI_Ev_LoSp_Click(object sender, RoutedEventArgs e)
        {
            Tab_LoWinds.IsSelected = true;
        }

        private void TWI_Ev_HiSp_Click(object sender, RoutedEventArgs e)
        {
            Tab_HiWinds.IsSelected = true;
        }

        private void TWI_Ev_NoPw_Click(object sender, RoutedEventArgs e)
        {
            Tab_NoPower.IsSelected = true;
        }

        #endregion 

        #region Support Classes

        public class DataOverview
        {
            #region Variables

            private int intValue;
            private string strValue;

            #endregion 

            public DataOverview(string strValue, int intValue)
            {
                this.strValue = strValue;
                this.intValue = intValue;
            }

            #region Properties

            public int IntegerData { get { return intValue; } set { intValue = value; } }
            public string StringData { get { return strValue; } set { strValue = value; } }

            #endregion
        }

        public class Event : BaseEvent
        {
            #region Variables

            private double extrmSpd = 0;
            private double minmmPow = 0;

            private TimeSpan sampleLen = new TimeSpan( 0, 9, 59);

            private Association assocEv = Association.NONE;
            private EventSource eSource;
            private NoPowerTime noPowTm;
            private WeatherType weather;        
            
            #endregion

            public Event() { }

            public Event(List<MeteoData.MeteoSample> data, WeatherType input)
            {
                FromAsset = data[0].AssetID;

                Start = data[0].TimeStamp;
                Finit = data[data.Count - 1].TimeStamp.Add(sampleLen);

                Durat = Finit - Start;

                eSource = EventSource.METMAST;
                Type = Types.WEATHER;
                weather = input;
                
                for (int i = 0; i < data.Count; i++)
                {
                    if (input == WeatherType.LOW_SP)
                    {
                        if (i == 0) { extrmSpd = data[i].WSpdR.Mean; }

                        if (data[i].WSpdR.Mean < extrmSpd) { extrmSpd = data[i].WSpdR.Mean; }
                    }
                    else if (input == WeatherType.HI_SPD)
                    {
                        if (i == 0) { extrmSpd = data[i].WSpdR.Mean; }

                        if (data[i].WSpdR.Mean > extrmSpd) { extrmSpd = data[i].WSpdR.Mean; }
                    }

                    EvTimes.Add(data[i].TimeStamp);
                }
            }

            public Event(List<ScadaData.ScadaSample> data)
            {
                FromAsset = data[0].AssetID;

                Start = data[0].TimeStamp;
                Finit = data[data.Count - 1].TimeStamp.Add(sampleLen);

                Durat = Finit - Start;

                eSource = EventSource.TURBINE;
                Type = Types.NOPOWER;

                for (int i = 0; i < data.Count; i++)
                {
                    if (i == 0) { minmmPow = data[i].Powers.Mean; }

                    if (data[i].Powers.Mean < minmmPow) { minmmPow = data[i].Powers.Mean; }

                    EvTimes.Add(data[i].TimeStamp);
                }

                if (Durat.TotalMinutes < 60) { noPowTm = NoPowerTime.DMNS; }
                else if (Durat.TotalMinutes < 60*5) { noPowTm = NoPowerTime.HORS; }
                else if (Durat.TotalMinutes < 60*10) { noPowTm = NoPowerTime.DHRS; }
                else if (Durat.TotalMinutes < 60*24) { noPowTm = NoPowerTime.DAYS; }
            }

            public Event(List<ScadaData.ScadaSample> data, WeatherType input)
            {
                FromAsset = data[0].AssetID;

                Start = data[0].TimeStamp;
                Finit = data[data.Count - 1].TimeStamp.Add(sampleLen);

                Durat = Finit - Start;
                
                eSource = EventSource.TURBINE;
                Type = Types.WEATHER;
                weather = input;

                for (int i = 0; i < data.Count; i++)
                {
                    if (input == WeatherType.LOW_SP)
                    {
                        if (i == 0) { extrmSpd = data[i].AnemoM.ActWinds.Mean; }

                        if (data[i].AnemoM.ActWinds.Mean < extrmSpd) { extrmSpd = data[i].AnemoM.ActWinds.Mean; }
                    }
                    else if (input == WeatherType.HI_SPD)
                    {
                        if (i == 0) { extrmSpd = data[i].AnemoM.ActWinds.Mean; }

                        if (data[i].AnemoM.ActWinds.Mean > extrmSpd) { extrmSpd = data[i].AnemoM.ActWinds.Mean; }
                    }

                    EvTimes.Add(data[i].TimeStamp);
                }
            }

            public enum Association
            {
                // an enum to list whether the power event is associated with a wind 
                // speed event or not -- other for not

                NONE,
                LO_SP,
                HI_SP,
                OTHER
            }

            public enum NoPowerTime
            {
                NONE,
                DMNS, // deciminutes
                HORS, // hours
                DHRS, // decihours
                DAYS // days
            }

            public enum EventSource
            {
                UNKNOWN,
                METMAST,
                TURBINE
            }

            public enum WeatherType
            {
                NORMAL,
                LOW_SP, // below cutin
                HI_SPD  // above cutout
            }

            #region Properties

            public double ExtrmSpd { get { return extrmSpd; } set { extrmSpd = value; } }
            public double MinmmPow { get { return minmmPow; } set { minmmPow = value; } }

            public Association AssocEv { get { return assocEv; } set { assocEv = value; } }
            public EventSource ESource { get { return eSource; } set { eSource = value; } }
            public NoPowerTime NoPowTm { get { return noPowTm; } set { noPowTm = value; } }
            public WeatherType Weather { get { return weather; } set { weather = value; } }

            #endregion
        }

        public class Structure : BaseStructure
        {
            #region Variables

            private DateTime startTime;
            private DateTime endTime;

            #endregion

            public Structure() { }

            private Structure(MeteoData.MetMastData metMast)
            {
                UnitID = metMast.UnitID;
                Type = metMast.Type;

                startTime = GetFirstOrLast(metMast.InclDtTm, true);
                endTime = GetFirstOrLast(metMast.InclDtTm, false);
            }

            private Structure(ScadaData.TurbineData turbine)
            {
                UnitID = turbine.UnitID;
                Type = turbine.Type;

                startTime = GetFirstOrLast(turbine.InclDtTm, true);
                endTime = GetFirstOrLast(turbine.InclDtTm, false);
            }

            private DateTime GetFirstOrLast(List<DateTime> times, bool getFirst)
            {
                DateTime result;

                List<DateTime> sortedTimes = times.OrderBy( s => s).ToList();

                if (getFirst)
                {
                    result = sortedTimes[0];
                }
                else
                {
                    result = sortedTimes[sortedTimes.Count - 1];
                }

                return result;
            }

            public static explicit operator Structure(MeteoData.MetMastData metMast)
            {
                return new Structure(metMast);
            }
            
            public static explicit operator Structure(ScadaData.TurbineData turbine)
            {
                return new Structure(turbine);
            }

            #region Properties

            public DateTime StartTime {  get { return startTime; } set { startTime = value; } }
            public DateTime EndTime { get { return endTime; } set { endTime = value; } }

            #endregion
        }

        #endregion

        #region Properties

        public bool GeoLoaded { get { return geoLoaded; } set { geoLoaded = value; } }
        public bool MeteoLoaded { get { return meteoLoaded; } set { meteoLoaded = value; } }
        public bool PosnsCombnd { get { return posnsCombnd; } set { posnsCombnd = value; } }
        public bool ScadaLoaded { get { return scadaLoaded; } set { scadaLoaded = value; } }

        public double CutIn { get { return cutIn; } set { cutIn = value; } }
        public double CutOut { get { return cutOut; } set { cutOut = value; } }

        public TimeSpan DuratFilter
        {
            get { return duratFilter; }
            set
            {
                if (duratFilter != value)
                {
                    duratFilter = value;
                    OnPropertyChanged("DuratFilter");
                }
            }
        }

        public List<DataOverview> Overview
        {
            get { return overview; }
            set
            {
                if (overview != value)
                {
                    overview = value;
                    OnPropertyChanged("Overview");
                }
            }
        }

        public ObservableCollection<Event> LoSpEvents
        {
            get { return loSpEvents; }
            set
            {
                if (loSpEvents!=value)
                {
                    loSpEvents = value;
                    OnPropertyChanged("LoSpEvents");
                }
            }
        }

        public ObservableCollection<Event> HiSpEvents
        {
            get { return hiSpEvents; }
            set
            {
                if (hiSpEvents != value)
                {
                    hiSpEvents = value;
                    OnPropertyChanged("HiSpEvents");
                }
            }
        }

        public ObservableCollection<Event> NoPwEvents
        {
            get { return noPwEvents; }
            set
            {
                if (noPwEvents != value)
                {
                    noPwEvents = value;
                    OnPropertyChanged("NoPwEvents");
                }
            }
        }

        public ObservableCollection<Structure> AssetList
        {
            get { return assetList; }
            set
            {
                if (assetList != value)
                {
                    assetList = value;
                    OnPropertyChanged("AssetList");
                }
            }
        }

        #endregion

    }

    public class FutureDevelopmentException : Exception { }

    public class LoadingCancelledException : Exception { }

    public class WritingCancelledException : Exception { }

    public class WrongFileTypeException : Exception { }

    public class UnsuitableTargetFileException : Exception { }

    public class NoFilesInFolderException : Exception { }

    public class CancelLoadingException : Exception { }
}
