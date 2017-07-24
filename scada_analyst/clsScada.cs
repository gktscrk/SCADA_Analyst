using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using scada_analyst.Shared;

namespace scada_analyst
{
    public class ScadaData : BaseMetaData
    {
        #region Variables

        private string outputName;

        private TimeSpan _systemSampleSeparation = new TimeSpan();
        private ScadaHeader fileHeader = new ScadaHeader();
        
        private List<TurbineData> _windFarm = new List<TurbineData>();

        #endregion

        #region Constructor

        public ScadaData() { }

        /// <summary>
        /// This method creates a copy of an existing instance of a ScadaData class
        /// </summary>
        /// <param name="_existingInfo"></param>
        public ScadaData(ScadaData _existingInfo)
        {
            for (int i = 0; i < _existingInfo.WindFarm.Count; i++)
            {
                _windFarm.Add(_existingInfo.WindFarm[i]);
            }

            for (int i = 0; i < _existingInfo.Included.Count; i++)
            {
                Included.Add(_existingInfo.Included[i]);
            }

            for (int i = 0; i < _existingInfo.FileName.Count; i++)
            {
                FileName.Add(_existingInfo.FileName[i]);
            }

            for (int i = 0; i < _existingInfo.Years.Count; i++)
            {
                Years.Add(_existingInfo.Years[i]);
            }
        }

        #endregion

        #region Load Data

        public void AppendFiles(string[] filenames, List<string> loadedFiles, Common.DateFormat _dateFormat, 
            int _singleTurbineLoading, double _rated, TimeSpan _sampleLength, IProgress<int> progress)
        {
            // map the global sample separation to this loading procedure
            _systemSampleSeparation = _sampleLength;

            for (int i = filenames.Length - 1; i >= 0; i--)
            {
                if (loadedFiles.Contains(filenames[i]))
                {
                    filenames = filenames.Where(w => w != filenames[i]).ToArray();
                }
            }

            LoadAndSort(filenames, _dateFormat, _singleTurbineLoading, _rated, progress);
        }

        private void LoadAndSort(string[] filenames, Common.DateFormat _dateFormat, int _singleTurbineLoading, double _rated,
            IProgress<int> progress)
        {
            // load files
            LoadFiles(filenames, _dateFormat, _singleTurbineLoading, _rated, progress);

            // rearrange files by timestamps
            SortScada();
            // add the sample separation field values
            CalculateSampleTimeDifferences();

            // if we want to pad these files, do this here: 
            // padding == adding datapoints to where there previously were none
            // all values will be NaN
            PadSamples();

            // some final calculations need to be done for the windfarm itself
            _windFarm = _windFarm.OrderBy(o => o.UnitID).ToList();
            GetBearings();

            // get capacity factor info
            GetCapacityFactors();
        }
        
        private void LoadFiles(string[] filenames, Common.DateFormat _dateFormat, int _singleTurbineLoading, double _rated,
            IProgress<int> progress)
        {
            for (int i = 0; i < filenames.Length; i++)
            {
                FileName.Add(filenames[i]);
                LoadScada(filenames[i], _dateFormat, _singleTurbineLoading, _rated, progress, filenames.Length, i);
            }
        }

        private void LoadScada(string filename, Common.DateFormat _dateFormat, int _singleTurbineLoading, double _rated,
            IProgress<int> progress, int numberOfFiles = 1, int i = 0)
        {
            using (StreamReader sR = new StreamReader(filename))
            {
                int count = 0;

                try
                {
                    bool readHeader = false;

                    while (!sR.EndOfStream)
                    {
                        if (readHeader == false)
                        {
                            // header information will be used for all concurrent loading
                            string header = sR.ReadLine();
                            header = header.ToLower().Replace("\"", String.Empty);
                            readHeader = true;

                            if (!header.Contains("wtc")) { throw new WrongFileTypeException(); }

                            fileHeader = new ScadaHeader(header);
                        }

                        string line = sR.ReadLine();

                        if (!line.Equals(""))
                        {
                            line = line.Replace("\"", String.Empty);

                            if (line.Contains(",,"))
                            {
                                while (line.Contains(",,")) { line = line.Replace(",,", ",\\N,"); }
                                line = line + "\\N";
                            }

                            string[] splits = Common.GetSplits(line, ',');

                            int thisAsset;

                            // if the file does not have an AssetID column, the Station Column should be used instead
                            if (fileHeader.AssetCol != -1)
                            {
                                thisAsset = Common.CanConvert<int>(splits[fileHeader.AssetCol]) ?
                                  Convert.ToInt32(splits[fileHeader.AssetCol]) : throw new FileFormatException();
                            }
                            else
                            {
                                thisAsset = Common.CanConvert<int>(splits[fileHeader.StatnCol]) ?
                                  Convert.ToInt32(splits[fileHeader.StatnCol]) : throw new FileFormatException();
                            }

                            // add in a section to check whether singleTurbineLoading mode is not -1
                            // if not, and has actual value, load only that turbine: _sTL

                            // organise loading so it would check which ones have already
                            // been loaded; work around the ones have have been and add data there
                            if (_singleTurbineLoading == -1)
                            {
                                if (Included.Contains(thisAsset))
                                {
                                    int index = _windFarm.FindIndex(x => x.UnitID == thisAsset);
                                    _windFarm[index].AddData(splits, fileHeader, _dateFormat);
                                    Years.Add(_windFarm[index].Data[_windFarm[index].Data.Count - 1].TimeStamp.Year);
                                }
                                else
                                {
                                    _windFarm.Add(new TurbineData(splits, fileHeader, _dateFormat, _rated));
                                    Included.Add(_windFarm[_windFarm.Count - 1].UnitID);
                                    Years.Add(_windFarm[_windFarm.Count - 1].Data[_windFarm[_windFarm.Count - 1].Data.Count - 1].TimeStamp.Year);
                                }
                            }
                            else
                            {
                                // if loading only one turbine, the previous conditional is false and program will come in here
                                // after that if the below conditional is true it will load, and if not it will bypass
                                if (thisAsset == _singleTurbineLoading)
                                {
                                    if (Included.Contains(thisAsset))
                                    {
                                        int index = _windFarm.FindIndex(x => x.UnitID == thisAsset);
                                        _windFarm[index].AddData(splits, fileHeader, _dateFormat);
                                        Years.Add(_windFarm[index].Data[_windFarm[index].Data.Count - 1].TimeStamp.Year);
                                    }
                                    else
                                    {
                                        _windFarm.Add(new TurbineData(splits, fileHeader, _dateFormat, _rated));
                                        Included.Add(_windFarm[_windFarm.Count - 1].UnitID);
                                        Years.Add(_windFarm[_windFarm.Count - 1].Data[_windFarm[_windFarm.Count - 1].Data.Count - 1].TimeStamp.Year);
                                    }
                                }
                            }
                        }

                        count++;

                        if (count % 1000 == 0)
                        {
                            if (progress != null)
                            {
                                progress.Report((int)((double)100 / numberOfFiles * i +
                                 (double)sR.BaseStream.Position * 100 / sR.BaseStream.Length / numberOfFiles));
                            }
                        }
                    }

                    Years = Years.Distinct().ToList();
                }
                catch (WrongDateTimeException) { throw; }
                catch
                {
                    count++;
                    throw new Exception("Problem with loading was caused by Line " + count + ".");
                }
                finally
                {
                    sR.Close();
                }
            }
        }

        private void CalculateSampleTimeDifferences()
        {
            // gets all of the inter-sample time differences for the dataset

            // reference point is the next sample, so first one will have no value
            for (int i = 0; i < _windFarm.Count; i++)
            {
                for (int j = 1; j < _windFarm[i].DataSorted.Count; j++)
                {
                    _windFarm[i].DataSorted[j].SampleSeparation = _windFarm[i].DataSorted[j].TimeStamp - _windFarm[i].DataSorted[j - 1].TimeStamp;
                }
            }
        }

        private void GetBearings()
        {
            for (int i = 0; i < _windFarm.Count; i++)
            {
                _windFarm[i].Bearings = new BaseStructure.MetaDataSetup(_windFarm[i], BaseStructure.MetaDataSetup.Mode.BEARINGS);
            }
        }

        private void GetCapacityFactors()
        {
            for (int i = 0; i < _windFarm.Count; i++)
            {
                _windFarm[i].Capacity = new BaseStructure.MetaDataSetup(_windFarm[i], BaseStructure.MetaDataSetup.Mode.CAPACITY);
            }
        }

        private void PadSamples()
        {
            // will add an empty no data sample everywhere where the length of the sample separation
            // is too great
            for (int i = 0; i < _windFarm.Count; i++)
            {
                for (int j = 0; j < _windFarm[i].DataSorted.Count; j++)
                {
                    if (_windFarm[i].DataSorted[j].SampleSeparation > _systemSampleSeparation)
                    {
                        int counter = (int)(_windFarm[i].DataSorted[j].SampleSeparation.TotalMinutes / _systemSampleSeparation.TotalMinutes);

                        for (int k = 1; k < counter; k++)
                        {
                            _windFarm[i].DataSorted.Add(new ScadaSample(_windFarm[i].DataSorted[j], 
                                _windFarm[i].DataSorted[j].TimeStamp.AddMinutes(-k * _systemSampleSeparation.TotalMinutes), false));
                        }
                    }
                }
            }

            SortSortedScada();
        }

        private void SortScada()
        {
            // sorts the data by the timestamp
            for (int i = 0; i < _windFarm.Count; i++)
            {
                _windFarm[i].DataSorted = _windFarm[i].Data.OrderBy(o => o.TimeStamp).ToList();
            }
        }

        private void SortSortedScada()
        {
            // sorts the data by the timestamp
            for (int i = 0; i < _windFarm.Count; i++)
            {
                _windFarm[i].DataSorted = _windFarm[i].DataSorted.OrderBy(o => o.TimeStamp).ToList();
            }
        }

        #endregion

        #region Export Data

        public void ExportFiles(IProgress<int> progress, string output,
            bool exportPowMaxm, bool exportPowMinm, bool exportPowMean, bool exportPowStdv,
            bool exportAmbMaxm, bool exportAmbMinm, bool exportAmbMean, bool exportAmbStdv,
            bool exportWSpMaxm, bool exportWSpMinm, bool exportWSpMean, bool exportWSpStdv,
            bool exportGBxMaxm, bool exportGBxMinm, bool exportGBxMean, bool exportGBxStdv,
            bool exportGenMaxm, bool exportGenMinm, bool exportGenMean, bool exportGenStdv,
            bool exportMBrMaxm, bool exportMBrMinm, bool exportMBrMean, bool exportMBrStdv,
            bool exportNacMaxm, bool exportNacMinm, bool exportNacMean, bool exportNacStdv,
            int oneTurbine, DateTime expStart, DateTime exprtEnd, bool _secondaryData)
        {
            // feed in proper arguments for this output file name and assign these
            outputName = output;

            // write the SCADA file out in a reasonable method
            WriteSCADA(progress,
                exportPowMaxm, exportPowMinm, exportPowMean, exportPowStdv,
                exportAmbMaxm, exportAmbMinm, exportAmbMean, exportAmbStdv,
                exportWSpMaxm, exportWSpMinm, exportWSpMean, exportWSpStdv,
                exportGBxMaxm, exportGBxMinm, exportGBxMean, exportGBxStdv,
                exportGenMaxm, exportGenMinm, exportGenMean, exportGenStdv,
                exportMBrMaxm, exportMBrMinm, exportMBrMean, exportMBrStdv,
                exportNacMaxm, exportNacMinm, exportNacMean, exportNacStdv,
                oneTurbine, expStart, exprtEnd, _secondaryData);
        }

