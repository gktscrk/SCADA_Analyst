using System;
using System.Collections.Generic;
using System.ComponentModel;
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

        private List<string> loadedFiles = new List<string>();

        private BackgroundWorker bgW = null;

        private GeoData geoFile;
        private MeteoData meteoFile = new MeteoData();
        private ScadaData scadaFile = new ScadaData();

        #endregion

        public MainWindow()
        {
            InitializeComponent();

            progress_ProgressBar.Visibility = Visibility.Collapsed;
            label_ProgressBar.Visibility = Visibility.Collapsed;
            cancel_ProgressBar.Visibility = Visibility.Collapsed;
            //counter_ProgressBar.Visibility = Visibility.Collapsed;
        }

        private void AboutClick(object sender, RoutedEventArgs e)
        {
            new Window_About(this).ShowDialog();
        }

        private void CancelProgress_Click(object sender, RoutedEventArgs e)
        {
            if (bgW != null && bgW.IsBusy)
            {
                bgW.CancelAsync();
                TaskCompleted();
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
        }

        private void ClearScadaData(object sender, RoutedEventArgs e)
        {
            scadaFile = null; scadaLoaded = false;

            StructureLocations();
        }

        private void Exit(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void LoadGeo(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Location files (*.csv)|*.csv|All files (*.*)|*.*";
            openFileDialog.Multiselect = true;

            if (openFileDialog.ShowDialog().Value)
            {
                ProgressBarVisible();

                string[] fileList = openFileDialog.FileNames;

                bgW = new BackgroundWorker();

                bgW.WorkerReportsProgress = true;
                bgW.WorkerSupportsCancellation = true;

                bgW.DoWork += BGW_Geog_DoWork;
                bgW.ProgressChanged += BGW_ProgressChanged;
                bgW.RunWorkerCompleted += BGW_Geog_RunWorkerCompleted;

                bgW.RunWorkerAsync(new object[] { fileList });
            }
        }

        private void LoadMet(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Meteorology files (*.csv)|*.csv|All files (*.*)|*.*";
            openFileDialog.Multiselect = true;

            if (openFileDialog.ShowDialog().Value)
            {
                ProgressBarVisible();

                string[] fileList = openFileDialog.FileNames;

                bgW = new BackgroundWorker();

                bgW.WorkerReportsProgress = true;
                bgW.WorkerSupportsCancellation = true;

                bgW.DoWork += BGW_Meteo_DoWork;
                bgW.ProgressChanged += BGW_ProgressChanged;
                bgW.RunWorkerCompleted += BGW_Meteo_RunWorkerCompleted;

                bgW.RunWorkerAsync(new object[] { fileList });
            }
        }

        private void LoadScada(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "SCADA files (*.csv)|*.csv|All files (*.*)|*.*";
            openFileDialog.Multiselect = true;

            if (openFileDialog.ShowDialog().Value)
            {
                ProgressBarVisible();

                string[] fileList = openFileDialog.FileNames;

                bgW = new BackgroundWorker();

                bgW.WorkerReportsProgress = true;
                bgW.WorkerSupportsCancellation = true;

                bgW.DoWork += BGW_Scada_DoWork;
                bgW.ProgressChanged += BGW_ProgressChanged;
                bgW.RunWorkerCompleted += BGW_Scada_RunWorkerCompleted;

                bgW.RunWorkerAsync(new object[] { fileList , scadaFile });
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

        #region BackgroundWorker

        void TaskBegun(BackgroundWorker bgW)
        {
            bgW.ReportProgress(0);
        }

        void ProgressBarVisible()
        {
            progress_ProgressBar.Visibility = Visibility.Visible;
            label_ProgressBar.Visibility = Visibility.Visible;
            cancel_ProgressBar.Visibility = Visibility.Visible;
            //counter_ProgressBar.Visibility = Visibility.Visible;
        }

        void BGW_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            progress_ProgressBar.Value = e.ProgressPercentage;

            label_ProgressBar.Content = String.Format("{0}%", e.ProgressPercentage);
        }

        void TaskCompleted()
        {
            bgW.Dispose();
            bgW = null;

            progress_ProgressBar.Visibility = Visibility.Collapsed;
            progress_ProgressBar.Value = 0;

            label_ProgressBar.Visibility = Visibility.Collapsed;
            label_ProgressBar.Content = "";
            cancel_ProgressBar.Visibility = Visibility.Collapsed;

            //counter_ProgressBar.Content = "";
            //counter_ProgressBar.Visibility = Visibility.Collapsed;
        }

        void BGW_Geog_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker bgW = (BackgroundWorker)sender;

            Object[] args = (Object[])e.Argument;
            string[] filenames = (string[])args[0];

            string errors;

            TaskBegun(bgW);

            try
            {
                for (int i = 0; i < filenames.Length; i++)
                {
                    if (!loadedFiles.Contains(filenames[i]))
                    {
                        GeoData geography = new GeoData(filenames[i], bgW);

                        e.Result = geography;

                        loadedFiles.Add(filenames[i]);
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex is LoadingCancelledException)
                {
                    e.Result = "Loading cancelled by user.";
                }
                else if (ex is WrongFileTypeException)
                {
                    e.Result = "This file cannot be loaded since it is of an incompatible file type for this function.";
                }
                else
                {
                    errors = string.Format("File: {0}\n\nError: {1}", filenames, ex.Message);

                    e.Result = errors;
                }
            }
        }

        void BGW_Meteo_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker bgW = (BackgroundWorker)sender;

            Object[] args = (Object[])e.Argument;
            string[] filenames = (string[])args[0];

            string errors;

            TaskBegun(bgW);

            try
            {
                List<MeteoData> meteoAnalysis = new List<MeteoData>();

                for (int i = 0; i < filenames.Length; i++)
                {
                    if (!loadedFiles.Contains(filenames[i]))
                    {
                        meteoAnalysis.Add(new MeteoData(filenames[i], bgW));

                        loadedFiles.Add(filenames[i]);
                    }
                }

                e.Result = meteoAnalysis;
            }
            catch (Exception ex)
            {
                if (ex is LoadingCancelledException)
                {
                    e.Result = "Loading cancelled by user.";
                }
                else if (ex is WrongFileTypeException)
                {
                    e.Result = "This file cannot be loaded since it is of an incompatible file type for this function.";
                }
                else
                {
                    errors = string.Format("File: {0}\n\nError: {1}", filenames, ex.Message);

                    e.Result = errors;
                }
            }
        }

        void BGW_Scada_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker bgW = (BackgroundWorker)sender;

            Object[] args = (Object[])e.Argument;
            string[] filenames = (string[])args[0];
            ScadaData existingData = (ScadaData)args[1];

            string errors;

            TaskBegun(bgW);

            try
            {
                ScadaData analysis = existingData;

                if (!scadaLoaded)
                {
                    analysis = new ScadaData(filenames, bgW);
                }
                else
                {
                    analysis.AppendFiles(filenames, bgW);
                }

                e.Result = analysis;
            }
            catch (Exception ex)
            {
                if (ex is LoadingCancelledException)
                {
                    e.Result = "Loading cancelled by user.";
                }
                else if (ex is WrongFileTypeException)
                {
                    e.Result = "This file cannot be loaded since it is of an incompatible file type for this function.";
                }
                else
                {
                    errors = string.Format("File: {0}\n\nError: {1}", filenames, ex.Message);

                    e.Result = errors;
                }
            }
        }

        void BGW_Geog_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Result is GeoData)
            {
                geoFile = (GeoData)e.Result;
                geoLoaded = true;
            }
            else
            {
                MessageBox.Show(string.Format("File not loaded:\n\n{0}", (string)e.Result),
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            TaskCompleted();
        }

        void BGW_Meteo_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Result is List<MeteoData>)
            {
                List<MeteoData> temp = (List<MeteoData>)e.Result;
                
                for (int i = 0; i < temp.Count; i++)
                {
                    for (int j = 0; j < temp[i].MetMasts.Count; j++)
                    {
                        meteoFile.MetMasts.Add(temp[i].MetMasts[j]);
                    }
                }

                meteoLoaded = true;
            }
            else
            {
                MessageBox.Show(string.Format("File not loaded:\n\n{0}", (string)e.Result),
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            TaskCompleted();
        }

        void BGW_Scada_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Result is ScadaData)
            {
                scadaFile = (ScadaData)e.Result;

                scadaLoaded = true;
            }
            else
            {
                MessageBox.Show(string.Format("File not loaded:\n\n{0}", (string)e.Result),
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            TaskCompleted();
        }

        #endregion

        #region Support Classes

        public class ProcessList : INotifyPropertyChanged
        {
            private string fileName, displayName;
            private int mergeIndex, mergeGroup, fileIndex, fileID;

            public ProcessList(string filename, bool splitName = false)
            {
                fileName = filename;
                displayName = System.IO.Path.GetFileNameWithoutExtension(filename);

                if (splitName)
                {
                    string[] nameParts = Common.GetSplits(displayName, new char[] { ' ', '-', '_' });

                    fileID = Convert.ToInt16(nameParts[nameParts.Length - 2]);

                    if (Convert.ToInt16(nameParts[nameParts.Length - 1]) < 99)
                    {
                        fileIndex = Convert.ToInt16(nameParts[nameParts.Length - 1]);

                        displayName = displayName.Replace(nameParts[nameParts.Length - 1], "");
                        displayName = displayName.Replace(" - ", "");
                    }
                }

                mergeIndex = -1;
                mergeGroup = -1;
            }

            #region Properties

            public string FileName { get { return fileName; } }
            public string DisplayName { get { return displayName; } set { displayName = value; } }
            public int FileIndex { get { return fileIndex; } set { fileIndex = value; } }
            public int FileID { get { return fileID; } set { fileID = value; } }

            public int MergeIndex
            {
                get { return mergeIndex; }
                set
                {
                    mergeIndex = value;
                    NotifyPropertyChanged("MergeIndex");
                }
            }

            public int MergeGroup
            {
                get { return mergeGroup; }
                set
                {
                    mergeGroup = value;
                    NotifyPropertyChanged("MergeGroup");
                }
            }

            #endregion

            #region Property changed

            public event PropertyChangedEventHandler PropertyChanged;

            protected void NotifyPropertyChanged(string property)
            {
                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs(property));
                }
            }

            #endregion
        }

        #endregion

        #region Properties

        public bool GeoLoaded { get { return geoLoaded; } set { geoLoaded = value; } }
        public bool MeteoLoaded { get { return meteoLoaded; } set { meteoLoaded = value; } }
        public bool PosnsCombnd { get { return posnsCombnd; } set { posnsCombnd = value; } }
        public bool ScadaLoaded { get { return scadaLoaded; } set { scadaLoaded = value; } }

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
