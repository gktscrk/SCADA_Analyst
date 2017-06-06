using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

namespace scada_analyst
{
    public class ScadaData : BaseMetaData
    {
        #region Variables

        private ScadaHeader fileHeader = new ScadaHeader();
        
        private List<TurbineData> windFarm = new List<TurbineData>(); 

        #endregion

        public ScadaData(string fileName, BackgroundWorker bgW)
        {
            if (!bgW.CancellationPending)
            {
                this.FileName = fileName;

                LoadScada(bgW);
            }
        }

        private void LoadScada(BackgroundWorker bgW)
        {
            if (!bgW.CancellationPending)
            {
                using (StreamReader sR = new StreamReader(FileName))
                {
                    try
                    {
                        int count = 0;
                        bool readHeader = false;

                        windFarm = new List<TurbineData>();

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

                                    if (windFarm.Count < 1)
                                    {
                                        windFarm.Add(new TurbineData(splits, fileHeader));
                                    }
                                    else
                                    {
                                        bool foundTurbine = false;

                                        for (int i = 0; i < windFarm.Count; i++)
                                        {
                                            if (windFarm[i].UnitID == Convert.ToInt32(splits[fileHeader.AssetCol]))
                                            {
                                                windFarm[i].Data.Add(new ScadaSample(splits, fileHeader));

                                                foundTurbine = true; break;
                                            }
                                        }

                                        if (!foundTurbine) { windFarm.Add(new TurbineData(splits, fileHeader)); }
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

        #region Support Classes

        public class TurbineData : BaseStructure
        {
            // this class represents a full wind turbine which includes a set of turbines which all have sets
            // of data

            #region Variables
            
            private List<ScadaSample> data = new List<ScadaSample>();

            #endregion

            public TurbineData() { }

            public TurbineData(string[] splits, ScadaHeader header)
            {
                Type = Types.TURBINE;

                data.Add(new ScadaSample(splits, header));

                if (UnitID == -1 && data.Count > 0)
                {
                    UnitID = data[0].AssetID;
                }
            }

            #region Properties

            public List<ScadaSample> Data { get { return data; } }

            #endregion
        }

        public class ScadaHeader : ScadaSample
        {
            // this class inherits all of the ScadaSample properties but I will treat this
            // as a separate instance where everything refers to the column index and not
            // the actual data itself

            // this will be initialised to begin with, and after that will be maintained for
            // the duration of loading the file            

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

                            #region Temperature File
                            if (parts[1] == "gen1u1tm")
                            {
                                if (parts[2] == "mean") { Genny.G1u1.MeanCol = i; }
                                else if (parts[2] == "stddev") { Genny.G1u1.StdvCol = i; }
                                else if (parts[2] == "max") { Genny.G1u1.MaxmCol = i; }
                                else if (parts[2] == "min") { Genny.G1u1.MinmCol = i; }
                            }
                            else if (parts[1] == "gen1v1tm")
                            {
                                if (parts[2] == "mean") { Genny.G1v1.MeanCol = i; }
                                else if (parts[2] == "stddev") { Genny.G1v1.StdvCol = i; }
                                else if (parts[2] == "max") { Genny.G1v1.MaxmCol = i; }
                                else if (parts[2] == "min") { Genny.G1v1.MinmCol = i; }
                            }
                            else if (parts[1] == "gen1w1tm")
                            {
                                if (parts[2] == "mean") { Genny.G1w1.MeanCol = i; }
                                else if (parts[2] == "stddev") { Genny.G1w1.StdvCol = i; }
                                else if (parts[2] == "max") { Genny.G1w1.MaxmCol = i; }
                                else if (parts[2] == "min") { Genny.G1w1.MinmCol = i; }
                            }
                            else if (parts[1] == "gen2u1tm")
                            {
                                if (parts[2] == "mean") { Genny.G2u1.MeanCol = i; }
                                else if (parts[2] == "stddev") { Genny.G2u1.StdvCol = i; }
                                else if (parts[2] == "max") { Genny.G2u1.MaxmCol = i; }
                                else if (parts[2] == "min") { Genny.G2u1.MinmCol = i; }
                            }
                            else if (parts[1] == "gen2v1tm")
                            {
                                if (parts[2] == "mean") { Genny.G2v1.MeanCol = i; }
                                else if (parts[2] == "stddev") { Genny.G2v1.StdvCol = i; }
                                else if (parts[2] == "max") { Genny.G2v1.MaxmCol = i; }
                                else if (parts[2] == "min") { Genny.G2v1.MinmCol = i; }
                            }
                            else if (parts[1] == "gen2w1tm")
                            {
                                if (parts[2] == "mean") { Genny.G2w1.MeanCol = i; }
                                else if (parts[2] == "stddev") { Genny.G2w1.StdvCol = i; }
                                else if (parts[2] == "max") { Genny.G2w1.MaxmCol = i; }
                                else if (parts[2] == "min") { Genny.G2w1.MinmCol = i; }
                            }
                            else if (parts[1] == "genbegtm")
                            {
                                if (parts[2] == "mean") { Genny.bearingG.MeanCol = i; }
                                else if (parts[2] == "stddev") { Genny.bearingG.StdvCol = i; }
                                else if (parts[2] == "max") { Genny.bearingG.MaxmCol = i; }
                                else if (parts[2] == "min") { Genny.bearingG.MinmCol = i; }
                            }
                            else if (parts[1] == "genbertm")
                            {
                                if (parts[2] == "mean") { Genny.bearingR.MeanCol = i; }
                                else if (parts[2] == "stddev") { Genny.bearingR.StdvCol = i; }
                                else if (parts[2] == "max") { Genny.bearingR.MaxmCol = i; }
                                else if (parts[2] == "min") { Genny.bearingR.MinmCol = i; }
                            }
                            else if (parts[1] == "geoiltmp")
                            {
                                if (parts[2] == "mean") { Gearbox.Oils.MeanCol = i; }
                                else if (parts[2] == "stddev") { Gearbox.Oils.StdvCol = i; }
                                else if (parts[2] == "max") { Gearbox.Oils.MaxmCol = i; }
                                else if (parts[2] == "min") { Gearbox.Oils.MinmCol = i; }
                            }
                            else if (parts[1] == "gfilb1tm")
                            {
                                if (parts[2] == "mean") { GridFilt.B1s.MeanCol = i; }
                                else if (parts[2] == "stddev") { GridFilt.B1s.StdvCol = i; }
                                else if (parts[2] == "max") { GridFilt.B1s.MaxmCol = i; }
                                else if (parts[2] == "min") { GridFilt.B1s.MinmCol = i; }
                            }
                            else if (parts[1] == "gfilb2tm")
                            {
                                if (parts[2] == "mean") { GridFilt.B2s.MeanCol = i; }
                                else if (parts[2] == "stddev") { GridFilt.B2s.StdvCol = i; }
                                else if (parts[2] == "max") { GridFilt.B2s.MaxmCol = i; }
                                else if (parts[2] == "min") { GridFilt.B2s.MinmCol = i; }
                            }
                            else if (parts[1] == "gfilb3tm")
                            {
                                if (parts[2] == "mean") { GridFilt.B3s.MeanCol = i; }
                                else if (parts[2] == "stddev") { GridFilt.B3s.StdvCol = i; }
                                else if (parts[2] == "max") { GridFilt.B3s.MaxmCol = i; }
                                else if (parts[2] == "min") { GridFilt.B3s.MinmCol = i; }
                            }
                            else if (parts[1] == "hsgentmp")
                            {
                                if (parts[2] == "mean") { Gearbox.Hs.Gens.MeanCol = i; }
                                else if (parts[2] == "stddev") { Gearbox.Hs.Gens.StdvCol = i; }
                                else if (parts[2] == "max") { Gearbox.Hs.Gens.MaxmCol = i; }
                                else if (parts[2] == "min") { Gearbox.Hs.Gens.MinmCol = i; }
                            }
                            else if (parts[1] == "hsrottmp")
                            {
                                if (parts[2] == "mean") { Gearbox.Hs.Rots.MeanCol = i; }
                                else if (parts[2] == "stddev") { Gearbox.Hs.Rots.StdvCol = i; }
                                else if (parts[2] == "max") { Gearbox.Hs.Rots.MaxmCol = i; }
                                else if (parts[2] == "min") { Gearbox.Hs.Rots.MinmCol = i; }
                            }
                            else if (parts[1] == "hubbrdtm")
                            {
                                if (parts[2] == "mean") { Hubs.Boards.MeanCol = i; }
                                else if (parts[2] == "stddev") { Hubs.Boards.StdvCol = i; }
                                else if (parts[2] == "max") { Hubs.Boards.MaxmCol = i; }
                                else if (parts[2] == "min") { Hubs.Boards.MinmCol = i; }
                            }
                            else if (parts[1] == "hubtemp")
                            {
                                if (parts[2] == "mean") { Hubs.Internals.MeanCol = i; }
                                else if (parts[2] == "stddev") { Hubs.Internals.StdvCol = i; }
                                else if (parts[2] == "max") { Hubs.Internals.MaxmCol = i; }
                                else if (parts[2] == "min") { Hubs.Internals.MinmCol = i; }
                            }
                            else if (parts[1] == "hubtref1")
                            {
                                if (parts[2] == "mean") { Hubs.Ref1s.MeanCol = i; }
                                else if (parts[2] == "stddev") { Hubs.Ref1s.StdvCol = i; }
                                else if (parts[2] == "max") { Hubs.Ref1s.MaxmCol = i; }
                                else if (parts[2] == "min") { Hubs.Ref1s.MinmCol = i; }
                            }
                            else if (parts[1] == "hubtref2")
                            {
                                if (parts[2] == "mean") { Hubs.Ref2s.MeanCol = i; }
                                else if (parts[2] == "stddev") { Hubs.Ref2s.StdvCol = i; }
                                else if (parts[2] == "max") { Hubs.Ref2s.MaxmCol = i; }
                                else if (parts[2] == "min") { Hubs.Ref2s.MinmCol = i; }
                            }
                            else if (parts[1] == "hydoiltm")
                            {
                                if (parts[2] == "mean") { HydOils.Temp.MeanCol = i; }
                                else if (parts[2] == "stddev") { HydOils.Temp.StdvCol = i; }
                                else if (parts[2] == "max") { HydOils.Temp.MaxmCol = i; }
                                else if (parts[2] == "min") { HydOils.Temp.MinmCol = i; }
                            }
                            else if (parts[1] == "imsgentm")
                            {
                                if (parts[2] == "mean") { Gearbox.Ims.Gens.MeanCol = i; }
                                else if (parts[2] == "stddev") { Gearbox.Ims.Gens.StdvCol = i; }
                                else if (parts[2] == "max") { Gearbox.Ims.Gens.MaxmCol = i; }
                                else if (parts[2] == "min") { Gearbox.Ims.Gens.MinmCol = i; }
                            }
                            else if (parts[1] == "imsrottm")
                            {
                                if (parts[2] == "mean") { Gearbox.Ims.Rots.MeanCol = i; }
                                else if (parts[2] == "stddev") { Gearbox.Ims.Rots.StdvCol = i; }
                                else if (parts[2] == "max") { Gearbox.Ims.Rots.MaxmCol = i; }
                                else if (parts[2] == "min") { Gearbox.Ims.Rots.MinmCol = i; }
                            }
                            #endregion
                            #region Grid File Variables
                            else if (parts[1] == "actpower")
                            {
                                if (parts[2] == "mean") { Powers.MeanCol = i; }
                                else if (parts[2] == "stddev") { Powers.StdvCol = i; }
                                else if (parts[2] == "max") { Powers.MaxmCol = i; }
                                else if (parts[2] == "min") { Powers.MinmCol = i; }
                                else if (parts[2] == "endvalue") { Powers.EndValCol = i; }
                                else if (parts[2] == "quality") { Powers.QualEndValCol = i; }
                            }
                            else if (parts[1] == "actregst")
                            {
                                if (parts[2] == "endvalue") { Powers.RgStEndValCol = i; }
                            }
                            else if (parts[1] == "ampphr")
                            {
                                if (parts[2] == "mean") { Currents.phR.MeanCol = i; }
                                else if (parts[2] == "stddev") { Currents.phR.StdvCol = i; }
                                else if (parts[2] == "max") { Currents.phR.MaxmCol = i; }
                                else if (parts[2] == "min") { Currents.phR.MinmCol = i; }
                            }
                            else if (parts[1] == "ampphs")
                            {
                                if (parts[2] == "mean") { Currents.phS.MeanCol = i; }
                                else if (parts[2] == "stddev") { Currents.phS.StdvCol = i; }
                                else if (parts[2] == "max") { Currents.phS.MaxmCol = i; }
                                else if (parts[2] == "min") { Currents.phS.MinmCol = i; }
                            }
                            else if (parts[1] == "amppht")
                            {
                                if (parts[2] == "mean") { Currents.phT.MeanCol = i; }
                                else if (parts[2] == "stddev") { Currents.phT.StdvCol = i; }
                                else if (parts[2] == "max") { Currents.phT.MaxmCol = i; }
                                else if (parts[2] == "min") { Currents.phT.MinmCol = i; }
                            }
                            else if (parts[1] == "cosphi")
                            {
                                if (parts[2] == "mean") { Powers.PowrFact.MeanCol = i; }
                                else if (parts[2] == "stddev") { Powers.PowrFact.StdvCol = i; }
                                else if (parts[2] == "max") { Powers.PowrFact.MaxmCol = i; }
                                else if (parts[2] == "min") { Powers.PowrFact.MinmCol = i; }
                                else if (parts[2] == "endvalue") { Powers.PowrFact.EndValCol = i; }
                            }
                            else if (parts[1] == "gridfreq")
                            {
                                if (parts[2] == "mean") { Powers.GridFreq.MeanCol = i; }
                                else if (parts[2] == "stddev") { Powers.GridFreq.StdvCol = i; }
                                else if (parts[2] == "max") { Powers.GridFreq.MaxmCol = i; }
                                else if (parts[2] == "min") { Powers.GridFreq.MinmCol = i; }
                            }
                            else if (parts[1] == "reactpwr")
                            {
                                if (parts[2] == "mean") { React.Powers.MeanCol = i; }
                                else if (parts[2] == "stddev") { React.Powers.StdvCol = i; }
                                else if (parts[2] == "max") { React.Powers.MaxmCol = i; }
                                else if (parts[2] == "min") { React.Powers.MinmCol = i; }
                                else if (parts[2] == "endvalue") { React.Powers.EndValCol = i; }
                            }
                            else if (parts[1] == "voltphr")
                            {
                                if (parts[2] == "mean") { Voltag.phR.MeanCol = i; }
                                else if (parts[2] == "stddev") { Voltag.phR.StdvCol = i; }
                                else if (parts[2] == "max") { Voltag.phR.MaxmCol = i; }
                                else if (parts[2] == "min") { Voltag.phR.MinmCol = i; }
                            }
                            else if (parts[1] == "voltphs")
                            {
                                if (parts[2] == "mean") { Voltag.phS.MeanCol = i; }
                                else if (parts[2] == "stddev") { Voltag.phS.StdvCol = i; }
                                else if (parts[2] == "max") { Voltag.phS.MaxmCol = i; }
                                else if (parts[2] == "min") { Voltag.phS.MinmCol = i; }
                            }
                            else if (parts[1] == "voltpht")
                            {
                                if (parts[2] == "mean") { Voltag.phT.MeanCol = i; }
                                else if (parts[2] == "stddev") { Voltag.phT.StdvCol = i; }
                                else if (parts[2] == "max") { Voltag.phT.MaxmCol = i; }
                                else if (parts[2] == "min") { Voltag.phT.MinmCol = i; }
                            }
                            #endregion
                            #region Turbine File
                            else if (parts[1] == "acwindsp")
                            {
                                if (parts[2] == "mean") { AnemoM.ActWinds.MeanCol = i; }
                                else if (parts[2] == "stddev") { AnemoM.ActWinds.StdvCol = i; }
                                else if (parts[2] == "max") { AnemoM.ActWinds.MaxmCol = i; }
                                else if (parts[2] == "min") { AnemoM.ActWinds.MinmCol = i; }
                            }
                            else if (parts[1] == "genrpm")
                            {
                                if (parts[2] == "mean") { Genny.Rpms.MeanCol = i; }
                                else if (parts[2] == "stddev") { Genny.Rpms.StdvCol = i; }
                                else if (parts[2] == "max") { Genny.Rpms.MaxmCol = i; }
                                else if (parts[2] == "min") { Genny.Rpms.MinmCol = i; }
                            }
                            else if (parts[1] == "prianemo")
                            {
                                if (parts[2] == "mean") { AnemoM.PriAnemo.MeanCol = i; }
                                else if (parts[2] == "stddev") { AnemoM.PriAnemo.StdvCol = i; }
                                else if (parts[2] == "max") { AnemoM.PriAnemo.MaxmCol = i; }
                                else if (parts[2] == "min") { AnemoM.PriAnemo.MinmCol = i; }
                            }
                            else if (parts[1] == "prwindsp")
                            {
                                if (parts[2] == "mean") { AnemoM.PriWinds.MeanCol = i; }
                                else if (parts[2] == "stddev") { AnemoM.PriWinds.StdvCol = i; }
                                else if (parts[2] == "max") { AnemoM.PriWinds.MaxmCol = i; }
                                else if (parts[2] == "min") { AnemoM.PriWinds.MinmCol = i; }
                            }
                            else if (parts[1] == "secanemo")
                            {
                                if (parts[2] == "mean") { AnemoM.SecAnemo.MeanCol = i; }
                                else if (parts[2] == "stddev") { AnemoM.SecAnemo.StdvCol = i; }
                                else if (parts[2] == "max") { AnemoM.SecAnemo.MaxmCol = i; }
                                else if (parts[2] == "min") { AnemoM.SecAnemo.MinmCol = i; }
                            }
                            else if (parts[1] == "sewindsp")
                            {
                                if (parts[2] == "mean") { AnemoM.SecWinds.MeanCol = i; }
                                else if (parts[2] == "stddev") { AnemoM.SecWinds.StdvCol = i; }
                                else if (parts[2] == "max") { AnemoM.SecWinds.MaxmCol = i; }
                                else if (parts[2] == "min") { AnemoM.SecWinds.MinmCol = i; }
                            }
                            else if (parts[1] == "tetanemo")
                            {
                                if (parts[2] == "mean") { AnemoM.TerAnemo.MeanCol = i; }
                                else if (parts[2] == "stddev") { AnemoM.TerAnemo.StdvCol = i; }
                                else if (parts[2] == "max") { AnemoM.TerAnemo.MaxmCol = i; }
                                else if (parts[2] == "min") { AnemoM.TerAnemo.MinmCol = i; }
                            }
                            #endregion
                        }
                    }
                }
            }
        }

        public class ScadaSample : BaseSampleData
        {
            // this class should be usable as the representation of a turbine, a set of which 
            // is grouped as a wind farm

            #region Variables
            
            private int error = -9999;

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
            private Voltage voltag = new Voltage();

            #endregion

            public ScadaSample() { }

            public ScadaSample(string[] data, ScadaHeader header)
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

                #region Grid File

                power.Mean = GetVals(power.Mean, data, header.Powers.MeanCol);
                power.Stdv = GetVals(power.Stdv, data, header.Powers.StdvCol);
                power.Maxm = GetVals(power.Maxm, data, header.Powers.MaxmCol);
                power.Minm = GetVals(power.Minm, data, header.Powers.MinmCol);
                power.EndVal = GetVals(power.EndVal, data, header.Powers.EndValCol);
                power.QualEndVal = GetVals(power.QualEndVal, data, header.Powers.QualEndValCol);
                power.RgStEndVal = GetVals(power.RgStEndVal, data, header.Powers.RgStEndValCol);

                power.GridFreq.Mean = GetVals(power.GridFreq.Mean, data, header.Powers.GridFreq.MeanCol);
                power.GridFreq.Stdv = GetVals(power.GridFreq.Stdv, data, header.Powers.GridFreq.StdvCol);
                power.GridFreq.Maxm = GetVals(power.GridFreq.Maxm, data, header.Powers.GridFreq.MaxmCol);
                power.GridFreq.Minm = GetVals(power.GridFreq.Minm, data, header.Powers.GridFreq.MinmCol);

                power.PowrFact.Mean = GetVals(power.PowrFact.Mean, data, header.Powers.PowrFact.MeanCol);
                power.PowrFact.Stdv = GetVals(power.PowrFact.Stdv, data, header.Powers.PowrFact.StdvCol);
                power.PowrFact.Maxm = GetVals(power.PowrFact.Maxm, data, header.Powers.PowrFact.MaxmCol);
                power.PowrFact.Minm = GetVals(power.PowrFact.Minm, data, header.Powers.PowrFact.MinmCol);
                power.PowrFact.EndVal = GetVals(power.PowrFact.EndVal, data, header.Powers.PowrFact.EndValCol);

                react.Powers.Mean = GetVals(react.Powers.Mean, data, header.React.Powers.MeanCol); 
                react.Powers.Stdv = GetVals(react.Powers.Stdv, data, header.React.Powers.StdvCol); 
                react.Powers.Maxm = GetVals(react.Powers.Maxm, data, header.React.Powers.MaxmCol); 
                react.Powers.Minm = GetVals(react.Powers.Minm, data, header.React.Powers.MinmCol); 
                react.Powers.EndVal = GetVals(react.Powers.EndVal, data, header.React.Powers.EndValCol);

                #endregion
                #region Temperature File

                gearbox.Hs.Gens.Mean = GetVals(gearbox.Hs.Gens.Mean, data, header.gearbox.Hs.Gens.MeanCol);                                                                                                                                                                                                                          
                gearbox.Hs.Gens.Stdv = GetVals(gearbox.Hs.Gens.Stdv, data, header.gearbox.Hs.Gens.StdvCol);
                gearbox.Hs.Gens.Maxm = GetVals(gearbox.Hs.Gens.Maxm, data, header.gearbox.Hs.Gens.MaxmCol);
                gearbox.Hs.Gens.Minm = GetVals(gearbox.Hs.Gens.Minm, data, header.gearbox.Hs.Gens.MinmCol);
                gearbox.Hs.Rots.Mean = GetVals(gearbox.Hs.Rots.Mean, data, header.gearbox.Hs.Rots.MeanCol);
                gearbox.Hs.Rots.Stdv = GetVals(gearbox.Hs.Rots.Stdv, data, header.gearbox.Hs.Rots.StdvCol);
                gearbox.Hs.Rots.Maxm = GetVals(gearbox.Hs.Rots.Maxm, data, header.gearbox.Hs.Rots.MaxmCol);
                gearbox.Hs.Rots.Minm = GetVals(gearbox.Hs.Rots.Minm, data, header.gearbox.Hs.Rots.MinmCol);

                gearbox.Ims.Gens.Mean = GetVals(gearbox.Ims.Gens.Mean, data, header.gearbox.Ims.Gens.MeanCol);
                gearbox.Ims.Gens.Stdv = GetVals(gearbox.Ims.Gens.Stdv, data, header.gearbox.Ims.Gens.StdvCol);
                gearbox.Ims.Gens.Maxm = GetVals(gearbox.Ims.Gens.Maxm, data, header.gearbox.Ims.Gens.MaxmCol);
                gearbox.Ims.Gens.Minm = GetVals(gearbox.Ims.Gens.Minm, data, header.gearbox.Ims.Gens.MinmCol);
                gearbox.Ims.Rots.Mean = GetVals(gearbox.Ims.Rots.Mean, data, header.gearbox.Ims.Rots.MeanCol);
                gearbox.Ims.Rots.Stdv = GetVals(gearbox.Ims.Rots.Stdv, data, header.gearbox.Ims.Rots.StdvCol);
                gearbox.Ims.Rots.Maxm = GetVals(gearbox.Ims.Rots.Maxm, data, header.gearbox.Ims.Rots.MaxmCol);
                gearbox.Ims.Rots.Minm = GetVals(gearbox.Ims.Rots.Minm, data, header.gearbox.Ims.Rots.MinmCol);

                #endregion
                #region Turbine File

                anemoM.ActWinds.Mean = GetVals(anemoM.ActWinds.Mean, data, header.AnemoM.ActWinds.MeanCol);
                anemoM.ActWinds.Stdv = GetVals(anemoM.ActWinds.Stdv, data, header.AnemoM.ActWinds.StdvCol);
                anemoM.ActWinds.Maxm = GetVals(anemoM.ActWinds.Maxm, data, header.AnemoM.ActWinds.MaxmCol);
                anemoM.ActWinds.Minm = GetVals(anemoM.ActWinds.Minm, data, header.AnemoM.ActWinds.MinmCol);
                                                                   
                anemoM.PriAnemo.Mean = GetVals(anemoM.PriAnemo.Mean, data, header.AnemoM.PriAnemo.MeanCol);
                anemoM.PriAnemo.Stdv = GetVals(anemoM.PriAnemo.Stdv, data, header.AnemoM.PriAnemo.StdvCol);
                anemoM.PriAnemo.Maxm = GetVals(anemoM.PriAnemo.Maxm, data, header.AnemoM.PriAnemo.MaxmCol);
                anemoM.PriAnemo.Minm = GetVals(anemoM.PriAnemo.Minm, data, header.AnemoM.PriAnemo.MinmCol);
                                                                   
                anemoM.PriWinds.Mean = GetVals(anemoM.PriWinds.Mean, data, header.AnemoM.PriWinds.MeanCol);
                anemoM.PriWinds.Stdv = GetVals(anemoM.PriWinds.Stdv, data, header.AnemoM.PriWinds.StdvCol);
                anemoM.PriWinds.Maxm = GetVals(anemoM.PriWinds.Maxm, data, header.AnemoM.PriWinds.MaxmCol);
                anemoM.PriWinds.Minm = GetVals(anemoM.PriWinds.Minm, data, header.AnemoM.PriWinds.MinmCol);
                                                                   
                anemoM.SecAnemo.Mean = GetVals(anemoM.SecAnemo.Mean, data, header.AnemoM.SecAnemo.MeanCol);
                anemoM.SecAnemo.Stdv = GetVals(anemoM.SecAnemo.Stdv, data, header.AnemoM.SecAnemo.StdvCol);
                anemoM.SecAnemo.Maxm = GetVals(anemoM.SecAnemo.Maxm, data, header.AnemoM.SecAnemo.MaxmCol);
                anemoM.SecAnemo.Minm = GetVals(anemoM.SecAnemo.Minm, data, header.AnemoM.SecAnemo.MinmCol);
                                                                                                    
                anemoM.SecWinds.Mean = GetVals(anemoM.SecWinds.Mean, data, header.AnemoM.SecWinds.MeanCol);
                anemoM.SecWinds.Stdv = GetVals(anemoM.SecWinds.Stdv, data, header.AnemoM.SecWinds.StdvCol);
                anemoM.SecWinds.Maxm = GetVals(anemoM.SecWinds.Maxm, data, header.AnemoM.SecWinds.MaxmCol);
                anemoM.SecWinds.Minm = GetVals(anemoM.SecWinds.Minm, data, header.AnemoM.SecWinds.MinmCol);
                                                                                                
                anemoM.TerAnemo.Mean = GetVals(anemoM.TerAnemo.Mean, data, header.AnemoM.TerAnemo.MeanCol);
                anemoM.TerAnemo.Stdv = GetVals(anemoM.TerAnemo.Stdv, data, header.AnemoM.TerAnemo.StdvCol);
                anemoM.TerAnemo.Maxm = GetVals(anemoM.TerAnemo.Maxm, data, header.AnemoM.TerAnemo.MaxmCol);
                anemoM.TerAnemo.Minm = GetVals(anemoM.TerAnemo.Minm, data, header.AnemoM.TerAnemo.MinmCol);

                genny.Rpms.Mean = GetVals(genny.Rpms.Mean, data, header.Genny.Rpms.MeanCol);
                genny.Rpms.Stdv = GetVals(genny.Rpms.Stdv, data, header.Genny.Rpms.StdvCol);
                genny.Rpms.Maxm = GetVals(genny.Rpms.Maxm, data, header.Genny.Rpms.MaxmCol);
                genny.Rpms.Minm = GetVals(genny.Rpms.Minm, data, header.Genny.Rpms.MinmCol);

                #endregion
            }

            private double GetVals(double value, string[] data, int index)
            {
                if (value == 0 || value == error)
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
                return index != -1 && Common.CanConvert<double>(data[index]) ? Convert.ToDouble(data[index]) : error;
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
            public Voltage Voltag { get { return voltag; } set { voltag = value; } }

            #endregion
        }

        #endregion

        #region Properties

        public ScadaHeader FileHeader { get { return fileHeader; } }

        public List<TurbineData> WindFarm { get { return windFarm; } }

        #endregion 
    }
}