        private void WriteSCADA(IProgress<int> progress,
            bool exportPowMaxm, bool exportPowMinm, bool exportPowMean, bool exportPowStdv,
            bool exportAmbMaxm, bool exportAmbMinm, bool exportAmbMean, bool exportAmbStdv,
            bool exportWSpMaxm, bool exportWSpMinm, bool exportWSpMean, bool exportWSpStdv,
            bool exportGBxMaxm, bool exportGBxMinm, bool exportGBxMean, bool exportGBxStdv,
            bool exportGenMaxm, bool exportGenMinm, bool exportGenMean, bool exportGenStdv,
            bool exportMBrMaxm, bool exportMBrMinm, bool exportMBrMean, bool exportMBrStdv,
            bool exportNacMaxm, bool exportNacMinm, bool exportNacMean, bool exportNacStdv,
            int oneTurbine, DateTime expStart, DateTime exprtEnd, bool _secondaryData)
        {
            using (StreamWriter sW = new StreamWriter(outputName))
            {
                try
                {
                    int count = 0;
                    bool header = false;

                    for (int i = 0; i < _windFarm.Count; i++)
                    {
                        bool _useAssetId = true;
                        if (_windFarm[i].DataSorted[0].AssetID == -1) { _useAssetId = false; }

                        for (int j = 0; j < _windFarm[i].DataSorted.Count; j++)
                        {
                            StringBuilder hB = new StringBuilder();
                            StringBuilder sB = new StringBuilder();

                            ScadaSample unit = _windFarm[i].DataSorted[j];

                            if (unit.TimeStamp >= expStart && unit.TimeStamp <= exprtEnd)
                            {
                                // all header lines are created every time as probably similar
                                // performance to ignoring them with a conditional statement
                                // -1 will represent the full set to be sent in
                                if (oneTurbine != -1)
                                {
                                    if (unit.AssetID == oneTurbine)
                                    { hB.Append("AssetUID" + ","); sB.Append(unit.AssetID + ","); }
                                    else if (unit.StationID == oneTurbine)
                                    { hB.Append("StationId" + ","); sB.Append(unit.StationID + ","); }
                                }
                                else
                                {
                                    if (_useAssetId) { hB.Append("AssetUID" + ","); sB.Append(unit.AssetID + ","); }
                                    else { hB.Append("StationId" + ","); sB.Append(unit.StationID + ","); }
                                }

                                #region Timestamp

                                hB.Append("TimeStamp" + ",");
                                sB.Append(unit.TimeStamp.Year + "-");

                                if (10 <= unit.TimeStamp.Month) { sB.Append(unit.TimeStamp.Month); }
                                else { sB.Append("0"); sB.Append(unit.TimeStamp.Month); }
                                sB.Append("-");

                                if (10 <= unit.TimeStamp.Day) { sB.Append(unit.TimeStamp.Day); }
                                else { sB.Append("0"); sB.Append(unit.TimeStamp.Day); }
                                sB.Append(" ");

                                if (10 <= unit.TimeStamp.Hour) { sB.Append(unit.TimeStamp.Hour); }
                                else { sB.Append("0"); sB.Append(unit.TimeStamp.Hour); }
                                sB.Append(":");

                                if (10 <= unit.TimeStamp.Minute) { sB.Append(unit.TimeStamp.Minute); }
                                else { sB.Append("0"); sB.Append(unit.TimeStamp.Minute); }
                                sB.Append(":");

                                if (10 <= unit.TimeStamp.Second) { sB.Append(unit.TimeStamp.Second + ","); }
                                else { sB.Append("0"); sB.Append(unit.TimeStamp.Second + ","); }

                                #endregion 

                                #region Necessary Data

                                if (exportPowMaxm) { hB.Append("wtc_ActPower_max" + ","); sB.Append(Common.GetStringDecimals(unit.Power.Maxm, 1) + ","); }
                                if (exportPowMinm) { hB.Append("wtc_ActPower_min" + ","); sB.Append(Common.GetStringDecimals(unit.Power.Minm, 1) + ","); }
                                if (exportPowMean) { hB.Append("wtc_ActPower_mean" + ","); sB.Append(Common.GetStringDecimals(unit.Power.Mean, 1) + ","); }
                                if (exportPowStdv) { hB.Append("wtc_ActPower_stddev" + ","); sB.Append(Common.GetStringDecimals(unit.Power.Stdv, 1) + ","); }

                                if (_secondaryData)
                                {
                                    if (exportPowMean) { hB.Append("wtc_ActPower_endvalue" + ","); sB.Append(Common.GetStringDecimals(unit.Power.EndValue, 1) + ","); }
                                    if (exportPowMean) { hB.Append("wtc_ActPower_Quality_endvalue" + ","); sB.Append(Common.GetStringDecimals(unit.Power.QualEndVal, 1) + ","); }
                                }

                                if (exportAmbMaxm) { hB.Append("wtc_AmbieTmp_max" + ","); sB.Append(Common.GetStringDecimals(unit.AmbTemps.Maxm, 1) + ","); }
                                if (exportAmbMinm) { hB.Append("wtc_AmbieTmp_min" + ","); sB.Append(Common.GetStringDecimals(unit.AmbTemps.Minm, 1) + ","); }
                                if (exportAmbMean) { hB.Append("wtc_AmbieTmp_mean" + ","); sB.Append(Common.GetStringDecimals(unit.AmbTemps.Mean, 1) + ","); }
                                if (exportAmbMean) { hB.Append("wtc_AmbieTmp_delta" + ","); sB.Append(Common.GetStringDecimals(unit.AmbTemps.Dlta, 1) + ","); }
                                if (exportAmbStdv) { hB.Append("wtc_AmbieTmp_stddev" + ","); sB.Append(Common.GetStringDecimals(unit.AmbTemps.Stdv, 1) + ","); }

                                if (_secondaryData)
                                {
                                    if (exportAmbMean) { hB.Append("wtc_twrhumid_mean" + ","); sB.Append(Common.GetStringDecimals(unit.Tower.Humid.Mean, 1) + ","); }
                                }

                                if (exportWSpMaxm) { hB.Append("wtc_AcWindSp_max" + ","); sB.Append(Common.GetStringDecimals(unit.Anemo.ActWinds.Maxm, 1) + ","); }
                                if (exportWSpMinm) { hB.Append("wtc_AcWindSp_min" + ","); sB.Append(Common.GetStringDecimals(unit.Anemo.ActWinds.Minm, 1) + ","); }
                                if (exportWSpMean) { hB.Append("wtc_AcWindSp_mean" + ","); sB.Append(Common.GetStringDecimals(unit.Anemo.ActWinds.Mean, 1) + ","); }
                                if (exportWSpStdv) { hB.Append("wtc_AcWindSp_stddev" + ","); sB.Append(Common.GetStringDecimals(unit.Anemo.ActWinds.Stdv,1) + ","); }

                                if (_secondaryData)
                                {
                                    if (exportWSpMaxm) { hB.Append("wtc_PrWindSp_max" + ","); sB.Append(Common.GetStringDecimals(unit.Anemo.PriWinds.Maxm, 1) + ","); }
                                    if (exportWSpMinm) { hB.Append("wtc_PrWindSp_min" + ","); sB.Append(Common.GetStringDecimals(unit.Anemo.PriWinds.Minm, 1) + ","); }
                                    if (exportWSpMean) { hB.Append("wtc_PrWindSp_mean" + ","); sB.Append(Common.GetStringDecimals(unit.Anemo.PriWinds.Mean, 1) + ","); }
                                    if (exportWSpStdv) { hB.Append("wtc_PrWindSp_stddev" + ","); sB.Append(Common.GetStringDecimals(unit.Anemo.PriWinds.Stdv, 1) + ","); }
                                    if (exportWSpMaxm) { hB.Append("wtc_SeWindSp_max" + ","); sB.Append(Common.GetStringDecimals(unit.Anemo.SecWinds.Maxm, 1) + ","); }
                                    if (exportWSpMinm) { hB.Append("wtc_SeWindSp_min" + ","); sB.Append(Common.GetStringDecimals(unit.Anemo.SecWinds.Minm, 1) + ","); }
                                    if (exportWSpMean) { hB.Append("wtc_SeWindSp_mean" + ","); sB.Append(Common.GetStringDecimals(unit.Anemo.SecWinds.Mean, 1) + ","); }
                                    if (exportWSpStdv) { hB.Append("wtc_SeWindSp_stddev" + ","); sB.Append(Common.GetStringDecimals(unit.Anemo.SecWinds.Stdv, 1) + ","); }

                                    if (exportWSpMaxm) { hB.Append("wtc_PriAnemo_max" + ","); sB.Append(Common.GetStringDecimals(unit.Anemo.PriAnemo.Maxm, 1) + ","); }
                                    if (exportWSpMinm) { hB.Append("wtc_PriAnemo_min" + ","); sB.Append(Common.GetStringDecimals(unit.Anemo.PriAnemo.Minm, 1) + ","); }
                                    if (exportWSpMean) { hB.Append("wtc_PriAnemo_mean" + ","); sB.Append(Common.GetStringDecimals(unit.Anemo.PriAnemo.Mean, 1) + ","); }
                                    if (exportWSpStdv) { hB.Append("wtc_PriAnemo_stddev" + ","); sB.Append(Common.GetStringDecimals(unit.Anemo.PriAnemo.Stdv, 1) + ","); }
                                    if (exportWSpMaxm) { hB.Append("wtc_SecAnemo_max" + ","); sB.Append(Common.GetStringDecimals(unit.Anemo.SecAnemo.Maxm, 1) + ","); }
                                    if (exportWSpMinm) { hB.Append("wtc_SecAnemo_min" + ","); sB.Append(Common.GetStringDecimals(unit.Anemo.SecAnemo.Minm, 1) + ","); }
                                    if (exportWSpMean) { hB.Append("wtc_SecAnemo_mean" + ","); sB.Append(Common.GetStringDecimals(unit.Anemo.SecAnemo.Mean, 1) + ","); }
                                    if (exportWSpStdv) { hB.Append("wtc_SecAnemo_stddev" + ","); sB.Append(Common.GetStringDecimals(unit.Anemo.SecAnemo.Stdv, 1) + ","); }
                                    if (exportWSpMaxm) { hB.Append("wtc_TetAnemo_max" + ","); sB.Append(Common.GetStringDecimals(unit.Anemo.TerAnemo.Maxm, 1) + ","); }
                                    if (exportWSpMinm) { hB.Append("wtc_TetAnemo_min" + ","); sB.Append(Common.GetStringDecimals(unit.Anemo.TerAnemo.Minm, 1) + ","); }
                                    if (exportWSpMean) { hB.Append("wtc_TetAnemo_mean" + ","); sB.Append(Common.GetStringDecimals(unit.Anemo.TerAnemo.Mean, 1) + ","); }
                                    if (exportWSpStdv) { hB.Append("wtc_TetAnemo_stddev" + ","); sB.Append(Common.GetStringDecimals(unit.Anemo.TerAnemo.Stdv, 1) + ","); }
                                }

                                if (exportWSpMaxm) { hB.Append("wtc_YawPos_max" + ","); sB.Append(Common.GetStringDecimals(unit.YawSys.YawPos.Maxm, 1) + ","); }
                                if (exportWSpMinm) { hB.Append("wtc_YawPos_min" + ","); sB.Append(Common.GetStringDecimals(unit.YawSys.YawPos.Minm, 1) + ","); }
                                if (exportWSpMean) { hB.Append("wtc_YawPos_mean" + ","); sB.Append(Common.GetStringDecimals(unit.YawSys.YawPos.Mean, 1) + ","); }
                                if (exportWSpStdv) { hB.Append("wtc_YawPos_stddev" + ","); sB.Append(Common.GetStringDecimals(unit.YawSys.YawPos.Stdv, 1) + ","); }
                                
                                #endregion

                                #region Nacelle

                                if (exportNacMaxm) { hB.Append("wtc_NacelTmp_max" + ","); sB.Append(Common.GetStringDecimals(unit.Nacel.Temp.Maxm, 1) + ","); }
                                if (exportNacMinm) { hB.Append("wtc_NacelTmp_min" + ","); sB.Append(Common.GetStringDecimals(unit.Nacel.Temp.Minm, 1) + ","); }
                                if (exportNacMean) { hB.Append("wtc_NacelTmp_mean" + ","); sB.Append(Common.GetStringDecimals(unit.Nacel.Temp.Mean, 1) + ","); }
                                if (exportNacStdv) { hB.Append("wtc_NacelTmp_stddev" + ","); sB.Append(Common.GetStringDecimals(unit.Nacel.Temp.Stdv, 1) + ","); }

                                #endregion

                                #region Gearbox

                                if (exportGBxMaxm) { hB.Append("wtc_HSGenTmp_max" + ","); sB.Append(Common.GetStringDecimals(unit.Gearbox.HsGen.Maxm, 1) + ","); }
                                if (exportGBxMinm) { hB.Append("wtc_HSGenTmp_min" + ","); sB.Append(Common.GetStringDecimals(unit.Gearbox.HsGen.Minm, 1) + ","); }
                                if (exportGBxMean) { hB.Append("wtc_HSGenTmp_mean" + ","); sB.Append(Common.GetStringDecimals(unit.Gearbox.HsGen.Mean, 1) + ","); }
                                if (exportGBxStdv) { hB.Append("wtc_HSGenTmp_stddev" + ","); sB.Append(Common.GetStringDecimals(unit.Gearbox.HsGen.Stdv, 1) + ","); }

                                if (exportGBxMaxm) { hB.Append("wtc_HSRotTmp_max" + ","); sB.Append(Common.GetStringDecimals(unit.Gearbox.HsRot.Maxm, 1) + ","); }
                                if (exportGBxMinm) { hB.Append("wtc_HSRotTmp_min" + ","); sB.Append(Common.GetStringDecimals(unit.Gearbox.HsRot.Minm, 1) + ","); }
                                if (exportGBxMean) { hB.Append("wtc_HSRotTmp_mean" + ","); sB.Append(Common.GetStringDecimals(unit.Gearbox.HsRot.Mean, 1) + ","); }
                                if (exportGBxStdv) { hB.Append("wtc_HSRotTmp_stddev" + ","); sB.Append(Common.GetStringDecimals(unit.Gearbox.HsRot.Stdv, 1) + ","); }

                                if (exportGBxMaxm) { hB.Append("wtc_IMSGenTm_max" + ","); sB.Append(Common.GetStringDecimals(unit.Gearbox.ImsGen.Maxm, 1) + ","); }
                                if (exportGBxMinm) { hB.Append("wtc_IMSGenTm_min" + ","); sB.Append(Common.GetStringDecimals(unit.Gearbox.ImsGen.Minm, 1) + ","); }
                                if (exportGBxMean) { hB.Append("wtc_IMSGenTm_mean" + ","); sB.Append(Common.GetStringDecimals(unit.Gearbox.ImsGen.Mean, 1) + ","); }
                                if (exportGBxStdv) { hB.Append("wtc_IMSGenTm_stddev" + ","); sB.Append(Common.GetStringDecimals(unit.Gearbox.ImsGen.Stdv, 1) + ","); }

                                if (exportGBxMaxm) { hB.Append("wtc_IMSRotTm_max" + ","); sB.Append(Common.GetStringDecimals(unit.Gearbox.ImsRot.Maxm, 1) + ","); }
                                if (exportGBxMinm) { hB.Append("wtc_IMSRotTm_min" + ","); sB.Append(Common.GetStringDecimals(unit.Gearbox.ImsRot.Minm, 1) + ","); }
                                if (exportGBxMean) { hB.Append("wtc_IMSRotTm_mean" + ","); sB.Append(Common.GetStringDecimals(unit.Gearbox.ImsRot.Mean, 1) + ","); }
                                if (exportGBxStdv) { hB.Append("wtc_IMSRotTm_stddev" + ", "); sB.Append(Common.GetStringDecimals(unit.Gearbox.ImsRot.Stdv, 1) + ", "); }

                                if (exportGBxMaxm) { hB.Append("wtc_GeOilTmp_max" + ","); sB.Append(Common.GetStringDecimals(unit.Gearbox.OilTemp.Maxm, 1) + ","); }
                                if (exportGBxMinm) { hB.Append("wtc_GeOilTmp_min" + ","); sB.Append(Common.GetStringDecimals(unit.Gearbox.OilTemp.Minm, 1) + ","); }
                                if (exportGBxMean) { hB.Append("wtc_GeOilTmp_mean" + ","); sB.Append(Common.GetStringDecimals(unit.Gearbox.OilTemp.Mean, 1) + ","); }
                                if (exportGBxStdv) { hB.Append("wtc_GeOilTmp_stddev" + ","); sB.Append(Common.GetStringDecimals(unit.Gearbox.OilTemp.Stdv, 1) + ","); }

                                if (exportGBxMean) { hB.Append("wtc_HSGenTmp_delta" + ","); sB.Append(Common.GetStringDecimals(unit.Gearbox.HsGen.Dlta, 1) + ","); }
                                if (exportGBxMean) { hB.Append("wtc_HSRotTmp_delta" + ","); sB.Append(Common.GetStringDecimals(unit.Gearbox.HsRot.Dlta, 1) + ","); }
                                if (exportGBxMean) { hB.Append("wtc_IMSGenTm_delta" + ","); sB.Append(Common.GetStringDecimals(unit.Gearbox.ImsGen.Dlta, 1) + ","); }
                                if (exportGBxMean) { hB.Append("wtc_IMSRotTm_delta" + ","); sB.Append(Common.GetStringDecimals(unit.Gearbox.ImsRot.Dlta, 1) + ","); }
                                if (exportGBxMean) { hB.Append("wtc_GeOilTmp_delta"); sB.Append(Common.GetStringDecimals(unit.Gearbox.OilTemp.Dlta, 1)); }
                                
                                #endregion

                                #region Generator

                                if (exportGenMaxm) { hB.Append("wtc_GenRpm_max" + ","); sB.Append(Common.GetStringDecimals(unit.Genny.RPMs.Maxm, 1) + ","); }
                                if (exportGenMinm) { hB.Append("wtc_GenRpm_min" + ","); sB.Append(Common.GetStringDecimals(unit.Genny.RPMs.Minm, 1) + ","); }
                                if (exportGenMean) { hB.Append("wtc_GenRpm_mean" + ","); sB.Append(Common.GetStringDecimals(unit.Genny.RPMs.Mean, 1) + ","); }
                                if (exportGenStdv) { hB.Append("wtc_GenRpm_stddev" + ","); sB.Append(Common.GetStringDecimals(unit.Genny.RPMs.Stdv, 1) + ","); }

                                if (exportGenMaxm) { hB.Append("wtc_GenBeGTm_max" + ","); sB.Append(Common.GetStringDecimals(unit.Genny.BearingG.Maxm, 1) + ","); }
                                if (exportGenMinm) { hB.Append("wtc_GenBeGTm_min" + ","); sB.Append(Common.GetStringDecimals(unit.Genny.BearingG.Minm, 1) + ","); }
                                if (exportGenMean) { hB.Append("wtc_GenBeGTm_mean" + ","); sB.Append(Common.GetStringDecimals(unit.Genny.BearingG.Mean, 1) + ","); }
                                if (exportGenStdv) { hB.Append("wtc_GenBeGTm_stddev" + ","); sB.Append(Common.GetStringDecimals(unit.Genny.BearingG.Stdv, 1) + ","); }
                                if (exportGenMaxm) { hB.Append("wtc_GenBeRTm_max" + ","); sB.Append(Common.GetStringDecimals(unit.Genny.BearingR.Maxm, 1) + ","); }
                                if (exportGenMinm) { hB.Append("wtc_GenBeRTm_min" + ","); sB.Append(Common.GetStringDecimals(unit.Genny.BearingR.Minm, 1) + ","); }
                                if (exportGenMean) { hB.Append("wtc_GenBeRTm_mean" + ","); sB.Append(Common.GetStringDecimals(unit.Genny.BearingR.Mean, 1) + ","); }
                                if (exportGenStdv) { hB.Append("wtc_GenBeRTm_stddev" + ","); sB.Append(Common.GetStringDecimals(unit.Genny.BearingR.Stdv, 1) + ","); }

                                if (exportGenMaxm) { hB.Append("wtc_Gen1U1Tm_max" + ","); sB.Append(Common.GetStringDecimals(unit.Genny.G1u1.Maxm, 1) + ","); }
                                if (exportGenMinm) { hB.Append("wtc_Gen1U1Tm_min" + ","); sB.Append(Common.GetStringDecimals(unit.Genny.G1u1.Minm, 1) + ","); }
                                if (exportGenMean) { hB.Append("wtc_Gen1U1Tm_mean" + ","); sB.Append(Common.GetStringDecimals(unit.Genny.G1u1.Mean, 1) + ","); }
                                if (exportGenStdv) { hB.Append("wtc_Gen1U1Tm_stddev" + ","); sB.Append(Common.GetStringDecimals(unit.Genny.G1u1.Stdv, 1) + ","); }
                                if (exportGenMaxm) { hB.Append("wtc_Gen1V1Tm_max" + ","); sB.Append(Common.GetStringDecimals(unit.Genny.G1v1.Maxm, 1) + ","); }
                                if (exportGenMinm) { hB.Append("wtc_Gen1V1Tm_min" + ","); sB.Append(Common.GetStringDecimals(unit.Genny.G1v1.Minm, 1) + ","); }
                                if (exportGenMean) { hB.Append("wtc_Gen1V1Tm_mean" + ","); sB.Append(Common.GetStringDecimals(unit.Genny.G1v1.Mean, 1) + ","); }
                                if (exportGenStdv) { hB.Append("wtc_Gen1V1Tm_stddev" + ","); sB.Append(Common.GetStringDecimals(unit.Genny.G1v1.Stdv, 1) + ","); }
                                if (exportGenMaxm) { hB.Append("wtc_Gen1W1Tm_max" + ","); sB.Append(Common.GetStringDecimals(unit.Genny.G1w1.Maxm, 1) + ","); }
                                if (exportGenMinm) { hB.Append("wtc_Gen1W1Tm_min" + ","); sB.Append(Common.GetStringDecimals(unit.Genny.G1w1.Minm, 1) + ","); }
                                if (exportGenMean) { hB.Append("wtc_Gen1W1Tm_mean" + ","); sB.Append(Common.GetStringDecimals(unit.Genny.G1w1.Mean, 1) + ","); }
                                if (exportGenStdv) { hB.Append("wtc_Gen1W1Tm_stddev" + ","); sB.Append(Common.GetStringDecimals(unit.Genny.G1w1.Stdv, 1) + ","); }

                                if (exportGenMaxm) { hB.Append("wtc_Gen2U1Tm_max" + ","); sB.Append(Common.GetStringDecimals(unit.Genny.G2u1.Maxm, 1) + ","); }
                                if (exportGenMinm) { hB.Append("wtc_Gen2U1Tm_min" + ","); sB.Append(Common.GetStringDecimals(unit.Genny.G2u1.Minm, 1) + ","); }
                                if (exportGenMean) { hB.Append("wtc_Gen2U1Tm_mean" + ","); sB.Append(Common.GetStringDecimals(unit.Genny.G2u1.Mean, 1) + ","); }
                                if (exportGenStdv) { hB.Append("wtc_Gen2U1Tm_stddev" + ","); sB.Append(Common.GetStringDecimals(unit.Genny.G2u1.Stdv, 1) + ","); }
                                if (exportGenMaxm) { hB.Append("wtc_Gen2V1Tm_max" + ","); sB.Append(Common.GetStringDecimals(unit.Genny.G2v1.Maxm, 1) + ","); }
                                if (exportGenMinm) { hB.Append("wtc_Gen2V1Tm_min" + ","); sB.Append(Common.GetStringDecimals(unit.Genny.G2v1.Minm, 1) + ","); }
                                if (exportGenMean) { hB.Append("wtc_Gen2V1Tm_mean" + ","); sB.Append(Common.GetStringDecimals(unit.Genny.G2v1.Mean, 1) + ","); }
                                if (exportGenStdv) { hB.Append("wtc_Gen2V1Tm_stddev" + ","); sB.Append(Common.GetStringDecimals(unit.Genny.G2v1.Stdv, 1) + ","); }
                                if (exportGenMaxm) { hB.Append("wtc_Gen2W1Tm_max" + ","); sB.Append(Common.GetStringDecimals(unit.Genny.G2w1.Maxm, 1) + ","); }
                                if (exportGenMinm) { hB.Append("wtc_Gen2W1Tm_min" + ","); sB.Append(Common.GetStringDecimals(unit.Genny.G2w1.Minm, 1) + ","); }
                                if (exportGenMean) { hB.Append("wtc_Gen2W1Tm_mean" + ","); sB.Append(Common.GetStringDecimals(unit.Genny.G2w1.Mean, 1) + ","); }
                                if (exportGenStdv) { hB.Append("wtc_Gen2W1Tm_stddev" + ","); sB.Append(Common.GetStringDecimals(unit.Genny.G2w1.Stdv, 1) + ","); }

                                if (exportGenMean) { hB.Append("wtc_GenRpm_delta" + ","); sB.Append(Common.GetStringDecimals(unit.Genny.RPMs.Dlta, 1) + ","); }
                                if (exportGenMean) { hB.Append("wtc_GenBeGTm_delta" + ","); sB.Append(Common.GetStringDecimals(unit.Genny.BearingG.Dlta, 1) + ","); }
                                if (exportGenMean) { hB.Append("wtc_GenBeRTm_delta" + ","); sB.Append(Common.GetStringDecimals(unit.Genny.BearingR.Dlta, 1) + ","); }
                                if (exportGenMean) { hB.Append("wtc_Gen1U1Tm_delta" + ","); sB.Append(Common.GetStringDecimals(unit.Genny.G1u1.Dlta, 1) + ","); }
                                if (exportGenMean) { hB.Append("wtc_Gen1V1Tm_delta" + ","); sB.Append(Common.GetStringDecimals(unit.Genny.G1v1.Dlta, 1) + ","); }
                                if (exportGenMean) { hB.Append("wtc_Gen1W1Tm_delta" + ","); sB.Append(Common.GetStringDecimals(unit.Genny.G1w1.Dlta, 1) + ","); }
                                if (exportGenMean) { hB.Append("wtc_Gen2U1Tm_delta" + ","); sB.Append(Common.GetStringDecimals(unit.Genny.G2u1.Dlta, 1) + ","); }
                                if (exportGenMean) { hB.Append("wtc_Gen2V1Tm_delta" + ","); sB.Append(Common.GetStringDecimals(unit.Genny.G2v1.Dlta, 1) + ","); }
                                if (exportGenMean) { hB.Append("wtc_Gen2W1Tm_delta" + ","); sB.Append(Common.GetStringDecimals(unit.Genny.G2w1.Dlta, 1) + ","); }
                                
                                #endregion

                                #region Main Bearing

                                if (exportMBrMaxm) { hB.Append("wtc_MainBTmp_max" + ","); sB.Append(Common.GetStringDecimals(unit.MainBear.Main.Maxm, 1) + ","); }
                                if (exportMBrMinm) { hB.Append("wtc_MainBTmp_min" + ","); sB.Append(Common.GetStringDecimals(unit.MainBear.Main.Minm, 1) + ","); }
                                if (exportMBrMean) { hB.Append("wtc_MainBTmp_mean" + ","); sB.Append(Common.GetStringDecimals(unit.MainBear.Main.Mean, 1) + ","); }
                                if (exportMBrStdv) { hB.Append("wtc_MainBTmp_stddev" + ","); sB.Append(Common.GetStringDecimals(unit.MainBear.Main.Stdv, 1) + ","); }

                                if (exportMBrMaxm) { hB.Append("wtc_MBearGTm_max" + ","); sB.Append(Common.GetStringDecimals(unit.MainBear.Gs.Maxm, 1) + ","); }
                                if (exportMBrMinm) { hB.Append("wtc_MBearGTm_min" + ","); sB.Append(Common.GetStringDecimals(unit.MainBear.Gs.Minm, 1) + ","); }
                                if (exportMBrMean) { hB.Append("wtc_MBearGTm_mean" + ","); sB.Append(Common.GetStringDecimals(unit.MainBear.Gs.Mean, 1) + ","); }
                                if (exportMBrStdv) { hB.Append("wtc_MBearGTm_stddev" + ","); sB.Append(Common.GetStringDecimals(unit.MainBear.Gs.Stdv, 1) + ","); }

                                if (exportMBrMaxm) { hB.Append("wtc_MBearHTm_max" + ","); sB.Append(Common.GetStringDecimals(unit.MainBear.Hs.Maxm, 1) + ","); }
                                if (exportMBrMinm) { hB.Append("wtc_MBearHTm_min" + ","); sB.Append(Common.GetStringDecimals(unit.MainBear.Hs.Minm, 1) + ","); }
                                if (exportMBrMean) { hB.Append("wtc_MBearHTm_mean" + ","); sB.Append(Common.GetStringDecimals(unit.MainBear.Hs.Mean, 1) + ","); }
                                if (exportMBrStdv) { hB.Append("wtc_MBearHTm_stddev" + ","); sB.Append(Common.GetStringDecimals(unit.MainBear.Hs.Stdv, 1) + ","); }

                                if (exportMBrMean) { hB.Append("wtc_MainBTmp_delta" + ","); sB.Append(Common.GetStringDecimals(unit.MainBear.Main.Dlta, 1) + ","); }
                                if (exportMBrMean) { hB.Append("wtc_MBearGTm_delta" + ","); sB.Append(Common.GetStringDecimals(unit.MainBear.Gs.Dlta, 1) + ","); }
                                if (exportMBrMean) { hB.Append("wtc_MBearHTm_delta" + ","); sB.Append(Common.GetStringDecimals(unit.MainBear.Hs.Dlta, 1) + ","); }
                                
                                #endregion

                                // note the last one does not use a comma so if you add more lines, add in a comma

                                if (header == false) { sW.WriteLine(hB.ToString()); header = true; }
                                sW.WriteLine(sB.ToString());
                            }

                            count++;

                            if (count % 1000 == 0)
                            {
                                if (progress != null)
                                {
                                    progress.Report((int)(((double)i / _windFarm.Count + (double)j / _windFarm[i].DataSorted.Count / _windFarm.Count) * 100));
                                }
                            }
                        }
                    }
                }
                finally
                {
                    sW.Close();
                }
            }
        }

