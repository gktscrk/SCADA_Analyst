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

using scada_analyst.Shared;

namespace scada_analyst
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
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

        private float cutIn = 4, cutOut = 25;

        private List<int> loadedAsset = new List<int>();
        private List<string> loadedFiles = new List<string>();

        private CancellationTokenSource cts;

        private GeoData geoFile;
        private MeteoData meteoFile = new MeteoData();
        private ScadaData scadaFile = new ScadaData();

        private ObservableCollection<Event> eventList = new ObservableCollection<Event>();
        private ObservableCollection<Structure> assetList = new ObservableCollection<Structure>();

        #endregion

        public MainWindow()
        {
            InitializeComponent();

            progress_ProgressBar.Visibility = Visibility.Collapsed;
            label_ProgressBar.Visibility = Visibility.Collapsed;
            cancel_ProgressBar.Visibility = Visibility.Collapsed;
            //counter_ProgressBar.Visibility = Visibility.Collapsed;

            LView_Overview.IsEnabled = false;
        }

        private void AboutClick(object sender, RoutedEventArgs e)
        {
            new Window_About(this).ShowDialog();
        }

        private void CancelProgress_Click(object sender, RoutedEventArgs e)
        {
            if (cts != null)
            {
                cts.Cancel();

                ProgressBarInvisible();
            }
        }

        private void ClearAllData(object sender, RoutedEventArgs e)
        {
            ClearGeoData(sender, e);
            ClearMeteoData(sender, e);
            ClearScadaData(sender, e);
        }

        private void ClearGeoData(object sender, RoutedEventArgs e)
        {
            geoFile = null; geoLoaded = false;

            StructureLocations();
        }

        private void ClearMeteoData(object sender, RoutedEventArgs e)
        {
            meteoFile = null; meteoLoaded = false;

            StructureLocations();
            UnPopulateOverview();
        }

        private void ClearScadaData(object sender, RoutedEventArgs e)
        {
            scadaFile = null; scadaLoaded = false;

            StructureLocations();
            UnPopulateOverview();
        }

        private void Exit(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private async void ExportMeteoDataAsync(object sender, RoutedEventArgs e)
        {

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
                SaveFileDialog saveFileDialog = new SaveFileDialog();
                // set a default file name
                saveFileDialog.FileName = ".csv";
                // set filters
                saveFileDialog.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*";

                if (saveFileDialog.ShowDialog().Value)
                {
                    ProgressBarVisible();

                    await Task.Run(() => scadaFile.ExportFiles(progress, saveFileDialog.FileName,
                        exportPowMaxm, exportPowMinm, exportPowMean, exportPowStdv,
                        exportAmbMaxm, exportAmbMinm, exportAmbMean, exportAmbStdv,
                        exportWSpMaxm, exportWSpMinm, exportWSpMean, exportWSpStdv,
                        exportGBxMaxm, exportGBxMinm, exportGBxMean, exportGBxStdv,
                        exportGenMaxm, exportGenMinm, exportGenMean, exportGenStdv,
                        exportMBrMaxm, exportMBrMinm, exportMBrMean, exportMBrStdv));

                    ProgressBarInvisible();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.GetType().Name + ": " + ex.Message);
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

                await Task.Run(() => FindEvents(progress));

                ProgressBarInvisible();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.GetType().Name + ": " + ex.Message);
            }
        }

        private void FindEvents(IProgress<int> progress)
        {
            FindFailure(progress);
            FindWeatherMeteo(progress);
            FindWeatherScada(progress);
        }

        private void FindFailure(IProgress<int> progress)
        {
            if (scadaFile.WindFarm != null)
            {

            }
        }

        private void FindWeatherMeteo(IProgress<int> progress)
        {
            if (meteoFile.MetMasts != null)
            {
                int count = 0;

                for (int i = 0; i < meteoFile.MetMasts.Count; i++)
                {
                    for (int j = 0; j < meteoFile.MetMasts[i].MetDataSorted.Count; j++)
                    {
                        if (meteoFile.MetMasts[i].MetDataSorted[j].WSpdR.Mean < cutIn)
                        {
                            List<MeteoData.MeteoSample> thisEvent = new List<MeteoData.MeteoSample>();
                            thisEvent.Add(meteoFile.MetMasts[i].MetDataSorted[j]);

                            for (int k = j + 1; k < meteoFile.MetMasts[i].MetDataSorted.Count; k++)
                            {
                                if (meteoFile.MetMasts[i].MetDataSorted[k].WSpdR.Mean > cutIn)
                                {
                                    j = k; break;
                                }

                                thisEvent.Add(meteoFile.MetMasts[i].MetDataSorted[k]);
                            }

                            eventList.Add(new Event(thisEvent, Event.WeatherType.LOW_SP));
                        }
                        else if (meteoFile.MetMasts[i].MetDataSorted[j].WSpdR.Mean > cutOut)
                        {
                            List<MeteoData.MeteoSample> thisEvent = new List<MeteoData.MeteoSample>();
                            thisEvent.Add(meteoFile.MetMasts[i].MetDataSorted[j]);

                            for (int k = j + 1; k < meteoFile.MetMasts[i].MetDataSorted.Count; k++)
                            {
                                if (meteoFile.MetMasts[i].MetDataSorted[k].WSpdR.Mean < cutOut)
                                {
                                    j = k; break;
                                }

                                thisEvent.Add(meteoFile.MetMasts[i].MetDataSorted[k]);
                            }

                            eventList.Add(new Event(thisEvent, Event.WeatherType.HI_SPD));
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

        private void FindWeatherScada(IProgress<int> progress)
        {
            if (scadaFile.WindFarm != null)
            {
                int count = 0;

                for (int i = 0; i < scadaFile.WindFarm.Count; i++)
                {
                    for (int j = 0; j < scadaFile.WindFarm[i].DataSorted.Count; j++)
                    {
                        if (scadaFile.WindFarm[i].DataSorted[j].AnemoM.ActWinds.Mean < cutIn)
                        {
                            List<ScadaData.ScadaSample> thisEvent = new List<ScadaData.ScadaSample>();
                            thisEvent.Add(scadaFile.WindFarm[i].DataSorted[j]);

                            for (int k = j + 1; k < scadaFile.WindFarm[i].DataSorted.Count; k++)
                            {
                                if (scadaFile.WindFarm[i].DataSorted[k].AnemoM.ActWinds.Mean > cutIn)
                                {
                                    j = k; break;
                                }

                                thisEvent.Add(scadaFile.WindFarm[i].DataSorted[k]);
                            }
                            
                            eventList.Add(new Event(thisEvent, Event.WeatherType.LOW_SP));
                        }
                        else if (scadaFile.WindFarm[i].DataSorted[j].AnemoM.ActWinds.Mean > cutOut)
                        {
                            List<ScadaData.ScadaSample> thisEvent = new List<ScadaData.ScadaSample>();
                            thisEvent.Add(scadaFile.WindFarm[i].DataSorted[j]);

                            for (int k = j + 1; k < scadaFile.WindFarm[i].DataSorted.Count; k++)
                            {
                                if (scadaFile.WindFarm[i].DataSorted[k].AnemoM.ActWinds.Mean < cutOut)
                                {
                                    j = k; break;
                                }

                                thisEvent.Add(scadaFile.WindFarm[i].DataSorted[k]);
                            }

                            eventList.Add(new Event(thisEvent, Event.WeatherType.HI_SPD));
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

        private async void LoadGeoAsync(object sender, RoutedEventArgs e)
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
                    
                    await Task.Run(() => GeographyLoading(openFileDialog.FileNames, progress));

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

        private async void LoadMetAsync(object sender, RoutedEventArgs e)
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

                    await Task.Run(() => MeteorologyLoading(meteoFile, openFileDialog.FileNames, meteoLoaded,
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

        private async void LoadScadaAsync(object sender, RoutedEventArgs e)
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
                    
                    await Task.Run(() => ScadaLoading(scadaFile, openFileDialog.FileNames, scadaLoaded, progress));

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

        private void PopulateOverview()
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

        private void UnPopulateOverview()
        {
            LView_Overview.ItemsSource = null;
            LView_Overview.IsEnabled = false;
        }

        #region Background Processes
        
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

        private void UpdateProgress(int value)
        {
            label_ProgressBar.Content = value + "%";
            progress_ProgressBar.Value = value;
        }

        private void GeographyLoading(string[] filenames, IProgress<int> progress)
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

        private void MeteorologyLoading(MeteoData existingData, string[] filenames, bool isLoaded, IProgress<int> progress)
        {
            try
            {
                MeteoData analysis = existingData;

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

        private void ScadaLoading(ScadaData existingData, string[] filenames, bool isLoaded, IProgress<int> progress)
        {
            try
            {
                ScadaData analysis = existingData;

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

        #endregion

        #region Support Classes
        
        public class Event : BaseEvent
        {
            #region Variables

            private FailureTime failure;
            private WeatherType weather;

            #endregion

            public Event() { }

            public Event(List<MeteoData.MeteoSample> data, WeatherType input)
            {
                FromAsset = data[0].AssetID;

                Start = data[0].TimeStamp;
                Finit = data[data.Count - 1].TimeStamp;

                Durat = Finit - Start;

                Type = Types.WEATHER;
                weather = input;
            }

            public Event(List<ScadaData.ScadaSample> data, WeatherType input)
            {
                FromAsset = data[0].AssetID;

                Start = data[0].TimeStamp;
                Finit = data[data.Count - 1].TimeStamp;

                Durat = Finit - Start;

                Type = Types.WEATHER;
                weather = input;
            }

            public enum FailureTime
            {
                NONE,
                DMNS, // deciminutes
                HORS, // hours
                DHRS, // decihours
                DAYS // days
            }

            public enum WeatherType
            {
                NORMAL,
                LOW_SP, // below cutin
                HI_SPD  // above cutout
            }

            #region Properties

            public FailureTime Failure { get { return failure; } set { failure = value; } }
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

        public float CutIn { get { return cutIn; } set { cutIn = value; } }
        public float CutOut { get { return cutOut; } set { cutOut = value; } }

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
