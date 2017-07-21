﻿using System;
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

        // a list for including the asset IDs for all loaded turbines
        private List<int> _inclTrbn = new List<int>();

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

            for (int i = 0; i < _existingInfo.InclTrbn.Count; i++)
            {
                _inclTrbn.Add(_existingInfo.InclTrbn[i]);
            }

            for (int i = 0; i < _existingInfo.FileName.Count; i++)
            {
                FileName.Add(_existingInfo.FileName[i]);
            }
        }

        #endregion

        #region Load Data

        public void AppendFiles(string[] filenames, List<string> loadedFiles, Common.DateFormat _dateFormat, 
            int _singleTurbineLoading, TimeSpan _sampleLength, IProgress<int> progress)
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

            LoadAndSort(filenames, _dateFormat, _singleTurbineLoading, progress);
        }

        private void LoadAndSort(string[] filenames, Common.DateFormat _dateFormat, int _singleTurbineLoading, IProgress<int> progress)
        {
            // load files
            LoadFiles(filenames, _dateFormat, _singleTurbineLoading, progress);

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
        }
        
        private void LoadFiles(string[] filenames, Common.DateFormat _dateFormat, int _singleTurbineLoading, IProgress<int> progress)
        {
            for (int i = 0; i < filenames.Length; i++)
            {
                FileName.Add(filenames[i]);
                LoadScada(filenames[i], _dateFormat, _singleTurbineLoading, progress, filenames.Length, i);
            }
        }

        private void LoadScada(string filename, Common.DateFormat _dateFormat, int _singleTurbineLoading, 
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
                                if (_inclTrbn.Contains(thisAsset))
                                {
                                    int index = _windFarm.FindIndex(x => x.UnitID == thisAsset);
                                    _windFarm[index].AddData(splits, fileHeader, _dateFormat);
                                }
                                else
                                {
                                    _windFarm.Add(new TurbineData(splits, fileHeader, _dateFormat));
                                    _inclTrbn.Add(_windFarm[_windFarm.Count - 1].UnitID);
                                }
                            }
                            else
                            {
                                if (thisAsset == _singleTurbineLoading)
                                {
                                    if (_inclTrbn.Contains(thisAsset))
                                    {
                                        int index = _windFarm.FindIndex(x => x.UnitID == thisAsset);
                                        _windFarm[index].AddData(splits, fileHeader, _dateFormat);
                                    }
                                    else
                                    {
                                        _windFarm.Add(new TurbineData(splits, fileHeader, _dateFormat));
                                        _inclTrbn.Add(_windFarm[_windFarm.Count - 1].UnitID);
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
                }
                catch (WrongDateTimeException)
                {
                    throw;
                }
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
                string mode = _windFarm[i].DataSorted.GroupBy(v => v.YawPostn.DStr).OrderByDescending(g => g.Count()).First().Key;

                _windFarm[i].PrevailingWindString = mode;
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
            DateTime expStart, DateTime exprtEnd)
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
                expStart, exprtEnd);
        }

        private void WriteSCADA(IProgress<int> progress,
            bool exportPowMaxm, bool exportPowMinm, bool exportPowMean, bool exportPowStdv,
            bool exportAmbMaxm, bool exportAmbMinm, bool exportAmbMean, bool exportAmbStdv,
            bool exportWSpMaxm, bool exportWSpMinm, bool exportWSpMean, bool exportWSpStdv,
            bool exportGBxMaxm, bool exportGBxMinm, bool exportGBxMean, bool exportGBxStdv,
            bool exportGenMaxm, bool exportGenMinm, bool exportGenMean, bool exportGenStdv,
            bool exportMBrMaxm, bool exportMBrMinm, bool exportMBrMean, bool exportMBrStdv,
            bool exportNacMaxm, bool exportNacMinm, bool exportNacMean, bool exportNacStdv,
            DateTime expStart, DateTime exprtEnd)
        {
            using (StreamWriter sW = new StreamWriter(outputName))
            {
                try
                {
                    int count = 0;
                    bool header = false;

                    for (int i = 0; i < _windFarm.Count; i++)
                    {
                        for (int j = 0; j < _windFarm[i].DataSorted.Count; j++)
                        {
                            StringBuilder hB = new StringBuilder();
                            StringBuilder sB = new StringBuilder();

                            ScadaSample unit = _windFarm[i].DataSorted[j];

                            if (unit.TimeStamp >= expStart && unit.TimeStamp <= exprtEnd)
                            {
                                hB.Append("AssetUID" + ","); sB.Append(unit.AssetID + ",");
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

                                #region Necessary Data
                                if (exportPowMaxm) { hB.Append("wtc_ActPower_max" + ","); sB.Append(Common.GetStringDecimals(unit.Powers.Maxm, 1) + ","); }
                                if (exportPowMinm) { hB.Append("wtc_ActPower_min" + ","); sB.Append(Common.GetStringDecimals(unit.Powers.Minm, 1) + ","); }
                                if (exportPowMean) { hB.Append("wtc_ActPower_mean" + ","); sB.Append(Common.GetStringDecimals(unit.Powers.Mean, 1) + ","); }
                                if (exportPowStdv) { hB.Append("wtc_ActPower_stddev" + ","); sB.Append(Common.GetStringDecimals(unit.Powers.Stdv, 1) + ","); }
                                if (exportPowMean) { hB.Append("wtc_ActPower_endvalue" + ","); sB.Append(Common.GetStringDecimals(unit.Powers.EndVal, 1) + ","); }
                                if (exportPowMean) { hB.Append("wtc_ActPower_Quality_endvalue" + ","); sB.Append(Common.GetStringDecimals(unit.Powers.QualEndVal, 1) + ","); }

                                if (exportAmbMaxm) { hB.Append("wtc_AmbieTmp_max" + ","); sB.Append(Common.GetStringDecimals(unit.AmbTemps.Maxm, 1) + ","); }
                                if (exportAmbMinm) { hB.Append("wtc_AmbieTmp_min" + ","); sB.Append(Common.GetStringDecimals(unit.AmbTemps.Minm, 1) + ","); }
                                if (exportAmbMean) { hB.Append("wtc_AmbieTmp_mean" + ","); sB.Append(Common.GetStringDecimals(unit.AmbTemps.Mean, 1) + ","); }
                                if (exportAmbMean) { hB.Append("wtc_AmbieTmp_delta" + ","); sB.Append(Common.GetStringDecimals(unit.AmbTemps.Dlta, 1) + ","); }
                                if (exportAmbStdv) { hB.Append("wtc_AmbieTmp_stddev" + ","); sB.Append(Common.GetStringDecimals(unit.AmbTemps.Stdv, 1) + ","); }
                                if (exportAmbMean) { hB.Append("wtc_twrhumid_mean" + ","); sB.Append(Common.GetStringDecimals(unit.Towers.Humid.Mean, 1) + ","); }

                                if (exportWSpMaxm) { hB.Append("wtc_AcWindSp_max" + ","); sB.Append(Common.GetStringDecimals(unit.AnemoM.ActWinds.Maxm, 1) + ","); }
                                if (exportWSpMinm) { hB.Append("wtc_AcWindSp_min" + ","); sB.Append(Common.GetStringDecimals(unit.AnemoM.ActWinds.Minm, 1) + ","); }
                                if (exportWSpMean) { hB.Append("wtc_AcWindSp_mean" + ","); sB.Append(Common.GetStringDecimals(unit.AnemoM.ActWinds.Mean, 1) + ","); }
                                if (exportWSpStdv) { hB.Append("wtc_AcWindSp_stddev" + ","); sB.Append(Common.GetStringDecimals(unit.AnemoM.ActWinds.Stdv,1) + ","); }
                                if (exportWSpMaxm) { hB.Append("wtc_PrWindSp_max" + ","); sB.Append(Common.GetStringDecimals(unit.AnemoM.PriWinds.Maxm, 1) + ","); }
                                if (exportWSpMinm) { hB.Append("wtc_PrWindSp_min" + ","); sB.Append(Common.GetStringDecimals(unit.AnemoM.PriWinds.Minm, 1) + ","); }
                                if (exportWSpMean) { hB.Append("wtc_PrWindSp_mean" + ","); sB.Append(Common.GetStringDecimals(unit.AnemoM.PriWinds.Mean, 1) + ","); }
                                if (exportWSpStdv) { hB.Append("wtc_PrWindSp_stddev" + ","); sB.Append(Common.GetStringDecimals(unit.AnemoM.PriWinds.Stdv, 1) + ","); }
                                if (exportWSpMaxm) { hB.Append("wtc_SeWindSp_max" + ","); sB.Append(Common.GetStringDecimals(unit.AnemoM.SecWinds.Maxm, 1) + ","); }
                                if (exportWSpMinm) { hB.Append("wtc_SeWindSp_min" + ","); sB.Append(Common.GetStringDecimals(unit.AnemoM.SecWinds.Minm, 1) + ","); }
                                if (exportWSpMean) { hB.Append("wtc_SeWindSp_mean" + ","); sB.Append(Common.GetStringDecimals(unit.AnemoM.SecWinds.Mean, 1) + ","); }
                                if (exportWSpStdv) { hB.Append("wtc_SeWindSp_stddev" + ","); sB.Append(Common.GetStringDecimals(unit.AnemoM.SecWinds.Stdv, 1) + ","); }

                                if (exportWSpMaxm) { hB.Append("wtc_PriAnemo_max" + ","); sB.Append(Common.GetStringDecimals(unit.AnemoM.PriAnemo.Maxm, 1) + ","); }
                                if (exportWSpMinm) { hB.Append("wtc_PriAnemo_min" + ","); sB.Append(Common.GetStringDecimals(unit.AnemoM.PriAnemo.Minm, 1) + ","); }
                                if (exportWSpMean) { hB.Append("wtc_PriAnemo_mean" + ","); sB.Append(Common.GetStringDecimals(unit.AnemoM.PriAnemo.Mean, 1) + ","); }
                                if (exportWSpStdv) { hB.Append("wtc_PriAnemo_stddev" + ","); sB.Append(Common.GetStringDecimals(unit.AnemoM.PriAnemo.Stdv, 1) + ","); }
                                if (exportWSpMaxm) { hB.Append("wtc_SecAnemo_max" + ","); sB.Append(Common.GetStringDecimals(unit.AnemoM.SecAnemo.Maxm, 1) + ","); }
                                if (exportWSpMinm) { hB.Append("wtc_SecAnemo_min" + ","); sB.Append(Common.GetStringDecimals(unit.AnemoM.SecAnemo.Minm, 1) + ","); }
                                if (exportWSpMean) { hB.Append("wtc_SecAnemo_mean" + ","); sB.Append(Common.GetStringDecimals(unit.AnemoM.SecAnemo.Mean, 1) + ","); }
                                if (exportWSpStdv) { hB.Append("wtc_SecAnemo_stddev" + ","); sB.Append(Common.GetStringDecimals(unit.AnemoM.SecAnemo.Stdv, 1) + ","); }
                                if (exportWSpMaxm) { hB.Append("wtc_TetAnemo_max" + ","); sB.Append(Common.GetStringDecimals(unit.AnemoM.TerAnemo.Maxm, 1) + ","); }
                                if (exportWSpMinm) { hB.Append("wtc_TetAnemo_min" + ","); sB.Append(Common.GetStringDecimals(unit.AnemoM.TerAnemo.Minm, 1) + ","); }
                                if (exportWSpMean) { hB.Append("wtc_TetAnemo_mean" + ","); sB.Append(Common.GetStringDecimals(unit.AnemoM.TerAnemo.Mean, 1) + ","); }
                                if (exportWSpStdv) { hB.Append("wtc_TetAnemo_stddev" + ","); sB.Append(Common.GetStringDecimals(unit.AnemoM.TerAnemo.Stdv, 1) + ","); }

                                if (exportWSpMaxm) { hB.Append("wtc_YawPos_max" + ","); sB.Append(Common.GetStringDecimals(unit.YawPostn.Maxm, 1) + ","); }
                                if (exportWSpMinm) { hB.Append("wtc_YawPos_min" + ","); sB.Append(Common.GetStringDecimals(unit.YawPostn.Minm, 1) + ","); }
                                if (exportWSpMean) { hB.Append("wtc_YawPos_mean" + ","); sB.Append(Common.GetStringDecimals(unit.YawPostn.Mean, 1) + ","); }
                                if (exportWSpStdv) { hB.Append("wtc_YawPos_stddev" + ","); sB.Append(Common.GetStringDecimals(unit.YawPostn.Stdv, 1) + ","); }
                                #endregion

                                #region Nacelle

                                if (exportNacMaxm) { hB.Append("wtc_NacelTmp_max" + ","); sB.Append(Common.GetStringDecimals(unit.Nacel.Maxm, 1) + ","); }
                                if (exportNacMinm) { hB.Append("wtc_NacelTmp_min" + ","); sB.Append(Common.GetStringDecimals(unit.Nacel.Minm, 1) + ","); }
                                if (exportNacMean) { hB.Append("wtc_NacelTmp_mean" + ","); sB.Append(Common.GetStringDecimals(unit.Nacel.Mean, 1) + ","); }
                                if (exportNacStdv) { hB.Append("wtc_NacelTmp_stddev" + ","); sB.Append(Common.GetStringDecimals(unit.Nacel.Stdv, 1) + ","); }

                                #endregion

                                #region Generator
                                if (exportGenMaxm) { hB.Append("wtc_GenRpm_max" + ","); sB.Append(Common.GetStringDecimals(unit.Genny.Rpms.Maxm, 1) + ","); }
                                if (exportGenMinm) { hB.Append("wtc_GenRpm_min" + ","); sB.Append(Common.GetStringDecimals(unit.Genny.Rpms.Minm, 1) + ","); }
                                if (exportGenMean) { hB.Append("wtc_GenRpm_mean" + ","); sB.Append(Common.GetStringDecimals(unit.Genny.Rpms.Mean, 1) + ","); }
                                if (exportGenStdv) { hB.Append("wtc_GenRpm_stddev" + ","); sB.Append(Common.GetStringDecimals(unit.Genny.Rpms.Stdv, 1) + ","); }

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

                                if (exportGenMean) { hB.Append("wtc_GenRpm_delta" + ","); sB.Append(Common.GetStringDecimals(unit.Genny.Rpms.Dlta, 1) + ","); }
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
                                if (exportMBrMinm) { hB.Append("wtc_MainBTmp_mean" + ","); sB.Append(Common.GetStringDecimals(unit.MainBear.Main.Minm, 1) + ","); }
                                if (exportMBrMean) { hB.Append("wtc_MainBTmp_min" + ","); sB.Append(Common.GetStringDecimals(unit.MainBear.Main.Mean, 1) + ","); }
                                if (exportMBrStdv) { hB.Append("wtc_MainBTmp_stddev" + ","); sB.Append(Common.GetStringDecimals(unit.MainBear.Main.Stdv, 1) + ","); }

                                if (exportMBrMaxm) { hB.Append("wtc_MBearGTm_max" + ","); sB.Append(Common.GetStringDecimals(unit.MainBear.Gs.Maxm, 1) + ","); }
                                if (exportMBrMinm) { hB.Append("wtc_MBearGTm_mean" + ","); sB.Append(Common.GetStringDecimals(unit.MainBear.Gs.Minm, 1) + ","); }
                                if (exportMBrMean) { hB.Append("wtc_MBearGTm_min" + ","); sB.Append(Common.GetStringDecimals(unit.MainBear.Gs.Mean, 1) + ","); }
                                if (exportMBrStdv) { hB.Append("wtc_MBearGTm_stddev" + ","); sB.Append(Common.GetStringDecimals(unit.MainBear.Gs.Stdv, 1) + ","); }
                                if (exportMBrMaxm) { hB.Append("wtc_MBearHTm_max" + ","); sB.Append(Common.GetStringDecimals(unit.MainBear.Hs.Maxm, 1) + ","); }
                                if (exportMBrMinm) { hB.Append("wtc_MBearHTm_mean" + ","); sB.Append(Common.GetStringDecimals(unit.MainBear.Hs.Minm, 1) + ","); }
                                if (exportMBrMean) { hB.Append("wtc_MBearHTm_min" + ","); sB.Append(Common.GetStringDecimals(unit.MainBear.Hs.Mean, 1) + ","); }
                                if (exportMBrStdv) { hB.Append("wtc_MBearHTm_stddev" + ","); sB.Append(Common.GetStringDecimals(unit.MainBear.Hs.Stdv, 1) + ","); }

                                if (exportMBrMean) { hB.Append("wtc_MainBTmp_delta" + ","); sB.Append(Common.GetStringDecimals(unit.MainBear.Main.Dlta, 1) + ","); }
                                if (exportMBrMean) { hB.Append("wtc_MBearGTm_delta" + ","); sB.Append(Common.GetStringDecimals(unit.MainBear.Gs.Dlta, 1) + ","); }
                                if (exportMBrMean) { hB.Append("wtc_MBearHTm_delta" + ","); sB.Append(Common.GetStringDecimals(unit.MainBear.Hs.Dlta, 1) + ","); }
                                #endregion
                                
                                #region Gearbox
                                if (exportGBxMaxm) { hB.Append("wtc_HSGenTmp_max" + ","); sB.Append(Common.GetStringDecimals(unit.Gearbox.Hs.Gens.Maxm, 1) + ","); }
                                if (exportGBxMinm) { hB.Append("wtc_HSGenTmp_min" + ","); sB.Append(Common.GetStringDecimals(unit.Gearbox.Hs.Gens.Minm, 1) + ","); }
                                if (exportGBxMean) { hB.Append("wtc_HSGenTmp_mean" + ","); sB.Append(Common.GetStringDecimals(unit.Gearbox.Hs.Gens.Mean, 1) + ","); }
                                if (exportGBxStdv) { hB.Append("wtc_HSGenTmp_stddev" + ","); sB.Append(Common.GetStringDecimals(unit.Gearbox.Hs.Gens.Stdv, 1) + ","); }
                                if (exportGBxMaxm) { hB.Append("wtc_HSRotTmp_max" + ","); sB.Append(Common.GetStringDecimals(unit.Gearbox.Hs.Rots.Maxm, 1) + ","); }
                                if (exportGBxMinm) { hB.Append("wtc_HSRotTmp_min" + ","); sB.Append(Common.GetStringDecimals(unit.Gearbox.Hs.Rots.Minm, 1) + ","); }
                                if (exportGBxMean) { hB.Append("wtc_HSRotTmp_mean" + ","); sB.Append(Common.GetStringDecimals(unit.Gearbox.Hs.Rots.Mean, 1) + ","); }
                                if (exportGBxStdv) { hB.Append("wtc_HSRotTmp_stddev" + ","); sB.Append(Common.GetStringDecimals(unit.Gearbox.Hs.Rots.Stdv, 1) + ","); }
                                if (exportGBxMaxm) { hB.Append("wtc_IMSGenTm_max" + ","); sB.Append(Common.GetStringDecimals(unit.Gearbox.Ims.Gens.Maxm, 1) + ","); }
                                if (exportGBxMinm) { hB.Append("wtc_IMSGenTm_min" + ","); sB.Append(Common.GetStringDecimals(unit.Gearbox.Ims.Gens.Minm, 1) + ","); }
                                if (exportGBxMean) { hB.Append("wtc_IMSGenTm_mean" + ","); sB.Append(Common.GetStringDecimals(unit.Gearbox.Ims.Gens.Mean, 1) + ","); }
                                if (exportGBxStdv) { hB.Append("wtc_IMSGenTm_stddev" + ","); sB.Append(Common.GetStringDecimals(unit.Gearbox.Ims.Gens.Stdv, 1) + ","); }
                                if (exportGBxMaxm) { hB.Append("wtc_IMSRotTm_max" + ","); sB.Append(Common.GetStringDecimals(unit.Gearbox.Ims.Rots.Maxm, 1) + ","); }
                                if (exportGBxMinm) { hB.Append("wtc_IMSRotTm_min" + ","); sB.Append(Common.GetStringDecimals(unit.Gearbox.Ims.Rots.Minm, 1) + ","); }
                                if (exportGBxMean) { hB.Append("wtc_IMSRotTm_mean" + ","); sB.Append(Common.GetStringDecimals(unit.Gearbox.Ims.Rots.Mean, 1) + ","); }
                                if (exportGBxStdv) { hB.Append("wtc_IMSRotTm_stddev" + ", "); sB.Append(Common.GetStringDecimals(unit.Gearbox.Ims.Rots.Stdv, 1) + ", "); }
                                if (exportGBxMaxm) { hB.Append("wtc_GeOilTmp_max" + ","); sB.Append(Common.GetStringDecimals(unit.Gearbox.Oils.Maxm, 1) + ","); }
                                if (exportGBxMinm) { hB.Append("wtc_GeOilTmp_min" + ","); sB.Append(Common.GetStringDecimals(unit.Gearbox.Oils.Minm, 1) + ","); }
                                if (exportGBxMean) { hB.Append("wtc_GeOilTmp_mean" + ","); sB.Append(Common.GetStringDecimals(unit.Gearbox.Oils.Mean, 1) + ","); }
                                if (exportGBxStdv) { hB.Append("wtc_GeOilTmp_stddev" + ","); sB.Append(Common.GetStringDecimals(unit.Gearbox.Oils.Stdv, 1) + ","); }

                                if (exportGBxMean) { hB.Append("wtc_HSGenTmp_delta" + ","); sB.Append(Common.GetStringDecimals(unit.Gearbox.Hs.Gens.Dlta, 1) + ","); }
                                if (exportGBxMean) { hB.Append("wtc_HSRotTmp_delta" + ","); sB.Append(Common.GetStringDecimals(unit.Gearbox.Hs.Rots.Dlta, 1) + ","); }
                                if (exportGBxMean) { hB.Append("wtc_IMSGenTm_delta" + ","); sB.Append(Common.GetStringDecimals(unit.Gearbox.Ims.Gens.Dlta, 1) + ","); }
                                if (exportGBxMean) { hB.Append("wtc_IMSRotTm_delta" + ","); sB.Append(Common.GetStringDecimals(unit.Gearbox.Ims.Rots.Dlta, 1) + ","); }
                                if (exportGBxMean) { hB.Append("wtc_GeOilTmp_delta"); sB.Append(Common.GetStringDecimals(unit.Gearbox.Oils.Dlta, 1)); }
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

        #endregion

        #region Support Classes

        public class TurbineData : BaseStructure
        {
            // this class represents a single turbine with its list of data

            #region Variables

            private List<ScadaSample> data = new List<ScadaSample>();
            private List<ScadaSample> dataSorted = new List<ScadaSample>();

            #endregion

            #region Constructor

            public TurbineData() { }

            public TurbineData(string[] splits, ScadaHeader header, Common.DateFormat _dateFormat)
            {
                // this is the first sample for a turbine which is only used once, all future loading
                // goes into the other method for every turbine

                Type = Types.TURBINE;

                data.Add(new ScadaSample(splits, header, _dateFormat));
                InclDtTm.Add(data[data.Count - 1].TimeStamp);
                
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

                if (InclDtTm.Contains(thisTime))
                {
                    int index = data.FindIndex(x => x.TimeStamp == thisTime);
                    data[index].AddDataFields(splits, header, _dateFormat);
                }
                else
                {
                    data.Add(new ScadaSample(splits, header, _dateFormat));
                    InclDtTm.Add(data[data.Count - 1].TimeStamp);
                }
            }

            #region Properties

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
                                if (parts[2] == "mean") { Powers.Mean = i; }
                                else if (parts[2] == "stddev") { Powers.Stdv = i; }
                                else if (parts[2] == "max") { Powers.Maxm = i; }
                                else if (parts[2] == "min") { Powers.Minm = i; }
                                else if (parts[2] == "endvalue") { Powers.EndValCol = i; }
                                else if (parts[2] == "quality") { Powers.QualEndValCol = i; }
                            }
                            else if (parts[1] == "actregst")
                            {
                                if (parts[2] == "endvalue") { Powers.RgStEndValCol = i; }
                            }
                            else if (parts[1] == "ampphr")
                            {
                                if (parts[2] == "mean") { Currents.PhR.Mean = i; }
                                else if (parts[2] == "stddev") { Currents.PhR.Stdv = i; }
                                else if (parts[2] == "max") { Currents.PhR.Maxm = i; }
                                else if (parts[2] == "min") { Currents.PhR.Minm = i; }
                            }
                            else if (parts[1] == "ampphs")
                            {
                                if (parts[2] == "mean") { Currents.PhS.Mean = i; }
                                else if (parts[2] == "stddev") { Currents.PhS.Stdv = i; }
                                else if (parts[2] == "max") { Currents.PhS.Maxm = i; }
                                else if (parts[2] == "min") { Currents.PhS.Minm = i; }
                            }
                            else if (parts[1] == "amppht")
                            {
                                if (parts[2] == "mean") { Currents.PhT.Mean = i; }
                                else if (parts[2] == "stddev") { Currents.PhT.Stdv = i; }
                                else if (parts[2] == "max") { Currents.PhT.Maxm = i; }
                                else if (parts[2] == "min") { Currents.PhT.Minm = i; }
                            }
                            else if (parts[1] == "cosphi")
                            {
                                if (parts[2] == "mean") { Powers.PowrFact.Mean = i; }
                                else if (parts[2] == "stddev") { Powers.PowrFact.Stdv = i; }
                                else if (parts[2] == "max") { Powers.PowrFact.Maxm = i; }
                                else if (parts[2] == "min") { Powers.PowrFact.Minm = i; }
                                else if (parts[2] == "endvalue") { Powers.PowrFact.EndValCol = i; }
                            }
                            else if (parts[1] == "gridfreq")
                            {
                                if (parts[2] == "mean") { Powers.GridFreq.Mean = i; }
                                else if (parts[2] == "stddev") { Powers.GridFreq.Stdv = i; }
                                else if (parts[2] == "max") { Powers.GridFreq.Maxm = i; }
                                else if (parts[2] == "min") { Powers.GridFreq.Minm = i; }
                            }
                            else if (parts[1] == "reactpwr")
                            {
                                if (parts[2] == "mean") { Powers.Reactives.Mean = i; }
                                else if (parts[2] == "stddev") { Powers.Reactives.Stdv = i; }
                                else if (parts[2] == "max") { Powers.Reactives.Maxm = i; }
                                else if (parts[2] == "min") { Powers.Reactives.Minm = i; }
                                else if (parts[2] == "endvalue") { Powers.Reactives.EndValCol = i; }
                            }
                            else if (parts[1] == "voltphr")
                            {
                                if (parts[2] == "mean") { Voltages.phR.Mean = i; }
                                else if (parts[2] == "stddev") { Voltages.phR.Stdv = i; }
                                else if (parts[2] == "max") { Voltages.phR.Maxm = i; }
                                else if (parts[2] == "min") { Voltages.phR.Minm = i; }
                            }
                            else if (parts[1] == "voltphs")
                            {
                                if (parts[2] == "mean") { Voltages.phS.Mean = i; }
                                else if (parts[2] == "stddev") { Voltages.phS.Stdv = i; }
                                else if (parts[2] == "max") { Voltages.phS.Maxm = i; }
                                else if (parts[2] == "min") { Voltages.phS.Minm = i; }
                            }
                            else if (parts[1] == "voltpht")
                            {
                                if (parts[2] == "mean") { Voltages.phT.Mean = i; }
                                else if (parts[2] == "stddev") { Voltages.phT.Stdv = i; }
                                else if (parts[2] == "max") { Voltages.phT.Maxm = i; }
                                else if (parts[2] == "min") { Voltages.phT.Minm = i; }
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
                                if (parts[2] == "mean") { Gearbox.Oils.Mean = i; }
                                else if (parts[2] == "stddev") { Gearbox.Oils.Stdv = i; }
                                else if (parts[2] == "max") { Gearbox.Oils.Maxm = i; }
                                else if (parts[2] == "min") { Gearbox.Oils.Minm = i; }
                                else if (parts[2] == "delta") { Gearbox.Oils.Dlta = i; }
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
                                if (parts[2] == "mean") { Gearbox.Hs.Gens.Mean = i; }
                                else if (parts[2] == "stddev") { Gearbox.Hs.Gens.Stdv = i; }
                                else if (parts[2] == "max") { Gearbox.Hs.Gens.Maxm = i; }
                                else if (parts[2] == "min") { Gearbox.Hs.Gens.Minm = i; }
                                else if (parts[2] == "delta") { Gearbox.Hs.Gens.Dlta = i; }
                            }
                            else if (parts[1] == "hsrottmp")
                            {
                                if (parts[2] == "mean") { Gearbox.Hs.Rots.Mean = i; }
                                else if (parts[2] == "stddev") { Gearbox.Hs.Rots.Stdv = i; }
                                else if (parts[2] == "max") { Gearbox.Hs.Rots.Maxm = i; }
                                else if (parts[2] == "min") { Gearbox.Hs.Rots.Minm = i; }
                                else if (parts[2] == "delta") { Gearbox.Hs.Rots.Dlta = i; }
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
                                if (parts[2] == "mean") { Gearbox.Ims.Gens.Mean = i; }
                                else if (parts[2] == "stddev") { Gearbox.Ims.Gens.Stdv = i; }
                                else if (parts[2] == "max") { Gearbox.Ims.Gens.Maxm = i; }
                                else if (parts[2] == "min") { Gearbox.Ims.Gens.Minm = i; }
                                else if (parts[2] == "delta") { Gearbox.Ims.Gens.Dlta = i; }
                            }
                            else if (parts[1] == "imsrottm")
                            {
                                if (parts[2] == "mean") { Gearbox.Ims.Rots.Mean = i; }
                                else if (parts[2] == "stddev") { Gearbox.Ims.Rots.Stdv = i; }
                                else if (parts[2] == "max") { Gearbox.Ims.Rots.Maxm = i; }
                                else if (parts[2] == "min") { Gearbox.Ims.Rots.Minm = i; }
                                else if (parts[2] == "delta") { Gearbox.Ims.Rots.Dlta = i; }
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
                                if (parts[2] == "mean") { Nacel.Mean = i; }
                                else if (parts[2] == "stddev") { Nacel.Stdv = i; }
                                else if (parts[2] == "max") { Nacel.Maxm = i; }
                                else if (parts[2] == "min") { Nacel.Minm = i; }
                            }
                            #endregion
                            #region Turbine File
                            else if (parts[1] == "curtime") { curTimeCol = i; }
                            else if (parts[1] == "acwindsp")
                            {
                                if (parts[2] == "mean") { AnemoM.ActWinds.Mean = i; }
                                else if (parts[2] == "stddev") { AnemoM.ActWinds.Stdv = i; }
                                else if (parts[2] == "max") { AnemoM.ActWinds.Maxm = i; }
                                else if (parts[2] == "min") { AnemoM.ActWinds.Minm = i; }
                            }
                            else if (parts[1] == "genrpm")
                            {
                                if (parts[2] == "mean") { Genny.Rpms.Mean = i; }
                                else if (parts[2] == "stddev") { Genny.Rpms.Stdv = i; }
                                else if (parts[2] == "max") { Genny.Rpms.Maxm = i; }
                                else if (parts[2] == "min") { Genny.Rpms.Minm = i; }
                                else if (parts[2] == "delta") { Genny.Rpms.Dlta = i; }
                            }
                            else if (parts[1] == "prianemo")
                            {
                                if (parts[2] == "mean") { AnemoM.PriAnemo.Mean = i; }
                                else if (parts[2] == "stddev") { AnemoM.PriAnemo.Stdv = i; }
                                else if (parts[2] == "max") { AnemoM.PriAnemo.Maxm = i; }
                                else if (parts[2] == "min") { AnemoM.PriAnemo.Minm = i; }
                            }
                            else if (parts[1] == "prwindsp")
                            {
                                if (parts[2] == "mean") { AnemoM.PriWinds.Mean = i; }
                                else if (parts[2] == "stddev") { AnemoM.PriWinds.Stdv = i; }
                                else if (parts[2] == "max") { AnemoM.PriWinds.Maxm = i; }
                                else if (parts[2] == "min") { AnemoM.PriWinds.Minm = i; }
                            }
                            else if (parts[1] == "secanemo")
                            {
                                if (parts[2] == "mean") { AnemoM.SecAnemo.Mean = i; }
                                else if (parts[2] == "stddev") { AnemoM.SecAnemo.Stdv = i; }
                                else if (parts[2] == "max") { AnemoM.SecAnemo.Maxm = i; }
                                else if (parts[2] == "min") { AnemoM.SecAnemo.Minm = i; }
                            }
                            else if (parts[1] == "sewindsp")
                            {
                                if (parts[2] == "mean") { AnemoM.SecWinds.Mean = i; }
                                else if (parts[2] == "stddev") { AnemoM.SecWinds.Stdv = i; }
                                else if (parts[2] == "max") { AnemoM.SecWinds.Maxm = i; }
                                else if (parts[2] == "min") { AnemoM.SecWinds.Minm = i; }
                            }
                            else if (parts[1] == "tetanemo")
                            {
                                if (parts[2] == "mean") { AnemoM.TerAnemo.Mean = i; }
                                else if (parts[2] == "stddev") { AnemoM.TerAnemo.Stdv = i; }
                                else if (parts[2] == "max") { AnemoM.TerAnemo.Maxm = i; }
                                else if (parts[2] == "min") { AnemoM.TerAnemo.Minm = i; }
                            }
                            else if (parts[1] == "twrhumid")
                            {
                                if (parts[2] == "mean") { Towers.Humid.Mean = i; }
                                else if (parts[2] == "stddev") { Towers.Humid.Stdv = i; }
                                else if (parts[2] == "max") { Towers.Humid.Maxm = i; }
                                else if (parts[2] == "min") { Towers.Humid.Minm = i; }
                            }
                            else if (parts[1] == "yawpos")
                            {
                                if (parts[2] == "mean") { YawPostn.Mean = i; }
                                else if (parts[2] == "stddev") { YawPostn.Stdv = i; }
                                else if (parts[2] == "max") { YawPostn.Maxm = i; }
                                else if (parts[2] == "min") { YawPostn.Minm = i; }
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

                Powers.Mean = _noValue;
                Powers.Stdv = _noValue;
                Powers.Maxm = _noValue;
                Powers.Minm = _noValue;

                Powers.GridFreq.Mean = _noValue;
                Powers.GridFreq.Stdv = _noValue;
                Powers.GridFreq.Maxm = _noValue;
                Powers.GridFreq.Minm = _noValue;

                Powers.PowrFact.Mean = _noValue;
                Powers.PowrFact.Stdv = _noValue;
                Powers.PowrFact.Maxm = _noValue;
                Powers.PowrFact.Minm = _noValue;

                Powers.Reactives.Mean = _noValue;
                Powers.Reactives.Stdv = _noValue;
                Powers.Reactives.Maxm = _noValue;
                Powers.Reactives.Minm = _noValue;

                Currents.PhR.Mean = _noValue;
                Currents.PhR.Stdv = _noValue;
                Currents.PhR.Maxm = _noValue;
                Currents.PhR.Minm = _noValue;

                Currents.PhS.Mean = _noValue;
                Currents.PhS.Stdv = _noValue;
                Currents.PhS.Maxm = _noValue;
                Currents.PhS.Minm = _noValue;

                Currents.PhT.Mean = _noValue;
                Currents.PhT.Stdv = _noValue;
                Currents.PhT.Maxm = _noValue;
                Currents.PhT.Minm = _noValue;

                Voltages.phR.Mean = _noValue;
                Voltages.phR.Stdv = _noValue;
                Voltages.phR.Maxm = _noValue;
                Voltages.phR.Minm = _noValue;

                Voltages.phS.Mean = _noValue;
                Voltages.phS.Stdv = _noValue;
                Voltages.phS.Maxm = _noValue;
                Voltages.phS.Minm = _noValue;

                Voltages.phT.Mean = _noValue;
                Voltages.phT.Stdv = _noValue;
                Voltages.phT.Maxm = _noValue;
                Voltages.phT.Minm = _noValue;

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

                Gearbox.Hs.Gens.Mean = _noValue;
                Gearbox.Hs.Gens.Stdv = _noValue;
                Gearbox.Hs.Gens.Maxm = _noValue;
                Gearbox.Hs.Gens.Minm = _noValue;
                Gearbox.Hs.Rots.Mean = _noValue;
                Gearbox.Hs.Rots.Stdv = _noValue;
                Gearbox.Hs.Rots.Maxm = _noValue;
                Gearbox.Hs.Rots.Minm = _noValue;

                Gearbox.Ims.Gens.Mean = _noValue;
                Gearbox.Ims.Gens.Stdv = _noValue;
                Gearbox.Ims.Gens.Maxm = _noValue;
                Gearbox.Ims.Gens.Minm = _noValue;
                Gearbox.Ims.Rots.Mean = _noValue;
                Gearbox.Ims.Rots.Stdv = _noValue;
                Gearbox.Ims.Rots.Maxm = _noValue;
                Gearbox.Ims.Rots.Minm = _noValue;

                Gearbox.Ims.Gens.Dlta = _noValue;
                Gearbox.Ims.Rots.Dlta = _noValue;
                Gearbox.Hs.Gens.Dlta = _noValue;
                Gearbox.Hs.Rots.Dlta = _noValue;

                Gearbox.Oils.Mean = _noValue;
                Gearbox.Oils.Stdv = _noValue;
                Gearbox.Oils.Maxm = _noValue;
                Gearbox.Oils.Minm = _noValue;
                Gearbox.Oils.Dlta = _noValue;

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

                Nacel.Mean = _noValue;
                Nacel.Stdv = _noValue;
                Nacel.Maxm = _noValue;
                Nacel.Minm = _noValue;

                AnemoM.ActWinds.Mean = _noValue;
                AnemoM.ActWinds.Stdv = _noValue;
                AnemoM.ActWinds.Maxm = _noValue;
                AnemoM.ActWinds.Minm = _noValue;

                AnemoM.PriAnemo.Mean = _noValue;
                AnemoM.PriAnemo.Stdv = _noValue;
                AnemoM.PriAnemo.Maxm = _noValue;
                AnemoM.PriAnemo.Minm = _noValue;

                AnemoM.PriWinds.Mean = _noValue;
                AnemoM.PriWinds.Stdv = _noValue;
                AnemoM.PriWinds.Maxm = _noValue;
                AnemoM.PriWinds.Minm = _noValue;

                AnemoM.SecAnemo.Mean = _noValue;
                AnemoM.SecAnemo.Stdv = _noValue;
                AnemoM.SecAnemo.Maxm = _noValue;
                AnemoM.SecAnemo.Minm = _noValue;

                AnemoM.SecWinds.Mean = _noValue;
                AnemoM.SecWinds.Stdv = _noValue;
                AnemoM.SecWinds.Maxm = _noValue;
                AnemoM.SecWinds.Minm = _noValue;

                AnemoM.TerAnemo.Mean = _noValue;
                AnemoM.TerAnemo.Stdv = _noValue;
                AnemoM.TerAnemo.Maxm = _noValue;
                AnemoM.TerAnemo.Minm = _noValue;

                YawPostn.Mean = _noValue;
                YawPostn.Stdv = _noValue;
                YawPostn.Maxm = _noValue;
                YawPostn.Minm = _noValue;

                Genny.Rpms.Mean = _noValue;
                Genny.Rpms.Stdv = _noValue;
                Genny.Rpms.Maxm = _noValue;
                Genny.Rpms.Minm = _noValue;
                Genny.Rpms.Dlta = _noValue;

                Towers.Humid.Mean = _noValue;
                Towers.Humid.Stdv = _noValue;
                Towers.Humid.Maxm = _noValue;
                Towers.Humid.Minm = _noValue;
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

            private AmbiTmpr _ambTmp = new AmbiTmpr();
            private Anemomtr _anemoM = new Anemomtr();
            private Board _board = new Board();
            private Brake _brake = new Brake();
            private Capactor _capac = new Capactor();
            private Coolant _coolant = new Coolant();
            private Current _current = new Current();
            private DeltaT _deltaT = new DeltaT();
            private Gear _gear = new Gear();
            private GearBox _grbox = new GearBox();
            private Generator _genny = new Generator();
            private GridFiltr _grdFlt = new GridFiltr();
            private Hub _hub = new Hub();
            private HydOil _hydOil = new HydOil();
            private Internal _intrnal = new Internal();
            private MainBearing _mainBear = new MainBearing();
            private Nacelle _nacelle = new Nacelle();
            private PowerVars _powrInfo = new PowerVars();
            private Reactor _reactr = new Reactor();
            private Tower _tower = new Tower();
            private Transformer _trafo = new Transformer();
            private Voltage _voltage = new Voltage();
            private YawPos _yawPos = new YawPos();

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

                _powrInfo.Mean = GetVals(_powrInfo.Mean, data, header.Powers.Mean);
                _powrInfo.Stdv = GetVals(_powrInfo.Stdv, data, header.Powers.Stdv);
                _powrInfo.Maxm = GetVals(_powrInfo.Maxm, data, header.Powers.Maxm);
                _powrInfo.Minm = GetVals(_powrInfo.Minm, data, header.Powers.Minm);
                _powrInfo.EndVal = GetVals(_powrInfo.EndVal, data, header.Powers.EndValCol);
                _powrInfo.QualEndVal = GetVals(_powrInfo.QualEndVal, data, header.Powers.QualEndValCol);
                _powrInfo.RgStEndVal = GetVals(_powrInfo.RgStEndVal, data, header.Powers.RgStEndValCol);

                _powrInfo.GridFreq.Mean = GetVals(_powrInfo.GridFreq.Mean, data, header.Powers.GridFreq.Mean);
                _powrInfo.GridFreq.Stdv = GetVals(_powrInfo.GridFreq.Stdv, data, header.Powers.GridFreq.Stdv);
                _powrInfo.GridFreq.Maxm = GetVals(_powrInfo.GridFreq.Maxm, data, header.Powers.GridFreq.Maxm);
                _powrInfo.GridFreq.Minm = GetVals(_powrInfo.GridFreq.Minm, data, header.Powers.GridFreq.Minm);

                _powrInfo.PowrFact.Mean = GetVals(_powrInfo.PowrFact.Mean, data, header.Powers.PowrFact.Mean);
                _powrInfo.PowrFact.Stdv = GetVals(_powrInfo.PowrFact.Stdv, data, header.Powers.PowrFact.Stdv);
                _powrInfo.PowrFact.Maxm = GetVals(_powrInfo.PowrFact.Maxm, data, header.Powers.PowrFact.Maxm);
                _powrInfo.PowrFact.Minm = GetVals(_powrInfo.PowrFact.Minm, data, header.Powers.PowrFact.Minm);
                _powrInfo.PowrFact.EndVal = GetVals(_powrInfo.PowrFact.EndVal, data, header.Powers.PowrFact.EndValCol);

                _powrInfo.Reactives.Mean = GetVals(_powrInfo.Reactives.Mean, data, header.Powers.Reactives.Mean);
                _powrInfo.Reactives.Stdv = GetVals(_powrInfo.Reactives.Stdv, data, header.Powers.Reactives.Stdv);
                _powrInfo.Reactives.Maxm = GetVals(_powrInfo.Reactives.Maxm, data, header.Powers.Reactives.Maxm);
                _powrInfo.Reactives.Minm = GetVals(_powrInfo.Reactives.Minm, data, header.Powers.Reactives.Minm);
                _powrInfo.Reactives.EndVal = GetVals(_powrInfo.Reactives.EndVal, data, header.Powers.Reactives.EndValCol);

                _current.PhR.Mean = GetVals(_current.PhR.Mean, data, header.Currents.PhR.Mean);
                _current.PhR.Stdv = GetVals(_current.PhR.Stdv, data, header.Currents.PhR.Stdv);
                _current.PhR.Maxm = GetVals(_current.PhR.Maxm, data, header.Currents.PhR.Maxm);
                _current.PhR.Minm = GetVals(_current.PhR.Minm, data, header.Currents.PhR.Minm);

                _current.PhS.Mean = GetVals(_current.PhS.Mean, data, header.Currents.PhS.Mean);
                _current.PhS.Stdv = GetVals(_current.PhS.Stdv, data, header.Currents.PhS.Stdv);
                _current.PhS.Maxm = GetVals(_current.PhS.Maxm, data, header.Currents.PhS.Maxm);
                _current.PhS.Minm = GetVals(_current.PhS.Minm, data, header.Currents.PhS.Minm);

                _current.PhT.Mean = GetVals(_current.PhT.Mean, data, header.Currents.PhT.Mean);
                _current.PhT.Stdv = GetVals(_current.PhT.Stdv, data, header.Currents.PhT.Stdv);
                _current.PhT.Maxm = GetVals(_current.PhT.Maxm, data, header.Currents.PhT.Maxm);
                _current.PhT.Minm = GetVals(_current.PhT.Minm, data, header.Currents.PhT.Minm);

                _voltage.phR.Mean = GetVals(_voltage.phR.Mean, data, header.Voltages.phR.Mean);
                _voltage.phR.Stdv = GetVals(_voltage.phR.Stdv, data, header.Voltages.phR.Stdv);
                _voltage.phR.Maxm = GetVals(_voltage.phR.Maxm, data, header.Voltages.phR.Maxm);
                _voltage.phR.Minm = GetVals(_voltage.phR.Minm, data, header.Voltages.phR.Minm);

                _voltage.phS.Mean = GetVals(_voltage.phS.Mean, data, header.Voltages.phS.Mean);
                _voltage.phS.Stdv = GetVals(_voltage.phS.Stdv, data, header.Voltages.phS.Stdv);
                _voltage.phS.Maxm = GetVals(_voltage.phS.Maxm, data, header.Voltages.phS.Maxm);
                _voltage.phS.Minm = GetVals(_voltage.phS.Minm, data, header.Voltages.phS.Minm);

                _voltage.phT.Mean = GetVals(_voltage.phT.Mean, data, header.Voltages.phT.Mean);
                _voltage.phT.Stdv = GetVals(_voltage.phT.Stdv, data, header.Voltages.phT.Stdv);
                _voltage.phT.Maxm = GetVals(_voltage.phT.Maxm, data, header.Voltages.phT.Maxm);
                _voltage.phT.Minm = GetVals(_voltage.phT.Minm, data, header.Voltages.phT.Minm);

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

                _grbox.Hs.Gens.Mean = GetVals(_grbox.Hs.Gens.Mean, data, header.Gearbox.Hs.Gens.Mean);
                _grbox.Hs.Gens.Stdv = GetVals(_grbox.Hs.Gens.Stdv, data, header.Gearbox.Hs.Gens.Stdv);
                _grbox.Hs.Gens.Maxm = GetVals(_grbox.Hs.Gens.Maxm, data, header.Gearbox.Hs.Gens.Maxm);
                _grbox.Hs.Gens.Minm = GetVals(_grbox.Hs.Gens.Minm, data, header.Gearbox.Hs.Gens.Minm);
                _grbox.Hs.Gens.Dlta = GetVals(_grbox.Hs.Gens.Dlta, data, header.Gearbox.Hs.Gens.Dlta);
                _grbox.Hs.Rots.Mean = GetVals(_grbox.Hs.Rots.Mean, data, header.Gearbox.Hs.Rots.Mean);
                _grbox.Hs.Rots.Stdv = GetVals(_grbox.Hs.Rots.Stdv, data, header.Gearbox.Hs.Rots.Stdv);
                _grbox.Hs.Rots.Maxm = GetVals(_grbox.Hs.Rots.Maxm, data, header.Gearbox.Hs.Rots.Maxm);
                _grbox.Hs.Rots.Minm = GetVals(_grbox.Hs.Rots.Minm, data, header.Gearbox.Hs.Rots.Minm);
                _grbox.Hs.Rots.Dlta = GetVals(_grbox.Hs.Rots.Dlta, data, header.Gearbox.Hs.Rots.Dlta);

                _grbox.Ims.Gens.Mean = GetVals(_grbox.Ims.Gens.Mean, data, header.Gearbox.Ims.Gens.Mean);
                _grbox.Ims.Gens.Stdv = GetVals(_grbox.Ims.Gens.Stdv, data, header.Gearbox.Ims.Gens.Stdv);
                _grbox.Ims.Gens.Maxm = GetVals(_grbox.Ims.Gens.Maxm, data, header.Gearbox.Ims.Gens.Maxm);
                _grbox.Ims.Gens.Minm = GetVals(_grbox.Ims.Gens.Minm, data, header.Gearbox.Ims.Gens.Minm);
                _grbox.Ims.Gens.Dlta = GetVals(_grbox.Ims.Gens.Dlta, data, header.Gearbox.Ims.Gens.Dlta);
                _grbox.Ims.Rots.Mean = GetVals(_grbox.Ims.Rots.Mean, data, header.Gearbox.Ims.Rots.Mean);
                _grbox.Ims.Rots.Stdv = GetVals(_grbox.Ims.Rots.Stdv, data, header.Gearbox.Ims.Rots.Stdv);
                _grbox.Ims.Rots.Maxm = GetVals(_grbox.Ims.Rots.Maxm, data, header.Gearbox.Ims.Rots.Maxm);
                _grbox.Ims.Rots.Minm = GetVals(_grbox.Ims.Rots.Minm, data, header.Gearbox.Ims.Rots.Minm);
                _grbox.Ims.Rots.Dlta = GetVals(_grbox.Ims.Rots.Dlta, data, header.Gearbox.Ims.Rots.Dlta);

                _grbox.Oils.Mean = GetVals(_grbox.Oils.Mean, data, header.Gearbox.Oils.Mean);
                _grbox.Oils.Stdv = GetVals(_grbox.Oils.Stdv, data, header.Gearbox.Oils.Stdv);
                _grbox.Oils.Maxm = GetVals(_grbox.Oils.Maxm, data, header.Gearbox.Oils.Maxm);
                _grbox.Oils.Minm = GetVals(_grbox.Oils.Minm, data, header.Gearbox.Oils.Minm);
                _grbox.Oils.Dlta = GetVals(_grbox.Oils.Dlta, data, header.Gearbox.Oils.Dlta);

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

                _nacelle.Mean = GetVals(_nacelle.Mean, data, header.Nacel.Mean);
                _nacelle.Stdv = GetVals(_nacelle.Stdv, data, header.Nacel.Stdv);
                _nacelle.Maxm = GetVals(_nacelle.Maxm, data, header.Nacel.Maxm);
                _nacelle.Minm = GetVals(_nacelle.Minm, data, header.Nacel.Minm);

                #endregion
                #region Turbine File

                if (header.CurTimeCol != -1 && !nullValues.Contains(data[header.CurTimeCol]))
                {
                    _timeStampEnd = Common.StringToDateTime(Common.GetSplits(data[header.CurTimeCol], new char[] { ' ' }), _dateFormat);
                }

                _anemoM.ActWinds.Mean = GetVals(_anemoM.ActWinds.Mean, data, header.AnemoM.ActWinds.Mean);
                _anemoM.ActWinds.Stdv = GetVals(_anemoM.ActWinds.Stdv, data, header.AnemoM.ActWinds.Stdv);
                _anemoM.ActWinds.Maxm = GetVals(_anemoM.ActWinds.Maxm, data, header.AnemoM.ActWinds.Maxm);
                _anemoM.ActWinds.Minm = GetVals(_anemoM.ActWinds.Minm, data, header.AnemoM.ActWinds.Minm);

                _anemoM.PriAnemo.Mean = GetVals(_anemoM.PriAnemo.Mean, data, header.AnemoM.PriAnemo.Mean);
                _anemoM.PriAnemo.Stdv = GetVals(_anemoM.PriAnemo.Stdv, data, header.AnemoM.PriAnemo.Stdv);
                _anemoM.PriAnemo.Maxm = GetVals(_anemoM.PriAnemo.Maxm, data, header.AnemoM.PriAnemo.Maxm);
                _anemoM.PriAnemo.Minm = GetVals(_anemoM.PriAnemo.Minm, data, header.AnemoM.PriAnemo.Minm);

                _anemoM.PriWinds.Mean = GetVals(_anemoM.PriWinds.Mean, data, header.AnemoM.PriWinds.Mean);
                _anemoM.PriWinds.Stdv = GetVals(_anemoM.PriWinds.Stdv, data, header.AnemoM.PriWinds.Stdv);
                _anemoM.PriWinds.Maxm = GetVals(_anemoM.PriWinds.Maxm, data, header.AnemoM.PriWinds.Maxm);
                _anemoM.PriWinds.Minm = GetVals(_anemoM.PriWinds.Minm, data, header.AnemoM.PriWinds.Minm);

                _anemoM.SecAnemo.Mean = GetVals(_anemoM.SecAnemo.Mean, data, header.AnemoM.SecAnemo.Mean);
                _anemoM.SecAnemo.Stdv = GetVals(_anemoM.SecAnemo.Stdv, data, header.AnemoM.SecAnemo.Stdv);
                _anemoM.SecAnemo.Maxm = GetVals(_anemoM.SecAnemo.Maxm, data, header.AnemoM.SecAnemo.Maxm);
                _anemoM.SecAnemo.Minm = GetVals(_anemoM.SecAnemo.Minm, data, header.AnemoM.SecAnemo.Minm);

                _anemoM.SecWinds.Mean = GetVals(_anemoM.SecWinds.Mean, data, header.AnemoM.SecWinds.Mean);
                _anemoM.SecWinds.Stdv = GetVals(_anemoM.SecWinds.Stdv, data, header.AnemoM.SecWinds.Stdv);
                _anemoM.SecWinds.Maxm = GetVals(_anemoM.SecWinds.Maxm, data, header.AnemoM.SecWinds.Maxm);
                _anemoM.SecWinds.Minm = GetVals(_anemoM.SecWinds.Minm, data, header.AnemoM.SecWinds.Minm);

                _anemoM.TerAnemo.Mean = GetVals(_anemoM.TerAnemo.Mean, data, header.AnemoM.TerAnemo.Mean);
                _anemoM.TerAnemo.Stdv = GetVals(_anemoM.TerAnemo.Stdv, data, header.AnemoM.TerAnemo.Stdv);
                _anemoM.TerAnemo.Maxm = GetVals(_anemoM.TerAnemo.Maxm, data, header.AnemoM.TerAnemo.Maxm);
                _anemoM.TerAnemo.Minm = GetVals(_anemoM.TerAnemo.Minm, data, header.AnemoM.TerAnemo.Minm);

                _yawPos.Mean = GetVals(_yawPos.Mean, data, header.YawPostn.Mean);
                _yawPos.Stdv = GetVals(_yawPos.Stdv, data, header.YawPostn.Stdv);
                _yawPos.Maxm = GetVals(_yawPos.Maxm, data, header.YawPostn.Maxm);
                _yawPos.Minm = GetVals(_yawPos.Minm, data, header.YawPostn.Minm);
                _yawPos.Dirc = _yawPos.Mean;

                _genny.Rpms.Mean = GetVals(_genny.Rpms.Mean, data, header.Genny.Rpms.Mean);
                _genny.Rpms.Stdv = GetVals(_genny.Rpms.Stdv, data, header.Genny.Rpms.Stdv);
                _genny.Rpms.Maxm = GetVals(_genny.Rpms.Maxm, data, header.Genny.Rpms.Maxm);
                _genny.Rpms.Minm = GetVals(_genny.Rpms.Minm, data, header.Genny.Rpms.Minm);
                _genny.Rpms.Dlta = GetVals(_genny.Rpms.Dlta, data, header.Genny.Rpms.Dlta);

                _tower.Humid.Mean = GetVals(_tower.Humid.Mean, data, header.Towers.Humid.Mean);
                _tower.Humid.Stdv = GetVals(_tower.Humid.Stdv, data, header.Towers.Humid.Stdv);
                _tower.Humid.Maxm = GetVals(_tower.Humid.Maxm, data, header.Towers.Humid.Maxm);
                _tower.Humid.Minm = GetVals(_tower.Humid.Minm, data, header.Towers.Humid.Minm);

                #endregion
            }

            #region Support Classes

            #region Base Variables

            public class Pressure : Stats { }
            public class Temperature : Stats { }

            #endregion 

            public class AmbiTmpr : Temperature { }
            public class Board : Temperature { }
            public class Coolant : Temperature { }
            public class DeltaT : Temperature { }
            public class Gear : Pressure { }
            public class Nacelle : Temperature { }
            public class YawPos : WindSpeeds { }

            public class Anemomtr
            {
                #region Variables

                protected WindSpeeds actWinds = new WindSpeeds();
                protected WindSpeeds priAnemo = new WindSpeeds();
                protected WindSpeeds priWinds = new WindSpeeds();
                protected WindSpeeds secAnemo = new WindSpeeds();
                protected WindSpeeds secWinds = new WindSpeeds();
                protected WindSpeeds terAnemo = new WindSpeeds();

                #endregion

                #region Properties

                public WindSpeeds ActWinds { get { return actWinds; } set { actWinds = value; } }
                public WindSpeeds PriAnemo { get { return priAnemo; } set { priAnemo = value; } }
                public WindSpeeds PriWinds { get { return priWinds; } set { priWinds = value; } }
                public WindSpeeds SecAnemo { get { return secAnemo; } set { secAnemo = value; } }
                public WindSpeeds SecWinds { get { return secWinds; } set { secWinds = value; } }
                public WindSpeeds TerAnemo { get { return terAnemo; } set { terAnemo = value; } }

                #endregion
            }

            public class Brake
            {
                #region Variables

                protected Pressure pressures = new Pressure();
                protected Gear gears = new Gear();
                protected Generator generators = new Generator();

                #endregion

                #region Support Classes

                public class Pressure : Stats { }

                public class Gear : Temperature { }
                public class Generator : Temperature { }

                #endregion

                #region Properties

                public Pressure Pressures { get { return pressures; } set { pressures = value; } }
                public Gear Gears { get { return gears; } set { gears = value; } }
                public Generator Generators { get { return generators; } set { generators = value; } }

                #endregion
            }

            public class Capactor
            {
                #region Variables

                protected PFM pfm = new PFM();
                protected PFS1 pfs1 = new PFS1();

                #endregion

                #region Support Classes

                public class PFM : Temperature { }
                public class PFS1 : Temperature { }

                #endregion

                #region Properties

                public PFM Pfm { get { return pfm; } set { pfm = value; } }
                public PFS1 Pfs1 { get { return pfs1; } set { pfs1 = value; } }

                #endregion
            }

            public class Current
            {
                #region Variables

                protected Phr _phr = new Phr();
                protected Phs _phs = new Phs();
                protected Pht _pht = new Pht();

                #endregion

                #region Support Classes

                public class Phr : Stats { }
                public class Phs : Stats { }
                public class Pht : Stats { }

                #endregion

                #region Properties

                public Phr PhR { get { return _phr; } set { _phr = value; } }
                public Phs PhS { get { return _phs; } set { _phs = value; } }
                public Pht PhT { get { return _pht; } set { _pht = value; } }

                #endregion
            }

            public class GearBox
            {
                #region Variables

                private Oil oils = new Oil();
                private HS hs = new HS();
                private IMS ims = new IMS();

                #endregion

                #region Support Classes

                public class Oil : Temperature { }

                public class HS
                {
                    #region Variables

                    protected Gen gen = new Gen();
                    protected Rot rot = new Rot();

                    #endregion

                    #region Support Classes

                    public class Gen : Temperature { }
                    public class Rot : Temperature { }

                    #endregion

                    #region Properties

                    public Gen Gens { get { return gen; } set { gen = value; } }
                    public Rot Rots { get { return rot; } set { rot = value; } }

                    #endregion
                }

                public class IMS
                {
                    #region Variables

                    protected Gen gen = new Gen();
                    protected Rot rot = new Rot();

                    #endregion

                    #region Support Classes

                    public class Gen : Temperature { }
                    public class Rot : Temperature { }

                    #endregion

                    #region Properties

                    public Gen Gens { get { return gen; } set { gen = value; } }
                    public Rot Rots { get { return rot; } set { rot = value; } }

                    #endregion
                }

                #endregion

                #region Properties

                public Oil Oils { get { return oils; } set { oils = value; } }
                public HS Hs { get { return hs; } set { hs = value; } }
                public IMS Ims { get { return ims; } set { ims = value; } }

                #endregion 
            }

            public class Generator
            {
                #region Variables

                protected G1U1 g1u1 = new G1U1();
                protected G1V1 g1v1 = new G1V1();
                protected G1W1 g1w1 = new G1W1();
                protected G2U1 g2u1 = new G2U1();
                protected G2V1 g2v1 = new G2V1();
                protected G2W1 g2w1 = new G2W1();
                protected BearingsG _bearingG = new BearingsG();
                protected BearingsR _bearingR = new BearingsR();
                protected Rpm rpms = new Rpm();

                #endregion

                #region Support Classes

                public class Rpm : Stats { }

                public class G1U1 : Temperature { }
                public class G1V1 : Temperature { }
                public class G1W1 : Temperature { }

                public class G2U1 : Temperature { }
                public class G2V1 : Temperature { }
                public class G2W1 : Temperature { }

                public class BearingsG : Temperature { }
                public class BearingsR : Temperature { }

                #endregion

                #region Properties

                public G1U1 G1u1 { get { return g1u1; } set { g1u1 = value; } }
                public G1V1 G1v1 { get { return g1v1; } set { g1v1 = value; } }
                public G1W1 G1w1 { get { return g1w1; } set { g1w1 = value; } }
                public G2U1 G2u1 { get { return g2u1; } set { g2u1 = value; } }
                public G2V1 G2v1 { get { return g2v1; } set { g2v1 = value; } }
                public G2W1 G2w1 { get { return g2w1; } set { g2w1 = value; } }
                public BearingsG BearingG { get { return _bearingG; } set { _bearingG = value; } }
                public BearingsR BearingR { get { return _bearingR; } set { _bearingR = value; } }
                public Rpm Rpms { get { return rpms; } set { rpms = value; } }

                #endregion
            }

            public class GridFiltr
            {
                #region Variables

                protected B1 b1 = new B1();
                protected B2 b2 = new B2();
                protected B3 b3 = new B3();

                #endregion

                #region Support Classes

                public class B1 : Temperature { }
                public class B2 : Temperature { }
                public class B3 : Temperature { }

                #endregion

                #region Properties

                public B1 B1s { get { return b1; } set { b1 = value; } }
                public B2 B2s { get { return b2; } set { b2 = value; } }
                public B3 B3s { get { return b3; } set { b3 = value; } }

                #endregion
            }

            public class Hub
            {
                #region Variables

                protected A a = new A();
                protected B b = new B();
                protected C c = new C();
                protected Board board = new Board();
                protected Internal inter = new Internal();
                protected Ref1 ref1 = new Ref1();
                protected Ref2 ref2 = new Ref2();

                #endregion

                #region Support Classes

                public class A : Pressure { }
                public class B : Pressure { }
                public class C : Pressure { }

                public class Board : Temperature { }
                public class Internal : Temperature { }

                public class Ref1 : Temperature { }
                public class Ref2 : Temperature { }

                #endregion

                #region Properties

                public A As { get { return a; } set { a = value; } }
                public B Bs { get { return b; } set { b = value; } }
                public C Cs { get { return c; } set { c = value; } }
                public Board Boards { get { return board; } set { board = value; } }
                public Internal Internals { get { return inter; } set { inter = value; } }
                public Ref1 Ref1s { get { return ref1; } set { ref1 = value; } }
                public Ref2 Ref2s { get { return ref2; } set { ref2 = value; } }

                #endregion
            }

            public class HydOil
            {
                #region Variables

                protected Pressure pressures = new Pressure();
                protected Temperature temp = new Temperature();

                #endregion

                #region Support Classes

                public class Pressure : Stats { }
                public class Temperature : Stats { }

                #endregion

                #region Properties

                public Pressure Pressures { get { return pressures; } set { pressures = value; } }
                public Temperature Temp { get { return temp; } set { temp = value; } }

                #endregion
            }

            public class Internal
            {
                #region Variables

                protected IO1 io1 = new IO1();
                protected IO2 io2 = new IO2();
                protected IO3 io3 = new IO3();

                #endregion

                #region Support Classes

                public class IO1 : Temperature { }
                public class IO2 : Temperature { }
                public class IO3 : Temperature { }

                #endregion

                #region Properties

                public IO1 Io1 { get { return io1; } set { io1 = value; } }
                public IO2 Io2 { get { return io2; } set { io2 = value; } }
                public IO3 Io3 { get { return io3; } set { io3 = value; } }

                #endregion
            }

            public class MainBearing
            {
                #region Variables

                protected Mains _main = new Mains();
                protected Gss _g = new Gss();
                protected Hss _h = new Hss();

                #endregion

                #region Support Classes

                public class Mains : Temperature { }

                public class Gss : Temperature { }
                public class Hss : Temperature { }

                #endregion

                #region Properties

                public Mains Main { get { return _main; } set { _main = value; } }
                public Gss Gs { get { return _g; } set { _g = value; } }
                public Hss Hs { get { return _h; } set { _h = value; } }

                #endregion
            }

            public class PowerVars : Stats
            {
                #region Variables

                protected double endValue = double.NaN;
                protected double qualEndVal = double.NaN;
                protected double rgStEndVal = double.NaN;
                protected int endValCol = -1, qualEndValCol = -1, rgStEndValCol = -1;

                protected GridFrequency gridFreq = new GridFrequency();
                protected PowerFactor powrFact = new PowerFactor();
                protected Reactive reactive = new Reactive();

                #endregion

                #region Support Classes            

                public class PowerFactor : Stats
                {
                    #region Variables

                    protected double endVal = double.NaN;
                    protected int endValCol = -1;

                    #endregion

                    #region Properties

                    public double EndVal { get { return endVal; } set { endVal = value; } }
                    public int EndValCol { get { return endValCol; } set { endValCol = value; } }

                    #endregion
                }

                public class GridFrequency : Stats
                {
                    #region Variables

                    protected double endValue = double.NaN;
                    protected int endValCol = -1;

                    #endregion

                    #region Properties

                    public double EndValue { get { return endValue; } set { endValue = value; } }
                    public int EndValCol { get { return endValCol; } set { endValCol = value; } }

                    #endregion
                }

                public class Reactive : Stats
                {
                    #region Variables

                    protected double endVal = double.NaN;
                    protected int endValCol = -1;

                    #endregion

                    #region Properties

                    public double EndVal { get { return endVal; } set { endVal = value; } }
                    public int EndValCol { get { return endValCol; } set { endValCol = value; } }

                    #endregion
                }

                #endregion

                #region Properties

                public double EndVal { get { return endValue; } set { endValue = value; } }
                public double QualEndVal { get { return qualEndVal; } set { qualEndVal = value; } }
                public double RgStEndVal { get { return rgStEndVal; } set { rgStEndVal = value; } }

                public int EndValCol { get { return endValCol; } set { endValCol = value; } }
                public int QualEndValCol { get { return qualEndValCol; } set { qualEndValCol = value; } }
                public int RgStEndValCol { get { return rgStEndValCol; } set { rgStEndValCol = value; } }

                public GridFrequency GridFreq { get { return gridFreq; } set { gridFreq = value; } }
                public PowerFactor PowrFact { get { return powrFact; } set { powrFact = value; } }
                public Reactive Reactives { get { return reactive; } set { reactive = value; } }

                #endregion
            }

            public class Reactor
            {
                #region Variables

                protected U u = new U();
                protected V v = new V();
                protected W w = new W();

                #endregion

                #region Support Classes

                public class U : Temperature { }
                public class V : Temperature { }
                public class W : Temperature { }

                #endregion

                #region Properties

                public U Us { get { return u; } set { u = value; } }
                public V Vs { get { return v; } set { v = value; } }
                public W Ws { get { return w; } set { w = value; } }

                #endregion
            }

            public class Tower
            {
                #region Variables

                protected Humidity humid = new Humidity();
                protected Frequenc freqs = new Frequenc();

                #endregion

                #region Support Classes

                public class Humidity : Stats { }
                public class Frequenc : Stats { }

                #endregion

                #region Properties

                public Humidity Humid { get { return humid; } set { humid = value; } }
                public Frequenc Freqs { get { return freqs; } set { freqs = value; } }

                #endregion
            }

            public class Transformer
            {
                #region Variables

                protected L1 l1 = new L1();
                protected L2 l2 = new L2();
                protected L3 l3 = new L3();
                protected Oil oil = new Oil();
                protected OilFiltered oilFilt = new OilFiltered();
                protected Room room = new Room();
                protected RoomFiltered roomFilt = new RoomFiltered();

                #endregion

                #region Support Classes

                public class L1 : Temperature { }
                public class L2 : Temperature { }
                public class L3 : Temperature { }
                public class Oil : Temperature { }
                public class OilFiltered : Temperature { }
                public class Room : Temperature { }
                public class RoomFiltered : Temperature { }

                #endregion

                #region Properties

                public L1 L1s { get { return l1; } set { l1 = value; } }
                public L2 L2s { get { return l2; } set { l2 = value; } }
                public L3 L3s { get { return l3; } set { l3 = value; } }
                public Oil Oils { get { return oil; } set { oil = value; } }
                public OilFiltered OilFilt { get { return oilFilt; } set { oilFilt = value; } }
                public Room Rooms { get { return room; } set { room = value; } }
                public RoomFiltered RoomFilt { get { return roomFilt; } set { roomFilt = value; } }

                #endregion
            }

            public class Voltage
            {
                #region Variables

                protected PhR phr = new PhR();
                protected PhS phs = new PhS();
                protected PhT pht = new PhT();

                #endregion

                #region Support Classes

                public class PhR : Stats { }
                public class PhS : Stats { }
                public class PhT : Stats { }

                #endregion

                #region Properties

                public PhR phR { get { return phr; } set { phr = value; } }
                public PhS phS { get { return phs; } set { phs = value; } }
                public PhT phT { get { return pht; } set { pht = value; } }

                #endregion
            }

            public enum TurbineMake
            {
                SIEMENS_2_3MW,
                UNK
            }

            #endregion

            #region Properties

            public bool HasData { get { return _hasData; } set { _hasData = value; } }

            public DateTime TimeStampEnd {  get { return _timeStampEnd; } set { _timeStampEnd = value; } }

            public AmbiTmpr AmbTemps { get { return _ambTmp; } set { _ambTmp = value; } }
            public Anemomtr AnemoM { get { return _anemoM; } set { _anemoM = value; } }
            public Board Boards { get { return _board; } set { _board = value; } }
            public Brake Brakes { get { return _brake; } set { _brake = value; } }
            public Capactor Capac { get { return _capac; } set { _capac = value; } }
            public Coolant Coolants { get { return _coolant; } set { _coolant = value; } }
            public Current Currents { get { return _current; } set { _current = value; } }
            public DeltaT DeltaTs { get { return _deltaT; } set { _deltaT = value; } }
            public Gear Gears { get { return _gear; } set { _gear = value; } }
            public GearBox Gearbox { get { return _grbox; } set { _grbox = value; } }
            public Generator Genny { get { return _genny; } set { _genny = value; } }
            public GridFiltr GridFilt { get { return _grdFlt; } set { _grdFlt = value; } }
            public Hub Hubs { get { return _hub; } set { _hub = value; } }
            public HydOil HydOils { get { return _hydOil; } set { _hydOil = value; } }
            public Internal Intern { get { return _intrnal; } set { _intrnal = value; } }
            public MainBearing MainBear { get { return _mainBear; } set { _mainBear = value; } }
            public Nacelle Nacel { get { return _nacelle; } set { _nacelle = value; } }
            public PowerVars Powers { get { return _powrInfo; } set { _powrInfo = value; } }
            public Reactor React { get { return _reactr; } set { _reactr = value; } }
            public Tower Towers { get { return _tower; } set { _tower = value; } }
            public Transformer Transf { get { return _trafo; } set { _trafo = value; } }
            public Voltage Voltages { get { return _voltage; } set { _voltage = value; } }
            public YawPos YawPostn { get { return _yawPos; } set { _yawPos = value; } }

            #endregion
        }

        #endregion

        #region Properties

        public ScadaHeader FileHeader { get { return fileHeader; } }

        public List<int> InclTrbn { get { return _inclTrbn; } }

        public List<TurbineData> WindFarm { get { return _windFarm; } }

        #endregion 
    }
}