        public enum ExportMode
        {
            FULL,
            SINGLE,
            EVENT_ONLY,
            EVENT_WEEK,
            EVENT_HISTORIC
        }

        #endregion

        #region Support Classes

        public class TurbineData : BaseStructure
        {
            // this class represents a single turbine with its list of data

            #region Variables

            private double _ratedPower;

            private List<ScadaSample> data = new List<ScadaSample>();
            private List<ScadaSample> dataSorted = new List<ScadaSample>();

            #endregion

            #region Constructor

            public TurbineData() { }

            public TurbineData(string[] splits, ScadaHeader header, Common.DateFormat _dateFormat, double _rated)
            {
                // this is the first sample for a turbine which is only used once, all future loading
                // goes into the other method for every turbine

                _ratedPower = _rated;
                Type = Types.TURBINE;

                data.Add(new ScadaSample(splits, header, _dateFormat));
                InclSamples.Add(data[data.Count - 1].TimeStamp);
                
                if (UnitID == -1 && data.Count > 0)
                {
                    UnitID = data[0].AssetID != 0 ? data[0].AssetID : data[0].StationID;
                }
            }

            #endregion

            public void AddData(string[] splits, ScadaHeader header, Common.DateFormat _dateFormat)
            {
                // this method is used to add data to a turbine
                DateTime thisTime = Common.StringToDateTime(Common.GetSplits(splits[header.TimesCol], new char[] { ' ' }), _dateFormat);

                if (InclSamples.Contains(thisTime))
                {
                    int index = data.FindIndex(x => x.TimeStamp == thisTime);
                    data[index].AddDataFields(splits, header, _dateFormat);
                }
                else
                {
                    data.Add(new ScadaSample(splits, header, _dateFormat));
                    InclSamples.Add(data[data.Count - 1].TimeStamp);
                }
            }

            #region Properties

            public double RatedPower { get { return _ratedPower; } set { _ratedPower = value; } }

            public List<ScadaSample> Data { get { return data; } set { data = value; } }
            public List<ScadaSample> DataSorted { get { return dataSorted; } set { dataSorted = value; } }

            #endregion
        }

        public class ScadaHeader : ScadaSample
        {
            // this class inherits all of the ScadaSample properties but I will treat this
            // as a separate instance where everything refers to the column index and not
            // the actual data itself

            // this will be initialised to begin with, and after that will be maintained for
            // the duration of loading the file as it contains all information pertaining to that file

            #region Variables

            // the noValue flag is used to make all column headers which should not be included be ignored
            private int _noValue = -1;

            private int curTimeCol = -1;
            private int assetCol = -1, sampleCol = -1, stationCol = -1, timeCol = -1;

            #endregion

            #region Constructor

            public ScadaHeader() { }

            public ScadaHeader(string header)
            {
                NullAllHeaderValues();
                HeaderSeparation(header);
            }

            #endregion 

            private void HeaderSeparation(string headerLine)
            {
                string[] splits = Common.GetSplits(headerLine, ',');

                HeaderSeparation(splits);
            }

