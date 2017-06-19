using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        public MeteoData() { }

        public MeteoData(MeteoData existingFiles)
        {
            for (int i = 0; i < existingFiles.MetMasts.Count; i++)
            {
                metMasts.Add(existingFiles.MetMasts[i]);
            }

            for (int i = 0; i < existingFiles.InclMetm.Count; i++)
            {
                inclMetm.Add(existingFiles.InclMetm[i]);
            }
        }

        public MeteoData(string[] filenames, IProgress<int> progress)
        {
            LoadNSortMet(filenames, progress);
        }

        public void AppendFiles(string[] filenames, IProgress<int> progress)
        {
            LoadNSortMet(filenames, progress);
        }

        public void ExportFiles(IProgress<int> progress, string output, DateTime startExp, DateTime endExprt)
        {
            // feed in proper arguments for this output file name and assign these
            outputName = output;

            // write the SCADA file out in a reasonable method
            WriteMeteo(progress, startExp, endExprt);
        }

        private void LoadMetFiles(string[] filenames, IProgress<int> progress)
        {
            for (int i = 0; i < filenames.Length; i++)
            {
                this.FileName = filenames[i];

                LoadMeteorology(progress, filenames.Length, i);
            }
        }

        private void LoadMeteorology(IProgress<int> progress, int numberOfFiles = 1, int i = 0)
        {
            using (StreamReader sR = new StreamReader(FileName))
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

                            int thisAsset = Common.CanConvert<int>(splits[metrHeader.AssetCol]) ?
                                Convert.ToInt32(splits[metrHeader.AssetCol]) : throw new FileFormatException();

                            // organise loading so it would check which ones have already
                            // been loaded; then work around the ones have have been

                            if (inclMetm.Contains(thisAsset))
                            {
                                int index = metMasts.FindIndex(x => x.UnitID == thisAsset);

                                metMasts[index].AddData(splits, metrHeader);
                            }
                            else
                            {
                                metMasts.Add(new MetMastData(splits, metrHeader));

                                inclMetm.Add(Convert.ToInt32(splits[metrHeader.AssetCol]));
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

        private void LoadNSortMet(string[] filenames, IProgress<int> progress)
        {
            LoadMetFiles(filenames, progress);

            SortMeteorology();
            PopulateTimeDif();
        }

        private void PopulateTimeDif()
        {
            for (int i = 0; i < metMasts.Count; i++)
            {
                for (int j = 1; j < metMasts[i].MetDataSorted.Count; j++)
                {
                    metMasts[i].MetDataSorted[j].DeltaTime = metMasts[i].MetDataSorted[j].TimeStamp - metMasts[i].MetDataSorted[j - 1].TimeStamp;
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

                                hB.Append("met_WindSpeedRot_mean" + ","); sB.Append(unit.WSpdR.Mean + ",");
                                hB.Append("met_Winddirection10_mean" + ","); sB.Append(unit.WDirc.Mean + ",");
                                hB.Append("met_TemperatureTen_mean" + ","); sB.Append(unit.Tempr.Mean + ",");
                                hB.Append("met_Humidity_mean" + ","); sB.Append(unit.Humid.Mean + ",");

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

        #region Support Classes

        public class MetMastData : BaseStructure
        {
            #region Variables

            private List<MeteoSample> metData = new List<MeteoSample>();
            private List<MeteoSample> metDataSorted = new List<MeteoSample>();

            #endregion

            public MetMastData() { }

            public MetMastData(string[] splits, MeteoHeader header)
            {
                Type = Types.METMAST;

                metData.Add(new MeteoSample(splits, header));

                InclDtTm.Add(Common.StringToDateTime(Common.GetSplits(splits[header.TimesCol], new char[] { ' ' })));

                if (UnitID == -1 && metData.Count > 0)
                {
                    UnitID = metData[0].AssetID;
                }
            }

            public void AddData(string[] splits, MeteoHeader header)
            {
                DateTime thisTime = Common.StringToDateTime(Common.GetSplits(splits[header.TimesCol], new char[] { ' ' }));

                if (InclDtTm.Contains(thisTime))
                {
                    int index = metData.FindIndex(x => x.TimeStamp == thisTime);

                    metData[index].AddDataFields(splits, header);
                }
                else
                {
                    metData.Add(new MeteoSample(splits, header));

                    InclDtTm.Add(thisTime);
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

                WDirc.Mean = noVal;
                WDirc.Stdv = noVal;
                WDirc.Maxm = noVal;
                WDirc.Minm = noVal;

                WSpdR.Mean = noVal;
                WSpdR.Stdv = noVal;
                WSpdR.Maxm = noVal;
                WSpdR.Minm = noVal;
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
                                if (parts[2] == "mean") { WDirc.Mean = i; }
                                else if (parts[2] == "stddev") { WDirc.Stdv = i; }
                                else if (parts[2] == "max") { WDirc.Maxm = i; }
                                else if (parts[2] == "min") { WDirc.Minm = i; }
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

            private int metError = -9998;

            private Humidity humid = new Humidity();
            private Tempratr tempr = new Tempratr();
            private WndDrctn wDirc = new WndDrctn();
            private WndSpRtr wSpdR = new WndSpRtr();
            
            #endregion
            
            public MeteoSample() { }

            public MeteoSample(string[] data, MeteoHeader header)
            {
                LoadData(data, header);
            }

            public void AddDataFields(string[] data, MeteoHeader header)
            {
                LoadData(data, header);
            }

            private void LoadData(string[] data, MeteoHeader header)
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
                    TimeStamp = Common.StringToDateTime(Common.GetSplits(data[header.TimesCol], new char[] { ' ' }));
                }

                humid.Mean = GetVals(humid.Mean, data, header.Humid.Mean);
                humid.Stdv = GetVals(humid.Stdv, data, header.Humid.Stdv);
                humid.Maxm = GetVals(humid.Maxm, data, header.Humid.Maxm);
                humid.Minm = GetVals(humid.Minm, data, header.Humid.Minm);

                tempr.Mean = GetVals(tempr.Mean, data, header.Tempr.Mean);
                tempr.Stdv = GetVals(tempr.Stdv, data, header.Tempr.Stdv);
                tempr.Maxm = GetVals(tempr.Maxm, data, header.Tempr.Maxm);
                tempr.Minm = GetVals(tempr.Minm, data, header.Tempr.Minm);

                wDirc.Mean = GetVals(wDirc.Mean, data, header.WDirc.Mean);
                wDirc.Stdv = GetVals(wDirc.Stdv, data, header.WDirc.Stdv);
                wDirc.Maxm = GetVals(wDirc.Maxm, data, header.WDirc.Maxm);
                wDirc.Minm = GetVals(wDirc.Minm, data, header.WDirc.Minm);

                wSpdR.Mean = GetVals(wSpdR.Mean, data, header.WSpdR.Mean);
                wSpdR.Stdv = GetVals(wSpdR.Stdv, data, header.WSpdR.Stdv);
                wSpdR.Maxm = GetVals(wSpdR.Maxm, data, header.WSpdR.Maxm);
                wSpdR.Minm = GetVals(wSpdR.Minm, data, header.WSpdR.Minm);
            }

            private double GetVals(double value, string[] data, double index)
            { 
                if (value == -999999 || value == metError)
                {
                    return Common.GetVals(data, (int)index, metError);
                }
                else
                {
                    return value;
                }
            }
            
            #region Support Classes

            public class Humidity : Stats { }
            public class Tempratr : Stats { }
            public class WndDrctn : Stats { }
            public class WndSpRtr : Stats { }

            #endregion

            #region Properties

            public Humidity Humid { get { return humid; } set { humid = value; } }
            public Tempratr Tempr { get { return tempr; } set { tempr = value; } }
            public WndDrctn WDirc { get { return wDirc; } set { wDirc = value; } }
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
