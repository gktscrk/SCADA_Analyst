using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace scada_analyst
{
    class MeteoData : BaseMetaData
    {
        #region Variables

        private MeteoHeader metrHeader = new MeteoHeader();

        private List<MetMastData> metMasts = new List<MetMastData>();

        #endregion

        public MeteoData() { }

        public MeteoData(string filename, BackgroundWorker bgW)
        {
            if (!bgW.CancellationPending)
            {
                this.FileName = filename;

                LoadMeteorology(bgW);

                SortMeteorology(bgW);
            }
        }

        private void LoadMeteorology(BackgroundWorker bgW)
        {
            if (!bgW.CancellationPending)
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
                            if (!bgW.CancellationPending)
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

                                    if (metMasts.Count < 1)
                                    {
                                        metMasts.Add(new MetMastData(splits, metrHeader));
                                    }
                                    else
                                    {
                                        bool foundMetMast = false;

                                        for (int i = 0; i < metMasts.Count; i++)
                                        {
                                            if (metMasts[i].UnitID == Convert.ToInt32(splits[metrHeader.AssetCol]))
                                            {
                                                metMasts[i].MetData.Add(new MeteoSample(splits, metrHeader));

                                                foundMetMast = true; break;
                                            }
                                        }

                                        if (!foundMetMast) { metMasts.Add(new MetMastData(splits, metrHeader)); }
                                    }
                                }

                                count++;

                                if (count % 500 == 0)
                                {
                                    bgW.ReportProgress((int)
                                        ((double)sR.BaseStream.Position * 100 / sR.BaseStream.Length));
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
        }

        private void SortMeteorology(BackgroundWorker bgW)
        {
            if (!bgW.CancellationPending)
            {
                for (int i = 0; i < metMasts.Count; i++)
                {
                    metMasts[i].MetDataSorted = metMasts[i].MetData.OrderBy(o => o.TimeStamp).ToList();
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

                if (UnitID == -1 && metData.Count > 0)
                {
                    UnitID = metData[0].AssetID;
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

            public MeteoHeader() { }

            public MeteoHeader(string header)
            {
                HeaderSeparation(header);
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
                                if (parts[2] == "mean") { Humid.MeanCol = i; }
                                else if (parts[2] == "stddev") { Humid.StdvCol = i; }
                                else if (parts[2] == "max") { Humid.MaxmCol = i; }
                                else if (parts[2] == "min") { Humid.MinmCol = i; }
                            }
                            else if (parts[1] == "temperatureten")
                            {
                                if (parts[2] == "mean") { Tempr.MeanCol = i; }
                                else if (parts[2] == "stddev") { Tempr.StdvCol = i; }
                                else if (parts[2] == "max") { Tempr.MaxmCol = i; }
                                else if (parts[2] == "min") { Tempr.MinmCol = i; }
                            }
                            else if (parts[1] == "winddirection10")
                            {
                                if (parts[2] == "mean") { WDirc.MeanCol = i; }
                                else if (parts[2] == "stddev") { WDirc.StdvCol = i; }
                                else if (parts[2] == "max") { WDirc.MaxmCol = i; }
                                else if (parts[2] == "min") { WDirc.MinmCol = i; }
                            }
                            else if (parts[1] == "windspeedrot")
                            {
                                if (parts[2] == "mean") { WSpdR.MeanCol = i; }
                                else if (parts[2] == "stddev") { WSpdR.StdvCol = i; }
                                else if (parts[2] == "max") { WSpdR.MaxmCol = i; }
                                else if (parts[2] == "min") { WSpdR.MinmCol = i; }
                            }
                        }
                    }
                }
            }
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

                humid.Mean = GetVals(humid.Mean, data, header.Humid.MeanCol);
                humid.Stdv = GetVals(humid.Stdv, data, header.Humid.StdvCol);
                humid.Maxm = GetVals(humid.Maxm, data, header.Humid.MaxmCol);
                humid.Minm = GetVals(humid.Minm, data, header.Humid.MinmCol);

                tempr.Mean = GetVals(tempr.Mean, data, header.Tempr.MeanCol);
                tempr.Stdv = GetVals(tempr.Stdv, data, header.Tempr.StdvCol);
                tempr.Maxm = GetVals(tempr.Maxm, data, header.Tempr.MaxmCol);
                tempr.Minm = GetVals(tempr.Minm, data, header.Tempr.MinmCol);

                wDirc.Mean = GetVals(wDirc.Mean, data, header.WDirc.MeanCol);
                wDirc.Stdv = GetVals(wDirc.Stdv, data, header.WDirc.StdvCol);
                wDirc.Maxm = GetVals(wDirc.Maxm, data, header.WDirc.MaxmCol);
                wDirc.Minm = GetVals(wDirc.Minm, data, header.WDirc.MinmCol);

                wSpdR.Mean = GetVals(wSpdR.Mean, data, header.WSpdR.MeanCol);
                wSpdR.Stdv = GetVals(wSpdR.Stdv, data, header.WSpdR.StdvCol);
                wSpdR.Maxm = GetVals(wSpdR.Maxm, data, header.WSpdR.MaxmCol);
                wSpdR.Minm = GetVals(wSpdR.Minm, data, header.WSpdR.MinmCol);
            }

            private double GetVals(double value, string[] data, int index)
            {
                if (value == 0 || value == metError)
                {
                    return GetVals(data, index);
                }
                else
                {
                    return value;
                }
            }

            private double GetVals(string[] data, int index)
            {
                return index != -1 && Common.CanConvert<double>(data[index]) ? Convert.ToDouble(data[index]) : metError;
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

        public List<MetMastData> MetMasts { get { return metMasts; } }

        #endregion
    }
}