            private void HeaderSeparation(string[] headerSplits)
            {
                for (int i = 0; i < headerSplits.Length; i++)
                {
                    if (headerSplits[i].Contains("assetuid")) { AssetCol = i; }
                    else if (headerSplits[i].Contains("sampleuid")) { SamplCol = i; }
                    else if (headerSplits[i].Contains("stationid")) { StatnCol = i; }
                    else if (headerSplits[i].Contains("timestamp")) { TimesCol = i; }
                    else
                    {
                        string[] parts = Common.GetSplits(headerSplits[i], '_');

                        if (parts.Length > 1)
                        {
                            // actual power has a special case where one "endvalue" is 
                            // represented by "quality"

                            #region Grid File Variables
                            if (parts[1] == "actpower")
                            {
                                if (parts[2] == "mean") { Power.Mean = i; }
                                else if (parts[2] == "stddev") { Power.Stdv = i; }
                                else if (parts[2] == "max") { Power.Maxm = i; }
                                else if (parts[2] == "min") { Power.Minm = i; }
                                else if (parts[2] == "endvalue") { Power.EndValCol = i; }
                                else if (parts[2] == "quality") { Power.QualEndValCol = i; }
                            }
                            else if (parts[1] == "actregst")
                            {
                                if (parts[2] == "endvalue") { Power.RgStEndValCol = i; }
                            }
                            else if (parts[1] == "ampphr")
                            {
                                if (parts[2] == "mean") { Power.Currents.PhR.Mean = i; }
                                else if (parts[2] == "stddev") { Power.Currents.PhR.Stdv = i; }
                                else if (parts[2] == "max") { Power.Currents.PhR.Maxm = i; }
                                else if (parts[2] == "min") { Power.Currents.PhR.Minm = i; }
                            }
                            else if (parts[1] == "ampphs")
                            {
                                if (parts[2] == "mean") { Power.Currents.PhS.Mean = i; }
                                else if (parts[2] == "stddev") { Power.Currents.PhS.Stdv = i; }
                                else if (parts[2] == "max") { Power.Currents.PhS.Maxm = i; }
                                else if (parts[2] == "min") { Power.Currents.PhS.Minm = i; }
                            }
                            else if (parts[1] == "amppht")
                            {
                                if (parts[2] == "mean") { Power.Currents.PhT.Mean = i; }
                                else if (parts[2] == "stddev") { Power.Currents.PhT.Stdv = i; }
                                else if (parts[2] == "max") { Power.Currents.PhT.Maxm = i; }
                                else if (parts[2] == "min") { Power.Currents.PhT.Minm = i; }
                            }
                            else if (parts[1] == "cosphi")
                            {
                                if (parts[2] == "mean") { Power.PowerFactor.Mean = i; }
                                else if (parts[2] == "stddev") { Power.PowerFactor.Stdv = i; }
                                else if (parts[2] == "max") { Power.PowerFactor.Maxm = i; }
                                else if (parts[2] == "min") { Power.PowerFactor.Minm = i; }
                                else if (parts[2] == "endvalue") { Power.PowerFactor.EndValCol = i; }
                            }
                            else if (parts[1] == "gridfreq")
                            {
                                if (parts[2] == "mean") { Power.GridFreq.Mean = i; }
                                else if (parts[2] == "stddev") { Power.GridFreq.Stdv = i; }
                                else if (parts[2] == "max") { Power.GridFreq.Maxm = i; }
                                else if (parts[2] == "min") { Power.GridFreq.Minm = i; }
                            }
                            else if (parts[1] == "reactpwr")
                            {
                                if (parts[2] == "mean") { Power.ReactivePwr.Mean = i; }
                                else if (parts[2] == "stddev") { Power.ReactivePwr.Stdv = i; }
                                else if (parts[2] == "max") { Power.ReactivePwr.Maxm = i; }
                                else if (parts[2] == "min") { Power.ReactivePwr.Minm = i; }
                                else if (parts[2] == "endvalue") { Power.ReactivePwr.EndValCol = i; }
                            }
                            else if (parts[1] == "voltphr")
                            {
                                if (parts[2] == "mean") { Power.Voltages.PhR.Mean = i; }
                                else if (parts[2] == "stddev") { Power.Voltages.PhR.Stdv = i; }
                                else if (parts[2] == "max") { Power.Voltages.PhR.Maxm = i; }
                                else if (parts[2] == "min") { Power.Voltages.PhR.Minm = i; }
                            }
                            else if (parts[1] == "voltphs")
                            {
                                if (parts[2] == "mean") { Power.Voltages.PhS.Mean = i; }
                                else if (parts[2] == "stddev") { Power.Voltages.PhS.Stdv = i; }
                                else if (parts[2] == "max") { Power.Voltages.PhS.Maxm = i; }
                                else if (parts[2] == "min") { Power.Voltages.PhS.Minm = i; }
                            }
                            else if (parts[1] == "voltpht")
                            {
                                if (parts[2] == "mean") { Power.Voltages.PhT.Mean = i; }
                                else if (parts[2] == "stddev") { Power.Voltages.PhT.Stdv = i; }
                                else if (parts[2] == "max") { Power.Voltages.PhT.Maxm = i; }
                                else if (parts[2] == "min") { Power.Voltages.PhT.Minm = i; }
                            }
                            #endregion
                            #region Temperature File
                            else if (parts[1] == "ambietmp")
                            {
                                if (parts[2] == "mean") { AmbTemps.Mean = i; }
                                else if (parts[2] == "stddev") { AmbTemps.Stdv = i; }
                                else if (parts[2] == "max") { AmbTemps.Maxm = i; }
                                else if (parts[2] == "min") { AmbTemps.Minm = i; }
                                else if (parts[2] == "delta") { AmbTemps.Dlta = i; }
                            }
                            else if (parts[1] == "deltatmp")
                            {
                                if (parts[2] == "mean") { DeltaTs.Mean = i; }
                                else if (parts[2] == "stddev") { DeltaTs.Stdv = i; }
                                else if (parts[2] == "max") { DeltaTs.Maxm = i; }
                                else if (parts[2] == "min") { DeltaTs.Minm = i; }
                            }
                            else if (parts[1] == "gen1u1tm")
                            {
                                if (parts[2] == "mean") { Genny.G1u1.Mean = i; }
                                else if (parts[2] == "stddev") { Genny.G1u1.Stdv = i; }
                                else if (parts[2] == "max") { Genny.G1u1.Maxm = i; }
                                else if (parts[2] == "min") { Genny.G1u1.Minm = i; }
                                else if (parts[2] == "delta") { Genny.G1u1.Dlta = i; }
                            }
                            else if (parts[1] == "gen1v1tm")
                            {
                                if (parts[2] == "mean") { Genny.G1v1.Mean = i; }
                                else if (parts[2] == "stddev") { Genny.G1v1.Stdv = i; }
                                else if (parts[2] == "max") { Genny.G1v1.Maxm = i; }
                                else if (parts[2] == "min") { Genny.G1v1.Minm = i; }
                                else if (parts[2] == "delta") { Genny.G1v1.Dlta = i; }
                            }
                            else if (parts[1] == "gen1w1tm")
                            {
                                if (parts[2] == "mean") { Genny.G1w1.Mean = i; }
                                else if (parts[2] == "stddev") { Genny.G1w1.Stdv = i; }
                                else if (parts[2] == "max") { Genny.G1w1.Maxm = i; }
                                else if (parts[2] == "min") { Genny.G1w1.Minm = i; }
                                else if (parts[2] == "delta") { Genny.G1w1.Dlta = i; }
                            }
                            else if (parts[1] == "gen2u1tm")
                            {
                                if (parts[2] == "mean") { Genny.G2u1.Mean = i; }
                                else if (parts[2] == "stddev") { Genny.G2u1.Stdv = i; }
                                else if (parts[2] == "max") { Genny.G2u1.Maxm = i; }
                                else if (parts[2] == "min") { Genny.G2u1.Minm = i; }
                                else if (parts[2] == "delta") { Genny.G2u1.Dlta = i; }
                            }
                            else if (parts[1] == "gen2v1tm")
                            {
                                if (parts[2] == "mean") { Genny.G2v1.Mean = i; }
                                else if (parts[2] == "stddev") { Genny.G2v1.Stdv = i; }
                                else if (parts[2] == "max") { Genny.G2v1.Maxm = i; }
                                else if (parts[2] == "min") { Genny.G2v1.Minm = i; }
                                else if (parts[2] == "delta") { Genny.G2v1.Dlta = i; }
                            }
                            else if (parts[1] == "gen2w1tm")
                            {
                                if (parts[2] == "mean") { Genny.G2w1.Mean = i; }
                                else if (parts[2] == "stddev") { Genny.G2w1.Stdv = i; }
                                else if (parts[2] == "max") { Genny.G2w1.Maxm = i; }
                                else if (parts[2] == "min") { Genny.G2w1.Minm = i; }
                                else if (parts[2] == "delta") { Genny.G2w1.Dlta = i; }
                            }
                            else if (parts[1] == "genbegtm")
                            {
                                if (parts[2] == "mean") { Genny.BearingG.Mean = i; }
                                else if (parts[2] == "stddev") { Genny.BearingG.Stdv = i; }
                                else if (parts[2] == "max") { Genny.BearingG.Maxm = i; }
                                else if (parts[2] == "min") { Genny.BearingG.Minm = i; }
                                else if (parts[2] == "delta") { Genny.BearingG.Dlta = i; }
                            }
                            else if (parts[1] == "genbertm")
                            {
                                if (parts[2] == "mean") { Genny.BearingR.Mean = i; }
                                else if (parts[2] == "stddev") { Genny.BearingR.Stdv = i; }
                                else if (parts[2] == "max") { Genny.BearingR.Maxm = i; }
                                else if (parts[2] == "min") { Genny.BearingR.Minm = i; }
                                else if (parts[2] == "delta") { Genny.BearingR.Dlta = i; }
                            }
                            else if (parts[1] == "geoiltmp")
                            {
                                if (parts[2] == "mean") { Gearbox.OilTemp.Mean = i; }
                                else if (parts[2] == "stddev") { Gearbox.OilTemp.Stdv = i; }
                                else if (parts[2] == "max") { Gearbox.OilTemp.Maxm = i; }
                                else if (parts[2] == "min") { Gearbox.OilTemp.Minm = i; }
                                else if (parts[2] == "delta") { Gearbox.OilTemp.Dlta = i; }
                            }
                            else if (parts[1] == "gfilb1tm")
                            {
                                if (parts[2] == "mean") { GridFilt.B1s.Mean = i; }
                                else if (parts[2] == "stddev") { GridFilt.B1s.Stdv = i; }
                                else if (parts[2] == "max") { GridFilt.B1s.Maxm = i; }
                                else if (parts[2] == "min") { GridFilt.B1s.Minm = i; }
                            }
                            else if (parts[1] == "gfilb2tm")
                            {
                                if (parts[2] == "mean") { GridFilt.B2s.Mean = i; }
                                else if (parts[2] == "stddev") { GridFilt.B2s.Stdv = i; }
                                else if (parts[2] == "max") { GridFilt.B2s.Maxm = i; }
                                else if (parts[2] == "min") { GridFilt.B2s.Minm = i; }
                            }
                            else if (parts[1] == "gfilb3tm")
                            {
                                if (parts[2] == "mean") { GridFilt.B3s.Mean = i; }
                                else if (parts[2] == "stddev") { GridFilt.B3s.Stdv = i; }
                                else if (parts[2] == "max") { GridFilt.B3s.Maxm = i; }
                                else if (parts[2] == "min") { GridFilt.B3s.Minm = i; }
                            }
                            else if (parts[1] == "hsgentmp")
                            {
                                if (parts[2] == "mean") { Gearbox.HsGen.Mean = i; }
                                else if (parts[2] == "stddev") { Gearbox.HsGen.Stdv = i; }
                                else if (parts[2] == "max") { Gearbox.HsGen.Maxm = i; }
                                else if (parts[2] == "min") { Gearbox.HsGen.Minm = i; }
                                else if (parts[2] == "delta") { Gearbox.HsGen.Dlta = i; }
                            }
                            else if (parts[1] == "hsrottmp")
                            {
                                if (parts[2] == "mean") { Gearbox.HsRot.Mean = i; }
                                else if (parts[2] == "stddev") { Gearbox.HsRot.Stdv = i; }
                                else if (parts[2] == "max") { Gearbox.HsRot.Maxm = i; }
                                else if (parts[2] == "min") { Gearbox.HsRot.Minm = i; }
                                else if (parts[2] == "delta") { Gearbox.HsRot.Dlta = i; }
                            }
                            else if (parts[1] == "hubbrdtm")
                            {
                                if (parts[2] == "mean") { Hubs.Boards.Mean = i; }
                                else if (parts[2] == "stddev") { Hubs.Boards.Stdv = i; }
                                else if (parts[2] == "max") { Hubs.Boards.Maxm = i; }
                                else if (parts[2] == "min") { Hubs.Boards.Minm = i; }
                            }
                            else if (parts[1] == "hubtemp")
                            {
                                if (parts[2] == "mean") { Hubs.Internals.Mean = i; }
                                else if (parts[2] == "stddev") { Hubs.Internals.Stdv = i; }
                                else if (parts[2] == "max") { Hubs.Internals.Maxm = i; }
                                else if (parts[2] == "min") { Hubs.Internals.Minm = i; }
                            }
                            else if (parts[1] == "hubtref1")
                            {
                                if (parts[2] == "mean") { Hubs.Ref1s.Mean = i; }
                                else if (parts[2] == "stddev") { Hubs.Ref1s.Stdv = i; }
                                else if (parts[2] == "max") { Hubs.Ref1s.Maxm = i; }
                                else if (parts[2] == "min") { Hubs.Ref1s.Minm = i; }
                            }
                            else if (parts[1] == "hubtref2")
                            {
                                if (parts[2] == "mean") { Hubs.Ref2s.Mean = i; }
                                else if (parts[2] == "stddev") { Hubs.Ref2s.Stdv = i; }
                                else if (parts[2] == "max") { Hubs.Ref2s.Maxm = i; }
                                else if (parts[2] == "min") { Hubs.Ref2s.Minm = i; }
                            }
                            else if (parts[1] == "hydoiltm")
                            {
                                if (parts[2] == "mean") { HydOils.Temp.Mean = i; }
                                else if (parts[2] == "stddev") { HydOils.Temp.Stdv = i; }
                                else if (parts[2] == "max") { HydOils.Temp.Maxm = i; }
                                else if (parts[2] == "min") { HydOils.Temp.Minm = i; }
                            }
                            else if (parts[1] == "imsgentm")
                            {
                                if (parts[2] == "mean") { Gearbox.ImsGen.Mean = i; }
                                else if (parts[2] == "stddev") { Gearbox.ImsGen.Stdv = i; }
                                else if (parts[2] == "max") { Gearbox.ImsGen.Maxm = i; }
                                else if (parts[2] == "min") { Gearbox.ImsGen.Minm = i; }
                                else if (parts[2] == "delta") { Gearbox.ImsGen.Dlta = i; }
                            }
                            else if (parts[1] == "imsrottm")
                            {
                                if (parts[2] == "mean") { Gearbox.ImsRot.Mean = i; }
                                else if (parts[2] == "stddev") { Gearbox.ImsRot.Stdv = i; }
                                else if (parts[2] == "max") { Gearbox.ImsRot.Maxm = i; }
                                else if (parts[2] == "min") { Gearbox.ImsRot.Minm = i; }
                                else if (parts[2] == "delta") { Gearbox.ImsRot.Dlta = i; }
                            }
                            else if (parts[1] == "mainbtmp")
                            {
                                if (parts[2] == "mean") { MainBear.Main.Mean = i; }
                                else if (parts[2] == "stddev") { MainBear.Main.Stdv = i; }
                                else if (parts[2] == "max") { MainBear.Main.Maxm = i; }
                                else if (parts[2] == "min") { MainBear.Main.Minm = i; }
                                else if (parts[2] == "delta") { MainBear.Main.Dlta = i; }
                            }
                            else if (parts[1] == "mbeargtm")
                            {
                                if (parts[2] == "mean") { MainBear.Gs.Mean = i; }
                                else if (parts[2] == "stddev") { MainBear.Gs.Stdv = i; }
                                else if (parts[2] == "max") { MainBear.Gs.Maxm = i; }
                                else if (parts[2] == "min") { MainBear.Gs.Minm = i; }
                                else if (parts[2] == "delta") { MainBear.Gs.Dlta = i; }
                            }
                            else if (parts[1] == "mbearhtm")
                            {
                                if (parts[2] == "mean") { MainBear.Hs.Mean = i; }
                                else if (parts[2] == "stddev") { MainBear.Hs.Stdv = i; }
                                else if (parts[2] == "max") { MainBear.Hs.Maxm = i; }
                                else if (parts[2] == "min") { MainBear.Hs.Minm = i; }
                                else if (parts[2] == "delta") { MainBear.Hs.Dlta = i; }
                            }
                            else if (parts[1] == "naceltmp")
                            {
                                if (parts[2] == "mean") { Nacel.Temp.Mean = i; }
                                else if (parts[2] == "stddev") { Nacel.Temp.Stdv = i; }
                                else if (parts[2] == "max") { Nacel.Temp.Maxm = i; }
                                else if (parts[2] == "min") { Nacel.Temp.Minm = i; }
                            }
                            #endregion
                            #region Turbine File
                            else if (parts[1] == "curtime") { curTimeCol = i; }
                            else if (parts[1] == "acwindsp")
                            {
                                if (parts[2] == "mean") { Anemo.ActWinds.Mean = i; }
                                else if (parts[2] == "stddev") { Anemo.ActWinds.Stdv = i; }
                                else if (parts[2] == "max") { Anemo.ActWinds.Maxm = i; }
                                else if (parts[2] == "min") { Anemo.ActWinds.Minm = i; }
                            }
                            else if (parts[1] == "genrpm")
                            {
                                if (parts[2] == "mean") { Genny.RPMs.Mean = i; }
                                else if (parts[2] == "stddev") { Genny.RPMs.Stdv = i; }
                                else if (parts[2] == "max") { Genny.RPMs.Maxm = i; }
                                else if (parts[2] == "min") { Genny.RPMs.Minm = i; }
                                else if (parts[2] == "delta") { Genny.RPMs.Dlta = i; }
                            }
                            else if (parts[1] == "prianemo")
                            {
                                if (parts[2] == "mean") { Anemo.PriAnemo.Mean = i; }
                                else if (parts[2] == "stddev") { Anemo.PriAnemo.Stdv = i; }
                                else if (parts[2] == "max") { Anemo.PriAnemo.Maxm = i; }
                                else if (parts[2] == "min") { Anemo.PriAnemo.Minm = i; }
                            }
                            else if (parts[1] == "prwindsp")
                            {
                                if (parts[2] == "mean") { Anemo.PriWinds.Mean = i; }
                                else if (parts[2] == "stddev") { Anemo.PriWinds.Stdv = i; }
                                else if (parts[2] == "max") { Anemo.PriWinds.Maxm = i; }
                                else if (parts[2] == "min") { Anemo.PriWinds.Minm = i; }
                            }
                            else if (parts[1] == "secanemo")
                            {
                                if (parts[2] == "mean") { Anemo.SecAnemo.Mean = i; }
                                else if (parts[2] == "stddev") { Anemo.SecAnemo.Stdv = i; }
                                else if (parts[2] == "max") { Anemo.SecAnemo.Maxm = i; }
                                else if (parts[2] == "min") { Anemo.SecAnemo.Minm = i; }
                            }
                            else if (parts[1] == "sewindsp")
                            {
                                if (parts[2] == "mean") { Anemo.SecWinds.Mean = i; }
                                else if (parts[2] == "stddev") { Anemo.SecWinds.Stdv = i; }
                                else if (parts[2] == "max") { Anemo.SecWinds.Maxm = i; }
                                else if (parts[2] == "min") { Anemo.SecWinds.Minm = i; }
                            }
                            else if (parts[1] == "tetanemo")
                            {
                                if (parts[2] == "mean") { Anemo.TerAnemo.Mean = i; }
                                else if (parts[2] == "stddev") { Anemo.TerAnemo.Stdv = i; }
                                else if (parts[2] == "max") { Anemo.TerAnemo.Maxm = i; }
                                else if (parts[2] == "min") { Anemo.TerAnemo.Minm = i; }
                            }
                            else if (parts[1] == "twrhumid")
                            {
                                if (parts[2] == "mean") { Tower.Humid.Mean = i; }
                                else if (parts[2] == "stddev") { Tower.Humid.Stdv = i; }
                                else if (parts[2] == "max") { Tower.Humid.Maxm = i; }
                                else if (parts[2] == "min") { Tower.Humid.Minm = i; }
                            }
                            else if (parts[1] == "yawpos")
                            {
                                if (parts[2] == "mean") { YawSys.YawPos.Mean = i; }
                                else if (parts[2] == "stddev") { YawSys.YawPos.Stdv = i; }
                                else if (parts[2] == "max") { YawSys.YawPos.Maxm = i; }
                                else if (parts[2] == "min") { YawSys.YawPos.Minm = i; }
                            }
                            #endregion
                        }
                    }
                }
            }

