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

        private string outputName;

        private List<int> inclMetm = new List<int>();

        private MeteoHeader metrHeader = new MeteoHeader();

        private List<MetMastData> metMasts = new List<MetMastData>();

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
                metMasts.Add(_existingInfo.MetMasts[i]);
            }

            for (int i = 0; i < _existingInfo.InclMetm.Count; i++)
            {
                inclMetm.Add(_existingInfo.InclMetm[i]);
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

            metMasts = metMasts.OrderBy(o => o.UnitID).ToList();
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

                    metMasts = new List<MetMastData>();

                    while (!sR.EndOfStream)
                    {
                        if (readHeader == false)
                        {
                            string header = sR.ReadLine();
                            header = header.ToLower().Replace("\"", String.Empty);
                            readHeader = true;

                            if (!header.Contains("met")) { throw new WrongFileTypeException(); }

                            metrHeader = new MeteoHeader(header);
                        }

                        string line = sR.ReadLine();

                        if (!line.Equals(""))
                        {
                            line = line.Replace("\"", String.Empty);

                            string[] splits = Common.GetSplits(line, ',');

                            int thisAsset;

                            // if the file does not have an AssetID column, the Station Column should be used instead
                            if (metrHeader.AssetCol != -1)
                            {
                                thisAsset = Common.CanConvert<int>(splits[metrHeader.AssetCol]) ?
                                  Convert.ToInt32(splits[metrHeader.AssetCol]) : throw new FileFormatException();
                            }
                            else
                            {
                                thisAsset = Common.CanConvert<int>(splits[metrHeader.StatnCol]) ?
                                  Convert.ToInt32(splits[metrHeader.StatnCol]) : throw new FileFormatException();
                            }

                            // organise loading so it would check which ones have already
                            // been loaded; then work around the ones have have been

                            if (inclMetm.Contains(thisAsset))
                            {
                                int index = metMasts.FindIndex(x => x.UnitID == thisAsset);

                                metMasts[index].AddData(splits, metrHeader, _dateFormat);
                            }
                            else
                            {
                                metMasts.Add(new MetMastData(splits, metrHeader, _dateFormat));
                                inclMetm.Add(metMasts[metMasts.Count - 1].UnitID);
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
            for (int i = 0; i < metMasts.Count;i++)
            {
                string mode = metMasts[i].MetDataSorted.GroupBy(v => v.WSpdR.DStr).OrderByDescending(g => g.Count()).First().Key;

                metMasts[i].PrevailingWindString = mode;
            }
        }

        private void PopulateTimeDif()
        {
            for (int i = 0; i < metMasts.Count; i++)
            {
                for (int j = 1; j < metMasts[i].MetDataSorted.Count; j++)
                {
                    metMasts[i].MetDataSorted[j].SampleSeparation = metMasts[i].MetDataSorted[j].TimeStamp - metMasts[i].MetDataSorted[j - 1].TimeStamp;
                }
            }
        }

        private void SortMeteorology()
        {
            for (int i = 0; i < metMasts.Count; i++)
            {
                metMasts[i].MetDataSorted = metMasts[i].MetData.OrderBy(o => o.TimeStamp).ToList();
            }
        }

        #endregion

        #region Export Data

        public void ExportFiles(IProgress<int> progress, string output, DateTime startExp, DateTime endExprt)
        {
            // feed in proper arguments for this output file name and assign these
            outputName = output;

            // write the SCADA file out in a reasonable method
            WriteMeteo(progress, startExp, endExprt);
        }

        private void WriteMeteo(IProgress<int> progress, DateTime startExp, DateTime endExprt)
        {
            using (StreamWriter sW = new StreamWriter(outputName))
            {
                try
                {
                    int count = 0;
                    bool header = false;

                    for (int i = 0; i < metMasts.Count; i++)
                    {
                        for (int j = 0; j < metMasts[i].MetDataSorted.Count; j++)
                        {
                            StringBuilder hB = new StringBuilder();
                            StringBuilder sB = new StringBuilder();

                            MeteoSample unit = metMasts[i].MetDataSorted[j];

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

                                // need to add in the respective actual meteorology file columns to make
                                // this work properly

                                hB.Append("met_WindSpeedRot_mean" + ","); sB.Append(Common.GetStringDecimals(unit.WSpdR.Mean, 3) + ",");
                                hB.Append("met_Winddirection10_mean" + ","); sB.Append(Common.GetStringDecimals(unit.WSpdR.Dirc, 1) + ",");
                                hB.Append("met_TemperatureTen_mean" + ","); sB.Append(Common.GetStringDecimals(unit.Tempr.Mean, 1) + ",");
                                hB.Append("met_Humidity_mean"); sB.Append(Common.GetStringDecimals(unit.Humid.Mean, 1));

                                if (header == false) { sW.WriteLine(hB.ToString()); header = true; }
                                sW.WriteLine(sB.ToString());
                            }

                            count++;

                            if (count % 1000 == 0)
                            {
                                if (progress != null)
                                {
                                    progress.Report((int)(((double)i / metMasts.Count + (double)j / metMasts[i].MetDataSorted.Count / metMasts.Count) * 100));
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

            public MeteoHeader() { }

            public MeteoHeader(string header)
            {
                HeaderNoValues();

                HeaderSeparation(header);
            }

            private void HeaderNoValues()
            {
                Humid.Mean = noVal;
                Humid.Stdv = noVal;
                Humid.Maxm = noVal;
                Humid.Minm = noVal;

                Tempr.Mean = noVal;
                Tempr.Stdv = noVal;
                Tempr.Maxm = noVal;
                Tempr.Minm = noVal;
                
                WSpdR.Mean = noVal;
                WSpdR.Stdv = noVal;
                WSpdR.Maxm = noVal;
                WSpdR.Minm = noVal;
                WSpdR.Dirc = noVal;
            }

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
                            else if (parts[1] == "temperatureten")
                            {
                                if (parts[2] == "mean") { Tempr.Mean = i; }
                                else if (parts[2] == "stddev") { Tempr.Stdv = i; }
                                else if (parts[2] == "max") { Tempr.Maxm = i; }
                                else if (parts[2] == "min") { Tempr.Minm = i; }
                            }
                            else if (parts[1] == "winddirection10")
                            {
                                if (parts[2] == "mean") { WSpdR.Dirc = i; }
                            }
                            else if (parts[1] == "windspeedrot")
                            {
                                if (parts[2] == "mean") { WSpdR.Mean = i; }
                                else if (parts[2] == "stddev") { WSpdR.Stdv = i; }
                                else if (parts[2] == "max") { WSpdR.Maxm = i; }
                                else if (parts[2] == "min") { WSpdR.Minm = i; }
                            }
                        }
                    }
                }
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
            
            private Humidity humid = new Humidity();
            private Tempratr tempr = new Tempratr();
            private WndSpRtr wSpdR = new WndSpRtr();
            
            #endregion
            
            public MeteoSample() { }

            public MeteoSample(string[] data, MeteoHeader header, Common.DateFormat _dateFormat)
            {
                LoadData(data, header, _dateFormat);
            }

            public void AddDataFields(string[] data, MeteoHeader header, Common.DateFormat _dateFormat)
            {
                LoadData(data, header, _dateFormat);
            }

            private void LoadData(string[] data, MeteoHeader header, Common.DateFormat _dateFormat)
            {
                if (header.AssetCol != -1)
                {
                    AssetID = Common.CanConvert<int>(data[header.AssetCol]) ? Convert.ToInt32(data[header.AssetCol]) : 0;
                }

                if (header.SamplCol != -1)
                {
                    SampleID = Common.CanConvert<int>(data[header.SamplCol]) ? Convert.ToInt32(data[header.SamplCol]) : 0;
                }

                if (header.StatnCol != -1)
                {
                    StationID = Common.CanConvert<int>(data[header.StatnCol]) ? Convert.ToInt32(data[header.StatnCol]) : 0;
                }

                if (header.TimesCol != -1)
                {
                    TimeStamp = Common.StringToDateTime(Common.GetSplits(data[header.TimesCol], new char[] { ' ' }),_dateFormat);
                }

                humid.Mean = GetVals(humid.Mean, data, header.Humid.Mean);
                humid.Stdv = GetVals(humid.Stdv, data, header.Humid.Stdv);
                humid.Maxm = GetVals(humid.Maxm, data, header.Humid.Maxm);
                humid.Minm = GetVals(humid.Minm, data, header.Humid.Minm);

                tempr.Mean = GetVals(tempr.Mean, data, header.Tempr.Mean);
                tempr.Stdv = GetVals(tempr.Stdv, data, header.Tempr.Stdv);
                tempr.Maxm = GetVals(tempr.Maxm, data, header.Tempr.Maxm);
                tempr.Minm = GetVals(tempr.Minm, data, header.Tempr.Minm);
                
                wSpdR.Mean = GetVals(wSpdR.Mean, data, header.WSpdR.Mean);
                wSpdR.Stdv = GetVals(wSpdR.Stdv, data, header.WSpdR.Stdv);
                wSpdR.Maxm = GetVals(wSpdR.Maxm, data, header.WSpdR.Maxm);
                wSpdR.Minm = GetVals(wSpdR.Minm, data, header.WSpdR.Minm);
                wSpdR.Dirc = GetVals(wSpdR.Dirc, data, header.WSpdR.Dirc);
            }
            
            #region Support Classes

            public class Humidity : Stats { }
            public class Tempratr : Stats { }
            public class WndSpRtr : Speed { }

            #endregion

            #region Properties
            
            public Humidity Humid { get { return humid; } set { humid = value; } }
            public Tempratr Tempr { get { return tempr; } set { tempr = value; } }
            public WndSpRtr WSpdR { get { return wSpdR; } set { wSpdR = value; } }

            #endregion
        }

        #endregion

        #region Properties

        public MeteoHeader MetrHeader { get { return metrHeader; } }

        public List<int> InclMetm { get { return inclMetm; } }

        public List<MetMastData> MetMasts { get { return metMasts; } }

        #endregion
    }
}
