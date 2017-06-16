using System;
using System.Collections.Generic;
using System.ComponentModel;
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

        private ScadaHeader fileHeader = new ScadaHeader();

        // a list for including the asset IDs for all loaded turbines
        private List<int> inclTrbn = new List<int>();

        private List<TurbineData> windFarm = new List<TurbineData>();

        #endregion

        public ScadaData() { }

        public ScadaData(string[] filenames, IProgress<int> progress)
        {
            LoadNSort(filenames, progress);
        }

        public void AppendFiles(string[] filenames, IProgress<int> progress)
        {
            LoadNSort(filenames, progress);
        }

        public void ExportFiles(IProgress<int> progress, string output,
            bool exportPowMaxm, bool exportPowMinm, bool exportPowMean, bool exportPowStdv,
            bool exportAmbMaxm, bool exportAmbMinm, bool exportAmbMean, bool exportAmbStdv,
            bool exportWSpMaxm, bool exportWSpMinm, bool exportWSpMean, bool exportWSpStdv,
            bool exportGBxMaxm, bool exportGBxMinm, bool exportGBxMean, bool exportGBxStdv,
            bool exportGenMaxm, bool exportGenMinm, bool exportGenMean, bool exportGenStdv,
            bool exportMBrMaxm, bool exportMBrMinm, bool exportMBrMean, bool exportMBrStdv)
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
                exportMBrMaxm, exportMBrMinm, exportMBrMean, exportMBrStdv);
        }

        private void LoadFiles(string[] filenames, IProgress<int> progress)
        {
            for (int i = 0; i < filenames.Length; i++)
            {
                this.FileName = filenames[i];

                LoadScada(progress, filenames.Length, i);
            }
        }

        private void LoadNSort(string[] filenames, IProgress<int> progress)
        {
            LoadFiles(filenames, progress);

            SortScada();
            PopulateTimeDif();
        }

        private void LoadScada(IProgress<int> progress, int numberOfFiles = 1, int i = 0)
        {
            using (StreamReader sR = new StreamReader(FileName))
            {
                try
                {
                    int count = 0;
                    bool readHeader = false;

                    while (!sR.EndOfStream)
                    {
                        if (readHeader == false)
                        {
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

                            string[] splits = Common.GetSplits(line, ',');

                            int thisAsset = Common.CanConvert<int>(splits[fileHeader.AssetCol]) ?
                                Convert.ToInt32(splits[fileHeader.AssetCol]) : throw new FileFormatException();

                            // organise loading so it would check which ones have already
                            // been loaded; then work around the ones have have been

                            if (inclTrbn.Contains(thisAsset))
                            {
                                int index = windFarm.FindIndex(x => x.UnitID == thisAsset);

                                windFarm[index].AddData(splits, fileHeader);
                            }
                            else
                            {
                                windFarm.Add(new TurbineData(splits, fileHeader));

                                inclTrbn.Add(Convert.ToInt32(splits[fileHeader.AssetCol]));
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
        
        private void PopulateTimeDif()
        {
            for (int i = 0; i < windFarm.Count; i++)
            {
                for (int j = 1; j < windFarm[i].DataSorted.Count; j++)
                {
                    windFarm[i].DataSorted[j].DeltaTime = windFarm[i].DataSorted[j].TimeStamp - windFarm[i].DataSorted[j - 1].TimeStamp;
                }
            }
        }

        private void SortScada()
        {
            for (int i = 0; i < windFarm.Count; i++)
            {
                windFarm[i].DataSorted = windFarm[i].Data.OrderBy(o => o.TimeStamp).ToList();
            }
        }

        private void WriteSCADA(IProgress<int> progress,
            bool exportPowMaxm, bool exportPowMinm, bool exportPowMean, bool exportPowStdv,
            bool exportAmbMaxm, bool exportAmbMinm, bool exportAmbMean, bool exportAmbStdv,
            bool exportWSpMaxm, bool exportWSpMinm, bool exportWSpMean, bool exportWSpStdv,
            bool exportGBxMaxm, bool exportGBxMinm, bool exportGBxMean, bool exportGBxStdv,
            bool exportGenMaxm, bool exportGenMinm, bool exportGenMean, bool exportGenStdv,
            bool exportMBrMaxm, bool exportMBrMinm, bool exportMBrMean, bool exportMBrStdv)
        {
            using (StreamWriter sW = new StreamWriter(outputName))
            {
                try
                {
                    int count = 0;
                    bool header = false;

                    for (int i = 0; i < windFarm.Count; i++)
                    {
                        for (int j = 0; j < windFarm[i].DataSorted.Count; j++)
                        {
                            StringBuilder hB = new StringBuilder();
                            StringBuilder sB = new StringBuilder();

                            ScadaSample unit = windFarm[i].DataSorted[j];

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

                            if (exportPowMaxm) { hB.Append("wtc_ActPower_max" + ","); sB.Append(unit.Powers.Maxm + ","); }
                            if (exportPowMinm) { hB.Append("wtc_ActPower_min" + ","); sB.Append(unit.Powers.Minm + ","); }
                            if (exportPowMean) { hB.Append("wtc_ActPower_mean" + ","); sB.Append(unit.Powers.Mean + ","); }
                            if (exportPowStdv) { hB.Append("wtc_ActPower_stddev" + ","); sB.Append(unit.Powers.Stdv + ","); }
                            if (exportPowMean) { hB.Append("wtc_ActPower_endvalue" + ","); sB.Append(unit.Powers.EndVal + ","); }
                            if (exportPowMean) { hB.Append("wtc_ActPower_Quality_endvalue" + ","); sB.Append(unit.Powers.QualEndVal + ","); }

                            if (exportAmbMaxm) { hB.Append("wtc_AmbieTmp_max" + ","); sB.Append(unit.AmbTemps.Maxm + ","); }
                            if (exportAmbMinm) { hB.Append("wtc_AmbieTmp_min" + ","); sB.Append(unit.AmbTemps.Minm + ","); }
                            if (exportAmbMean) { hB.Append("wtc_AmbieTmp_mean" + ","); sB.Append(unit.AmbTemps.Mean + ","); }
                            if (exportAmbStdv) { hB.Append("wtc_AmbieTmp_stddev" + ","); sB.Append(unit.AmbTemps.Stdv + ","); }
                            if (exportAmbMean) { hB.Append("wtc_twrhumid_mean" + ","); sB.Append(unit.Towers.Humid.Mean + ","); }

                            if (exportWSpMaxm) { hB.Append("wtc_AcWindSp_max" + ","); sB.Append(unit.AnemoM.ActWinds.Maxm + ","); }
                            if (exportWSpMinm) { hB.Append("wtc_AcWindSp_min" + ","); sB.Append(unit.AnemoM.ActWinds.Minm + ","); }
                            if (exportWSpMean) { hB.Append("wtc_AcWindSp_mean" + ","); sB.Append(unit.AnemoM.ActWinds.Mean + ","); }
                            if (exportWSpStdv) { hB.Append("wtc_AcWindSp_stddev" + ","); sB.Append(unit.AnemoM.ActWinds.Stdv + ","); }
                            if (exportWSpMaxm) { hB.Append("wtc_PriAnemo_max" + ","); sB.Append(unit.AnemoM.PriAnemo.Maxm + ","); }
                            if (exportWSpMinm) { hB.Append("wtc_PriAnemo_min" + ","); sB.Append(unit.AnemoM.PriAnemo.Minm + ","); }
                            if (exportWSpMean) { hB.Append("wtc_PriAnemo_mean" + ","); sB.Append(unit.AnemoM.PriAnemo.Mean + ","); }
                            if (exportWSpStdv) { hB.Append("wtc_PriAnemo_stddev" + ","); sB.Append(unit.AnemoM.PriAnemo.Stdv + ","); }
                            if (exportWSpMaxm) { hB.Append("wtc_SecAnemo_max" + ","); sB.Append(unit.AnemoM.SecAnemo.Maxm + ","); }
                            if (exportWSpMinm) { hB.Append("wtc_SecAnemo_min" + ","); sB.Append(unit.AnemoM.SecAnemo.Minm + ","); }
                            if (exportWSpMean) { hB.Append("wtc_SecAnemo_mean" + ","); sB.Append(unit.AnemoM.SecAnemo.Mean + ","); }
                            if (exportWSpStdv) { hB.Append("wtc_SecAnemo_stddev" + ","); sB.Append(unit.AnemoM.SecAnemo.Stdv + ","); }
                            if (exportWSpMaxm) { hB.Append("wtc_TetAnemo_max" + ","); sB.Append(unit.AnemoM.TerAnemo.Maxm + ","); }
                            if (exportWSpMinm) { hB.Append("wtc_TetAnemo_min" + ","); sB.Append(unit.AnemoM.TerAnemo.Minm + ","); }
                            if (exportWSpMean) { hB.Append("wtc_TetAnemo_mean" + ","); sB.Append(unit.AnemoM.TerAnemo.Mean + ","); }
                            if (exportWSpStdv) { hB.Append("wtc_TetAnemo_stddev" + ","); sB.Append(unit.AnemoM.TerAnemo.Stdv + ","); }

                            if (exportWSpMaxm) { hB.Append("wtc_PrWindSp_max" + ","); sB.Append(unit.AnemoM.PriWinds.Maxm + ","); }
                            if (exportWSpMinm) { hB.Append("wtc_PrWindSp_min" + ","); sB.Append(unit.AnemoM.PriWinds.Minm + ","); }
                            if (exportWSpMean) { hB.Append("wtc_PrWindSp_mean" + ","); sB.Append(unit.AnemoM.PriWinds.Mean + ","); }
                            if (exportWSpStdv) { hB.Append("wtc_PrWindSp_stddev" + ","); sB.Append(unit.AnemoM.PriWinds.Stdv + ","); }
                            if (exportWSpMaxm) { hB.Append("wtc_SeWindSp_max" + ","); sB.Append(unit.AnemoM.SecWinds.Maxm + ","); }
                            if (exportWSpMinm) { hB.Append("wtc_SeWindSp_min" + ","); sB.Append(unit.AnemoM.SecWinds.Minm + ","); }
                            if (exportWSpMean) { hB.Append("wtc_SeWindSp_mean" + ","); sB.Append(unit.AnemoM.SecWinds.Mean + ","); }
                            if (exportWSpStdv) { hB.Append("wtc_SeWindSp_stddev" + ","); sB.Append(unit.AnemoM.SecWinds.Stdv + ","); }

                            if (exportGenMaxm) { hB.Append("wtc_GenBeGTm_max" + ","); sB.Append(unit.Genny.bearingG.Maxm + ","); }
                            if (exportGenMinm) { hB.Append("wtc_GenBeGTm_min" + ","); sB.Append(unit.Genny.bearingG.Minm + ","); }
                            if (exportGenMean) { hB.Append("wtc_GenBeGTm_mean" + ","); sB.Append(unit.Genny.bearingG.Mean + ","); }
                            if (exportGenStdv) { hB.Append("wtc_GenBeGTm_stddev" + ","); sB.Append(unit.Genny.bearingG.Stdv + ","); }
                            if (exportGenMaxm) { hB.Append("wtc_GenBeRTm_max" + ","); sB.Append(unit.Genny.bearingR.Maxm + ","); }
                            if (exportGenMinm) { hB.Append("wtc_GenBeRTm_min" + ","); sB.Append(unit.Genny.bearingR.Minm + ","); }
                            if (exportGenMean) { hB.Append("wtc_GenBeRTm_mean" + ","); sB.Append(unit.Genny.bearingR.Mean + ","); }
                            if (exportGenStdv) { hB.Append("wtc_GenBeRTm_stddev" + ","); sB.Append(unit.Genny.bearingR.Stdv + ","); }

                            if (exportGenMaxm) { hB.Append("wtc_Gen1U1Tm_max" + ","); sB.Append(unit.Genny.G1u1.Maxm + ","); }
                            if (exportGenMinm) { hB.Append("wtc_Gen1U1Tm_min" + ","); sB.Append(unit.Genny.G1u1.Minm + ","); }
                            if (exportGenMean) { hB.Append("wtc_Gen1U1Tm_mean" + ","); sB.Append(unit.Genny.G1u1.Mean + ","); }
                            if (exportGenStdv) { hB.Append("wtc_Gen1U1Tm_stddev" + ","); sB.Append(unit.Genny.G1u1.Stdv + ","); }
                            if (exportGenMaxm) { hB.Append("wtc_Gen1V1Tm_max" + ","); sB.Append(unit.Genny.G1v1.Maxm + ","); }
                            if (exportGenMinm) { hB.Append("wtc_Gen1V1Tm_min" + ","); sB.Append(unit.Genny.G1v1.Minm + ","); }
                            if (exportGenMean) { hB.Append("wtc_Gen1V1Tm_mean" + ","); sB.Append(unit.Genny.G1v1.Mean + ","); }
                            if (exportGenStdv) { hB.Append("wtc_Gen1V1Tm_stddev" + ","); sB.Append(unit.Genny.G1v1.Stdv + ","); }
                            if (exportGenMaxm) { hB.Append("wtc_Gen1W1Tm_max" + ","); sB.Append(unit.Genny.G1w1.Maxm + ","); }
                            if (exportGenMinm) { hB.Append("wtc_Gen1W1Tm_min" + ","); sB.Append(unit.Genny.G1w1.Minm + ","); }
                            if (exportGenMean) { hB.Append("wtc_Gen1W1Tm_mean" + ","); sB.Append(unit.Genny.G1w1.Mean + ","); }
                            if (exportGenStdv) { hB.Append("wtc_Gen1W1Tm_stddev" + ","); sB.Append(unit.Genny.G1w1.Stdv + ","); }
                            if (exportGenMaxm) { hB.Append("wtc_Gen2U1Tm_max" + ","); sB.Append(unit.Genny.G2u1.Maxm + ","); }
                            if (exportGenMinm) { hB.Append("wtc_Gen2U1Tm_min" + ","); sB.Append(unit.Genny.G2u1.Minm + ","); }
                            if (exportGenMean) { hB.Append("wtc_Gen2U1Tm_mean"  + ","); sB.Append(unit.Genny.G2u1.Mean + ","); }
                            if (exportGenStdv) { hB.Append("wtc_Gen2U1Tm_stddev" + ","); sB.Append(unit.Genny.G2u1.Stdv + ","); }
                            if (exportGenMaxm) { hB.Append("wtc_Gen2V1Tm_max" + ","); sB.Append(unit.Genny.G2v1.Maxm + ","); }
                            if (exportGenMinm) { hB.Append("wtc_Gen2V1Tm_min" + ","); sB.Append(unit.Genny.G2v1.Minm + ","); }
                            if (exportGenMean) { hB.Append("wtc_Gen2V1Tm_mean" + ","); sB.Append(unit.Genny.G2v1.Mean + ","); }
                            if (exportGenStdv) { hB.Append("wtc_Gen2V1Tm_stddev" + ","); sB.Append(unit.Genny.G2v1.Stdv + ","); }
                            if (exportGenMaxm) { hB.Append("wtc_Gen2W1Tm_max" + ","); sB.Append(unit.Genny.G2w1.Maxm + ","); }
                            if (exportGenMinm) { hB.Append("wtc_Gen2W1Tm_min" + ","); sB.Append(unit.Genny.G2w1.Minm + ","); }
                            if (exportGenMean) { hB.Append("wtc_Gen2W1Tm_mean" + ","); sB.Append(unit.Genny.G2w1.Mean + ","); }
                            if (exportGenStdv) { hB.Append("wtc_Gen2W1Tm_stddev" + ","); sB.Append(unit.Genny.G2w1.Stdv + ","); }

                            if (exportMBrMaxm) { hB.Append("wtc_MainBTmp_max" + ","); sB.Append(unit.MainBear.Standards.Maxm + ","); }
                            if (exportMBrMinm) { hB.Append("wtc_MainBTmp_mean" + ","); sB.Append(unit.MainBear.Standards.Minm + ","); }
                            if (exportMBrMean) { hB.Append("wtc_MainBTmp_min" + ","); sB.Append(unit.MainBear.Standards.Mean + ","); }
                            if (exportMBrStdv) { hB.Append("wtc_MainBTmp_stddev" + ","); sB.Append(unit.MainBear.Standards.Stdv + ","); }

                            if (exportMBrMaxm) { hB.Append("wtc_MBearGTm_max" + ","); sB.Append(unit.MainBear.Gs.Maxm + ","); }
                            if (exportMBrMinm) { hB.Append("wtc_MBearGTm_mean" + ","); sB.Append(unit.MainBear.Gs.Minm + ","); }
                            if (exportMBrMean) { hB.Append("wtc_MBearGTm_min" + ","); sB.Append(unit.MainBear.Gs.Mean + ","); }
                            if (exportMBrStdv) { hB.Append("wtc_MBearGTm_stddev" + ","); sB.Append(unit.MainBear.Gs.Stdv + ","); }
                            if (exportMBrMaxm) { hB.Append("wtc_MBearHTm_max" + ","); sB.Append(unit.MainBear.Hs.Maxm + ","); }
                            if (exportMBrMinm) { hB.Append("wtc_MBearHTm_mean" + ","); sB.Append(unit.MainBear.Hs.Minm + ","); }
                            if (exportMBrMean) { hB.Append("wtc_MBearHTm_min" + ","); sB.Append(unit.MainBear.Hs.Mean + ","); }
                            if (exportMBrStdv) { hB.Append("wtc_MBearHTm_stddev" + ","); sB.Append(unit.MainBear.Hs.Stdv + ","); }

                            if (exportGBxMaxm) { hB.Append("wtc_HSGenTmp_max" + ","); sB.Append(unit.Gearbox.Hs.Gens.Maxm + ","); }
                            if (exportGBxMinm) { hB.Append("wtc_HSGenTmp_min" + ","); sB.Append(unit.Gearbox.Hs.Gens.Minm + ","); }
                            if (exportGBxMean) { hB.Append("wtc_HSGenTmp_mean" + ","); sB.Append(unit.Gearbox.Hs.Gens.Mean + ","); }
                            if (exportGBxStdv) { hB.Append("wtc_HSGenTmp_stddev" + ","); sB.Append(unit.Gearbox.Hs.Gens.Stdv + ","); }
                            if (exportGBxMaxm) { hB.Append("wtc_HSRotTmp_max" + ","); sB.Append(unit.Gearbox.Hs.Rots.Maxm + ","); }
                            if (exportGBxMinm) { hB.Append("wtc_HSRotTmp_min" + ","); sB.Append(unit.Gearbox.Hs.Rots.Minm + ","); }
                            if (exportGBxMean) { hB.Append("wtc_HSRotTmp_mean" + ","); sB.Append(unit.Gearbox.Hs.Rots.Mean + ","); }
                            if (exportGBxStdv) { hB.Append("wtc_HSRotTmp_stddev" + ","); sB.Append(unit.Gearbox.Hs.Rots.Stdv + ","); }
                            if (exportGBxMaxm) { hB.Append("wtc_IMSGenTm_max" + ","); sB.Append(unit.Gearbox.Ims.Gens.Maxm + ","); }
                            if (exportGBxMinm) { hB.Append("wtc_IMSGenTm_min" + ","); sB.Append(unit.Gearbox.Ims.Gens.Minm + ","); }
                            if (exportGBxMean) { hB.Append("wtc_IMSGenTm_mean" + ","); sB.Append(unit.Gearbox.Ims.Gens.Mean + ","); }
                            if (exportGBxStdv) { hB.Append("wtc_IMSGenTm_stddev" + ","); sB.Append(unit.Gearbox.Ims.Gens.Stdv + ","); }
                            if (exportGBxMaxm) { hB.Append("wtc_IMSRotTm_max" + ","); sB.Append(unit.Gearbox.Ims.Rots.Maxm + ","); }
                            if (exportGBxMinm) { hB.Append("wtc_IMSRotTm_min" + ","); sB.Append(unit.Gearbox.Ims.Rots.Minm + ","); }
                            if (exportGBxMean) { hB.Append("wtc_IMSRotTm_mean" + ","); sB.Append(unit.Gearbox.Ims.Rots.Mean + ","); }
                            if (exportGBxStdv) { hB.Append("wtc_IMSRotTm_stddev" + ","); sB.Append(unit.Gearbox.Ims.Rots.Stdv + ","); }

                            if (header == false) { sW.WriteLine(hB.ToString()); header = true; }
                            sW.WriteLine(sB.ToString());

                            count++;

                            if (count % 1000 == 0)
                            {
                                if (progress != null)
                                {
                                    progress.Report((int)(((double)i / windFarm.Count + (double)j / windFarm[i].DataSorted.Count / windFarm.Count) * 100));
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

        public class TurbineData : BaseStructure
        {
            // this class represents a full wind turbine which includes a set of 
            // turbines which all have sets of data

            #region Variables

            private List<ScadaSample> data = new List<ScadaSample>();
            private List<ScadaSample> dataSorted = new List<ScadaSample>();

            #endregion

            public TurbineData() { }

            public TurbineData(string[] splits, ScadaHeader header)
            {
                Type = Types.TURBINE;

                data.Add(new ScadaSample(splits, header));

                InclDtTm.Add(Common.StringToDateTime(Common.GetSplits(splits[header.TimesCol], new char[] { ' ' })));
                
                if (UnitID == -1 && data.Count > 0)
                {
                    UnitID = data[0].AssetID;
                }
            }
                        
            public void AddData(string[] splits, ScadaHeader header)
            {
                DateTime thisTime = Common.StringToDateTime(Common.GetSplits(splits[header.TimesCol], new char[] { ' ' }));

                if (InclDtTm.Contains(thisTime))
                {
                    int index = data.FindIndex(x => x.TimeStamp == thisTime);

                    data[index].AddDataFields(splits, header);
                }
                else
                {
                    data.Add(new ScadaSample(splits, header));
                    
                    InclDtTm.Add(thisTime);
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
            // the duration of loading the file            

            #region Variabes

            private int curTimeCol = -1;
            private int noVal = -1;
            private int assetCol = -1, sampleCol = -1, stationCol = -1, timeCol = -1;

            #endregion

            public ScadaHeader() { }

            public ScadaHeader(string header)
            {
                HeaderNoValues();

                HeaderSeparation(header);
            }

            private void HeaderNoValues()
            {
                Powers.Mean = noVal;
                Powers.Stdv = noVal;
                Powers.Maxm = noVal;
                Powers.Minm = noVal;

                Powers.GridFreq.Mean = noVal;
                Powers.GridFreq.Stdv = noVal;
                Powers.GridFreq.Maxm = noVal;
                Powers.GridFreq.Minm = noVal;

                Powers.PowrFact.Mean = noVal;
                Powers.PowrFact.Stdv = noVal;
                Powers.PowrFact.Maxm = noVal;
                Powers.PowrFact.Minm = noVal;

                Powers.Reactives.Mean = noVal;
                Powers.Reactives.Stdv = noVal;
                Powers.Reactives.Maxm = noVal;
                Powers.Reactives.Minm = noVal;

                Currents.phR.Mean = noVal;
                Currents.phR.Stdv = noVal;
                Currents.phR.Maxm = noVal;
                Currents.phR.Minm = noVal;

                Currents.phS.Mean = noVal;
                Currents.phS.Stdv = noVal;
                Currents.phS.Maxm = noVal;
                Currents.phS.Minm = noVal;

                Currents.phT.Mean = noVal;
                Currents.phT.Stdv = noVal;
                Currents.phT.Maxm = noVal;
                Currents.phT.Minm = noVal;

                Voltages.phR.Mean = noVal;
                Voltages.phR.Stdv = noVal;
                Voltages.phR.Maxm = noVal;
                Voltages.phR.Minm = noVal;

                Voltages.phS.Mean = noVal;
                Voltages.phS.Stdv = noVal;
                Voltages.phS.Maxm = noVal;
                Voltages.phS.Minm = noVal;

                Voltages.phT.Mean = noVal;
                Voltages.phT.Stdv = noVal;
                Voltages.phT.Maxm = noVal;
                Voltages.phT.Minm = noVal;

                Genny.G1u1.Mean = noVal;
                Genny.G1u1.Stdv = noVal;
                Genny.G1u1.Maxm = noVal;
                Genny.G1u1.Minm = noVal;
                Genny.G1v1.Mean = noVal;
                Genny.G1v1.Stdv = noVal;
                Genny.G1v1.Maxm = noVal;
                Genny.G1v1.Minm = noVal;
                Genny.G1w1.Mean = noVal;
                Genny.G1w1.Stdv = noVal;
                Genny.G1w1.Maxm = noVal;
                Genny.G1w1.Minm = noVal;

                Genny.G2u1.Mean = noVal;
                Genny.G2u1.Stdv = noVal;
                Genny.G2u1.Maxm = noVal;
                Genny.G2u1.Minm = noVal;
                Genny.G2v1.Mean = noVal;
                Genny.G2v1.Stdv = noVal;
                Genny.G2v1.Maxm = noVal;
                Genny.G2v1.Minm = noVal;
                Genny.G2w1.Mean = noVal;
                Genny.G2w1.Stdv = noVal;
                Genny.G2w1.Maxm = noVal;
                Genny.G2w1.Minm = noVal;

                AmbTemps.Mean = noVal;
                AmbTemps.Stdv = noVal;
                AmbTemps.Maxm = noVal;
                AmbTemps.Minm = noVal;
                DeltaTs.Mean = noVal;
                DeltaTs.Stdv = noVal;
                DeltaTs.Maxm = noVal;
                DeltaTs.Minm = noVal;

                Gearbox.Hs.Gens.Mean = noVal;
                Gearbox.Hs.Gens.Stdv = noVal;
                Gearbox.Hs.Gens.Maxm = noVal;
                Gearbox.Hs.Gens.Minm = noVal;
                Gearbox.Hs.Rots.Mean = noVal;
                Gearbox.Hs.Rots.Stdv = noVal;
                Gearbox.Hs.Rots.Maxm = noVal;
                Gearbox.Hs.Rots.Minm = noVal;
                Gearbox.Ims.Gens.Mean = noVal;
                Gearbox.Ims.Gens.Stdv = noVal;
                Gearbox.Ims.Gens.Maxm = noVal;
                Gearbox.Ims.Gens.Minm = noVal;
                Gearbox.Ims.Rots.Mean = noVal;
                Gearbox.Ims.Rots.Stdv = noVal;
                Gearbox.Ims.Rots.Maxm = noVal;
                Gearbox.Ims.Rots.Minm = noVal;
                Gearbox.Oils.Mean = noVal;
                Gearbox.Oils.Stdv = noVal;
                Gearbox.Oils.Maxm = noVal;
                Gearbox.Oils.Minm = noVal;

                Genny.bearingG.Mean = noVal;
                Genny.bearingG.Stdv = noVal;
                Genny.bearingG.Maxm = noVal;
                Genny.bearingG.Minm = noVal;
                Genny.bearingR.Mean = noVal;
                Genny.bearingR.Stdv = noVal;
                Genny.bearingR.Maxm = noVal;
                Genny.bearingR.Minm = noVal;

                MainBear.Standards.Mean = noVal;
                MainBear.Standards.Stdv = noVal;
                MainBear.Standards.Maxm = noVal;
                MainBear.Standards.Minm = noVal;
                MainBear.Gs.Mean = noVal;
                MainBear.Gs.Stdv = noVal;
                MainBear.Gs.Maxm = noVal;
                MainBear.Gs.Minm = noVal;
                MainBear.Hs.Mean = noVal;
                MainBear.Hs.Stdv = noVal;
                MainBear.Hs.Maxm = noVal;
                MainBear.Hs.Minm = noVal;

                Nacel.Mean = noVal;
                Nacel.Stdv = noVal;
                Nacel.Maxm = noVal;
                Nacel.Minm = noVal;

                AnemoM.ActWinds.Mean = noVal;
                AnemoM.ActWinds.Stdv = noVal;
                AnemoM.ActWinds.Maxm = noVal;
                AnemoM.ActWinds.Minm = noVal;

                AnemoM.PriAnemo.Mean = noVal;
                AnemoM.PriAnemo.Stdv = noVal;
                AnemoM.PriAnemo.Maxm = noVal;
                AnemoM.PriAnemo.Minm = noVal;

                AnemoM.PriWinds.Mean = noVal;
                AnemoM.PriWinds.Stdv = noVal;
                AnemoM.PriWinds.Maxm = noVal;
                AnemoM.PriWinds.Minm = noVal;

                AnemoM.SecAnemo.Mean = noVal;
                AnemoM.SecAnemo.Stdv = noVal;
                AnemoM.SecAnemo.Maxm = noVal;
                AnemoM.SecAnemo.Minm = noVal;

                AnemoM.SecWinds.Mean = noVal;
                AnemoM.SecWinds.Stdv = noVal;
                AnemoM.SecWinds.Maxm = noVal;
                AnemoM.SecWinds.Minm = noVal;

                AnemoM.TerAnemo.Mean = noVal;
                AnemoM.TerAnemo.Stdv = noVal;
                AnemoM.TerAnemo.Maxm = noVal;
                AnemoM.TerAnemo.Minm = noVal;

                Genny.Rpms.Mean = noVal;
                Genny.Rpms.Stdv = noVal;
                Genny.Rpms.Maxm = noVal;
                Genny.Rpms.Minm = noVal;

                Towers.Humid.Mean = noVal;
                Towers.Humid.Stdv = noVal;
                Towers.Humid.Maxm = noVal;
                Towers.Humid.Minm = noVal;
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
                                if (parts[2] == "mean") { Currents.phR.Mean = i; }
                                else if (parts[2] == "stddev") { Currents.phR.Stdv = i; }
                                else if (parts[2] == "max") { Currents.phR.Maxm = i; }
                                else if (parts[2] == "min") { Currents.phR.Minm = i; }
                            }
                            else if (parts[1] == "ampphs")
                            {
                                if (parts[2] == "mean") { Currents.phS.Mean = i; }
                                else if (parts[2] == "stddev") { Currents.phS.Stdv = i; }
                                else if (parts[2] == "max") { Currents.phS.Maxm = i; }
                                else if (parts[2] == "min") { Currents.phS.Minm = i; }
                            }
                            else if (parts[1] == "amppht")
                            {
                                if (parts[2] == "mean") { Currents.phT.Mean = i; }
                                else if (parts[2] == "stddev") { Currents.phT.Stdv = i; }
                                else if (parts[2] == "max") { Currents.phT.Maxm = i; }
                                else if (parts[2] == "min") { Currents.phT.Minm = i; }
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
                            }
                            else if (parts[1] == "gen1v1tm")
                            {
                                if (parts[2] == "mean") { Genny.G1v1.Mean = i; }
                                else if (parts[2] == "stddev") { Genny.G1v1.Stdv = i; }
                                else if (parts[2] == "max") { Genny.G1v1.Maxm = i; }
                                else if (parts[2] == "min") { Genny.G1v1.Minm = i; }
                            }
                            else if (parts[1] == "gen1w1tm")
                            {
                                if (parts[2] == "mean") { Genny.G1w1.Mean = i; }
                                else if (parts[2] == "stddev") { Genny.G1w1.Stdv = i; }
                                else if (parts[2] == "max") { Genny.G1w1.Maxm = i; }
                                else if (parts[2] == "min") { Genny.G1w1.Minm = i; }
                            }
                            else if (parts[1] == "gen2u1tm")
                            {
                                if (parts[2] == "mean") { Genny.G2u1.Mean = i; }
                                else if (parts[2] == "stddev") { Genny.G2u1.Stdv = i; }
                                else if (parts[2] == "max") { Genny.G2u1.Maxm = i; }
                                else if (parts[2] == "min") { Genny.G2u1.Minm = i; }
                            }
                            else if (parts[1] == "gen2v1tm")
                            {
                                if (parts[2] == "mean") { Genny.G2v1.Mean = i; }
                                else if (parts[2] == "stddev") { Genny.G2v1.Stdv = i; }
                                else if (parts[2] == "max") { Genny.G2v1.Maxm = i; }
                                else if (parts[2] == "min") { Genny.G2v1.Minm = i; }
                            }
                            else if (parts[1] == "gen2w1tm")
                            {
                                if (parts[2] == "mean") { Genny.G2w1.Mean = i; }
                                else if (parts[2] == "stddev") { Genny.G2w1.Stdv = i; }
                                else if (parts[2] == "max") { Genny.G2w1.Maxm = i; }
                                else if (parts[2] == "min") { Genny.G2w1.Minm = i; }
                            }
                            else if (parts[1] == "genbegtm")
                            {
                                if (parts[2] == "mean") { Genny.bearingG.Mean = i; }
                                else if (parts[2] == "stddev") { Genny.bearingG.Stdv = i; }
                                else if (parts[2] == "max") { Genny.bearingG.Maxm = i; }
                                else if (parts[2] == "min") { Genny.bearingG.Minm = i; }
                            }
                            else if (parts[1] == "genbertm")
                            {
                                if (parts[2] == "mean") { Genny.bearingR.Mean = i; }
                                else if (parts[2] == "stddev") { Genny.bearingR.Stdv = i; }
                                else if (parts[2] == "max") { Genny.bearingR.Maxm = i; }
                                else if (parts[2] == "min") { Genny.bearingR.Minm = i; }
                            }
                            else if (parts[1] == "geoiltmp")
                            {
                                if (parts[2] == "mean") { Gearbox.Oils.Mean = i; }
                                else if (parts[2] == "stddev") { Gearbox.Oils.Stdv = i; }
                                else if (parts[2] == "max") { Gearbox.Oils.Maxm = i; }
                                else if (parts[2] == "min") { Gearbox.Oils.Minm = i; }
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
                            }
                            else if (parts[1] == "hsrottmp")
                            {
                                if (parts[2] == "mean") { Gearbox.Hs.Rots.Mean = i; }
                                else if (parts[2] == "stddev") { Gearbox.Hs.Rots.Stdv = i; }
                                else if (parts[2] == "max") { Gearbox.Hs.Rots.Maxm = i; }
                                else if (parts[2] == "min") { Gearbox.Hs.Rots.Minm = i; }
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
                            }
                            else if (parts[1] == "imsrottm")
                            {
                                if (parts[2] == "mean") { Gearbox.Ims.Rots.Mean = i; }
                                else if (parts[2] == "stddev") { Gearbox.Ims.Rots.Stdv = i; }
                                else if (parts[2] == "max") { Gearbox.Ims.Rots.Maxm = i; }
                                else if (parts[2] == "min") { Gearbox.Ims.Rots.Minm = i; }
                            }
                            else if (parts[1] == "mainbtmp")
                            {
                                if (parts[2] == "mean") { MainBear.Standards.Mean = i; }
                                else if (parts[2] == "stddev") { MainBear.Standards.Stdv = i; }
                                else if (parts[2] == "max") { MainBear.Standards.Maxm = i; }
                                else if (parts[2] == "min") { MainBear.Standards.Minm = i; }
                            }
                            else if (parts[1] == "mbeargtm")
                            {
                                if (parts[2] == "mean") { MainBear.Gs.Mean = i; }
                                else if (parts[2] == "stddev") { MainBear.Gs.Stdv = i; }
                                else if (parts[2] == "max") { MainBear.Gs.Maxm = i; }
                                else if (parts[2] == "min") { MainBear.Gs.Minm = i; }
                            }
                            else if (parts[1] == "mbearhtm")
                            {
                                if (parts[2] == "mean") { MainBear.Hs.Mean = i; }
                                else if (parts[2] == "stddev") { MainBear.Hs.Stdv = i; }
                                else if (parts[2] == "max") { MainBear.Hs.Maxm = i; }
                                else if (parts[2] == "min") { MainBear.Hs.Minm = i; }
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
                            #endregion
                        }
                    }
                }
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
            // this class should be usable as the representation of a turbine, a set of which 
            // is grouped as a wind farm

            #region Variables
            
            private int error = -9999;

            private DateTime curTime = new DateTime();

            private Ambient ambTemps = new Ambient();
            private Anemometry anemoM = new Anemometry();
            private Board board = new Board();
            private Brake brake = new Brake();
            private Capacitor capac = new Capacitor();
            private Coolant coolant = new Coolant();
            private Current current = new Current();
            private DeltaT deltaT = new DeltaT();
            private Gear gear = new Gear();
            private GearBox gearbox = new GearBox();
            private Generator genny = new Generator();
            private GridFilter gridFilt = new GridFilter();
            private Hub hub = new Hub();
            private HydOil hydOil = new HydOil();
            private Internal intern = new Internal();
            private MainBearing mainBear = new MainBearing();
            private Nacelle nacel = new Nacelle();
            private Power power = new Power();
            private Reactor react = new Reactor();
            private Tower tower = new Tower();
            private Transformer transf = new Transformer();
            private Voltage voltage = new Voltage();

            #endregion

            public ScadaSample() { }

            public ScadaSample(string[] data, ScadaHeader header)
            {
                LoadData(data, header);
            }

            public void AddDataFields(string[] data, ScadaHeader header)
            {
                LoadData(data, header);
            }

            private void LoadData(string[] data, ScadaHeader header)
            {
                if (header.TimesCol != -1)
                {
                    TimeStamp = Common.StringToDateTime(Common.GetSplits(data[header.TimesCol], new char[] { ' ' }));
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

                power.Mean = GetVals(power.Mean, data, header.Powers.Mean);
                power.Stdv = GetVals(power.Stdv, data, header.Powers.Stdv);
                power.Maxm = GetVals(power.Maxm, data, header.Powers.Maxm);
                power.Minm = GetVals(power.Minm, data, header.Powers.Minm);
                power.EndVal = GetVals(power.EndVal, data, header.Powers.EndValCol); 
                power.QualEndVal = GetVals(power.QualEndVal, data, header.Powers.QualEndValCol); 
                power.RgStEndVal = GetVals(power.RgStEndVal, data, header.Powers.RgStEndValCol); 

                power.GridFreq.Mean = GetVals(power.GridFreq.Mean, data, header.Powers.GridFreq.Mean);
                power.GridFreq.Stdv = GetVals(power.GridFreq.Stdv, data, header.Powers.GridFreq.Stdv);
                power.GridFreq.Maxm = GetVals(power.GridFreq.Maxm, data, header.Powers.GridFreq.Maxm);
                power.GridFreq.Minm = GetVals(power.GridFreq.Minm, data, header.Powers.GridFreq.Minm);

                power.PowrFact.Mean = GetVals(power.PowrFact.Mean, data, header.Powers.PowrFact.Mean);
                power.PowrFact.Stdv = GetVals(power.PowrFact.Stdv, data, header.Powers.PowrFact.Stdv);
                power.PowrFact.Maxm = GetVals(power.PowrFact.Maxm, data, header.Powers.PowrFact.Maxm);
                power.PowrFact.Minm = GetVals(power.PowrFact.Minm, data, header.Powers.PowrFact.Minm);
                power.PowrFact.EndVal = GetVals(power.PowrFact.EndVal, data, header.Powers.PowrFact.EndValCol);

                power.Reactives.Mean = GetVals(power.Reactives.Mean, data, header.Powers.Reactives.Mean);
                power.Reactives.Stdv = GetVals(power.Reactives.Stdv, data, header.Powers.Reactives.Stdv);
                power.Reactives.Maxm = GetVals(power.Reactives.Maxm, data, header.Powers.Reactives.Maxm);
                power.Reactives.Minm = GetVals(power.Reactives.Minm, data, header.Powers.Reactives.Minm);
                power.Reactives.EndVal = GetVals(power.Reactives.EndVal, data, header.Powers.Reactives.EndValCol);

                current.phR.Mean = GetVals(current.phR.Mean, data, header.Currents.phR.Mean);
                current.phR.Stdv = GetVals(current.phR.Stdv, data, header.Currents.phR.Stdv);
                current.phR.Maxm = GetVals(current.phR.Maxm, data, header.Currents.phR.Maxm);
                current.phR.Minm = GetVals(current.phR.Minm, data, header.Currents.phR.Minm);

                current.phS.Mean = GetVals(current.phS.Mean, data, header.Currents.phS.Mean);
                current.phS.Stdv = GetVals(current.phS.Stdv, data, header.Currents.phS.Stdv);
                current.phS.Maxm = GetVals(current.phS.Maxm, data, header.Currents.phS.Maxm);
                current.phS.Minm = GetVals(current.phS.Minm, data, header.Currents.phS.Minm);

                current.phT.Mean = GetVals(current.phT.Mean, data, header.Currents.phT.Mean);
                current.phT.Stdv = GetVals(current.phT.Stdv, data, header.Currents.phT.Stdv);
                current.phT.Maxm = GetVals(current.phT.Maxm, data, header.Currents.phT.Maxm);
                current.phT.Minm = GetVals(current.phT.Minm, data, header.Currents.phT.Minm);

                voltage.phR.Mean = GetVals(voltage.phR.Mean, data, header.Voltages.phR.Mean);
                voltage.phR.Stdv = GetVals(voltage.phR.Stdv, data, header.Voltages.phR.Stdv);
                voltage.phR.Maxm = GetVals(voltage.phR.Maxm, data, header.Voltages.phR.Maxm);
                voltage.phR.Minm = GetVals(voltage.phR.Minm, data, header.Voltages.phR.Minm);

                voltage.phS.Mean = GetVals(voltage.phS.Mean, data, header.Voltages.phS.Mean);
                voltage.phS.Stdv = GetVals(voltage.phS.Stdv, data, header.Voltages.phS.Stdv);
                voltage.phS.Maxm = GetVals(voltage.phS.Maxm, data, header.Voltages.phS.Maxm);
                voltage.phS.Minm = GetVals(voltage.phS.Minm, data, header.Voltages.phS.Minm);

                voltage.phT.Mean = GetVals(voltage.phT.Mean, data, header.Voltages.phT.Mean);
                voltage.phT.Stdv = GetVals(voltage.phT.Stdv, data, header.Voltages.phT.Stdv);
                voltage.phT.Maxm = GetVals(voltage.phT.Maxm, data, header.Voltages.phT.Maxm);
                voltage.phT.Minm = GetVals(voltage.phT.Minm, data, header.Voltages.phT.Minm);

                #endregion
                #region Temperature File

                ambTemps.Maxm = GetVals(ambTemps.Mean, data, header.AmbTemps.Mean);
                ambTemps.Stdv = GetVals(ambTemps.Stdv, data, header.AmbTemps.Stdv);
                ambTemps.Maxm = GetVals(ambTemps.Maxm, data, header.AmbTemps.Maxm);
                ambTemps.Minm = GetVals(ambTemps.Minm, data, header.AmbTemps.Minm);

                deltaT.Maxm = GetVals(deltaT.Mean, data, header.DeltaTs.Mean);
                deltaT.Stdv = GetVals(deltaT.Stdv, data, header.DeltaTs.Stdv);
                deltaT.Maxm = GetVals(deltaT.Maxm, data, header.DeltaTs.Maxm);
                deltaT.Minm = GetVals(deltaT.Minm, data, header.DeltaTs.Minm);

                gearbox.Hs.Gens.Mean = GetVals(gearbox.Hs.Gens.Mean, data, header.Gearbox.Hs.Gens.Mean);
                gearbox.Hs.Gens.Stdv = GetVals(gearbox.Hs.Gens.Stdv, data, header.Gearbox.Hs.Gens.Stdv);
                gearbox.Hs.Gens.Maxm = GetVals(gearbox.Hs.Gens.Maxm, data, header.Gearbox.Hs.Gens.Maxm);
                gearbox.Hs.Gens.Minm = GetVals(gearbox.Hs.Gens.Minm, data, header.Gearbox.Hs.Gens.Minm);
                gearbox.Hs.Rots.Mean = GetVals(gearbox.Hs.Rots.Mean, data, header.Gearbox.Hs.Rots.Mean);
                gearbox.Hs.Rots.Stdv = GetVals(gearbox.Hs.Rots.Stdv, data, header.Gearbox.Hs.Rots.Stdv);
                gearbox.Hs.Rots.Maxm = GetVals(gearbox.Hs.Rots.Maxm, data, header.Gearbox.Hs.Rots.Maxm);
                gearbox.Hs.Rots.Minm = GetVals(gearbox.Hs.Rots.Minm, data, header.Gearbox.Hs.Rots.Minm);

                gearbox.Ims.Gens.Mean = GetVals(gearbox.Ims.Gens.Mean, data, header.Gearbox.Ims.Gens.Mean);
                gearbox.Ims.Gens.Stdv = GetVals(gearbox.Ims.Gens.Stdv, data, header.Gearbox.Ims.Gens.Stdv);
                gearbox.Ims.Gens.Maxm = GetVals(gearbox.Ims.Gens.Maxm, data, header.Gearbox.Ims.Gens.Maxm);
                gearbox.Ims.Gens.Minm = GetVals(gearbox.Ims.Gens.Minm, data, header.Gearbox.Ims.Gens.Minm);
                gearbox.Ims.Rots.Mean = GetVals(gearbox.Ims.Rots.Mean, data, header.Gearbox.Ims.Rots.Mean);
                gearbox.Ims.Rots.Stdv = GetVals(gearbox.Ims.Rots.Stdv, data, header.Gearbox.Ims.Rots.Stdv);
                gearbox.Ims.Rots.Maxm = GetVals(gearbox.Ims.Rots.Maxm, data, header.Gearbox.Ims.Rots.Maxm);
                gearbox.Ims.Rots.Minm = GetVals(gearbox.Ims.Rots.Minm, data, header.Gearbox.Ims.Rots.Minm);

                gearbox.Oils.Mean = GetVals(gearbox.Oils.Mean, data, header.Gearbox.Oils.Mean);
                gearbox.Oils.Stdv = GetVals(gearbox.Oils.Stdv, data, header.Gearbox.Oils.Stdv);
                gearbox.Oils.Maxm = GetVals(gearbox.Oils.Maxm, data, header.Gearbox.Oils.Maxm);
                gearbox.Oils.Minm = GetVals(gearbox.Oils.Minm, data, header.Gearbox.Oils.Minm);

                genny.G1u1.Mean = GetVals(genny.G1u1.Mean, data, header.Genny.G1u1.Mean);
                genny.G1u1.Stdv = GetVals(genny.G1u1.Stdv, data, header.Genny.G1u1.Stdv);
                genny.G1u1.Maxm = GetVals(genny.G1u1.Maxm, data, header.Genny.G1u1.Maxm);
                genny.G1u1.Minm = GetVals(genny.G1u1.Minm, data, header.Genny.G1u1.Minm);
                genny.G1v1.Mean = GetVals(genny.G1v1.Mean, data, header.Genny.G1v1.Mean);
                genny.G1v1.Stdv = GetVals(genny.G1v1.Stdv, data, header.Genny.G1v1.Stdv);
                genny.G1v1.Maxm = GetVals(genny.G1v1.Maxm, data, header.Genny.G1v1.Maxm);
                genny.G1v1.Minm = GetVals(genny.G1v1.Minm, data, header.Genny.G1v1.Minm);
                genny.G1w1.Mean = GetVals(genny.G1w1.Mean, data, header.Genny.G1w1.Mean);
                genny.G1w1.Stdv = GetVals(genny.G1w1.Stdv, data, header.Genny.G1w1.Stdv);
                genny.G1w1.Maxm = GetVals(genny.G1w1.Maxm, data, header.Genny.G1w1.Maxm);
                genny.G1w1.Minm = GetVals(genny.G1w1.Minm, data, header.Genny.G1w1.Minm);

                genny.G2u1.Mean = GetVals(genny.G2u1.Mean, data, header.Genny.G2u1.Mean);
                genny.G2u1.Stdv = GetVals(genny.G2u1.Stdv, data, header.Genny.G2u1.Stdv);
                genny.G2u1.Maxm = GetVals(genny.G2u1.Maxm, data, header.Genny.G2u1.Maxm);
                genny.G2u1.Minm = GetVals(genny.G2u1.Minm, data, header.Genny.G2u1.Minm);
                genny.G2v1.Mean = GetVals(genny.G2v1.Mean, data, header.Genny.G2v1.Mean);
                genny.G2v1.Stdv = GetVals(genny.G2v1.Stdv, data, header.Genny.G2v1.Stdv);
                genny.G2v1.Maxm = GetVals(genny.G2v1.Maxm, data, header.Genny.G2v1.Maxm);
                genny.G2v1.Minm = GetVals(genny.G2v1.Minm, data, header.Genny.G2v1.Minm);
                genny.G2w1.Mean = GetVals(genny.G2w1.Mean, data, header.Genny.G2w1.Mean);
                genny.G2w1.Stdv = GetVals(genny.G2w1.Stdv, data, header.Genny.G2w1.Stdv);
                genny.G2w1.Maxm = GetVals(genny.G2w1.Maxm, data, header.Genny.G2w1.Maxm);
                genny.G2w1.Minm = GetVals(genny.G2w1.Minm, data, header.Genny.G2w1.Minm);

                genny.bearingG.Mean = GetVals(genny.bearingG.Mean, data, header.Genny.bearingG.Mean);
                genny.bearingG.Stdv = GetVals(genny.bearingG.Stdv, data, header.Genny.bearingG.Stdv);
                genny.bearingG.Maxm = GetVals(genny.bearingG.Maxm, data, header.Genny.bearingG.Maxm);
                genny.bearingG.Minm = GetVals(genny.bearingG.Minm, data, header.Genny.bearingG.Minm);
                genny.bearingR.Mean = GetVals(genny.bearingR.Mean, data, header.Genny.bearingR.Mean);
                genny.bearingR.Stdv = GetVals(genny.bearingR.Stdv, data, header.Genny.bearingR.Stdv);
                genny.bearingR.Maxm = GetVals(genny.bearingR.Maxm, data, header.Genny.bearingR.Maxm);
                genny.bearingR.Minm = GetVals(genny.bearingR.Minm, data, header.Genny.bearingR.Minm);

                mainBear.Standards.Mean = GetVals(mainBear.Standards.Mean, data, header.MainBear.Standards.Mean);
                mainBear.Standards.Stdv = GetVals(mainBear.Standards.Stdv, data, header.MainBear.Standards.Stdv);
                mainBear.Standards.Maxm = GetVals(mainBear.Standards.Maxm, data, header.MainBear.Standards.Maxm);
                mainBear.Standards.Minm = GetVals(mainBear.Standards.Minm, data, header.MainBear.Standards.Minm);

                mainBear.Gs.Mean = GetVals(mainBear.Gs.Mean, data, header.MainBear.Gs.Mean);
                mainBear.Gs.Stdv = GetVals(mainBear.Gs.Stdv, data, header.MainBear.Gs.Stdv);
                mainBear.Gs.Maxm = GetVals(mainBear.Gs.Maxm, data, header.MainBear.Gs.Maxm);
                mainBear.Gs.Minm = GetVals(mainBear.Gs.Minm, data, header.MainBear.Gs.Minm);
                mainBear.Hs.Mean = GetVals(mainBear.Hs.Mean, data, header.MainBear.Hs.Mean);
                mainBear.Hs.Stdv = GetVals(mainBear.Hs.Stdv, data, header.MainBear.Hs.Stdv);
                mainBear.Hs.Maxm = GetVals(mainBear.Hs.Maxm, data, header.MainBear.Hs.Maxm);
                mainBear.Hs.Minm = GetVals(mainBear.Hs.Minm, data, header.MainBear.Hs.Minm);

                nacel.Mean = GetVals(nacel.Mean, data, header.Nacel.Mean);
                nacel.Stdv = GetVals(nacel.Stdv, data, header.Nacel.Stdv);
                nacel.Maxm = GetVals(nacel.Maxm, data, header.Nacel.Maxm);
                nacel.Minm = GetVals(nacel.Minm, data, header.Nacel.Minm);

                #endregion
                #region Turbine File

                if (header.CurTimeCol != -1)
                {
                    curTime = Common.StringToDateTime(Common.GetSplits(data[header.CurTimeCol], new char[] { ' ' }));
                }

                anemoM.ActWinds.Mean = GetVals(anemoM.ActWinds.Mean, data, header.AnemoM.ActWinds.Mean);
                anemoM.ActWinds.Stdv = GetVals(anemoM.ActWinds.Stdv, data, header.AnemoM.ActWinds.Stdv);
                anemoM.ActWinds.Maxm = GetVals(anemoM.ActWinds.Maxm, data, header.AnemoM.ActWinds.Maxm);
                anemoM.ActWinds.Minm = GetVals(anemoM.ActWinds.Minm, data, header.AnemoM.ActWinds.Minm);

                anemoM.PriAnemo.Mean = GetVals(anemoM.PriAnemo.Mean, data, header.AnemoM.PriAnemo.Mean);
                anemoM.PriAnemo.Stdv = GetVals(anemoM.PriAnemo.Stdv, data, header.AnemoM.PriAnemo.Stdv);
                anemoM.PriAnemo.Maxm = GetVals(anemoM.PriAnemo.Maxm, data, header.AnemoM.PriAnemo.Maxm);
                anemoM.PriAnemo.Minm = GetVals(anemoM.PriAnemo.Minm, data, header.AnemoM.PriAnemo.Minm);

                anemoM.PriWinds.Mean = GetVals(anemoM.PriWinds.Mean, data, header.AnemoM.PriWinds.Mean);
                anemoM.PriWinds.Stdv = GetVals(anemoM.PriWinds.Stdv, data, header.AnemoM.PriWinds.Stdv);
                anemoM.PriWinds.Maxm = GetVals(anemoM.PriWinds.Maxm, data, header.AnemoM.PriWinds.Maxm);
                anemoM.PriWinds.Minm = GetVals(anemoM.PriWinds.Minm, data, header.AnemoM.PriWinds.Minm);

                anemoM.SecAnemo.Mean = GetVals(anemoM.SecAnemo.Mean, data, header.AnemoM.SecAnemo.Mean);
                anemoM.SecAnemo.Stdv = GetVals(anemoM.SecAnemo.Stdv, data, header.AnemoM.SecAnemo.Stdv);
                anemoM.SecAnemo.Maxm = GetVals(anemoM.SecAnemo.Maxm, data, header.AnemoM.SecAnemo.Maxm);
                anemoM.SecAnemo.Minm = GetVals(anemoM.SecAnemo.Minm, data, header.AnemoM.SecAnemo.Minm);

                anemoM.SecWinds.Mean = GetVals(anemoM.SecWinds.Mean, data, header.AnemoM.SecWinds.Mean);
                anemoM.SecWinds.Stdv = GetVals(anemoM.SecWinds.Stdv, data, header.AnemoM.SecWinds.Stdv);
                anemoM.SecWinds.Maxm = GetVals(anemoM.SecWinds.Maxm, data, header.AnemoM.SecWinds.Maxm);
                anemoM.SecWinds.Minm = GetVals(anemoM.SecWinds.Minm, data, header.AnemoM.SecWinds.Minm);

                anemoM.TerAnemo.Mean = GetVals(anemoM.TerAnemo.Mean, data, header.AnemoM.TerAnemo.Mean);
                anemoM.TerAnemo.Stdv = GetVals(anemoM.TerAnemo.Stdv, data, header.AnemoM.TerAnemo.Stdv);
                anemoM.TerAnemo.Maxm = GetVals(anemoM.TerAnemo.Maxm, data, header.AnemoM.TerAnemo.Maxm);
                anemoM.TerAnemo.Minm = GetVals(anemoM.TerAnemo.Minm, data, header.AnemoM.TerAnemo.Minm);

                genny.Rpms.Mean = GetVals(genny.Rpms.Mean, data, header.Genny.Rpms.Mean);
                genny.Rpms.Stdv = GetVals(genny.Rpms.Stdv, data, header.Genny.Rpms.Stdv);
                genny.Rpms.Maxm = GetVals(genny.Rpms.Maxm, data, header.Genny.Rpms.Maxm);
                genny.Rpms.Minm = GetVals(genny.Rpms.Minm, data, header.Genny.Rpms.Minm);

                tower.Humid.Mean = GetVals(tower.Humid.Mean, data, header.Towers.Humid.Mean);
                tower.Humid.Stdv = GetVals(tower.Humid.Stdv, data, header.Towers.Humid.Stdv);
                tower.Humid.Maxm = GetVals(tower.Humid.Maxm, data, header.Towers.Humid.Maxm);
                tower.Humid.Minm = GetVals(tower.Humid.Minm, data, header.Towers.Humid.Minm);

                #endregion
            }

            private double GetVals(double value, string[] data, double index)
            {
                if (value == -999999 || value == error)
                {
                    return Common.GetVals(data, (int)index, error);
                }
                else
                {
                    return value;
                }
            }
            
            #region Support Classes

            #region Base Variables

            public class Pressure : Stats { }
            public class Temperature : Stats { }

            #endregion 

            public class Ambient : Temperature { }
            public class Board : Temperature { }
            public class Coolant : Temperature { }
            public class DeltaT : Temperature { }
            public class Gear : Pressure { }
            public class Nacelle : Temperature { }

            public class Anemometry
            {
                #region Variables

                protected WindSpeeds actWinds = new WindSpeeds();
                protected WindSpeeds priAnemo = new WindSpeeds();
                protected WindSpeeds priWinds = new WindSpeeds();
                protected WindSpeeds secAnemo = new WindSpeeds();
                protected WindSpeeds secWinds = new WindSpeeds();
                protected WindSpeeds terAnemo = new WindSpeeds();

                #endregion

                #region Support Classes

                public class WindSpeeds : Stats { }
                
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
            
            public class Capacitor
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
                protected BearingG bearingg = new BearingG();
                protected BearingR bearingr = new BearingR();
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

                public class BearingG : Temperature { }
                public class BearingR : Temperature { }

                #endregion

                #region Properties

                public G1U1 G1u1 { get { return g1u1; } set { g1u1 = value; } }
                public G1V1 G1v1 { get { return g1v1; } set { g1v1 = value; } }
                public G1W1 G1w1 { get { return g1w1; } set { g1w1 = value; } }
                public G2U1 G2u1 { get { return g2u1; } set { g2u1 = value; } }
                public G2V1 G2v1 { get { return g2v1; } set { g2v1 = value; } }
                public G2W1 G2w1 { get { return g2w1; } set { g2w1 = value; } }
                public BearingG bearingG { get { return bearingg; } set { bearingg = value; } }
                public BearingR bearingR { get { return bearingr; } set { bearingr = value; } }
                public Rpm Rpms { get { return rpms; } set { rpms = value; } }

                #endregion
            }

            public class GridFilter
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

                protected Standard standard = new Standard();
                protected G g = new G();
                protected H h = new H();

                #endregion

                #region Support Classes

                public class Standard : Temperature { }

                public class G : Temperature { }
                public class H : Temperature { }

                #endregion

                #region Properties
                 
                public Standard Standards { get { return standard; } set { standard = value; } }
                public G Gs { get { return g; } set { g = value; } }
                public H Hs { get { return h; } set { h = value; } }

                #endregion
            }

            public class Power : Stats
            {
                #region Variables

                protected double endValue = -9999;
                protected double qualEndVal = -9999;
                protected double rgStEndVal = -9999;
                protected int endValCol = -1, qualEndValCol = -1, rgStEndValCol = -1;

                protected GridFrequency gridFreq = new GridFrequency();
                protected PowerFactor powrFact = new PowerFactor();
                protected Reactive reactive = new Reactive();
                
                #endregion

                #region Support Classes            

                public class PowerFactor : Stats
                {
                    #region Variables

                    protected double endVal = -9999;
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

                    protected double endValue = -9999;
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

                    protected double endVal = -9999;
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

            public int Error { get { return error; } set { error = value; } }

            public DateTime CurTime {  get { return curTime; } set { curTime = value; } }

            public Ambient AmbTemps { get { return ambTemps; } set { ambTemps = value; } }
            public Anemometry AnemoM { get { return anemoM; } set { anemoM = value; } }
            public Board Boards { get { return board; } set { board = value; } }
            public Brake Brakes { get { return brake; } set { brake = value; } }
            public Capacitor Capac { get { return capac; } set { capac = value; } }
            public Coolant Coolants { get { return coolant; } set { coolant = value; } }
            public Current Currents { get { return current; } set { current = value; } }
            public DeltaT DeltaTs { get { return deltaT; } set { deltaT = value; } }
            public Gear Gears { get { return gear; } set { gear = value; } }
            public GearBox Gearbox { get { return gearbox; } set { gearbox = value; } }
            public Generator Genny { get { return genny; } set { genny = value; } }
            public GridFilter GridFilt { get { return gridFilt; } set { gridFilt = value; } }
            public Hub Hubs { get { return hub; } set { hub = value; } }
            public HydOil HydOils { get { return hydOil; } set { hydOil = value; } }
            public Internal Intern { get { return intern; } set { intern = value; } }
            public MainBearing MainBear { get { return mainBear; } set { mainBear = value; } }
            public Nacelle Nacel { get { return nacel; } set { nacel = value; } }
            public Power Powers { get { return power; } set { power = value; } }
            public Reactor React { get { return react; } set { react = value; } }
            public Tower Towers { get { return tower; } set { tower = value; } }
            public Transformer Transf { get { return transf; } set { transf = value; } }
            public Voltage Voltages { get { return voltage; } set { voltage = value; } }

            #endregion
        }

        #endregion

        #region Properties

        public ScadaHeader FileHeader { get { return fileHeader; } }

        public List<int> InclTrbn { get { return inclTrbn; } }

        public List<TurbineData> WindFarm { get { return windFarm; } }

        #endregion 
    }
}