            private void NullAllHeaderValues()
            {
                // in loading, all columns first need to be equal to the noValue so that 
                // they can be edited afterwards

                Power.Mean = _noValue;
                Power.Stdv = _noValue;
                Power.Maxm = _noValue;
                Power.Minm = _noValue;

                Power.GridFreq.Mean = _noValue;
                Power.GridFreq.Stdv = _noValue;
                Power.GridFreq.Maxm = _noValue;
                Power.GridFreq.Minm = _noValue;

                Power.PowerFactor.Mean = _noValue;
                Power.PowerFactor.Stdv = _noValue;
                Power.PowerFactor.Maxm = _noValue;
                Power.PowerFactor.Minm = _noValue;

                Power.ReactivePwr.Mean = _noValue;
                Power.ReactivePwr.Stdv = _noValue;
                Power.ReactivePwr.Maxm = _noValue;
                Power.ReactivePwr.Minm = _noValue;

                Power.Currents.PhR.Mean = _noValue;
                Power.Currents.PhR.Stdv = _noValue;
                Power.Currents.PhR.Maxm = _noValue;
                Power.Currents.PhR.Minm = _noValue;
                
                Power.Currents.PhS.Mean = _noValue;
                Power.Currents.PhS.Stdv = _noValue;
                Power.Currents.PhS.Maxm = _noValue;
                Power.Currents.PhS.Minm = _noValue;
                
                Power.Currents.PhT.Mean = _noValue;
                Power.Currents.PhT.Stdv = _noValue;
                Power.Currents.PhT.Maxm = _noValue;
                Power.Currents.PhT.Minm = _noValue;

                Power.Voltages.PhR.Mean = _noValue;
                Power.Voltages.PhR.Stdv = _noValue;
                Power.Voltages.PhR.Maxm = _noValue;
                Power.Voltages.PhR.Minm = _noValue;

                Power.Voltages.PhS.Mean = _noValue;
                Power.Voltages.PhS.Stdv = _noValue;
                Power.Voltages.PhS.Maxm = _noValue;
                Power.Voltages.PhS.Minm = _noValue;

                Power.Voltages.PhT.Mean = _noValue;
                Power.Voltages.PhT.Stdv = _noValue;
                Power.Voltages.PhT.Maxm = _noValue;
                Power.Voltages.PhT.Minm = _noValue;

                Genny.G1u1.Mean = _noValue;
                Genny.G1u1.Stdv = _noValue;
                Genny.G1u1.Maxm = _noValue;
                Genny.G1u1.Minm = _noValue;
                Genny.G1v1.Mean = _noValue;
                Genny.G1v1.Stdv = _noValue;
                Genny.G1v1.Maxm = _noValue;
                Genny.G1v1.Minm = _noValue;
                Genny.G1w1.Mean = _noValue;
                Genny.G1w1.Stdv = _noValue;
                Genny.G1w1.Maxm = _noValue;
                Genny.G1w1.Minm = _noValue;

                Genny.G2u1.Mean = _noValue;
                Genny.G2u1.Stdv = _noValue;
                Genny.G2u1.Maxm = _noValue;
                Genny.G2u1.Minm = _noValue;
                Genny.G2v1.Mean = _noValue;
                Genny.G2v1.Stdv = _noValue;
                Genny.G2v1.Maxm = _noValue;
                Genny.G2v1.Minm = _noValue;
                Genny.G2w1.Mean = _noValue;
                Genny.G2w1.Stdv = _noValue;
                Genny.G2w1.Maxm = _noValue;
                Genny.G2w1.Minm = _noValue;

                Genny.G1u1.Dlta = _noValue;
                Genny.G1v1.Dlta = _noValue;
                Genny.G1w1.Dlta = _noValue;
                Genny.G2u1.Dlta = _noValue;
                Genny.G2v1.Dlta = _noValue;
                Genny.G2w1.Dlta = _noValue;

                AmbTemps.Mean = _noValue;
                AmbTemps.Dlta = _noValue;
                AmbTemps.Stdv = _noValue;
                AmbTemps.Maxm = _noValue;
                AmbTemps.Minm = _noValue;

                DeltaTs.Mean = _noValue;
                DeltaTs.Stdv = _noValue;
                DeltaTs.Maxm = _noValue;
                DeltaTs.Minm = _noValue;

                Gearbox.HsGen.Mean = _noValue;
                Gearbox.HsGen.Stdv = _noValue;
                Gearbox.HsGen.Maxm = _noValue;
                Gearbox.HsGen.Minm = _noValue;
                Gearbox.HsRot.Mean = _noValue;
                Gearbox.HsRot.Stdv = _noValue;
                Gearbox.HsRot.Maxm = _noValue;
                Gearbox.HsRot.Minm = _noValue;
                Gearbox.HsGen.Dlta = _noValue;
                Gearbox.HsRot.Dlta = _noValue;

                Gearbox.ImsGen.Mean = _noValue;
                Gearbox.ImsGen.Stdv = _noValue;
                Gearbox.ImsGen.Maxm = _noValue;
                Gearbox.ImsGen.Minm = _noValue;
                Gearbox.ImsRot.Mean = _noValue;
                Gearbox.ImsRot.Stdv = _noValue;
                Gearbox.ImsRot.Maxm = _noValue;
                Gearbox.ImsRot.Minm = _noValue;
                Gearbox.ImsGen.Dlta = _noValue;
                Gearbox.ImsRot.Dlta = _noValue;

                Gearbox.OilTemp.Mean = _noValue;
                Gearbox.OilTemp.Stdv = _noValue;
                Gearbox.OilTemp.Maxm = _noValue;
                Gearbox.OilTemp.Minm = _noValue;
                Gearbox.OilTemp.Dlta = _noValue;

                Genny.BearingG.Mean = _noValue;
                Genny.BearingG.Stdv = _noValue;
                Genny.BearingG.Maxm = _noValue;
                Genny.BearingG.Minm = _noValue;
                Genny.BearingR.Mean = _noValue;
                Genny.BearingR.Stdv = _noValue;
                Genny.BearingR.Maxm = _noValue;
                Genny.BearingR.Minm = _noValue;
                Genny.BearingG.Dlta = _noValue;
                Genny.BearingR.Dlta = _noValue;

                MainBear.Main.Mean = _noValue;
                MainBear.Main.Stdv = _noValue;
                MainBear.Main.Maxm = _noValue;
                MainBear.Main.Minm = _noValue;
                MainBear.Gs.Mean = _noValue;
                MainBear.Gs.Stdv = _noValue;
                MainBear.Gs.Maxm = _noValue;
                MainBear.Gs.Minm = _noValue;
                MainBear.Hs.Mean = _noValue;
                MainBear.Hs.Stdv = _noValue;
                MainBear.Hs.Maxm = _noValue;
                MainBear.Hs.Minm = _noValue;

                MainBear.Main.Dlta = _noValue;
                MainBear.Gs.Dlta = _noValue;
                MainBear.Hs.Dlta = _noValue;

                Nacel.Temp.Mean = _noValue;
                Nacel.Temp.Stdv = _noValue;
                Nacel.Temp.Maxm = _noValue;
                Nacel.Temp.Minm = _noValue;

                Anemo.ActWinds.Mean = _noValue;
                Anemo.ActWinds.Stdv = _noValue;
                Anemo.ActWinds.Maxm = _noValue;
                Anemo.ActWinds.Minm = _noValue;

                Anemo.PriAnemo.Mean = _noValue;
                Anemo.PriAnemo.Stdv = _noValue;
                Anemo.PriAnemo.Maxm = _noValue;
                Anemo.PriAnemo.Minm = _noValue;

                Anemo.PriWinds.Mean = _noValue;
                Anemo.PriWinds.Stdv = _noValue;
                Anemo.PriWinds.Maxm = _noValue;
                Anemo.PriWinds.Minm = _noValue;

                Anemo.SecAnemo.Mean = _noValue;
                Anemo.SecAnemo.Stdv = _noValue;
                Anemo.SecAnemo.Maxm = _noValue;
                Anemo.SecAnemo.Minm = _noValue;

                Anemo.SecWinds.Mean = _noValue;
                Anemo.SecWinds.Stdv = _noValue;
                Anemo.SecWinds.Maxm = _noValue;
                Anemo.SecWinds.Minm = _noValue;

                Anemo.TerAnemo.Mean = _noValue;
                Anemo.TerAnemo.Stdv = _noValue;
                Anemo.TerAnemo.Maxm = _noValue;
                Anemo.TerAnemo.Minm = _noValue;

                YawSys.YawPos.Mean = _noValue;
                YawSys.YawPos.Stdv = _noValue;
                YawSys.YawPos.Maxm = _noValue;
                YawSys.YawPos.Minm = _noValue;

                Genny.RPMs.Mean = _noValue;
                Genny.RPMs.Stdv = _noValue;
                Genny.RPMs.Maxm = _noValue;
                Genny.RPMs.Minm = _noValue;
                Genny.RPMs.Dlta = _noValue;

                Tower.Humid.Mean = _noValue;
                Tower.Humid.Stdv = _noValue;
                Tower.Humid.Maxm = _noValue;
                Tower.Humid.Minm = _noValue;
            }

            #region Properties

            public int AssetCol { get { return assetCol; } set { assetCol = value; } }
            public int CurTimeCol { get { return curTimeCol; } set { curTimeCol = value; } }
            public int SamplCol { get { return sampleCol; } set { sampleCol = value; } }
            public int StatnCol { get { return stationCol; } set { stationCol = value; } }
            public int TimesCol { get { return timeCol; } set { timeCol = value; } }

            #endregion
        }

        public class ScadaSample : BaseSampleData
        {
            // this class should be usable as the representation of a sample from a turbine
            // a set of which is grouped as the data from a turbine

            #region Variables

            // the hasData flag will be used to highlight which turbine samples actually have data and 
            // which are used to fill in the timegaps
            private bool _hasData = true;

            private string[] nullValues = { "\\N" };

            private DateTime _timeStampEnd = new DateTime();

            private PowerInfo _powrInfo = new PowerInfo();
            private WindInfo _winds = new WindInfo();

            private Ambient _ambTmp = new Ambient();
            private DeltaT _deltaT = new DeltaT();

