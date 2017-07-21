using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using scada_analyst.Shared;

namespace scada_analyst
{
    public class MeteoData : BaseMetaData
    {
        #region Variables

        private string _outputName;

        private List<int> _inclMetMasts = new List<int>();

        private MeteoHeader _meteoHeader = new MeteoHeader();

        private List<MetMastData> _metMasts = new List<MetMastData>();

        #endregion

        #region Constructor

        public MeteoData() { }

        #endregion

        #region Load Data

        /// <summary>
        /// This method creates a copy of an existing instance of a ScadaData class
        /// </summary>
        /// <param name="_existingInfo"></param>
        public MeteoData(MeteoData _existingInfo)
        {
            for (int i = 0; i < _existingInfo.MetMasts.Count; i++)
            {
                _metMasts.Add(_existingInfo.MetMasts[i]);
            }

            for (int i = 0; i < _existingInfo.InclMetm.Count; i++)
            {
                _inclMetMasts.Add(_existingInfo.InclMetm[i]);
            }

            for (int i = 0; i < _existingInfo.FileName.Count; i++)
            {
                FileName.Add(_existingInfo.FileName[i]);
            }
        }

        public void AppendFiles(string[] filenames, List<string> loadedFiles, Common.DateFormat _dateFormat, IProgress<int> progress)
        {
            for (int i = filenames.Length - 1; i >= 0; i--)
            {
                if (loadedFiles.Contains(filenames[i]))
                {
                    filenames = filenames.Where(w => w != filenames[i]).ToArray();
                }
            }

            LoadAndSort(filenames, _dateFormat, progress);
        }

        private void LoadAndSort(string[] filenames, Common.DateFormat _dateFormat, IProgress<int> progress)
        {
            LoadMetFiles(filenames, _dateFormat, progress);

            SortMeteorology();
            PopulateTimeDif();
            GetBearings();

            _metMasts = _metMasts.OrderBy(o => o.UnitID).ToList();
        }

        private void LoadMetFiles(string[] filenames, Common.DateFormat _dateFormat, IProgress<int> progress)
        {
            for (int i = 0; i < filenames.Length; i++)
            {
                FileName.Add(filenames[i]);
                LoadMeteorology(filenames[i], _dateFormat, progress, filenames.Length, i);
            }
        }

        private void LoadMeteorology(string filename, Common.DateFormat _dateFormat, IProgress<int> progress, int numberOfFiles = 1, int i = 0)
        {
            using (StreamReader sR = new StreamReader(filename))
            {
                try
                {
                    int count = 0;
                    bool readHeader = false;

                    _metMasts = new List<MetMastData>();

                    while (!sR.EndOfStream)
                    {
                        if (readHeader == false)
                        {
                            string header = sR.ReadLine();
                            header = header.ToLower().Replace("\"", String.Empty);
                            readHeader = true;

                            if (!header.Contains("met")) { throw new WrongFileTypeException(); }

                            _meteoHeader = new MeteoHeader(header);
                        }

                        string line = sR.ReadLine();

                        if (!line.Equals(""))
                        {
                            line = line.Replace("\"", String.Empty);

                            string[] splits = Common.GetSplits(line, ',');

                            int thisAsset;

                            // if the file does not have an AssetID column, the Station Column should be used instead
                            if (_meteoHeader.AssetCol != -1)
                            {
                                thisAsset = Common.CanConvert<int>(splits[_meteoHeader.AssetCol]) ?
                                  Convert.ToInt32(splits[_meteoHeader.AssetCol]) : throw new FileFormatException();
                            }
                            else
                            {
                                thisAsset = Common.CanConvert<int>(splits[_meteoHeader.StatnCol]) ?
                                  Convert.ToInt32(splits[_meteoHeader.StatnCol]) : throw new FileFormatException();
                            }

                            // organise loading so it would check which ones have already
                            // been loaded; then work around the ones have have been

                            if (_inclMetMasts.Contains(thisAsset))
                            {
                                int index = _metMasts.FindIndex(x => x.UnitID == thisAsset);
                                _metMasts[index].AddData(splits, _meteoHeader, _dateFormat);
                            }
                            else
                            {
                                _metMasts.Add(new MetMastData(splits, _meteoHeader, _dateFormat));
                                _inclMetMasts.Add(_metMasts[_metMasts.Count - 1].UnitID);
                            }
                        }

                        count++;

                        if (count % 500 == 0)
                        {
                            if (progress != null)
                            {
                                progress.Report((int)((double)100 / numberOfFiles * i +
                                 (double)sR.BaseStream.Position * 100 / sR.BaseStream.Length / numberOfFiles));
                            }
                        }
                    }
                }
                catch
                {
                    throw;
                }
                finally
                {
                    sR.Close();
                }
            }
        }

