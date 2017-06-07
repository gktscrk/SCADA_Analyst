using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;

using scada_analyst.Shared;

namespace scada_analyst
{
    public class ScadaData : BaseMetaData
    {
        #region Variables

        private ScadaHeader fileHeader = new ScadaHeader();

        // a list for including the asset IDs for all loaded turbines
        private List<int> inclTrbn = new List<int>(); 

        private List<TurbineData> windFarm = new List<TurbineData>();

        #endregion

        public ScadaData() { }

        public ScadaData(string filename, BackgroundWorker bgW)
        {
            if (!bgW.CancellationPending)
            {
                this.FileName = filename;

                LoadScada(bgW);
            }
        }

        public ScadaData(string[] filenames, BackgroundWorker bgW)
        {
            if (!bgW.CancellationPending)
            {
                LoadNSort(filenames, bgW);
            }
        }

        public void AppendFiles(string[] filenames, BackgroundWorker bgW)
        {
            if (!bgW.CancellationPending)
            {
                LoadNSort(filenames, bgW);
            }
        }

        private void LoadFiles(string[] filenames, BackgroundWorker bgW)
        {
            if (!bgW.CancellationPending)
            {
                for (int i = 0; i < filenames.Length; i++)
                {
                    this.FileName = filenames[i];

                    LoadScada(bgW, filenames.Length, i);
                }
            }
        }
        
        private void LoadNSort(string[] filenames, BackgroundWorker bgW)
        {
            LoadFiles(filenames, bgW);

            SortScada(bgW);
        }