            private Board _board = new Board();
            private Brake _brake = new Brake();
            private Capacitor _capac = new Capacitor();
            private GearBox _grbox = new GearBox();
            private Generator _genny = new Generator();
            private GridFilter _grdFlt = new GridFilter();
            private Hub _hub = new Hub();
            private HydraulicOil _hydOil = new HydraulicOil();
            private Internal _intrnal = new Internal();
            private MainBearing _mainBear = new MainBearing();
            private Nacelle _nacelle = new Nacelle();
            private Reactor _reactr = new Reactor();
            private TowerInfo _tower = new TowerInfo();
            private Transformer _trafo = new Transformer();
            private YawSystem _yawSys = new YawSystem();

            #endregion

            #region Constructor

            public ScadaSample() { }

            public ScadaSample(ScadaSample input, DateTime _thisTime, bool _hasData)
            {
                // this should be a fully NaN sample created at a specific time
                this._hasData = _hasData;

                this.AssetID = input.AssetID;
                this.StationID = input.StationID;

                this.TimeStamp = _thisTime;
            }

            public ScadaSample(ScadaSample input)
            {
                // this method should go through all properties but is not referenced right now
                // should be noted as incomplete for the time being!!!
                this.TimeStamp = input.TimeStamp;
                this.AssetID = input.AssetID;
                this.StationID = input.StationID;
                this.SampleID = input.SampleID;

                this.AmbTemps = input.AmbTemps;

                this.Gearbox = input.Gearbox;
                this.Genny = input.Genny;
                this.MainBear = input.MainBear;
            }

            public ScadaSample(string[] data, ScadaHeader header, Common.DateFormat _dateFormat)
            {
                LoadData(data, header, _dateFormat);
            }

            #endregion

            public void AddDataFields(string[] data, ScadaHeader header, Common.DateFormat _dateFormat)
            {
                LoadData(data, header, _dateFormat);
            }

            private void LoadData(string[] data, ScadaHeader header, Common.DateFormat _dateFormat)
            {
                if (header.TimesCol != -1)
                {
                    TimeStamp = Common.StringToDateTime(Common.GetSplits(data[header.TimesCol], new char[] { ' ' }), _dateFormat);                    
                }

                if (header.AssetCol != -1)
                {
                    AssetID = Common.CanConvert<int>(data[header.AssetCol]) ? Convert.ToInt32(data[header.AssetCol]) : 0;
                }

                if (header.StatnCol != -1)
                {
                    StationID = Common.CanConvert<int>(data[header.StatnCol]) ? Convert.ToInt32(data[header.StatnCol]) : 0;
                }

                if (header.SamplCol != -1)
                {
                    SampleID = Common.CanConvert<int>(data[header.SamplCol]) ? Convert.ToInt32(data[header.SamplCol]) : 0;
                }

                #region Grid File

                _powrInfo.Mean = GetVals(_powrInfo.Mean, data, header.Power.Mean);
                _powrInfo.Stdv = GetVals(_powrInfo.Stdv, data, header.Power.Stdv);
                _powrInfo.Maxm = GetVals(_powrInfo.Maxm, data, header.Power.Maxm);
                _powrInfo.Minm = GetVals(_powrInfo.Minm, data, header.Power.Minm);
                _powrInfo.EndValue = GetVals(_powrInfo.EndValue, data, header.Power.EndValCol);
                _powrInfo.QualEndVal = GetVals(_powrInfo.QualEndVal, data, header.Power.QualEndValCol);
                _powrInfo.RgStEndVal = GetVals(_powrInfo.RgStEndVal, data, header.Power.RgStEndValCol);

                _powrInfo.GridFreq.Mean = GetVals(_powrInfo.GridFreq.Mean, data, header.Power.GridFreq.Mean);
                _powrInfo.GridFreq.Stdv = GetVals(_powrInfo.GridFreq.Stdv, data, header.Power.GridFreq.Stdv);
                _powrInfo.GridFreq.Maxm = GetVals(_powrInfo.GridFreq.Maxm, data, header.Power.GridFreq.Maxm);
                _powrInfo.GridFreq.Minm = GetVals(_powrInfo.GridFreq.Minm, data, header.Power.GridFreq.Minm);

                _powrInfo.PowerFactor.Mean = GetVals(_powrInfo.PowerFactor.Mean, data, header.Power.PowerFactor.Mean);
                _powrInfo.PowerFactor.Stdv = GetVals(_powrInfo.PowerFactor.Stdv, data, header.Power.PowerFactor.Stdv);
                _powrInfo.PowerFactor.Maxm = GetVals(_powrInfo.PowerFactor.Maxm, data, header.Power.PowerFactor.Maxm);
                _powrInfo.PowerFactor.Minm = GetVals(_powrInfo.PowerFactor.Minm, data, header.Power.PowerFactor.Minm);
                _powrInfo.PowerFactor.EndValue = GetVals(_powrInfo.PowerFactor.EndValue, data, header.Power.PowerFactor.EndValCol);

                _powrInfo.ReactivePwr.Mean = GetVals(_powrInfo.ReactivePwr.Mean, data, header.Power.ReactivePwr.Mean);
                _powrInfo.ReactivePwr.Stdv = GetVals(_powrInfo.ReactivePwr.Stdv, data, header.Power.ReactivePwr.Stdv);
                _powrInfo.ReactivePwr.Maxm = GetVals(_powrInfo.ReactivePwr.Maxm, data, header.Power.ReactivePwr.Maxm);
                _powrInfo.ReactivePwr.Minm = GetVals(_powrInfo.ReactivePwr.Minm, data, header.Power.ReactivePwr.Minm);
                _powrInfo.ReactivePwr.EndValue = GetVals(_powrInfo.ReactivePwr.EndValue, data, header.Power.ReactivePwr.EndValCol);

                _powrInfo.Currents.PhR.Mean = GetVals(_powrInfo.Currents.PhR.Mean, data, header.Power.Currents.PhR.Mean);
                _powrInfo.Currents.PhR.Stdv = GetVals(_powrInfo.Currents.PhR.Stdv, data, header.Power.Currents.PhR.Stdv);
                _powrInfo.Currents.PhR.Maxm = GetVals(_powrInfo.Currents.PhR.Maxm, data, header.Power.Currents.PhR.Maxm);
                _powrInfo.Currents.PhR.Minm = GetVals(_powrInfo.Currents.PhR.Minm, data, header.Power.Currents.PhR.Minm);
                _powrInfo.Currents.PhS.Mean = GetVals(_powrInfo.Currents.PhS.Mean, data, header.Power.Currents.PhS.Mean);
                _powrInfo.Currents.PhS.Stdv = GetVals(_powrInfo.Currents.PhS.Stdv, data, header.Power.Currents.PhS.Stdv);
                _powrInfo.Currents.PhS.Maxm = GetVals(_powrInfo.Currents.PhS.Maxm, data, header.Power.Currents.PhS.Maxm);
                _powrInfo.Currents.PhS.Minm = GetVals(_powrInfo.Currents.PhS.Minm, data, header.Power.Currents.PhS.Minm);
                _powrInfo.Currents.PhT.Mean = GetVals(_powrInfo.Currents.PhT.Mean, data, header.Power.Currents.PhT.Mean);
                _powrInfo.Currents.PhT.Stdv = GetVals(_powrInfo.Currents.PhT.Stdv, data, header.Power.Currents.PhT.Stdv);
                _powrInfo.Currents.PhT.Maxm = GetVals(_powrInfo.Currents.PhT.Maxm, data, header.Power.Currents.PhT.Maxm);
                _powrInfo.Currents.PhT.Minm = GetVals(_powrInfo.Currents.PhT.Minm, data, header.Power.Currents.PhT.Minm);

                _powrInfo.Voltages.PhR.Mean = GetVals(_powrInfo.Voltages.PhR.Mean, data, header.Power.Voltages.PhR.Mean);
                _powrInfo.Voltages.PhR.Stdv = GetVals(_powrInfo.Voltages.PhR.Stdv, data, header.Power.Voltages.PhR.Stdv);
                _powrInfo.Voltages.PhR.Maxm = GetVals(_powrInfo.Voltages.PhR.Maxm, data, header.Power.Voltages.PhR.Maxm);
                _powrInfo.Voltages.PhR.Minm = GetVals(_powrInfo.Voltages.PhR.Minm, data, header.Power.Voltages.PhR.Minm);
                _powrInfo.Voltages.PhS.Mean = GetVals(_powrInfo.Voltages.PhS.Mean, data, header.Power.Voltages.PhS.Mean);
                _powrInfo.Voltages.PhS.Stdv = GetVals(_powrInfo.Voltages.PhS.Stdv, data, header.Power.Voltages.PhS.Stdv);
                _powrInfo.Voltages.PhS.Maxm = GetVals(_powrInfo.Voltages.PhS.Maxm, data, header.Power.Voltages.PhS.Maxm);
                _powrInfo.Voltages.PhS.Minm = GetVals(_powrInfo.Voltages.PhS.Minm, data, header.Power.Voltages.PhS.Minm);
                _powrInfo.Voltages.PhT.Mean = GetVals(_powrInfo.Voltages.PhT.Mean, data, header.Power.Voltages.PhT.Mean);
                _powrInfo.Voltages.PhT.Stdv = GetVals(_powrInfo.Voltages.PhT.Stdv, data, header.Power.Voltages.PhT.Stdv);
                _powrInfo.Voltages.PhT.Maxm = GetVals(_powrInfo.Voltages.PhT.Maxm, data, header.Power.Voltages.PhT.Maxm);
                _powrInfo.Voltages.PhT.Minm = GetVals(_powrInfo.Voltages.PhT.Minm, data, header.Power.Voltages.PhT.Minm);

                #endregion
                #region Temperature File

                _ambTmp.Mean = GetVals(_ambTmp.Mean, data, header.AmbTemps.Mean);
                _ambTmp.Stdv = GetVals(_ambTmp.Stdv, data, header.AmbTemps.Stdv);
                _ambTmp.Maxm = GetVals(_ambTmp.Maxm, data, header.AmbTemps.Maxm);
                _ambTmp.Minm = GetVals(_ambTmp.Minm, data, header.AmbTemps.Minm);
                _ambTmp.Dlta = GetVals(_ambTmp.Dlta, data, header.AmbTemps.Dlta);

                _deltaT.Mean = GetVals(_deltaT.Mean, data, header.DeltaTs.Mean);
                _deltaT.Stdv = GetVals(_deltaT.Stdv, data, header.DeltaTs.Stdv);
                _deltaT.Maxm = GetVals(_deltaT.Maxm, data, header.DeltaTs.Maxm);
                _deltaT.Minm = GetVals(_deltaT.Minm, data, header.DeltaTs.Minm);

                _grbox.HsGen.Mean = GetVals(_grbox.HsGen.Mean, data, header.Gearbox.HsGen.Mean);
                _grbox.HsGen.Stdv = GetVals(_grbox.HsGen.Stdv, data, header.Gearbox.HsGen.Stdv);
                _grbox.HsGen.Maxm = GetVals(_grbox.HsGen.Maxm, data, header.Gearbox.HsGen.Maxm);
                _grbox.HsGen.Minm = GetVals(_grbox.HsGen.Minm, data, header.Gearbox.HsGen.Minm);
                _grbox.HsGen.Dlta = GetVals(_grbox.HsGen.Dlta, data, header.Gearbox.HsGen.Dlta);
                _grbox.HsRot.Mean = GetVals(_grbox.HsRot.Mean, data, header.Gearbox.HsRot.Mean);
                _grbox.HsRot.Stdv = GetVals(_grbox.HsRot.Stdv, data, header.Gearbox.HsRot.Stdv);
                _grbox.HsRot.Maxm = GetVals(_grbox.HsRot.Maxm, data, header.Gearbox.HsRot.Maxm);
                _grbox.HsRot.Minm = GetVals(_grbox.HsRot.Minm, data, header.Gearbox.HsRot.Minm);
                _grbox.HsRot.Dlta = GetVals(_grbox.HsRot.Dlta, data, header.Gearbox.HsRot.Dlta);

                _grbox.ImsGen.Mean = GetVals(_grbox.ImsGen.Mean, data, header.Gearbox.ImsGen.Mean);
                _grbox.ImsGen.Stdv = GetVals(_grbox.ImsGen.Stdv, data, header.Gearbox.ImsGen.Stdv);
                _grbox.ImsGen.Maxm = GetVals(_grbox.ImsGen.Maxm, data, header.Gearbox.ImsGen.Maxm);
                _grbox.ImsGen.Minm = GetVals(_grbox.ImsGen.Minm, data, header.Gearbox.ImsGen.Minm);
                _grbox.ImsGen.Dlta = GetVals(_grbox.ImsGen.Dlta, data, header.Gearbox.ImsGen.Dlta);
                _grbox.ImsRot.Mean = GetVals(_grbox.ImsRot.Mean, data, header.Gearbox.ImsRot.Mean);
                _grbox.ImsRot.Stdv = GetVals(_grbox.ImsRot.Stdv, data, header.Gearbox.ImsRot.Stdv);
                _grbox.ImsRot.Maxm = GetVals(_grbox.ImsRot.Maxm, data, header.Gearbox.ImsRot.Maxm);
                _grbox.ImsRot.Minm = GetVals(_grbox.ImsRot.Minm, data, header.Gearbox.ImsRot.Minm);
                _grbox.ImsRot.Dlta = GetVals(_grbox.ImsRot.Dlta, data, header.Gearbox.ImsRot.Dlta);

                _grbox.OilTemp.Mean = GetVals(_grbox.OilTemp.Mean, data, header.Gearbox.OilTemp.Mean);
                _grbox.OilTemp.Stdv = GetVals(_grbox.OilTemp.Stdv, data, header.Gearbox.OilTemp.Stdv);
                _grbox.OilTemp.Maxm = GetVals(_grbox.OilTemp.Maxm, data, header.Gearbox.OilTemp.Maxm);
                _grbox.OilTemp.Minm = GetVals(_grbox.OilTemp.Minm, data, header.Gearbox.OilTemp.Minm);
                _grbox.OilTemp.Dlta = GetVals(_grbox.OilTemp.Dlta, data, header.Gearbox.OilTemp.Dlta);

                _genny.G1u1.Mean = GetVals(_genny.G1u1.Mean, data, header.Genny.G1u1.Mean);
                _genny.G1u1.Stdv = GetVals(_genny.G1u1.Stdv, data, header.Genny.G1u1.Stdv);
                _genny.G1u1.Maxm = GetVals(_genny.G1u1.Maxm, data, header.Genny.G1u1.Maxm);
                _genny.G1u1.Minm = GetVals(_genny.G1u1.Minm, data, header.Genny.G1u1.Minm);
                _genny.G1v1.Mean = GetVals(_genny.G1v1.Mean, data, header.Genny.G1v1.Mean);
                _genny.G1v1.Stdv = GetVals(_genny.G1v1.Stdv, data, header.Genny.G1v1.Stdv);
                _genny.G1v1.Maxm = GetVals(_genny.G1v1.Maxm, data, header.Genny.G1v1.Maxm);
                _genny.G1v1.Minm = GetVals(_genny.G1v1.Minm, data, header.Genny.G1v1.Minm);
                _genny.G1w1.Mean = GetVals(_genny.G1w1.Mean, data, header.Genny.G1w1.Mean);
                _genny.G1w1.Stdv = GetVals(_genny.G1w1.Stdv, data, header.Genny.G1w1.Stdv);
                _genny.G1w1.Maxm = GetVals(_genny.G1w1.Maxm, data, header.Genny.G1w1.Maxm);
                _genny.G1w1.Minm = GetVals(_genny.G1w1.Minm, data, header.Genny.G1w1.Minm);

                _genny.G2u1.Mean = GetVals(_genny.G2u1.Mean, data, header.Genny.G2u1.Mean);
                _genny.G2u1.Stdv = GetVals(_genny.G2u1.Stdv, data, header.Genny.G2u1.Stdv);
                _genny.G2u1.Maxm = GetVals(_genny.G2u1.Maxm, data, header.Genny.G2u1.Maxm);
                _genny.G2u1.Minm = GetVals(_genny.G2u1.Minm, data, header.Genny.G2u1.Minm);
                _genny.G2v1.Mean = GetVals(_genny.G2v1.Mean, data, header.Genny.G2v1.Mean);
                _genny.G2v1.Stdv = GetVals(_genny.G2v1.Stdv, data, header.Genny.G2v1.Stdv);
                _genny.G2v1.Maxm = GetVals(_genny.G2v1.Maxm, data, header.Genny.G2v1.Maxm);
                _genny.G2v1.Minm = GetVals(_genny.G2v1.Minm, data, header.Genny.G2v1.Minm);
                _genny.G2w1.Mean = GetVals(_genny.G2w1.Mean, data, header.Genny.G2w1.Mean);
                _genny.G2w1.Stdv = GetVals(_genny.G2w1.Stdv, data, header.Genny.G2w1.Stdv);
                _genny.G2w1.Maxm = GetVals(_genny.G2w1.Maxm, data, header.Genny.G2w1.Maxm);
                _genny.G2w1.Minm = GetVals(_genny.G2w1.Minm, data, header.Genny.G2w1.Minm);

                _genny.G1u1.Dlta = GetVals(_genny.G1u1.Dlta, data, header.Genny.G1u1.Dlta);
                _genny.G1v1.Dlta = GetVals(_genny.G1v1.Dlta, data, header.Genny.G1v1.Dlta);
                _genny.G1w1.Dlta = GetVals(_genny.G1w1.Dlta, data, header.Genny.G1w1.Dlta);
                _genny.G2u1.Dlta = GetVals(_genny.G2u1.Dlta, data, header.Genny.G2u1.Dlta);
                _genny.G2v1.Dlta = GetVals(_genny.G2v1.Dlta, data, header.Genny.G2v1.Dlta);
                _genny.G2w1.Dlta = GetVals(_genny.G2w1.Dlta, data, header.Genny.G2w1.Dlta);

                _genny.BearingG.Mean = GetVals(_genny.BearingG.Mean, data, header.Genny.BearingG.Mean);
                _genny.BearingG.Stdv = GetVals(_genny.BearingG.Stdv, data, header.Genny.BearingG.Stdv);
                _genny.BearingG.Maxm = GetVals(_genny.BearingG.Maxm, data, header.Genny.BearingG.Maxm);
                _genny.BearingG.Minm = GetVals(_genny.BearingG.Minm, data, header.Genny.BearingG.Minm);
                _genny.BearingR.Mean = GetVals(_genny.BearingR.Mean, data, header.Genny.BearingR.Mean);
                _genny.BearingR.Stdv = GetVals(_genny.BearingR.Stdv, data, header.Genny.BearingR.Stdv);
                _genny.BearingR.Maxm = GetVals(_genny.BearingR.Maxm, data, header.Genny.BearingR.Maxm);
                _genny.BearingR.Minm = GetVals(_genny.BearingR.Minm, data, header.Genny.BearingR.Minm);

                _genny.BearingG.Dlta = GetVals(_genny.BearingG.Dlta, data, header.Genny.BearingG.Dlta);
                _genny.BearingR.Dlta = GetVals(_genny.BearingR.Dlta, data, header.Genny.BearingR.Dlta);

                _mainBear.Main.Mean = GetVals(_mainBear.Main.Mean, data, header.MainBear.Main.Mean);
                _mainBear.Main.Stdv = GetVals(_mainBear.Main.Stdv, data, header.MainBear.Main.Stdv);
                _mainBear.Main.Maxm = GetVals(_mainBear.Main.Maxm, data, header.MainBear.Main.Maxm);
                _mainBear.Main.Minm = GetVals(_mainBear.Main.Minm, data, header.MainBear.Main.Minm);

                _mainBear.Gs.Mean = GetVals(_mainBear.Gs.Mean, data, header.MainBear.Gs.Mean);
                _mainBear.Gs.Stdv = GetVals(_mainBear.Gs.Stdv, data, header.MainBear.Gs.Stdv);
                _mainBear.Gs.Maxm = GetVals(_mainBear.Gs.Maxm, data, header.MainBear.Gs.Maxm);
                _mainBear.Gs.Minm = GetVals(_mainBear.Gs.Minm, data, header.MainBear.Gs.Minm);
                _mainBear.Hs.Mean = GetVals(_mainBear.Hs.Mean, data, header.MainBear.Hs.Mean);
                _mainBear.Hs.Stdv = GetVals(_mainBear.Hs.Stdv, data, header.MainBear.Hs.Stdv);
                _mainBear.Hs.Maxm = GetVals(_mainBear.Hs.Maxm, data, header.MainBear.Hs.Maxm);
                _mainBear.Hs.Minm = GetVals(_mainBear.Hs.Minm, data, header.MainBear.Hs.Minm);

                _mainBear.Main.Dlta = GetVals(_mainBear.Main.Dlta, data, header.MainBear.Main.Dlta);
                _mainBear.Gs.Dlta = GetVals(_mainBear.Gs.Dlta, data, header.MainBear.Gs.Dlta);
                _mainBear.Hs.Dlta = GetVals(_mainBear.Hs.Dlta, data, header.MainBear.Hs.Dlta);

                _nacelle.Temp.Mean = GetVals(_nacelle.Temp.Mean, data, header.Nacel.Temp.Mean);
                _nacelle.Temp.Stdv = GetVals(_nacelle.Temp.Stdv, data, header.Nacel.Temp.Stdv);
                _nacelle.Temp.Maxm = GetVals(_nacelle.Temp.Maxm, data, header.Nacel.Temp.Maxm);
                _nacelle.Temp.Minm = GetVals(_nacelle.Temp.Minm, data, header.Nacel.Temp.Minm);

                #endregion
                #region Turbine File

                if (header.CurTimeCol != -1 && !nullValues.Contains(data[header.CurTimeCol]))
                {
                    _timeStampEnd = Common.StringToDateTime(Common.GetSplits(data[header.CurTimeCol], new char[] { ' ' }), _dateFormat);
                }

                _winds.ActWinds.Mean = GetVals(_winds.ActWinds.Mean, data, header.Anemo.ActWinds.Mean);
                _winds.ActWinds.Stdv = GetVals(_winds.ActWinds.Stdv, data, header.Anemo.ActWinds.Stdv);
                _winds.ActWinds.Maxm = GetVals(_winds.ActWinds.Maxm, data, header.Anemo.ActWinds.Maxm);
                _winds.ActWinds.Minm = GetVals(_winds.ActWinds.Minm, data, header.Anemo.ActWinds.Minm);

                _winds.PriAnemo.Mean = GetVals(_winds.PriAnemo.Mean, data, header.Anemo.PriAnemo.Mean);
                _winds.PriAnemo.Stdv = GetVals(_winds.PriAnemo.Stdv, data, header.Anemo.PriAnemo.Stdv);
                _winds.PriAnemo.Maxm = GetVals(_winds.PriAnemo.Maxm, data, header.Anemo.PriAnemo.Maxm);
                _winds.PriAnemo.Minm = GetVals(_winds.PriAnemo.Minm, data, header.Anemo.PriAnemo.Minm);

                _winds.PriWinds.Mean = GetVals(_winds.PriWinds.Mean, data, header.Anemo.PriWinds.Mean);
                _winds.PriWinds.Stdv = GetVals(_winds.PriWinds.Stdv, data, header.Anemo.PriWinds.Stdv);
                _winds.PriWinds.Maxm = GetVals(_winds.PriWinds.Maxm, data, header.Anemo.PriWinds.Maxm);
                _winds.PriWinds.Minm = GetVals(_winds.PriWinds.Minm, data, header.Anemo.PriWinds.Minm);

                _winds.SecAnemo.Mean = GetVals(_winds.SecAnemo.Mean, data, header.Anemo.SecAnemo.Mean);
                _winds.SecAnemo.Stdv = GetVals(_winds.SecAnemo.Stdv, data, header.Anemo.SecAnemo.Stdv);
                _winds.SecAnemo.Maxm = GetVals(_winds.SecAnemo.Maxm, data, header.Anemo.SecAnemo.Maxm);
                _winds.SecAnemo.Minm = GetVals(_winds.SecAnemo.Minm, data, header.Anemo.SecAnemo.Minm);

                _winds.SecWinds.Mean = GetVals(_winds.SecWinds.Mean, data, header.Anemo.SecWinds.Mean);
                _winds.SecWinds.Stdv = GetVals(_winds.SecWinds.Stdv, data, header.Anemo.SecWinds.Stdv);
                _winds.SecWinds.Maxm = GetVals(_winds.SecWinds.Maxm, data, header.Anemo.SecWinds.Maxm);
                _winds.SecWinds.Minm = GetVals(_winds.SecWinds.Minm, data, header.Anemo.SecWinds.Minm);

                _winds.TerAnemo.Mean = GetVals(_winds.TerAnemo.Mean, data, header.Anemo.TerAnemo.Mean);
                _winds.TerAnemo.Stdv = GetVals(_winds.TerAnemo.Stdv, data, header.Anemo.TerAnemo.Stdv);
                _winds.TerAnemo.Maxm = GetVals(_winds.TerAnemo.Maxm, data, header.Anemo.TerAnemo.Maxm);
                _winds.TerAnemo.Minm = GetVals(_winds.TerAnemo.Minm, data, header.Anemo.TerAnemo.Minm);

                _yawSys.YawPos.Mean = GetVals(_yawSys.YawPos.Mean, data, header.YawSys.YawPos.Mean);
                _yawSys.YawPos.Stdv = GetVals(_yawSys.YawPos.Stdv, data, header.YawSys.YawPos.Stdv);
                _yawSys.YawPos.Maxm = GetVals(_yawSys.YawPos.Maxm, data, header.YawSys.YawPos.Maxm);
                _yawSys.YawPos.Minm = GetVals(_yawSys.YawPos.Minm, data, header.YawSys.YawPos.Minm);

                _genny.RPMs.Mean = GetVals(_genny.RPMs.Mean, data, header.Genny.RPMs.Mean);
                _genny.RPMs.Stdv = GetVals(_genny.RPMs.Stdv, data, header.Genny.RPMs.Stdv);
                _genny.RPMs.Maxm = GetVals(_genny.RPMs.Maxm, data, header.Genny.RPMs.Maxm);
                _genny.RPMs.Minm = GetVals(_genny.RPMs.Minm, data, header.Genny.RPMs.Minm);
                _genny.RPMs.Dlta = GetVals(_genny.RPMs.Dlta, data, header.Genny.RPMs.Dlta);

                _tower.Humid.Mean = GetVals(_tower.Humid.Mean, data, header.Tower.Humid.Mean);
                _tower.Humid.Stdv = GetVals(_tower.Humid.Stdv, data, header.Tower.Humid.Stdv);
                _tower.Humid.Maxm = GetVals(_tower.Humid.Maxm, data, header.Tower.Humid.Maxm);
                _tower.Humid.Minm = GetVals(_tower.Humid.Minm, data, header.Tower.Humid.Minm);

                #endregion
            }