        private void GetBearings()
        {
            for (int i = 0; i < _metMasts.Count;i++)
            {
                string mode;
                
                if (_meteoHeader.Dircs.Measured == MeteoSample.HeightInfo.MeasuringHeight.UNKNOWN)
                {
                    mode = "Unknown";
                }
                else if (_meteoHeader.Dircs.Measured == MeteoSample.HeightInfo.MeasuringHeight.M_10)
                {
                    mode = _metMasts[i].MetDataSorted.GroupBy(v => v.Dircs.Metres10.DStr).OrderByDescending(g => g.Count()).First().Key;
                }
                else
                {
                    mode = _metMasts[i].MetDataSorted.GroupBy(v => v.Dircs.MetresRt.DStr).OrderByDescending(g => g.Count()).First().Key;
                }

                _metMasts[i].PrevailingWindString = mode;
            }
        }

        private void PopulateTimeDif()
        {
            for (int i = 0; i < _metMasts.Count; i++)
            {
                for (int j = 1; j < _metMasts[i].MetDataSorted.Count; j++)
                {
                    _metMasts[i].MetDataSorted[j].SampleSeparation = _metMasts[i].MetDataSorted[j].TimeStamp - _metMasts[i].MetDataSorted[j - 1].TimeStamp;
                }
            }
        }

        private void SortMeteorology()
        {
            for (int i = 0; i < _metMasts.Count; i++)
            {
                _metMasts[i].MetDataSorted = _metMasts[i].MetData.OrderBy(o => o.TimeStamp).ToList();
            }
        }

        #endregion

        #region Export Data

        public void ExportFiles(IProgress<int> progress, string output, DateTime startExp, DateTime endExprt)
        {
            // feed in proper arguments for this output file name and assign these
            _outputName = output;

            // write the SCADA file out in a reasonable method
            WriteMeteo(progress, startExp, endExprt);
        }