        private void LoadScada(BackgroundWorker bgW, int numberOfFiles = 1, int i = 0)
        {
            if (!bgW.CancellationPending)
            {
                using (StreamReader sR = new StreamReader(FileName))
                {
                    try
                    {
                        int count = 0;
                        bool readHeader = false;

                        while (!sR.EndOfStream)
                        {
                            if (!bgW.CancellationPending)
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
                                    bgW.ReportProgress((int)
                                        ((double)100 / numberOfFiles * i +
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
        }

        private void SortScada(BackgroundWorker bgW)
        {
            if (!bgW.CancellationPending)
            {
                for (int i = 0; i < windFarm.Count; i++)
                {
                    windFarm[i].DataSorted = windFarm[i].Data.OrderBy(o => o.TimeStamp).ToList();
                }
            }
        }

        #region Support Classes

        public class TurbineData : BaseStructure
        {
            // this class represents a full wind turbine which includes a set of 
            // turbines which all have sets of data

            #region Variables

            private List<DateTime> inclDtTm = new List<DateTime>();

            private List<ScadaSample> data = new List<ScadaSample>();
            private List<ScadaSample> dataSorted = new List<ScadaSample>();

            #endregion

            public TurbineData() { }

            public TurbineData(string[] splits, ScadaHeader header)
            {
                Type = Types.TURBINE;

                data.Add(new ScadaSample(splits, header));

                inclDtTm.Add(Common.StringToDateTime(Common.GetSplits(splits[header.TimesCol], new char[] { ' ' })));
                
                if (UnitID == -1 && data.Count > 0)
                {
                    UnitID = data[0].AssetID;
                }
            }
                        
            public void AddData(string[] splits, ScadaHeader header)
            {
                DateTime thisTime = Common.StringToDateTime(Common.GetSplits(splits[header.TimesCol], new char[] { ' ' }));

                if (inclDtTm.Contains(thisTime))
                {
                    int index = data.FindIndex(x => x.TimeStamp == thisTime);

                    data[index].AddDataFields(splits, header);
                }
                else
                {
                    data.Add(new ScadaSample(splits, header));
                    
                    inclDtTm.Add(thisTime);
                }
            }

            #region Properties

            public List<DateTime> InclDtTm { get { return inclDtTm; } }

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
            private int assetCol = -1, sampleCol = -1, stationCol = -1, timeCol = -1;

            #endregion

            public ScadaHeader() { }

            public ScadaHeader(string header)
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
                            // actual power has a special case where one "endvalue" is 
                            // represented by "quality"

                            #region Grid File Variables
                            if (parts[1] == "actpower")
                            {
                                if (parts[2] == "mean") { Powers.Mean = i; }
                                else if (parts[2] == "stddev") { Powers.Stdv = i; }
                                else if (parts[2] == "max") { Powers.Maxm = i; }
                                else if (parts[2] == "min") { Powers.Minm = i; }
                                else if (parts[2] == "endvalue") { Powers.EndVal = i; }
                                else if (parts[2] == "quality") { Powers.QualEndVal = i; }
                            }
                            else if (parts[1] == "actregst")
                            {
                                if (parts[2] == "endvalue") { Powers.RgStEndVal = i; }
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
                                else if (parts[2] == "endvalue") { Powers.PowrFact.EndVal = i; }
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
                                if (parts[2] == "mean") { React.Powers.Mean = i; }
                                else if (parts[2] == "stddev") { React.Powers.Stdv = i; }
                                else if (parts[2] == "max") { React.Powers.Maxm = i; }
                                else if (parts[2] == "min") { React.Powers.Minm = i; }
                                else if (parts[2] == "endvalue") { React.Powers.EndVal = i; }
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

                react.Powers.Mean = GetVals(react.Powers.Mean, data, header.React.Powers.Mean);
                react.Powers.Stdv = GetVals(react.Powers.Stdv, data, header.React.Powers.Stdv);
                react.Powers.Maxm = GetVals(react.Powers.Maxm, data, header.React.Powers.Maxm);
                react.Powers.Minm = GetVals(react.Powers.Minm, data, header.React.Powers.Minm);
                react.Powers.EndVal = GetVals(react.Powers.EndVal, data, header.React.Powers.EndValCol);

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

                gearbox.Hs.Gens.Mean = GetVals(gearbox.Hs.Gens.Mean, data, header.gearbox.Hs.Gens.Mean);
                gearbox.Hs.Gens.Stdv = GetVals(gearbox.Hs.Gens.Stdv, data, header.gearbox.Hs.Gens.Stdv);
                gearbox.Hs.Gens.Maxm = GetVals(gearbox.Hs.Gens.Maxm, data, header.gearbox.Hs.Gens.Maxm);
                gearbox.Hs.Gens.Minm = GetVals(gearbox.Hs.Gens.Minm, data, header.gearbox.Hs.Gens.Minm);
                gearbox.Hs.Rots.Mean = GetVals(gearbox.Hs.Rots.Mean, data, header.gearbox.Hs.Rots.Mean);
                gearbox.Hs.Rots.Stdv = GetVals(gearbox.Hs.Rots.Stdv, data, header.gearbox.Hs.Rots.Stdv);
                gearbox.Hs.Rots.Maxm = GetVals(gearbox.Hs.Rots.Maxm, data, header.gearbox.Hs.Rots.Maxm);
                gearbox.Hs.Rots.Minm = GetVals(gearbox.Hs.Rots.Minm, data, header.gearbox.Hs.Rots.Minm);

                gearbox.Ims.Gens.Mean = GetVals(gearbox.Ims.Gens.Mean, data, header.gearbox.Ims.Gens.Mean);
                gearbox.Ims.Gens.Stdv = GetVals(gearbox.Ims.Gens.Stdv, data, header.gearbox.Ims.Gens.Stdv);
                gearbox.Ims.Gens.Maxm = GetVals(gearbox.Ims.Gens.Maxm, data, header.gearbox.Ims.Gens.Maxm);
                gearbox.Ims.Gens.Minm = GetVals(gearbox.Ims.Gens.Minm, data, header.gearbox.Ims.Gens.Minm);
                gearbox.Ims.Rots.Mean = GetVals(gearbox.Ims.Rots.Mean, data, header.gearbox.Ims.Rots.Mean);
                gearbox.Ims.Rots.Stdv = GetVals(gearbox.Ims.Rots.Stdv, data, header.gearbox.Ims.Rots.Stdv);
                gearbox.Ims.Rots.Maxm = GetVals(gearbox.Ims.Rots.Maxm, data, header.gearbox.Ims.Rots.Maxm);
                gearbox.Ims.Rots.Minm = GetVals(gearbox.Ims.Rots.Minm, data, header.gearbox.Ims.Rots.Minm);

                #endregion
                #region Turbine File

                curTime = Common.StringToDateTime(Common.GetSplits(data[header.CurTimeCol], new char[] { ' ' }));

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



                #endregion
            }

            private double GetVals(double value, string[] data, double index)
            {
                if (value == 0 || value == error)
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

                protected double endValue;
                protected double qualEndVal;
                protected double rgStEndVal;
                protected GridFrequency gridFreq = new GridFrequency();
                protected PowerFactor powrFact = new PowerFactor();

                protected int endValCol, qualEndValCol, rgStEndValCol;

                #endregion

                #region Support Classes            

                public class PowerFactor : Stats
                {
                    #region Variables

                    protected double endVal;
                    protected int endValCol;

                    #endregion

                    #region Properties

                    public double EndVal { get { return endVal; } set { endVal = value; } }
                    public int EndValCol { get { return endValCol; } set { endValCol = value; } }

                    #endregion
                }

                public class GridFrequency : Stats
                {
                    #region Variables

                    protected double endValue;
                    protected int endValCol;

                    #endregion

                    #region Properties

                    public double EndValue { get { return endValue; } set { endValue = value; } }
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

                #endregion
            }

            public class Reactor
            {
                #region Variables

                protected Power power = new Power();
                protected U u = new U();
                protected V v = new V();
                protected W w = new W();
                
                #endregion

                #region Support Classes

                public class Power : Stats
                {
                    #region Variables

                    protected double endVal;
                    protected int endValCol;

                    #endregion

                    #region Properties

                    public double EndVal { get { return endVal; } set { endVal = value; } }
                    public int EndValCol { get { return endValCol; } set { endValCol = value; } }

                    #endregion
                }

                public class U : Temperature { }
                public class V : Temperature { }
                public class W : Temperature { }

                #endregion

                #region Properties

                public Power Powers { get { return power; } set { power = value; } }
                public U Us { get { return u; } set { u = value; } }
                public V Vs { get { return v; } set { v = value; } }
                public W Ws { get { return w; } set { w = value; } }

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