            #region Support Classes

            #region Generic Measures

            public class Ambient : Temperature
            {
                #region Constructor

                public Ambient()
                {
                    Description = "Ambient temperature";
                }

                #endregion
            }

            public class DeltaT : Temperature
            {
                #region Constructor

                public DeltaT()
                {
                    Description = "Temperature in delta modules (tower)";
                }

                #endregion
            }

            #endregion

            #region Comprehensive Measures

            public class PowerInfo : Power
            {
                #region Variables

                protected double qualEndVal = double.NaN;
                protected double rgStEndVal = double.NaN;

                protected int qualEndValCol = -1;
                protected int rgStEndValCol = -1;

                protected Frequency _gridFrequency = new Frequency();
                protected Power _powerFactor = new Power();
                protected Power _reactivePwr = new Power();

                protected Current _current = new Current();
                protected Voltage _voltage = new Voltage();

                #endregion

                #region Constructor

                public PowerInfo()
                {
                    Description = "Active power";
                    _gridFrequency.Description = "Grid frequency";
                    _powerFactor.Description = "Power factor (ratio)";
                    _reactivePwr.Description = "Reactive power (kVar)";

                    _current.PhR.Description = "Current, phase R";
                    _current.PhS.Description = "Current, phase S";
                    _current.PhT.Description = "Current, phase T";
                    _voltage.PhR.Description = "Voltage, phase R";
                    _voltage.PhS.Description = "Voltage, phase S";
                    _voltage.PhT.Description = "Voltage, phase T";
                }

                #endregion

                #region Properties

                public double QualEndVal { get { return qualEndVal; } set { qualEndVal = value; } }
                public double RgStEndVal { get { return rgStEndVal; } set { rgStEndVal = value; } }

                public int QualEndValCol { get { return qualEndValCol; } set { qualEndValCol = value; } }
                public int RgStEndValCol { get { return rgStEndValCol; } set { rgStEndValCol = value; } }

                public Frequency GridFreq { get { return _gridFrequency; } set { _gridFrequency = value; } }
                public Power PowerFactor { get { return _powerFactor; } set { _powerFactor = value; } }
                public Power ReactivePwr { get { return _reactivePwr; } set { _reactivePwr = value; } }

                public Current Currents { get { return _current; } set { _current = value; } }
                public Voltage Voltages { get { return _voltage; } set { _voltage = value; } }

                #endregion
            }

            public class WindInfo
            {
                #region Variables

                protected Speed actWinds = new Speed();
                protected Speed priAnemo = new Speed();
                protected Speed priWinds = new Speed();
                protected Speed secAnemo = new Speed();
                protected Speed secWinds = new Speed();
                protected Speed terAnemo = new Speed();

                #endregion

                #region Constructor

                public WindInfo()
                {
                    actWinds.Description = "Wind speed from active wind sensor";
                    priAnemo.Description = "Primary anemometer (mechanic)";
                    priWinds.Description = "Wind speed from primary wind sensor";
                    secAnemo.Description = "Secondary anemometer (mechanic)";
                    secWinds.Description = "Wind speed from secondary wind sensor";
                    terAnemo.Description = "Tertiary anemometer";
                }

                #endregion

                #region Properties

                public Speed ActWinds { get { return actWinds; } set { actWinds = value; } }
                public Speed PriAnemo { get { return priAnemo; } set { priAnemo = value; } }
                public Speed PriWinds { get { return priWinds; } set { priWinds = value; } }
                public Speed SecAnemo { get { return secAnemo; } set { secAnemo = value; } }
                public Speed SecWinds { get { return secWinds; } set { secWinds = value; } }
                public Speed TerAnemo { get { return terAnemo; } set { terAnemo = value; } }

                #endregion
            }

            #endregion

            #region Devices and Objects

            public class Board : Equipment
            {
                #region Variables

                protected Temperature _temp = new Temperature();

                #endregion

                #region Constructor