        private void WriteMeteo(IProgress<int> progress, DateTime startExp, DateTime endExprt)
        {
            using (StreamWriter sW = new StreamWriter(_outputName))
            {
                try
                {
                    int count = 0;
                    bool header = false;

                    for (int i = 0; i < _metMasts.Count; i++)
                    {
                        for (int j = 0; j < _metMasts[i].MetDataSorted.Count; j++)
                        {
                            StringBuilder hB = new StringBuilder();
                            StringBuilder sB = new StringBuilder();

                            MeteoSample unit = _metMasts[i].MetDataSorted[j];

                            if (unit.TimeStamp >= startExp && unit.TimeStamp <= endExprt)
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

                                // need to add in the respective meteorology file columns to make this work properly

                                // this method takes into account which height version of the data has been loaded in at this point
                                if (_meteoHeader.Dircs.Measured == MeteoSample.HeightInfo.MeasuringHeight.BOTH)
                                {
                                    hB.Append("met_WinddirectionRot_mean" + ","); sB.Append(Common.GetStringDecimals(unit.Dircs.MetresRt.Mean, 1) + ",");
                                    hB.Append("met_Winddirection10_mean" + ","); sB.Append(Common.GetStringDecimals(unit.Dircs.Metres10.Mean, 1) + ",");
                                }
                                else if (_meteoHeader.Dircs.Measured == MeteoSample.HeightInfo.MeasuringHeight.M_10)
                                {
                                    hB.Append("met_Winddirection10_mean" + ","); sB.Append(Common.GetStringDecimals(unit.Dircs.Metres10.Mean, 1) + ",");
                                }
                                else if (_meteoHeader.Dircs.Measured == MeteoSample.HeightInfo.MeasuringHeight.ROT)
                                {
                                    hB.Append("met_WinddirectionRot_mean" + ","); sB.Append(Common.GetStringDecimals(unit.Dircs.MetresRt.Mean, 1) + ",");
                                }

                                if (_meteoHeader.WSpdR.Measured == MeteoSample.HeightInfo.MeasuringHeight.BOTH)
                                {
                                    hB.Append("met_WindSpeedRot_mean" + ","); sB.Append(Common.GetStringDecimals(unit.WSpdR.MetresRt.Mean, 2) + ",");
                                    hB.Append("met_WindSpeedTen_mean" + ","); sB.Append(Common.GetStringDecimals(unit.WSpdR.Metres10.Mean, 2) + ",");
                                }
                                else if (_meteoHeader.Dircs.Measured == MeteoSample.HeightInfo.MeasuringHeight.M_10)
                                {
                                    hB.Append("met_WindSpeedTen_mean" + ","); sB.Append(Common.GetStringDecimals(unit.WSpdR.Metres10.Mean, 2) + ",");
                                }
                                else if (_meteoHeader.Dircs.Measured == MeteoSample.HeightInfo.MeasuringHeight.ROT)
                                {
                                    hB.Append("met_WindSpeedRot_mean" + ","); sB.Append(Common.GetStringDecimals(unit.WSpdR.MetresRt.Mean, 2) + ",");
                                }

                                if (_meteoHeader.Tempr.Measured == MeteoSample.HeightInfo.MeasuringHeight.BOTH)
                                {
                                    hB.Append("met_TemperatureRot_mean" + ","); sB.Append(Common.GetStringDecimals(unit.Tempr.MetresRt.Mean, 1) + ",");
                                    hB.Append("met_TemperatureTen_mean" + ","); sB.Append(Common.GetStringDecimals(unit.Tempr.Metres10.Mean, 1) + ",");
                                }
                                else if (_meteoHeader.Dircs.Measured == MeteoSample.HeightInfo.MeasuringHeight.M_10)
                                {
                                    hB.Append("met_TemperatureTen_mean" + ","); sB.Append(Common.GetStringDecimals(unit.Tempr.Metres10.Mean, 1) + ",");
                                }
                                else if (_meteoHeader.Dircs.Measured == MeteoSample.HeightInfo.MeasuringHeight.ROT)
                                {
                                    hB.Append("met_TemperatureRot_mean" + ","); sB.Append(Common.GetStringDecimals(unit.Tempr.MetresRt.Mean, 1) + ",");
                                }

                                hB.Append("met_Humidity_mean"); sB.Append(Common.GetStringDecimals(unit.Humid.Mean, 1));

                                if (header == false) { sW.WriteLine(hB.ToString()); header = true; }
                                sW.WriteLine(sB.ToString());
                            }

                            count++;

                            if (count % 1000 == 0)
                            {
                                if (progress != null)
                                {
                                    progress.Report((int)(((double)i / _metMasts.Count + (double)j / _metMasts[i].MetDataSorted.Count / _metMasts.Count) * 100));
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

        public class MetMastData : BaseStructure
        {
            #region Variables

            private List<MeteoSample> metData = new List<MeteoSample>();
            private List<MeteoSample> metDataSorted = new List<MeteoSample>();

            #endregion

            #region Constructor

            public MetMastData() { }

            public MetMastData(string[] splits, MeteoHeader header, Common.DateFormat _dateFormat)
            {
                Type = Types.METMAST;

                metData.Add(new MeteoSample(splits, header, _dateFormat));
                InclSamples.Add(metData[metData.Count - 1].TimeStamp);

                if (UnitID == -1 && metData.Count > 0)
                {
                    UnitID = metData[0].AssetID != 0 ? metData[0].AssetID : metData[0].StationID;
                }
            }

            #endregion

            public void AddData(string[] splits, MeteoHeader header, Common.DateFormat _dateFormat)
            {
                DateTime thisTime = Common.StringToDateTime(Common.GetSplits(splits[header.TimesCol], new char[] { ' ' }), _dateFormat);

                if (InclSamples.Contains(thisTime))
                {
                    int index = metData.FindIndex(x => x.TimeStamp == thisTime);

                    metData[index].AddDataFields(splits, header, _dateFormat);
                }
                else
                {
                    metData.Add(new MeteoSample(splits, header, _dateFormat));

                    InclSamples.Add(thisTime);
                }
            }

            #region Properties

            public List<MeteoSample> MetData { get { return metData; } set { metData = value; } }
            public List<MeteoSample> MetDataSorted { get { return metDataSorted; } set { metDataSorted = value; } }

            #endregion
        }

        public class MeteoHeader : MeteoSample
        {
            // this class serves the same purpose as the similar class
            // in the scada data fields, opening up the option for meteo
            // files to be of variable structure without crashing the entire
            // thing

            #region Variables

            private int noVal = -1;
            private int assetCol = -1, sampleCol = -1, stationCol = -1, timeCol = -1;

            #endregion

            #region Constructor

            public MeteoHeader() { }

            public MeteoHeader(string header)
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
                            if (parts[1] == "humidity")
                            {
                                if (parts[2] == "mean") { Humid.Mean = i; }
                                else if (parts[2] == "stddev") { Humid.Stdv = i; }
                                else if (parts[2] == "max") { Humid.Maxm = i; }
                                else if (parts[2] == "min") { Humid.Minm = i; }
                            }
                            else if (parts[1].Contains("temperature"))
                            {
                                if (parts[1].Contains("ten") || parts[1].Contains("10"))
                                {
                                    if (Tempr.Measured == HeightInfo.MeasuringHeight.UNKNOWN) { Tempr.Measured = HeightInfo.MeasuringHeight.M_10; }
                                    else { Tempr.Measured = HeightInfo.MeasuringHeight.BOTH; }

                                    if (parts[2] == "mean") { Tempr.Metres10.Mean = i; }
                                    else if (parts[2] == "stddev") { Tempr.Metres10.Stdv = i; }
                                    else if (parts[2] == "max") { Tempr.Metres10.Maxm = i; }
                                    else if (parts[2] == "min") { Tempr.Metres10.Minm = i; }
                                }
                                else if (parts[1].Contains("rot"))
                                {
                                    if (Tempr.Measured == HeightInfo.MeasuringHeight.UNKNOWN) { Tempr.Measured = HeightInfo.MeasuringHeight.ROT; }
                                    else { Tempr.Measured = HeightInfo.MeasuringHeight.BOTH; }

                                    if (parts[2] == "mean") { Tempr.MetresRt.Mean = i; }
                                    else if (parts[2] == "stddev") { Tempr.MetresRt.Stdv = i; }
                                    else if (parts[2] == "max") { Tempr.MetresRt.Maxm = i; }
                                    else if (parts[2] == "min") { Tempr.MetresRt.Minm = i; }
                                }
                            }
                            else if (parts[1].Contains("direction"))
                            {
                                if (parts[1].Contains("ten") || parts[1].Contains("10"))
                                {
                                    if (Dircs.Measured == HeightInfo.MeasuringHeight.UNKNOWN) { Dircs.Measured = HeightInfo.MeasuringHeight.M_10; }
                                    else { Dircs.Measured = HeightInfo.MeasuringHeight.BOTH; }

                                    if (parts[2] == "mean") { Dircs.Metres10.Mean = i; }
                                    else if (parts[2] == "stddev") { Dircs.Metres10.Stdv = i; }
                                    else if (parts[2] == "max") { Dircs.Metres10.Maxm = i; }
                                    else if (parts[2] == "min") { Dircs.Metres10.Minm = i; }
                                }
                                else if (parts[1].Contains("rot"))
                                {
                                    if (Dircs.Measured == HeightInfo.MeasuringHeight.UNKNOWN) { Dircs.Measured = HeightInfo.MeasuringHeight.ROT; }
                                    else { Dircs.Measured = HeightInfo.MeasuringHeight.BOTH; }

                                    if (parts[2] == "mean") { Dircs.MetresRt.Mean = i; }
                                    else if (parts[2] == "stddev") { Dircs.MetresRt.Stdv = i; }
                                    else if (parts[2] == "max") { Dircs.MetresRt.Maxm = i; }
                                    else if (parts[2] == "min") { Dircs.MetresRt.Minm = i; }
                                }
                            }
                            else if (parts[1].Contains("speed"))
                            {
                                if (parts[1].Contains("ten") || parts[1].Contains("10"))
                                {
                                    if (WSpdR.Measured == HeightInfo.MeasuringHeight.UNKNOWN){ WSpdR.Measured = HeightInfo.MeasuringHeight.M_10; }
                                    else { WSpdR.Measured = HeightInfo.MeasuringHeight.BOTH; }

                                    if (parts[2] == "mean") { WSpdR.Metres10.Mean = i; }
                                    else if (parts[2] == "stddev") { WSpdR.Metres10.Stdv = i; }
                                    else if (parts[2] == "max") { WSpdR.Metres10.Maxm = i; }
                                    else if (parts[2] == "min") { WSpdR.Metres10.Minm = i; }
                                }
                                else if (parts[1].Contains("rot"))
                                {
                                    if (WSpdR.Measured == HeightInfo.MeasuringHeight.UNKNOWN) { WSpdR.Measured = HeightInfo.MeasuringHeight.ROT; }
                                    else { WSpdR.Measured = HeightInfo.MeasuringHeight.BOTH; }

                                    if (parts[2] == "mean") { WSpdR.MetresRt.Mean = i; }
                                    else if (parts[2] == "stddev") { WSpdR.MetresRt.Stdv = i; }
                                    else if (parts[2] == "max") { WSpdR.MetresRt.Maxm = i; }
                                    else if (parts[2] == "min") { WSpdR.MetresRt.Minm = i; }
                                }
                            }
                        }
                    }
                }
            }

            private void NullAllHeaderValues()
            {
                Humid.Mean = noVal;
                Humid.Stdv = noVal;
                Humid.Maxm = noVal;
                Humid.Minm = noVal;

                Dircs.Metres10.Mean = noVal;
                Dircs.Metres10.Stdv = noVal;
                Dircs.Metres10.Maxm = noVal;
                Dircs.Metres10.Minm = noVal;
                Tempr.Metres10.Mean = noVal;
                Tempr.Metres10.Stdv = noVal;
                Tempr.Metres10.Maxm = noVal;
                Tempr.Metres10.Minm = noVal;                
                WSpdR.Metres10.Mean = noVal;
                WSpdR.Metres10.Stdv = noVal;
                WSpdR.Metres10.Maxm = noVal;
                WSpdR.Metres10.Minm = noVal;

                Dircs.MetresRt.Mean = noVal;
                Dircs.MetresRt.Stdv = noVal;
                Dircs.MetresRt.Maxm = noVal;
                Dircs.MetresRt.Minm = noVal;
                Tempr.MetresRt.Mean = noVal;
                Tempr.MetresRt.Stdv = noVal;
                Tempr.MetresRt.Maxm = noVal;
                Tempr.MetresRt.Minm = noVal;
                WSpdR.MetresRt.Mean = noVal;
                WSpdR.MetresRt.Stdv = noVal;
                WSpdR.MetresRt.Maxm = noVal;
                WSpdR.MetresRt.Minm = noVal;
            }

            #region Properties

            public int AssetCol { get { return assetCol; } set { assetCol = value; } }
            public int SamplCol { get { return sampleCol; } set { sampleCol = value; } }
            public int StatnCol { get { return stationCol; } set { stationCol = value; } }
            public int TimesCol { get { return timeCol; } set { timeCol = value; } }

            #endregion
        }

        public class MeteoSample : BaseSampleData
        {
            #region Variables

            private Humidity _humid = new Humidity();

            private HeightInfo _dircs = new HeightInfo();
            private HeightInfo _tempr = new HeightInfo();
            private HeightInfo _wSpdR = new HeightInfo();

            #endregion

            #region Constructor

            public MeteoSample() { }

            public MeteoSample(string[] data, MeteoHeader header, Common.DateFormat _dateFormat)
            {
                LoadData(data, header, _dateFormat);
            }

            #endregion

            public void AddDataFields(string[] data, MeteoHeader header, Common.DateFormat _dateFormat)
            {
                LoadData(data, header, _dateFormat);
            }

            private void LoadData(string[] data, MeteoHeader header, Common.DateFormat _dateFormat)
            {
                if (header.AssetCol != -1)
                { AssetID = Common.CanConvert<int>(data[header.AssetCol]) ? Convert.ToInt32(data[header.AssetCol]) : 0; }

                if (header.SamplCol != -1)
                { SampleID = Common.CanConvert<int>(data[header.SamplCol]) ? Convert.ToInt32(data[header.SamplCol]) : 0; }

                if (header.StatnCol != -1)
                { StationID = Common.CanConvert<int>(data[header.StatnCol]) ? Convert.ToInt32(data[header.StatnCol]) : 0; }

                if (header.TimesCol != -1)
                { TimeStamp = Common.StringToDateTime(Common.GetSplits(data[header.TimesCol], new char[] { ' ' }), _dateFormat); }

                _humid.Description = "Relative humidity (%)";
                _humid.Mean = GetVals(_humid.Mean, data, header.Humid.Mean);
                _humid.Stdv = GetVals(_humid.Stdv, data, header.Humid.Stdv);
                _humid.Maxm = GetVals(_humid.Maxm, data, header.Humid.Maxm);
                _humid.Minm = GetVals(_humid.Minm, data, header.Humid.Minm);

                _tempr.Metres10.Mean = GetVals(_tempr.Metres10.Mean, data, header.Tempr.Metres10.Mean);
                _tempr.Metres10.Stdv = GetVals(_tempr.Metres10.Stdv, data, header.Tempr.Metres10.Stdv);
                _tempr.Metres10.Maxm = GetVals(_tempr.Metres10.Maxm, data, header.Tempr.Metres10.Maxm);
                _tempr.Metres10.Minm = GetVals(_tempr.Metres10.Minm, data, header.Tempr.Metres10.Minm);
                _tempr.MetresRt.Mean = GetVals(_tempr.MetresRt.Mean, data, header.Tempr.MetresRt.Mean);
                _tempr.MetresRt.Stdv = GetVals(_tempr.MetresRt.Stdv, data, header.Tempr.MetresRt.Stdv);
                _tempr.MetresRt.Maxm = GetVals(_tempr.MetresRt.Maxm, data, header.Tempr.MetresRt.Maxm);
                _tempr.MetresRt.Minm = GetVals(_tempr.MetresRt.Minm, data, header.Tempr.MetresRt.Minm);

                _wSpdR.Metres10.Mean = GetVals(_wSpdR.Metres10.Mean, data, header.WSpdR.Metres10.Mean);
                _wSpdR.Metres10.Stdv = GetVals(_wSpdR.Metres10.Stdv, data, header.WSpdR.Metres10.Stdv);
                _wSpdR.Metres10.Maxm = GetVals(_wSpdR.Metres10.Maxm, data, header.WSpdR.Metres10.Maxm);
                _wSpdR.Metres10.Minm = GetVals(_wSpdR.Metres10.Minm, data, header.WSpdR.Metres10.Minm);
                _wSpdR.MetresRt.Mean = GetVals(_wSpdR.MetresRt.Mean, data, header.WSpdR.MetresRt.Mean);
                _wSpdR.MetresRt.Stdv = GetVals(_wSpdR.MetresRt.Stdv, data, header.WSpdR.MetresRt.Stdv);
                _wSpdR.MetresRt.Maxm = GetVals(_wSpdR.MetresRt.Maxm, data, header.WSpdR.MetresRt.Maxm);
                _wSpdR.MetresRt.Minm = GetVals(_wSpdR.MetresRt.Minm, data, header.WSpdR.MetresRt.Minm);

                _dircs.Metres10.Mean = GetVals(_dircs.Metres10.Mean, data, header.Dircs.Metres10.Mean);
                _dircs.Metres10.Stdv = GetVals(_dircs.Metres10.Stdv, data, header.Dircs.Metres10.Stdv);
                _dircs.Metres10.Maxm = GetVals(_dircs.Metres10.Maxm, data, header.Dircs.Metres10.Maxm);
                _dircs.Metres10.Minm = GetVals(_dircs.Metres10.Minm, data, header.Dircs.Metres10.Minm);
                _dircs.MetresRt.Mean = GetVals(_dircs.MetresRt.Mean, data, header.Dircs.MetresRt.Mean);
                _dircs.MetresRt.Stdv = GetVals(_dircs.MetresRt.Stdv, data, header.Dircs.MetresRt.Stdv);
                _dircs.MetresRt.Maxm = GetVals(_dircs.MetresRt.Maxm, data, header.Dircs.MetresRt.Maxm);
                _dircs.MetresRt.Minm = GetVals(_dircs.MetresRt.Minm, data, header.Dircs.MetresRt.Minm);
            }

            #region Support Classes
            
            public class HeightInfo
            {
                #region Variables

                private MeasuringHeight _measured = MeasuringHeight.UNKNOWN;

                private Direction _metres10 = new Direction();
                private Direction _metresRt = new Direction();

                #endregion

                #region Constructor

                public HeightInfo()
                {
                    _metres10.Description = "Measured at 10m height";
                    _metresRt.Description = "Measured at rotor height";
                }

                #endregion

                #region Support Classes

                public enum MeasuringHeight
                {
                    UNKNOWN,
                    M_10,
                    ROT,
                    BOTH
                }

                #endregion 

                #region Properties

                public MeasuringHeight Measured { get { return _measured; } set { _measured = value; } }

                public Direction Metres10 { get { return _metres10; } set { _metres10 = value; } }
                public Direction MetresRt { get { return _metresRt; } set { _metresRt = value; } }

                #endregion
            }
            
            #endregion

            #region Properties

            public Humidity Humid { get { return _humid; } set { _humid = value; } }

            public HeightInfo Dircs { get { return _dircs; } set { _dircs = value; } }
            public HeightInfo Tempr { get { return _tempr; } set { _tempr = value; } }
            public HeightInfo WSpdR { get { return _wSpdR; } set { _wSpdR = value; } }

            #endregion
        }

        #endregion

        #region Properties

        public MeteoHeader MetrHeader { get { return _meteoHeader; } }

        public List<int> InclMetm { get { return _inclMetMasts; } }

        public List<MetMastData> MetMasts { get { return _metMasts; } }

        #endregion
    }
}