                public Board()
                {
                    Name = "Board";

                    _temp.Description = "Board temperature, Grid module";
                }

                #endregion

                #region Properties

                public Temperature Temp { get { return _temp; } set { _temp = value; } }

                #endregion
            }
            
            public class Brake : Equipment
            {
                #region Variables

                protected Pressure _pres = new Pressure();
                protected Temperature _gear = new Temperature();
                protected Temperature _genr = new Temperature();

                #endregion

                #region Constructor

                public Brake()
                {
                    Name = "Brake";

                    _gear.Description = "Brake temperature, GEAR";
                    _genr.Description = "Brake temperature, GEN";
                }

                #endregion
                
                #region Properties

                public Pressure Pressures { get { return _pres; } set { _pres = value; } }
                public Temperature Gears { get { return _gear; } set { _gear = value; } }
                public Temperature Genny { get { return _genr; } set { _genr = value; } }

                #endregion
            }

            public class Capacitor : Equipment
            {
                #region Variables

                protected Temperature pfm = new Temperature();
                protected Temperature pfs1 = new Temperature();

                #endregion

                #region Constructor

                public Capacitor()
                {
                    Name = "Capacitor";

                    pfm.Description = "Temperature at capacitors on the master unit";
                    pfs1.Description = "Temperature at capacitors on the slave unit 1";
                }

                #endregion

                #region Properties

                public Temperature Pfm { get { return pfm; } set { pfm = value; } }
                public Temperature Pfs1 { get { return pfs1; } set { pfs1 = value; } }

                #endregion
            }

            public class GearBox : Equipment
            {
                #region Variables

                private Pressure _oilPres = new Pressure();

                private Temperature _oilTemp = new Temperature();

                private Temperature _hsGen = new Temperature();
                private Temperature _hsRot = new Temperature();
                private Temperature _imsGen = new Temperature();
                private Temperature _imsRot = new Temperature();

                #endregion

                #region Constructor

                public GearBox()
                {
                    Name = "Gearbox";

                    _oilPres.Description = "Gear oil pressure";
                    _oilTemp.Description = "Gear oil temperature";
                    _hsGen.Description = "Gear bearing temperature, HS-GEN";
                    _hsRot.Description = "Gear bearing temperature, HS-ROT";
                    _imsGen.Description = "Gear bearing temperature, IMS-GEN";
                    _imsRot.Description = "Gear bearing temperature, IMS-ROT";
                }

                #endregion

                #region Properties

                public Pressure OilPres { get { return _oilPres; } set { _oilPres = value; } }
                public Temperature OilTemp { get { return _oilTemp; } set { _oilTemp = value; } }

                public Temperature HsGen { get { return _hsGen; } set { _hsGen = value; } }
                public Temperature HsRot { get { return _hsRot; } set { _hsRot = value; } }
                public Temperature ImsGen { get { return _imsGen; } set { _imsGen = value; } }
                public Temperature ImsRot { get { return _imsRot; } set { _imsRot = value; } }

                #endregion 
            }

            public class Generator : Equipment
            {
                #region Variables

                protected Temperature _g1u1 = new Temperature();
                protected Temperature _g1v1 = new Temperature();
                protected Temperature _g1w1 = new Temperature();
                protected Temperature _g2u1 = new Temperature();
                protected Temperature _g2v1 = new Temperature();
                protected Temperature _g2w1 = new Temperature();

                protected Revolutions _rpms = new Revolutions();

                protected Temperature _bearingG = new Temperature();
                protected Temperature _bearingR = new Temperature();
                protected Temperature _bearingIt = new Temperature();
                protected Temperature _bearingOt = new Temperature();

                #endregion

                #region Constructor

                public Generator()
                {
                    Name = "Generator";

                    _g1u1.Description = "Generator temperature, 1 U1";
                    _g1v1.Description = "Generator temperature, 1 V1";
                    _g1w1.Description = "Generator temperature, 1 W1";
                    _g2u1.Description = "Generator temperature, 2 U1";
                    _g2v1.Description = "Generator temperature, 2 V1";
                    _g2w1.Description = "Generator temperature, 2 W1";

                    _rpms.Description = "Generator RPM";

                    _bearingG.Description = "Generator bearing temperature, NDE";
                    _bearingR.Description = "Generator bearing temperature, DE";
                }        

                #endregion

                #region Properties

                public Temperature G1u1 { get { return _g1u1; } set { _g1u1 = value; } }
                public Temperature G1v1 { get { return _g1v1; } set { _g1v1 = value; } }
                public Temperature G1w1 { get { return _g1w1; } set { _g1w1 = value; } }
                public Temperature G2u1 { get { return _g2u1; } set { _g2u1 = value; } }
                public Temperature G2v1 { get { return _g2v1; } set { _g2v1 = value; } }
                public Temperature G2w1 { get { return _g2w1; } set { _g2w1 = value; } }

                public Temperature BearingG { get { return _bearingG; } set { _bearingG = value; } }
                public Temperature BearingR { get { return _bearingR; } set { _bearingR = value; } }

                public Temperature BearingIt { get { return _bearingIt; } set { _bearingIt = value; } }
                public Temperature BearingOt { get { return _bearingOt; } set { _bearingOt = value; } }

                public Revolutions RPMs { get { return _rpms; } set { _rpms = value; } }

                #endregion
            }

            public class GridFilter : Equipment
            {
                #region Variables

                protected Temperature _b1 = new Temperature();
                protected Temperature _b2 = new Temperature();
                protected Temperature _b3 = new Temperature();

                #endregion

                #region Constructor

                public GridFilter()
                {
                    Name = "Grid Filter";

                    _b1.Description = "Grid filter B1 temperature";
                    _b2.Description = "Grid filter B2 temperature";
                    _b3.Description = "Grid filter B3 temperature";
                }

                #endregion

                #region Properties

                public Temperature B1s { get { return _b1; } set { _b1 = value; } }
                public Temperature B2s { get { return _b2; } set { _b2 = value; } }
                public Temperature B3s { get { return _b3; } set { _b3 = value; } }

                #endregion
            }

            public class Hub : Equipment
            {
                #region Variables

                protected Pressure _a = new Pressure();
                protected Pressure _b = new Pressure();
                protected Pressure _c = new Pressure();

                protected Temperature _board = new Temperature();
                protected Temperature _inter = new Temperature();
                protected Temperature _ref1 = new Temperature();
                protected Temperature _ref2 = new Temperature();

                #endregion

                #region Constructor

                public Hub()
                {
                    Name = "Hub";

                    _a.Description = "Oil pressure, blade A";
                    _b.Description = "Oil pressure, blade B";
                    _c.Description = "Oil pressure, blade C";

                    _board.Description = "Hub computer board temperature";
                    _inter.Description = "Temperature in hub";
                    _ref1.Description = "Temperature reference 1, Hub computer";
                    _ref2.Description = "Temperature reference 2, Hub computer";
                }

                #endregion

                #region Properties

                public Pressure As { get { return _a; } set { _a = value; } }
                public Pressure Bs { get { return _b; } set { _b = value; } }
                public Pressure Cs { get { return _c; } set { _c = value; } }
                public Temperature Boards { get { return _board; } set { _board = value; } }
                public Temperature Internals { get { return _inter; } set { _inter = value; } }
                public Temperature Ref1s { get { return _ref1; } set { _ref1 = value; } }
                public Temperature Ref2s { get { return _ref2; } set { _ref2 = value; } }

                #endregion
            }

            public class HydraulicOil : Equipment
            {
                #region Variables

                protected Pressure _pres = new Pressure();
                protected Temperature _temp = new Temperature();

                #endregion

                #region Constructor

                public HydraulicOil()
                {
                    Name = "Hydraulic Oil";

                    _pres.Description = "Hydraulic oil pressure measured at pump station";
                    _temp.Description = "Temperature in hydraulic oil";
                }

                #endregion

                #region Properties

                public Pressure Pressures { get { return _pres; } set { _pres = value; } }
                public Temperature Temp { get { return _temp; } set { _temp = value; } }

                #endregion
            }

            public class Internal : Equipment
            {
                #region Variables

                protected Temperature _io1 = new Temperature();
                protected Temperature _io2 = new Temperature();
                protected Temperature _io3 = new Temperature();

                #endregion

                #region Constructor

                public Internal()
                {
                    Name = "Internal Modules";

                    _io1.Description = "Internal temperature at IO module 1";
                    _io2.Description = "Internal temperature at IO module 2";
                    _io3.Description = "Internal temperature at IO module 3";
                }

                #endregion

                #region Properties

                public Temperature Io1 { get { return _io1; } set { _io1 = value; } }
                public Temperature Io2 { get { return _io2; } set { _io2 = value; } }
                public Temperature Io3 { get { return _io3; } set { _io3 = value; } }

                #endregion
            }

            public class MainBearing : Equipment
            {
                #region Variables

                protected Temperature _main = new Temperature();
                protected Temperature _gs = new Temperature();
                protected Temperature _hs = new Temperature();

                #endregion

                #region Constructor

                public MainBearing()
                {
                    Name = "Main Bearing";

                    _main.Description = "Main bearing temperature measurement";

                    // below only exist if the turbine has two main bearings
                    _gs.Description = "Temperature of main bearing near the gear";
                    _hs.Description = "Temperature of main bearing near the hub";
                }

                #endregion

                #region Properties

                public Temperature Main { get { return _main; } set { _main = value; } }
                public Temperature Gs { get { return _gs; } set { _gs = value; } }
                public Temperature Hs { get { return _hs; } set { _hs = value; } }

                #endregion
            }

            public class Nacelle : Equipment
            {
                #region Variables

                private Temperature _ccoolant = new Temperature();
                private Temperature _temps = new Temperature();
                private Speed _position = new Speed();

                #endregion

                #region Constructor

                public Nacelle()
                {
                    Name = "Nacelle";

                    _ccoolant.Description = "Nacelle converter coolant temperature";
                    _temps.Description = "Nacelle temperature";
                    _position.Description = "Nacelle position";
                }

                #endregion

                #region Properties

                public Temperature CCoolant { get { return _ccoolant; } set { _ccoolant = value; } }
                public Temperature Temp { get { return _temps; } set { _temps = value; } }
                public Speed Position { get { return _position; } set { _position = value; } }

                #endregion
            }
            
            public class Reactor : Equipment
            {
                #region Variables

                protected Temperature _u = new Temperature();
                protected Temperature _v = new Temperature();
                protected Temperature _w = new Temperature();

                #endregion

                #region Constructor

                public Reactor()
                {
                    Name = "Main Reactor";

                    _u.Description = "Main reactor U temperature";
                    _v.Description = "Main reactor V temperature";
                    _w.Description = "Main reactor W temperature";
                }

                #endregion

                #region Properties

                public Temperature Us { get { return _u; } set { _u = value; } }
                public Temperature Vs { get { return _v; } set { _v = value; } }
                public Temperature Ws { get { return _w; } set { _w = value; } }

                #endregion
            }

            public class TowerInfo : Equipment
            {
                #region Variables

                protected Humidity _humid = new Humidity();
                protected Frequency _freqs = new Frequency();

                #endregion

                #region Constructor

                public TowerInfo()
                {
                    Name = "Tower";

                    _humid.Description = "Humidity in tower (%)";
                    _freqs.Description = "Tower frequency detected by GS1 (Hz)";
                }
            
                #endregion

                #region Properties

                public Humidity Humid { get { return _humid; } set { _humid = value; } }
                public Frequency Freqs { get { return _freqs; } set { _freqs = value; } }

                #endregion
            }

            public class Transformer : Equipment
            {
                #region Variables

                protected Temperature _l1 = new Temperature();
                protected Temperature _l2 = new Temperature();
                protected Temperature _l3 = new Temperature();
                protected Temperature _oil = new Temperature();
                protected Temperature _oilFilt = new Temperature();
                protected Temperature _room = new Temperature();
                protected Temperature _roomFilt = new Temperature();

                #endregion

                #region Constructor

                public Transformer()
                {
                    Name = "Transformer";

                    _l1.Description = "Transformer temperature, L1";
                    _l2.Description = "Transformer temperature, L2";
                    _l3.Description = "Transformer temperature, L3";
                    _oil.Description = "Transformer oil temperature";
                    _oilFilt.Description = "Filtered transformer oil temperature";
                    _room.Description = "Transformer room temperature";
                    _roomFilt.Description = "Filtered temperature in transformer room.";
                }

                #endregion

                #region Properties

                public Temperature L1s { get { return _l1; } set { _l1 = value; } }
                public Temperature L2s { get { return _l2; } set { _l2 = value; } }
                public Temperature L3s { get { return _l3; } set { _l3 = value; } }
                public Temperature Oils { get { return _oil; } set { _oil = value; } }
                public Temperature OilFilt { get { return _oilFilt; } set { _oilFilt = value; } }
                public Temperature Rooms { get { return _room; } set { _room = value; } }
                public Temperature RoomFilt { get { return _roomFilt; } set { _roomFilt = value; } }

                #endregion
            }

            public class YawSystem : Equipment
            {
                #region Variables

                // wtc_NacelPos is recommended when available
                protected Direction _yawPos = new Direction();

                #endregion

                #region Constructor

                public YawSystem()
                {
                    Name = "Yaw System";

                    _yawPos.Description = "Yaw position";
                }

                #endregion

                #region Properties

                public Direction YawPos { get { return _yawPos; } set { _yawPos = value; } }

                #endregion
            }

            #endregion

            public enum TurbineMake
            {
                UNKNOWN,
                SIEMENS_2_3MW,
                SIEMENS_3_6MW
            }

            #endregion

            #region Properties

            public bool HasData { get { return _hasData; } set { _hasData = value; } }
            
            public Ambient AmbTemps { get { return _ambTmp; } set { _ambTmp = value; } }
            public Board Boards { get { return _board; } set { _board = value; } }
            public Brake Brakes { get { return _brake; } set { _brake = value; } }
            public Capacitor Capac { get { return _capac; } set { _capac = value; } }
            public DeltaT DeltaTs { get { return _deltaT; } set { _deltaT = value; } }
            public GearBox Gearbox { get { return _grbox; } set { _grbox = value; } }
            public Generator Genny { get { return _genny; } set { _genny = value; } }
            public GridFilter GridFilt { get { return _grdFlt; } set { _grdFlt = value; } }
            public Hub Hubs { get { return _hub; } set { _hub = value; } }
            public HydraulicOil HydOils { get { return _hydOil; } set { _hydOil = value; } }
            public Internal Intern { get { return _intrnal; } set { _intrnal = value; } }
            public MainBearing MainBear { get { return _mainBear; } set { _mainBear = value; } }
            public Nacelle Nacel { get { return _nacelle; } set { _nacelle = value; } }
            public PowerInfo Power { get { return _powrInfo; } set { _powrInfo = value; } }
            public Reactor React { get { return _reactr; } set { _reactr = value; } }
            public TowerInfo Tower { get { return _tower; } set { _tower = value; } }
            public Transformer Trafo { get { return _trafo; } set { _trafo = value; } }
            public WindInfo Anemo { get { return _winds; } set { _winds = value; } }
            public YawSystem YawSys { get { return _yawSys; } set { _yawSys = value; } }

            #endregion
        }

        #endregion

        #region Properties

        public ScadaHeader FileHeader { get { return fileHeader; } }

        public List<TurbineData> WindFarm { get { return _windFarm; } }

        #endregion 
    }
}