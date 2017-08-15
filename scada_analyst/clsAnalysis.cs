using System;
using System.Collections.Generic;
using System.Linq;

using scada_analyst.Shared;
using System.Data;

namespace scada_analyst
{
    public class Analysis : ObservableObject
    {
        #region Variables

        private double _cutIn = 4, _cutOut = 25, _powerLim = 0, _ratedPwr = 2300; // ratedPwr always in kW !!!

        private TimeSpan _workHrsMorning = new TimeSpan(7, 0, 0);
        private TimeSpan _workHrsEvening = new TimeSpan(20, 0, 0);

        private TimeSpan _duratFilter = new TimeSpan(0, 10, 0);

        // this is to allow changing the property of the timestep in the loaded scada data at some point
        private TimeSpan _scadaSeprtr = new TimeSpan(0, 10, 0);

        private ScadaData.TurbineData _fleetMeans = new ScadaData.TurbineData();

        private ScadaData.ScadaSample.GearBox _gbox = new ScadaData.ScadaSample.GearBox();
        private ScadaData.ScadaSample.Generator _genr = new ScadaData.ScadaSample.Generator();
        private ScadaData.ScadaSample.MainBearing _mbrg = new ScadaData.ScadaSample.MainBearing();

        private List<EventData> _allWtrEvts = new List<EventData>();
        private List<EventData> _loSpEvents = new List<EventData>();
        private List<EventData> _hiSpEvents = new List<EventData>();
        private List<EventData> _allPwrEvts = new List<EventData>();
        private List<EventData> _noPwEvents = new List<EventData>();
        private List<EventData> _hiPwEvents = new List<EventData>();

        private List<ScadaData.ScadaSample> _eventScadaOnly = new List<ScadaData.ScadaSample>();
        private List<ScadaData.ScadaSample> _weekBeforeInfo = new List<ScadaData.ScadaSample>();
        private List<ScadaData.ScadaSample> _fullHistory = new List<ScadaData.ScadaSample>();
        private List<ScadaData.ScadaSample> _avgEventScadaOnly = new List<ScadaData.ScadaSample>();
        private List<ScadaData.ScadaSample> _avgWeekBeforeInfo = new List<ScadaData.ScadaSample>();
        private List<ScadaData.ScadaSample> _avgFullHistory = new List<ScadaData.ScadaSample>();

        private List<EventData> _thresEvnts = new List<EventData>();
        private List<EventData> _rChngEvnts = new List<EventData>();

        private List<Structure> _assetList = new List<Structure>();
        private List<Distances> _intervals = new List<Distances>();

        private List<AnalyticLimit> _thresholds;
        private List<AnalyticLimit> _rateChange;

        #endregion

        #region Constructor

        public Analysis()
        {
            _thresholds = CreateThresholdLimits();
            _rateChange = CreateRateChangeLimits();
        }

        #endregion 

        #region Reset Events

        public void ResetEventList()
        {
            NoPwEvents.Clear(); RtPwEvents.Clear();

            //this method resets the eventlist in case the user so wishes
            for (int i = 0; i < AllPwrEvts.Count; i++)
            {
                if (AllPwrEvts[i].PwrProd == scada_analyst.EventData.PwrProdType.NO_PWR)
                {
                    NoPwEvents.Add(AllPwrEvts[i]);
                }
                else if (AllPwrEvts[i].PwrProd == scada_analyst.EventData.PwrProdType.HI_PWR)
                {
                    RtPwEvents.Add(AllPwrEvts[i]);
                }
            }
        }

        #endregion

        #region Event Finding (Basic Step)

        /// <summary>
        /// Overall method for finding events: references separate functions that call the required
        /// events for finding times with no power, times with high power, and times with wind speeds
        /// over and below the cut-in and cut-out speeds of the turbine.
        /// </summary>
        /// <param name="progress"></param>
        public void FindEvents(ScadaData scadaFile, MeteoData meteoFile, IProgress<int> progress)
        {
            // all of the find events methods follow a similar methodology
            //
            // firstly the full set of applicable data is taken to investigate
            // the criteria these are tested against are defined based on wind speed or power output
            // and a certain number of if-conditions need to be passed for the testing to take place
            //
            // namely whether the dataset is continuous in time, whether it doesn't end with the file,
            // and whether all of the tested samples meet the same conditions

            try
            {
                FindNoPowerEvents(scadaFile, progress);
                FindRatedPowerEvents(scadaFile, progress);
                FindWeatherFromMeteo(meteoFile, progress);
                FindWeatherFromScada(scadaFile, progress);
            }
            catch
            {
                throw;
            }
        }

        public List<ScadaData.ScadaSample> GetSpecEventDetails(ScadaData scadaFile, int targetAsset,
            DateTime _startTime, DateTime _endTime)
        {
            List<ScadaData.ScadaSample> thisEvent = new List<ScadaData.ScadaSample>();

            for (int i = 0; i < scadaFile.WindFarm.Count; i++)
            {
                if (scadaFile.WindFarm[i].UnitID == targetAsset)
                {
                    for (int j = 0; j < scadaFile.WindFarm[i].DataSorted.Count; j++)
                    {
                        if (scadaFile.WindFarm[i].DataSorted[j].TimeStamp >= _startTime &&
                            scadaFile.WindFarm[i].DataSorted[j].TimeStamp <= _endTime)
                        {
                            thisEvent.Add(scadaFile.WindFarm[i].DataSorted[j]);
                        }
                    }
                }
            }

            return thisEvent;
        }

        private void FindNoPowerEvents(ScadaData scadaFile, IProgress<int> progress)
        {
            // this method investigates all loaded scada files for low power (defined as below 0)
            // times in order to determine when the turbine may have been inactive --
            // which will be carried out in a different method later on

            // purpose of this is to find all suitable events

            try
            {
                if (scadaFile.WindFarm != null)
                {
                    int count = 0;

                    for (int i = 0; i < scadaFile.WindFarm.Count; i++)
                    {
                        for (int j = 0; j < scadaFile.WindFarm[i].DataSorted.Count; j++)
                        {
                            if (scadaFile.WindFarm[i].DataSorted[j].Power.Mean < PowerLim &&
                                scadaFile.WindFarm[i].DataSorted[j].Power.Mean != double.NaN)
                            {
                                List<ScadaData.ScadaSample> thisEvent = new List<ScadaData.ScadaSample>();
                                thisEvent.Add(scadaFile.WindFarm[i].DataSorted[j]);

                                for (int k = j + 1; k < scadaFile.WindFarm[i].DataSorted.Count; k++)
                                {
                                    if (scadaFile.WindFarm[i].DataSorted[k].SampleSeparation > ScadaSeprtr) { j = k - 1; break; }

                                    if (scadaFile.WindFarm[i].DataSorted[k].Power.Mean > PowerLim) { j = k - 1; break; }
                                    else if (k == scadaFile.WindFarm[i].DataSorted.Count - 1) { j = k; }

                                    thisEvent.Add(scadaFile.WindFarm[i].DataSorted[k]);
                                }

                                _noPwEvents.Add(new EventData(thisEvent, scada_analyst.EventData.PwrProdType.NO_PWR));
                                _allPwrEvts.Add(_noPwEvents[_noPwEvents.Count - 1]);
                            }

                            count++;

                            if (count % 1000 == 0)
                            {
                                if (progress != null)
                                {
                                    progress.Report((int)(((double)i / scadaFile.WindFarm.Count +
                                        (double)j / scadaFile.WindFarm[i].DataSorted.Count / scadaFile.WindFarm.Count) * 100));
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                throw;
            }
        }

        private void FindRatedPowerEvents(ScadaData scadaFile, IProgress<int> progress)
        {
            // this method sees when and how often the turbine was operating
            // at rated power in the time that we are inputting

            try
            {
                if (scadaFile.WindFarm != null)
                {
                    int count = 0;

                    for (int i = 0; i < scadaFile.WindFarm.Count; i++)
                    {
                        for (int j = 0; j < scadaFile.WindFarm[i].DataSorted.Count; j++)
                        {
                            if (scadaFile.WindFarm[i].DataSorted[j].Power.Mean >= RatedPwr &&
                                scadaFile.WindFarm[i].DataSorted[j].Power.Mean != -9999)
                            {
                                List<ScadaData.ScadaSample> thisEvent = new List<ScadaData.ScadaSample>();
                                thisEvent.Add(scadaFile.WindFarm[i].DataSorted[j]);

                                for (int k = j + 1; k < scadaFile.WindFarm[i].DataSorted.Count; k++)
                                {
                                    if (scadaFile.WindFarm[i].DataSorted[k].SampleSeparation > ScadaSeprtr) { j = k - 1; break; }

                                    if (scadaFile.WindFarm[i].DataSorted[k].Power.Mean < RatedPwr) { j = k - 1; break; }
                                    else if (k == scadaFile.WindFarm[i].DataSorted.Count - 1) { j = k; }

                                    thisEvent.Add(scadaFile.WindFarm[i].DataSorted[k]);
                                }

                                _hiPwEvents.Add(new EventData(thisEvent, scada_analyst.EventData.PwrProdType.HI_PWR));
                                _allPwrEvts.Add(_noPwEvents[_noPwEvents.Count - 1]);
                            }

                            count++;

                            if (count % 1000 == 0)
                            {
                                if (progress != null)
                                {
                                    progress.Report((int)(((double)i / scadaFile.WindFarm.Count +
                                        (double)j / scadaFile.WindFarm[i].DataSorted.Count / scadaFile.WindFarm.Count) * 100));
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                throw;
            }
        }

        private void FindWeatherFromMeteo(MeteoData meteoFile, IProgress<int> progress)
        {
            // this method investigates all loaded meteorologic files for low and high wind speed
            // times in order to determine when the turbine may have been inactive due to that --
            // which will be carried out in a different method later on

            // purpose of this is to find all suitable events

            try
            {
                if (meteoFile.MetMasts != null)
                {
                    int count = 0;

                    for (int i = 0; i < meteoFile.MetMasts.Count; i++)
                    {
                        for (int j = 0; j < meteoFile.MetMasts[i].MetDataSorted.Count; j++)
                        {
                            if (meteoFile.MetMasts[i].MetDataSorted[j].Speed.Measured == MeteoData.MeteoSample.HeightInfo.MeasuringHeight.M_10)
                            {
                                if (meteoFile.MetMasts[i].MetDataSorted[j].Speed.Metres10.Mean < CutIn &&
                                    meteoFile.MetMasts[i].MetDataSorted[j].Speed.Metres10.Mean >= 0)
                                {
                                    List<MeteoData.MeteoSample> thisEvent = new List<MeteoData.MeteoSample>();
                                    thisEvent.Add(meteoFile.MetMasts[i].MetDataSorted[j]);

                                    for (int k = j + 1; k < meteoFile.MetMasts[i].MetDataSorted.Count; k++)
                                    {
                                        if (meteoFile.MetMasts[i].MetDataSorted[k].SampleSeparation > new TimeSpan(0, 10, 0)) { j = k - 1; break; }

                                        if (meteoFile.MetMasts[i].MetDataSorted[k].Speed.Metres10.Mean > CutIn) { j = k - 1; break; }
                                        else if (k == meteoFile.MetMasts[i].MetDataSorted.Count - 1) { j = k; }

                                        thisEvent.Add(meteoFile.MetMasts[i].MetDataSorted[k]);
                                    }

                                    _loSpEvents.Add(new EventData(thisEvent, scada_analyst.EventData.WeatherType.LO_SPD));
                                    _allWtrEvts.Add(_loSpEvents[_loSpEvents.Count - 1]);
                                }
                                else if (meteoFile.MetMasts[i].MetDataSorted[j].Speed.Metres10.Mean > CutOut)
                                {
                                    List<MeteoData.MeteoSample> thisEvent = new List<MeteoData.MeteoSample>();
                                    thisEvent.Add(meteoFile.MetMasts[i].MetDataSorted[j]);

                                    for (int k = j + 1; k < meteoFile.MetMasts[i].MetDataSorted.Count; k++)
                                    {
                                        if (meteoFile.MetMasts[i].MetDataSorted[k].SampleSeparation > new TimeSpan(0, 10, 0)) { j = k - 1; break; }

                                        if (meteoFile.MetMasts[i].MetDataSorted[k].Speed.Metres10.Mean < CutOut) { j = k - 1; break; }
                                        else if (k == meteoFile.MetMasts[i].MetDataSorted.Count - 1) { j = k; }

                                        thisEvent.Add(meteoFile.MetMasts[i].MetDataSorted[k]);
                                    }

                                    _hiSpEvents.Add(new EventData(thisEvent, scada_analyst.EventData.WeatherType.HI_SPD));
                                    _allWtrEvts.Add(_hiSpEvents[_hiSpEvents.Count - 1]);
                                }
                            }
                            else
                            {
                                if (meteoFile.MetMasts[i].MetDataSorted[j].Speed.MetresRt.Mean < CutIn &&
                                    meteoFile.MetMasts[i].MetDataSorted[j].Speed.MetresRt.Mean >= 0)
                                {
                                    List<MeteoData.MeteoSample> thisEvent = new List<MeteoData.MeteoSample>();
                                    thisEvent.Add(meteoFile.MetMasts[i].MetDataSorted[j]);

                                    for (int k = j + 1; k < meteoFile.MetMasts[i].MetDataSorted.Count; k++)
                                    {
                                        if (meteoFile.MetMasts[i].MetDataSorted[k].SampleSeparation > new TimeSpan(0, 10, 0)) { j = k - 1; break; }

                                        if (meteoFile.MetMasts[i].MetDataSorted[k].Speed.MetresRt.Mean > CutIn) { j = k - 1; break; }
                                        else if (k == meteoFile.MetMasts[i].MetDataSorted.Count - 1) { j = k; }

                                        thisEvent.Add(meteoFile.MetMasts[i].MetDataSorted[k]);
                                    }

                                    _loSpEvents.Add(new EventData(thisEvent, scada_analyst.EventData.WeatherType.LO_SPD));
                                    _allWtrEvts.Add(_loSpEvents[_loSpEvents.Count - 1]);
                                }
                                else if (meteoFile.MetMasts[i].MetDataSorted[j].Speed.MetresRt.Mean > CutOut)
                                {
                                    List<MeteoData.MeteoSample> thisEvent = new List<MeteoData.MeteoSample>();
                                    thisEvent.Add(meteoFile.MetMasts[i].MetDataSorted[j]);

                                    for (int k = j + 1; k < meteoFile.MetMasts[i].MetDataSorted.Count; k++)
                                    {
                                        if (meteoFile.MetMasts[i].MetDataSorted[k].SampleSeparation > new TimeSpan(0, 10, 0)) { j = k - 1; break; }

                                        if (meteoFile.MetMasts[i].MetDataSorted[k].Speed.MetresRt.Mean < CutOut) { j = k - 1; break; }
                                        else if (k == meteoFile.MetMasts[i].MetDataSorted.Count - 1) { j = k; }

                                        thisEvent.Add(meteoFile.MetMasts[i].MetDataSorted[k]);
                                    }

                                    _hiSpEvents.Add(new EventData(thisEvent, scada_analyst.EventData.WeatherType.HI_SPD));
                                    _allWtrEvts.Add(_hiSpEvents[_hiSpEvents.Count - 1]);
                                }
                            }

                            count++;

                            if (count % 1000 == 0)
                            {
                                if (progress != null)
                                {
                                    progress.Report((int)(((double)i / meteoFile.MetMasts.Count +
                                        (double)j / meteoFile.MetMasts[i].MetDataSorted.Count / meteoFile.MetMasts.Count) * 100));
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                throw;
            }
        }

        private void FindWeatherFromScada(ScadaData scadaFile, IProgress<int> progress)
        {
            // this method investigates all loaded scada files for low and high wind speed
            // times in order to determine when the turbine may have been inactive due to that --
            // which will be carried out in a different method later on

            // purpose of this is to find all suitable events

            try
            {
                if (scadaFile.WindFarm != null)
                {
                    int count = 0;

                    for (int i = 0; i < scadaFile.WindFarm.Count; i++)
                    {
                        for (int j = 0; j < scadaFile.WindFarm[i].DataSorted.Count; j++)
                        {
                            if (scadaFile.WindFarm[i].DataSorted[j].Anemo.ActWinds.Mean < CutIn &&
                                scadaFile.WindFarm[i].DataSorted[j].Anemo.ActWinds.Mean >= 0)
                            {
                                List<ScadaData.ScadaSample> thisEvent = new List<ScadaData.ScadaSample>();
                                thisEvent.Add(scadaFile.WindFarm[i].DataSorted[j]);

                                for (int k = j + 1; k < scadaFile.WindFarm[i].DataSorted.Count; k++)
                                {
                                    if (scadaFile.WindFarm[i].DataSorted[k].SampleSeparation > ScadaSeprtr) { j = k - 1; break; }

                                    if (scadaFile.WindFarm[i].DataSorted[k].Anemo.ActWinds.Mean > CutIn) { j = k - 1; break; }
                                    else if (k == scadaFile.WindFarm[i].DataSorted.Count - 1) { j = k; }

                                    thisEvent.Add(scadaFile.WindFarm[i].DataSorted[k]);
                                }

                                _loSpEvents.Add(new EventData(thisEvent, scada_analyst.EventData.WeatherType.LO_SPD));
                                _allWtrEvts.Add(_loSpEvents[_loSpEvents.Count - 1]);
                            }
                            else if (scadaFile.WindFarm[i].DataSorted[j].Anemo.ActWinds.Mean > CutOut)
                            {
                                List<ScadaData.ScadaSample> thisEvent = new List<ScadaData.ScadaSample>();
                                thisEvent.Add(scadaFile.WindFarm[i].DataSorted[j]);

                                for (int k = j + 1; k < scadaFile.WindFarm[i].DataSorted.Count; k++)
                                {
                                    if (scadaFile.WindFarm[i].DataSorted[k].SampleSeparation > ScadaSeprtr) { j = k - 1; break; }

                                    if (scadaFile.WindFarm[i].DataSorted[k].Anemo.ActWinds.Mean < CutOut) { j = k - 1; break; }
                                    else if (k == scadaFile.WindFarm[i].DataSorted.Count - 1) { j = k; }

                                    thisEvent.Add(scadaFile.WindFarm[i].DataSorted[k]);
                                }

                                _hiSpEvents.Add(new EventData(thisEvent, scada_analyst.EventData.WeatherType.HI_SPD));
                                _allWtrEvts.Add(_hiSpEvents[_hiSpEvents.Count - 1]);
                            }

                            count++;

                            if (count % 1000 == 0)
                            {
                                if (progress != null)
                                {
                                    progress.Report((int)(((double)i / scadaFile.WindFarm.Count +
                                        (double)j / scadaFile.WindFarm[i].DataSorted.Count / scadaFile.WindFarm.Count) * 100));
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                throw;
            }
        }

        #endregion

        #region Algorithmic Analysis
        // these events here should be the real workhorse, trying to find connections between data, etc

        #region Rate of Change

        public void RatesOfChange(List<ScadaData.ScadaSample> eventData)
        {
            // this method is the public introduction to the rate of change analysis display result

            ExtractRateChangeTimes(eventData);
        }

        private List<AnalyticLimit> CreateRateChangeLimits()
        {
            List<AnalyticLimit> _newLimits = new List<AnalyticLimit>();

            _newLimits.Add(new AnalyticLimit(AnalyticLimit.Equipment.GEARBOX, _gbox.OilTemp.Description, 0, 20));
            _newLimits.Add(new AnalyticLimit(AnalyticLimit.Equipment.GEARBOX, _gbox.HsGen.Description, 0, 20));
            _newLimits.Add(new AnalyticLimit(AnalyticLimit.Equipment.GEARBOX, _gbox.HsRot.Description, 0, 20));
            _newLimits.Add(new AnalyticLimit(AnalyticLimit.Equipment.GEARBOX, _gbox.ImsGen.Description, 0, 20));
            _newLimits.Add(new AnalyticLimit(AnalyticLimit.Equipment.GEARBOX, _gbox.ImsRot.Description, 0, 20));
            _newLimits.Add(new AnalyticLimit(AnalyticLimit.Equipment.GENERATOR, _genr.RPMs.Description, 0, 1000));
            _newLimits.Add(new AnalyticLimit(AnalyticLimit.Equipment.GENERATOR, _genr.BearingG.Description, 0, 20));
            _newLimits.Add(new AnalyticLimit(AnalyticLimit.Equipment.GENERATOR, _genr.BearingR.Description, 0, 20));
            _newLimits.Add(new AnalyticLimit(AnalyticLimit.Equipment.MAIN_BEAR, _mbrg.Main.Description, 0, 20));
            _newLimits.Add(new AnalyticLimit(AnalyticLimit.Equipment.MAIN_BEAR, _mbrg.Gs.Description, 0, 20));
            _newLimits.Add(new AnalyticLimit(AnalyticLimit.Equipment.MAIN_BEAR, _mbrg.Hs.Description, 0, 20));

            return _newLimits;
        }

        private void ExtractRateChangeTimes(List<ScadaData.ScadaSample> eventData)
        {
            _rChngEvnts.Clear();

            #region Gearbox

            // for all samples in the dataset
            for (int i = 1; i < eventData.Count; i++)
            {
                // find which index in the list of thresholds concerns itself with this variable
                int index = _rateChange.FindIndex(x => x.Type == AnalyticLimit.Equipment.GEARBOX && x.VarName == _gbox.OilTemp.Description);

                // if the variable difference between this sample and the previous one is higher than the limit
                if (eventData[i].SampleSeparation == ScadaSeprtr &&
                    Math.Abs(eventData[i].Gearbox.OilTemp.Mean - eventData[i - 1].Gearbox.OilTemp.Mean) > _rateChange[index].MaxVars)
                {
                    // create a new sample data list and add a first sample to it
                    List<ScadaData.ScadaSample> thisEvent = new List<ScadaData.ScadaSample>();
                    thisEvent.Add(eventData[i - 1]); thisEvent.Add(eventData[i]);

                    // and now check how long this rate of change-crossing happened for
                    for (int j = i + 1; j < eventData.Count; j++)
                    {
                        // if the samples are further apart than the SCADA sample interval (standard 10 min), break
                        if (eventData[j].SampleSeparation > ScadaSeprtr) { i = j - 1; break; }

                        // if the sample value is below the rate of change limit, break
                        if (Math.Abs(eventData[j].Gearbox.OilTemp.Mean - eventData[j - 1].Gearbox.OilTemp.Mean) < _rateChange[index].MaxVars) { i = j - 1; break; }
                        else if (j == eventData.Count - 1) { i = j; }

                        // otherwise add to this rate of change-crossing list
                        thisEvent.Add(eventData[j]);
                    }

                    // once those have been checked, create a rate of change-crossing event which is displayed
                    _rChngEvnts.Add(new EventData(thisEvent, scada_analyst.EventData.AnomalySource.GEAR_OIL));
                }
            }

            for (int i = 1; i < eventData.Count; i++)
            {
                // every timestep must check what the previous one had as its value
                int index = _rateChange.FindIndex(x => x.Type == AnalyticLimit.Equipment.GEARBOX && x.VarName == _gbox.HsGen.Description);

                if (eventData[i].SampleSeparation == ScadaSeprtr &&
                    Math.Abs(eventData[i].Gearbox.HsGen.Mean - eventData[i - 1].Gearbox.HsGen.Mean) > _rateChange[index].MaxVars)
                {
                    List<ScadaData.ScadaSample> thisEvent = new List<ScadaData.ScadaSample>();
                    thisEvent.Add(eventData[i - 1]); thisEvent.Add(eventData[i]);

                    for (int j = i + 1; j < eventData.Count; j++)
                    {
                        if (eventData[j].SampleSeparation > ScadaSeprtr) { i = j - 1; break; }

                        if (Math.Abs(eventData[j].Gearbox.HsGen.Mean - eventData[j - 1].Gearbox.HsGen.Mean) < _rateChange[index].MaxVars) { i = j - 1; break; }
                        else if (j == eventData.Count - 1) { i = j; }

                        thisEvent.Add(eventData[j]);
                    }

                    _rChngEvnts.Add(new EventData(thisEvent, scada_analyst.EventData.AnomalySource.GEAR_HS_GEN));
                }
            }

            for (int i = 1; i < eventData.Count; i++)
            {
                // every timestep must check what the previous one had as its value
                int index = _rateChange.FindIndex(x => x.Type == AnalyticLimit.Equipment.GEARBOX && x.VarName == _gbox.HsRot.Description);

                if (eventData[i].SampleSeparation == ScadaSeprtr &&
                    Math.Abs(eventData[i].Gearbox.HsRot.Mean - eventData[i - 1].Gearbox.HsRot.Mean) > _rateChange[index].MaxVars)
                {
                    List<ScadaData.ScadaSample> thisEvent = new List<ScadaData.ScadaSample>();
                    thisEvent.Add(eventData[i - 1]); thisEvent.Add(eventData[i]);

                    for (int j = i + 1; j < eventData.Count; j++)
                    {
                        if (eventData[j].SampleSeparation > ScadaSeprtr) { i = j - 1; break; }

                        if (Math.Abs(eventData[j].Gearbox.HsRot.Mean - eventData[j - 1].Gearbox.HsRot.Mean) < _rateChange[index].MaxVars) { i = j - 1; break; }
                        else if (j == eventData.Count - 1) { i = j; }

                        thisEvent.Add(eventData[j]);
                    }

                    _rChngEvnts.Add(new EventData(thisEvent, scada_analyst.EventData.AnomalySource.GEAR_HS_ROT));
                }
            }

            for (int i = 1; i < eventData.Count; i++)
            {
                // every timestep must check what the previous one had as its value
                int index = _rateChange.FindIndex(x => x.Type == AnalyticLimit.Equipment.GEARBOX && x.VarName == _gbox.ImsGen.Description);

                if (eventData[i].SampleSeparation == ScadaSeprtr &&
                    Math.Abs(eventData[i].Gearbox.ImsGen.Mean - eventData[i - 1].Gearbox.ImsGen.Mean) > _rateChange[index].MaxVars)
                {
                    List<ScadaData.ScadaSample> thisEvent = new List<ScadaData.ScadaSample>();
                    thisEvent.Add(eventData[i - 1]); thisEvent.Add(eventData[i]);

                    for (int j = i + 1; j < eventData.Count; j++)
                    {
                        if (eventData[j].SampleSeparation > ScadaSeprtr) { i = j - 1; break; }

                        if (Math.Abs(eventData[j].Gearbox.ImsGen.Mean - eventData[j - 1].Gearbox.ImsGen.Mean) < _rateChange[index].MaxVars) { i = j - 1; break; }
                        else if (j == eventData.Count - 1) { i = j; }

                        thisEvent.Add(eventData[j]);
                    }

                    _rChngEvnts.Add(new EventData(thisEvent, scada_analyst.EventData.AnomalySource.GEAR_IM_GEN));
                }
            }

            for (int i = 1; i < eventData.Count; i++)
            {
                // every timestep must check what the previous one had as its value
                int index = _rateChange.FindIndex(x => x.Type == AnalyticLimit.Equipment.GEARBOX && x.VarName == _gbox.ImsRot.Description);

                if (eventData[i].SampleSeparation == ScadaSeprtr &&
                    Math.Abs(eventData[i].Gearbox.ImsRot.Mean - eventData[i - 1].Gearbox.ImsRot.Mean) > _rateChange[index].MaxVars)
                {
                    List<ScadaData.ScadaSample> thisEvent = new List<ScadaData.ScadaSample>();
                    thisEvent.Add(eventData[i - 1]); thisEvent.Add(eventData[i]);

                    for (int j = i + 1; j < eventData.Count; j++)
                    {
                        if (eventData[j].SampleSeparation > ScadaSeprtr) { i = j - 1; break; }

                        if (Math.Abs(eventData[j].Gearbox.ImsRot.Mean - eventData[j - 1].Gearbox.ImsRot.Mean) < _rateChange[index].MaxVars) { i = j - 1; break; }
                        else if (j == eventData.Count - 1) { i = j; }

                        thisEvent.Add(eventData[j]);
                    }

                    _rChngEvnts.Add(new EventData(thisEvent, scada_analyst.EventData.AnomalySource.GEAR_IM_ROT));
                }
            }

            #endregion

            #region Generator

            for (int i = 1; i < eventData.Count; i++)
            {
                // every timestep must check what the previous one had as its value

                int index = _rateChange.FindIndex(x => x.Type == AnalyticLimit.Equipment.GENERATOR && x.VarName == _genr.RPMs.Description);

                if (eventData[i].SampleSeparation == ScadaSeprtr &&
                    Math.Abs(eventData[i].Genny.RPMs.Mean - eventData[i - 1].Genny.RPMs.Mean) > _rateChange[index].MaxVars)
                {
                    List<ScadaData.ScadaSample> thisEvent = new List<ScadaData.ScadaSample>();
                    thisEvent.Add(eventData[i - 1]); thisEvent.Add(eventData[i]);

                    for (int j = i + 1; j < eventData.Count; j++)
                    {
                        if (eventData[j].SampleSeparation > ScadaSeprtr) { i = j - 1; break; }

                        if (Math.Abs(eventData[j].Genny.RPMs.Mean - eventData[j - 1].Genny.RPMs.Mean) < _rateChange[index].MaxVars) { i = j - 1; break; }
                        else if (j == eventData.Count - 1) { i = j; }

                        thisEvent.Add(eventData[j]);
                    }

                    _rChngEvnts.Add(new EventData(thisEvent, scada_analyst.EventData.AnomalySource.GENNY_RPM));
                }
            }

            for (int i = 1; i < eventData.Count; i++)
            {
                // every timestep must check what the previous one had as its value

                int index = _rateChange.FindIndex(x => x.Type == AnalyticLimit.Equipment.GENERATOR && x.VarName == _genr.BearingG.Description);

                if (eventData[i].SampleSeparation == ScadaSeprtr &&
                    Math.Abs(eventData[i].Genny.BearingG.Mean - eventData[i - 1].Genny.BearingG.Mean) > _rateChange[index].MaxVars)
                {
                    List<ScadaData.ScadaSample> thisEvent = new List<ScadaData.ScadaSample>();
                    thisEvent.Add(eventData[i - 1]); thisEvent.Add(eventData[i]);

                    for (int j = i + 1; j < eventData.Count; j++)
                    {
                        if (eventData[j].SampleSeparation > ScadaSeprtr) { i = j - 1; break; }

                        if (Math.Abs(eventData[j].Genny.BearingG.Mean - eventData[j - 1].Genny.BearingG.Mean) < _rateChange[index].MaxVars) { i = j - 1; break; }
                        else if (j == eventData.Count - 1) { i = j; }

                        thisEvent.Add(eventData[j]);
                    }

                    _rChngEvnts.Add(new EventData(thisEvent, scada_analyst.EventData.AnomalySource.GENNY_G));
                }
            }

            for (int i = 1; i < eventData.Count; i++)
            {
                // every timestep must check what the previous one had as its value

                int index = _rateChange.FindIndex(x => x.Type == AnalyticLimit.Equipment.GENERATOR && x.VarName == _genr.BearingR.Description);

                if (eventData[i].SampleSeparation == ScadaSeprtr &&
                    Math.Abs(eventData[i].Genny.BearingR.Mean - eventData[i - 1].Genny.BearingR.Mean) > _rateChange[index].MaxVars)
                {
                    List<ScadaData.ScadaSample> thisEvent = new List<ScadaData.ScadaSample>();
                    thisEvent.Add(eventData[i - 1]); thisEvent.Add(eventData[i]);

                    for (int j = i + 1; j < eventData.Count; j++)
                    {
                        if (eventData[j].SampleSeparation > ScadaSeprtr) { i = j - 1; break; }

                        if (Math.Abs(eventData[j].Genny.BearingR.Mean - eventData[j - 1].Genny.BearingR.Mean) < _rateChange[index].MaxVars) { i = j - 1; break; }
                        else if (j == eventData.Count - 1) { i = j; }

                        thisEvent.Add(eventData[j]);
                    }

                    _rChngEvnts.Add(new EventData(thisEvent, scada_analyst.EventData.AnomalySource.GENNY_R));
                }
            }

            #endregion

            #region Main Bearing

            for (int i = 1; i < eventData.Count; i++)
            {
                // every timestep must check what the previous one had as its value

                int index = _rateChange.FindIndex(x => x.Type == AnalyticLimit.Equipment.MAIN_BEAR && x.VarName == _mbrg.Main.Description);

                if (eventData[i].SampleSeparation == ScadaSeprtr &&
                    Math.Abs(eventData[i].MainBear.Main.Mean - eventData[i - 1].MainBear.Main.Mean) > _rateChange[index].MaxVars)
                {
                    List<ScadaData.ScadaSample> thisEvent = new List<ScadaData.ScadaSample>();
                    thisEvent.Add(eventData[i - 1]); thisEvent.Add(eventData[i]);

                    for (int j = i + 1; j < eventData.Count; j++)
                    {
                        if (eventData[j].SampleSeparation > ScadaSeprtr) { i = j - 1; break; }

                        if (Math.Abs(eventData[j].MainBear.Main.Mean - eventData[j - 1].MainBear.Main.Mean) < _rateChange[index].MaxVars) { i = j - 1; break; }
                        else if (j == eventData.Count - 1) { i = j; }

                        thisEvent.Add(eventData[j]);
                    }

                    _rChngEvnts.Add(new EventData(thisEvent, scada_analyst.EventData.AnomalySource.BEARING));
                }
            }

            for (int i = 1; i < eventData.Count; i++)
            {
                // every timestep must check what the previous one had as its value

                int index = _rateChange.FindIndex(x => x.Type == AnalyticLimit.Equipment.MAIN_BEAR && x.VarName == _mbrg.Gs.Description);

                if (eventData[i].SampleSeparation == ScadaSeprtr &&
                    Math.Abs(eventData[i].MainBear.Hs.Mean - eventData[i - 1].MainBear.Hs.Mean) > _rateChange[index].MaxVars)
                {
                    List<ScadaData.ScadaSample> thisEvent = new List<ScadaData.ScadaSample>();
                    thisEvent.Add(eventData[i - 1]); thisEvent.Add(eventData[i]);

                    for (int j = i + 1; j < eventData.Count; j++)
                    {
                        if (eventData[j].SampleSeparation > ScadaSeprtr) { i = j - 1; break; }

                        if (Math.Abs(eventData[j].MainBear.Hs.Mean - eventData[j - 1].MainBear.Hs.Mean) < _rateChange[index].MaxVars) { i = j - 1; break; }
                        else if (j == eventData.Count - 1) { i = j; }

                        thisEvent.Add(eventData[j]);
                    }

                    _rChngEvnts.Add(new EventData(thisEvent, scada_analyst.EventData.AnomalySource.BEARING_GS));
                }
            }

            for (int i = 1; i < eventData.Count; i++)
            {
                // every timestep must check what the previous one had as its value

                int index = _rateChange.FindIndex(x => x.Type == AnalyticLimit.Equipment.MAIN_BEAR && x.VarName == _mbrg.Hs.Description);

                if (eventData[i].SampleSeparation == ScadaSeprtr &&
                    Math.Abs(eventData[i].MainBear.Gs.Mean - eventData[i - 1].MainBear.Gs.Mean) > _rateChange[index].MaxVars)
                {
                    List<ScadaData.ScadaSample> thisEvent = new List<ScadaData.ScadaSample>();
                    thisEvent.Add(eventData[i - 1]); thisEvent.Add(eventData[i]);

                    for (int j = i + 1; j < eventData.Count; j++)
                    {
                        if (eventData[j].SampleSeparation > ScadaSeprtr) { i = j - 1; break; }

                        if (Math.Abs(eventData[j].MainBear.Gs.Mean - eventData[j - 1].MainBear.Gs.Mean) < _rateChange[index].MaxVars) { i = j - 1; break; }
                        else if (j == eventData.Count - 1) { i = j; }

                        thisEvent.Add(eventData[j]);
                    }

                    _rChngEvnts.Add(new EventData(thisEvent, scada_analyst.EventData.AnomalySource.BEARING_HS));
                }
            }

            #endregion
        }

        #endregion 

        #region Thresholding

        /// <summary>
        /// This method is the general introduction into checking what threshold values exist in the data as
        /// well as highlighting when the data values are too close to the extremities of the allowed range.
        /// </summary>
        public void Thresholding(List<ScadaData.ScadaSample> eventData)
        {
            // now this method needs to call whatever other items are necessary to exercise control over
            // the thresholding process

            // all variables we are interested in will have their own threshold levels - these should be easy
            // to change on the user side, but let's start with fixed values. We want to highlight all instances
            // where the variables is above or below the levels we are interested in, let's say within a 10% 
            // range of the threshold itself. 

            // need new classes for threshold limits which is implemented, when we come into this method the 
            // threshold limits already exist and can be modified. Hence the calculations are what this calls

            ExtractThresholdTimes(eventData);
        }

        private List<AnalyticLimit> CreateThresholdLimits()
        {
            List<AnalyticLimit> _newLimits = new List<AnalyticLimit>();

            _newLimits.Add(new AnalyticLimit(AnalyticLimit.Equipment.GEARBOX, _gbox.OilTemp.Description, 0, 70));
            _newLimits.Add(new AnalyticLimit(AnalyticLimit.Equipment.GEARBOX, _gbox.HsGen.Description, 0, 70));
            _newLimits.Add(new AnalyticLimit(AnalyticLimit.Equipment.GEARBOX, _gbox.HsRot.Description, 0, 70));
            _newLimits.Add(new AnalyticLimit(AnalyticLimit.Equipment.GEARBOX, _gbox.ImsGen.Description, 0, 70));
            _newLimits.Add(new AnalyticLimit(AnalyticLimit.Equipment.GEARBOX, _gbox.ImsRot.Description, 0, 70));
            _newLimits.Add(new AnalyticLimit(AnalyticLimit.Equipment.GENERATOR, _genr.RPMs.Description, 0, 1600));
            _newLimits.Add(new AnalyticLimit(AnalyticLimit.Equipment.GENERATOR, _genr.BearingG.Description, 0, 70));
            _newLimits.Add(new AnalyticLimit(AnalyticLimit.Equipment.GENERATOR, _genr.BearingR.Description, 0, 70));
            _newLimits.Add(new AnalyticLimit(AnalyticLimit.Equipment.MAIN_BEAR, _mbrg.Main.Description, 0, 50));
            _newLimits.Add(new AnalyticLimit(AnalyticLimit.Equipment.MAIN_BEAR, _mbrg.Gs.Description, 0, 50));
            _newLimits.Add(new AnalyticLimit(AnalyticLimit.Equipment.MAIN_BEAR, _mbrg.Hs.Description, 0, 50));

            return _newLimits;
        }

        private void ExtractThresholdTimes(List<ScadaData.ScadaSample> eventData)
        {
            _thresEvnts.Clear();

            #region Gearbox

            // for all samples in the dataset
            for (int i = 0; i < eventData.Count; i++)
            {
                // find which index in the list of thresholds concerns itself with this variable
                int index = _thresholds.FindIndex(x => x.Type == AnalyticLimit.Equipment.GEARBOX && x.VarName == _gbox.OilTemp.Description);

                // if the variable at that sample is higher than the user-specified value...
                if (eventData[i].Gearbox.OilTemp.Mean > _thresholds[index].MaxVars)
                {
                    // create a new sample data list and add a first sample to it
                    List<ScadaData.ScadaSample> thisEvent = new List<ScadaData.ScadaSample>();
                    thisEvent.Add(eventData[i]);

                    // and now check how long this threshold-crossing happened for
                    for (int j = i + 1; j < eventData.Count; j++)
                    {
                        // if the samples are further apart than the SCADA sample interval (standard 10 min), break
                        if (eventData[j].SampleSeparation > ScadaSeprtr) { i = j - 1; break; }

                        // if the sample value is below the threshold value, break
                        if (eventData[j].Gearbox.OilTemp.Mean < _thresholds[index].MaxVars) { i = j - 1; break; }
                        else if (j == eventData.Count - 1) { i = j; }

                        // otherwise add to this threshold-crossing list
                        thisEvent.Add(eventData[j]);
                    }

                    // once those have been checked, create a threshold-crossing event which is displayed
                    _thresEvnts.Add(new EventData(thisEvent, scada_analyst.EventData.AnomalySource.GEAR_OIL));
                }
            }

            for (int i = 0; i < eventData.Count; i++)
            {
                int index = _thresholds.FindIndex(x => x.Type == AnalyticLimit.Equipment.GEARBOX && x.VarName == _gbox.HsGen.Description);

                if (eventData[i].Gearbox.HsGen.Mean > _thresholds[index].MaxVars)
                {
                    List<ScadaData.ScadaSample> thisEvent = new List<ScadaData.ScadaSample>();
                    thisEvent.Add(eventData[i]);

                    for (int j = i + 1; j < eventData.Count; j++)
                    {
                        if (eventData[j].SampleSeparation > ScadaSeprtr) { i = j - 1; break; }

                        if (eventData[j].Gearbox.HsGen.Mean < _thresholds[index].MaxVars) { i = j - 1; break; }
                        else if (j == eventData.Count - 1) { i = j; }

                        thisEvent.Add(eventData[j]);
                    }

                    _thresEvnts.Add(new EventData(thisEvent, scada_analyst.EventData.AnomalySource.GEAR_HS_GEN));
                }
            }

            for (int i = 0; i < eventData.Count; i++)
            {
                int index = _thresholds.FindIndex(x => x.Type == AnalyticLimit.Equipment.GEARBOX && x.VarName == _gbox.HsRot.Description);

                if (eventData[i].Gearbox.HsRot.Mean > _thresholds[index].MaxVars)
                {
                    List<ScadaData.ScadaSample> thisEvent = new List<ScadaData.ScadaSample>();
                    thisEvent.Add(eventData[i]);

                    for (int j = i + 1; j < eventData.Count; j++)
                    {
                        if (eventData[j].SampleSeparation > ScadaSeprtr) { i = j - 1; break; }

                        if (eventData[j].Gearbox.HsRot.Mean < _thresholds[index].MaxVars) { i = j - 1; break; }
                        else if (j == eventData.Count - 1) { i = j; }

                        thisEvent.Add(eventData[j]);
                    }

                    _thresEvnts.Add(new EventData(thisEvent, scada_analyst.EventData.AnomalySource.GEAR_HS_ROT));
                }
            }

            for (int i = 0; i < eventData.Count; i++)
            {
                int index = _thresholds.FindIndex(x => x.Type == AnalyticLimit.Equipment.GEARBOX && x.VarName == _gbox.ImsGen.Description);

                if (eventData[i].Gearbox.ImsGen.Mean > _thresholds[index].MaxVars)
                {
                    List<ScadaData.ScadaSample> thisEvent = new List<ScadaData.ScadaSample>();
                    thisEvent.Add(eventData[i]);

                    for (int j = i + 1; j < eventData.Count; j++)
                    {
                        if (eventData[j].SampleSeparation > ScadaSeprtr) { i = j - 1; break; }

                        if (eventData[j].Gearbox.ImsGen.Mean < _thresholds[index].MaxVars) { i = j - 1; break; }
                        else if (j == eventData.Count - 1) { i = j; }

                        thisEvent.Add(eventData[j]);
                    }

                    _thresEvnts.Add(new EventData(thisEvent, scada_analyst.EventData.AnomalySource.GEAR_IM_GEN));
                }
            }

            for (int i = 0; i < eventData.Count; i++)
            {
                int index = _thresholds.FindIndex(x => x.Type == AnalyticLimit.Equipment.GEARBOX && x.VarName == _gbox.ImsRot.Description);

                if (eventData[i].Gearbox.ImsRot.Mean > _thresholds[index].MaxVars)
                {
                    List<ScadaData.ScadaSample> thisEvent = new List<ScadaData.ScadaSample>();
                    thisEvent.Add(eventData[i]);

                    for (int j = i + 1; j < eventData.Count; j++)
                    {
                        if (eventData[j].SampleSeparation > ScadaSeprtr) { i = j - 1; break; }

                        if (eventData[j].Gearbox.ImsRot.Mean < _thresholds[index].MaxVars) { i = j - 1; break; }
                        else if (j == eventData.Count - 1) { i = j; }

                        thisEvent.Add(eventData[j]);
                    }

                    _thresEvnts.Add(new EventData(thisEvent, scada_analyst.EventData.AnomalySource.GEAR_IM_ROT));
                }
            }

            #endregion

            #region Generator

            for (int i = 0; i < eventData.Count; i++)
            {
                int index = _thresholds.FindIndex(x => x.Type == AnalyticLimit.Equipment.GENERATOR && x.VarName == _genr.RPMs.Description);

                if (eventData[i].Genny.RPMs.Mean > _thresholds[index].MaxVars)
                {
                    List<ScadaData.ScadaSample> thisEvent = new List<ScadaData.ScadaSample>();
                    thisEvent.Add(eventData[i]);

                    for (int j = i + 1; j < eventData.Count; j++)
                    {
                        if (eventData[j].SampleSeparation > ScadaSeprtr) { i = j - 1; break; }

                        if (eventData[j].Genny.RPMs.Mean < _thresholds[index].MaxVars) { i = j - 1; break; }
                        else if (j == eventData.Count - 1) { i = j; }

                        thisEvent.Add(eventData[j]);
                    }

                    _thresEvnts.Add(new EventData(thisEvent, scada_analyst.EventData.AnomalySource.GENNY_RPM));
                }
            }

            for (int i = 0; i < eventData.Count; i++)
            {
                int index = _thresholds.FindIndex(x => x.Type == AnalyticLimit.Equipment.GENERATOR && x.VarName == _genr.BearingG.Description);

                if (eventData[i].Genny.BearingG.Mean > _thresholds[index].MaxVars)
                {
                    List<ScadaData.ScadaSample> thisEvent = new List<ScadaData.ScadaSample>();
                    thisEvent.Add(eventData[i]);

                    for (int j = i + 1; j < eventData.Count; j++)
                    {
                        if (eventData[j].SampleSeparation > ScadaSeprtr) { i = j - 1; break; }

                        if (eventData[j].Genny.BearingG.Mean < _thresholds[index].MaxVars) { i = j - 1; break; }
                        else if (j == eventData.Count - 1) { i = j; }

                        thisEvent.Add(eventData[j]);
                    }

                    _thresEvnts.Add(new EventData(thisEvent, scada_analyst.EventData.AnomalySource.GENNY_G));
                }
            }

            for (int i = 0; i < eventData.Count; i++)
            {
                int index = _thresholds.FindIndex(x => x.Type == AnalyticLimit.Equipment.GENERATOR && x.VarName == _genr.BearingR.Description);

                if (eventData[i].Genny.BearingR.Mean > _thresholds[index].MaxVars)
                {
                    List<ScadaData.ScadaSample> thisEvent = new List<ScadaData.ScadaSample>();
                    thisEvent.Add(eventData[i]);

                    for (int j = i + 1; j < eventData.Count; j++)
                    {
                        if (eventData[j].SampleSeparation > ScadaSeprtr) { i = j - 1; break; }

                        if (eventData[j].Genny.BearingG.Mean < _thresholds[index].MaxVars) { i = j - 1; break; }
                        else if (j == eventData.Count - 1) { i = j; }

                        thisEvent.Add(eventData[j]);
                    }

                    _thresEvnts.Add(new EventData(thisEvent, scada_analyst.EventData.AnomalySource.GENNY_R));
                }
            }

            #endregion

            #region Main Bearing

            for (int i = 0; i < eventData.Count; i++)
            {
                int index = _thresholds.FindIndex(x => x.Type == AnalyticLimit.Equipment.MAIN_BEAR && x.VarName == _mbrg.Main.Description);

                if (eventData[i].MainBear.Main.Mean > _thresholds[index].MaxVars)
                {
                    List<ScadaData.ScadaSample> thisEvent = new List<ScadaData.ScadaSample>();
                    thisEvent.Add(eventData[i]);

                    for (int j = i + 1; j < eventData.Count; j++)
                    {
                        if (eventData[j].SampleSeparation > ScadaSeprtr) { i = j - 1; break; }

                        if (eventData[j].MainBear.Main.Mean < _thresholds[index].MaxVars) { i = j - 1; break; }
                        else if (j == eventData.Count - 1) { i = j; }

                        thisEvent.Add(eventData[j]);
                    }

                    _thresEvnts.Add(new EventData(thisEvent, scada_analyst.EventData.AnomalySource.BEARING));
                }
            }

            for (int i = 0; i < eventData.Count; i++)
            {
                int index = _thresholds.FindIndex(x => x.Type == AnalyticLimit.Equipment.MAIN_BEAR && x.VarName == _mbrg.Gs.Description);

                if (eventData[i].MainBear.Hs.Mean > _thresholds[index].MaxVars)
                {
                    List<ScadaData.ScadaSample> thisEvent = new List<ScadaData.ScadaSample>();
                    thisEvent.Add(eventData[i]);

                    for (int j = i + 1; j < eventData.Count; j++)
                    {
                        if (eventData[j].SampleSeparation > ScadaSeprtr) { i = j - 1; break; }

                        if (eventData[j].MainBear.Hs.Mean < _thresholds[index].MaxVars) { i = j - 1; break; }
                        else if (j == eventData.Count - 1) { i = j; }

                        thisEvent.Add(eventData[j]);
                    }

                    _thresEvnts.Add(new EventData(thisEvent, scada_analyst.EventData.AnomalySource.BEARING_GS));
                }
            }

            for (int i = 0; i < eventData.Count; i++)
            {
                int index = _thresholds.FindIndex(x => x.Type == AnalyticLimit.Equipment.MAIN_BEAR && x.VarName == _mbrg.Hs.Description);

                if (eventData[i].MainBear.Hs.Mean > _thresholds[index].MaxVars)
                {
                    List<ScadaData.ScadaSample> thisEvent = new List<ScadaData.ScadaSample>();
                    thisEvent.Add(eventData[i]);

                    for (int j = i + 1; j < eventData.Count; j++)
                    {
                        if (eventData[j].SampleSeparation > ScadaSeprtr) { i = j - 1; break; }

                        if (eventData[j].MainBear.Hs.Mean < _thresholds[index].MaxVars) { i = j - 1; break; }
                        else if (j == eventData.Count - 1) { i = j; }

                        thisEvent.Add(eventData[j]);
                    }

                    _thresEvnts.Add(new EventData(thisEvent, scada_analyst.EventData.AnomalySource.BEARING_HS));
                }
            }

            #endregion
        }

        private void GetThresholdEvents(List<ScadaData.ScadaSample> eventData, int input, double variable, int index,
            EventData.AnomalySource type)
        {
            // try to build one comprehensive method

            // for every variable: all doubles: this method proceeds to check whether
            // the user-defined threshold value has been exceeded or not
            // if it has, we create a new event to add the data into there
            // after the event is over, we can add this to the threshold events complete list

            for (int i = 0; i < eventData.Count; i++)
            {
                if (eventData[i].Gearbox.ImsRot.Mean > _thresholds[index].MaxVars)
                {
                    List<ScadaData.ScadaSample> thisEvent = new List<ScadaData.ScadaSample>();
                    thisEvent.Add(eventData[i]);

                    for (int j = i + 1; j < eventData.Count; j++)
                    {
                        if (eventData[j].SampleSeparation > ScadaSeprtr) { i = j - 1; break; }

                        if (eventData[j].Gearbox.ImsRot.Mean < _thresholds[index].MaxVars) { i = j - 1; break; }
                        else if (j == eventData.Count - 1) { i = j; }

                        thisEvent.Add(eventData[j]);
                    }

                    _thresEvnts.Add(new EventData(thisEvent, scada_analyst.EventData.AnomalySource.GEAR_IM_ROT));
                }
            }
        }

        #endregion

        public DataTable GetYearlyBearings(ScadaData _scada, MeteoData _meteo, int _year, IProgress<int> progress)
        {
            DataTable _table = new DataTable();

            try
            {
                int count = 0;

                // add column for the asset ID 
                _table.Columns.Add("Asset", typeof(string));

                // for every loaded asset
                for (int i = 0; i < _assetList.Count; i++)
                {
                    int index;

                    DataRow _add = _table.NewRow();

                    // check whether we are dealing with turbines or metmasts
                    if (_assetList[i].Type == BaseStructure.Types.TURBINE)
                    {
                        // get the ID of the one we're investigating
                        index = _scada.WindFarm.FindIndex(x => x.UnitID == _assetList[i].UnitID);

                        // add asset value
                        _add["Asset"] = _scada.WindFarm[index].UnitID.ToString();

                        // add samples to the data table
                        // note that their order is not important!
                        for (int j = 0; j < _scada.WindFarm[index].DataSorted.Count; j++)
                        {
                            if (_scada.WindFarm[index].DataSorted[j].TimeStamp.Year == _year)
                            {
                                // if this column does not exist, add it on
                                if (!_table.Columns.Contains(_scada.WindFarm[index].DataSorted[j].TimeStamp.ToString()))
                                {
                                    _table.Columns.Add(_scada.WindFarm[index].DataSorted[j].TimeStamp.ToString(), typeof(string));
                                }

                                // add the sample value to the column
                                _add[_scada.WindFarm[index].DataSorted[j].TimeStamp.ToString()] =
                                    _scada.WindFarm[index].DataSorted[j].YawSys.YawPos.Mean.ToString();
                            }

                            count++;

                            if (count % 1000 == 0)
                            {
                                if (progress != null)
                                {
                                    progress.Report((int)((double)100 / _assetList.Count * i +
                                     (double)j * 100 / _scada.WindFarm[index].DataSorted.Count / _assetList.Count));
                                }
                            }
                        }
                    }
                    else if (_assetList[i].Type == BaseStructure.Types.METMAST)
                    {
                        // get the ID of the one we're investigating
                        index = _meteo.MetMasts.FindIndex(x => x.UnitID == _assetList[i].UnitID);

                        // add asset value
                        _add["Asset"] = _meteo.MetMasts[index].UnitID.ToString();

                        // add samples to the data table
                        // note that their order is not important!
                        for (int j = 0; j < _meteo.MetMasts[index].MetDataSorted.Count; j++)
                        {
                            if (_meteo.MetMasts[index].MetDataSorted[j].TimeStamp.Year == _year)
                            {
                                // if this column does not exist, add it on
                                if (!_table.Columns.Contains(_meteo.MetMasts[index].MetDataSorted[j].TimeStamp.ToString()))
                                {
                                    _table.Columns.Add(_meteo.MetMasts[index].MetDataSorted[j].TimeStamp.ToString(), typeof(string));
                                }

                                // add the sample value to the column but also check for what height samples
                                if (_meteo.MetrHeader.Dircs.Measured == MeteoData.MeteoSample.HeightInfo.MeasuringHeight.M_10)
                                {
                                    _add[_meteo.MetMasts[index].MetDataSorted[j].TimeStamp.ToString()] =
                                        _meteo.MetMasts[index].MetDataSorted[j].Dircs.Metres10.Mean.ToString();
                                }
                                else if (_meteo.MetrHeader.Dircs.Measured == MeteoData.MeteoSample.HeightInfo.MeasuringHeight.ROT ||
                                    _meteo.MetrHeader.Dircs.Measured == MeteoData.MeteoSample.HeightInfo.MeasuringHeight.BOTH)
                                {
                                    _add[_meteo.MetMasts[index].MetDataSorted[j].TimeStamp.ToString()] =
                                        _meteo.MetMasts[index].MetDataSorted[j].Dircs.MetresRt.Mean.ToString();
                                }
                            }

                            count++;

                            if (count % 1000 == 0)
                            {
                                if (progress != null)
                                {
                                    progress.Report((int)((double)100 / _assetList.Count * i +
                                     (double)j * 100 / _meteo.MetMasts[index].MetDataSorted.Count / _assetList.Count));
                                }
                            }
                        }
                    }

                    // lastly, definitely helps to add the row to the table
                    _table.Rows.Add(_add);
                }
            }
            catch { }

            return _table;
        }

        public List<DataTable> GetYearlyBearingsWithSpeed(ScadaData _scada, MeteoData _meteo, int _year, IProgress<int> progress)
        {
            List<DataTable> _tableList = new List<DataTable>();

            try
            {
                int count = 0;

                // for every loaded asset
                for (int i = 0; i < _assetList.Count; i++)
                {
                    // add a new table for every asset
                    _tableList.Add(new DataTable());
                    DataTable _thisTable = _tableList[_tableList.Count - 1];

                    // add column for the asset ID 
                    _thisTable.Columns.Add("Direction", typeof(string));
                    _thisTable.Columns.Add("Speed", typeof(string));

                    int index;

                    // check whether we are dealing with turbines or metmasts
                    if (_assetList[i].Type == BaseStructure.Types.TURBINE)
                    {
                        // get the ID of the one we're investigating
                        index = _scada.WindFarm.FindIndex(x => x.UnitID == _assetList[i].UnitID);
                        
                        // add samples to the data table
                        // note that their order is not important!
                        for (int j = 0; j < _scada.WindFarm[index].DataSorted.Count; j++)
                        {
                            if (_scada.WindFarm[index].DataSorted[j].TimeStamp.Year == _year)
                            {
                                DataRow _add = _thisTable.NewRow();

                                // add the sample value to the column
                                _add["Direction"] = _scada.WindFarm[index].DataSorted[j].YawSys.YawPos.Mean.ToString("#.0");
                                _add["Speed"] = _scada.WindFarm[index].DataSorted[j].Anemo.ActWinds.Mean.ToString("#.00");

                                // lastly, definitely helps to add the row to the table
                                _thisTable.Rows.Add(_add);
                            }

                            count++;

                            if (count % 1000 == 0)
                            {
                                if (progress != null)
                                {
                                    progress.Report((int)((double)100 / _assetList.Count * i +
                                     (double)j * 100 / _scada.WindFarm[index].DataSorted.Count / _assetList.Count));
                                }
                            }
                        }
                    }
                    else if (_assetList[i].Type == BaseStructure.Types.METMAST)
                    {
                        // get the ID of the one we're investigating
                        index = _meteo.MetMasts.FindIndex(x => x.UnitID == _assetList[i].UnitID);
                        
                        // add samples to the data table
                        // note that their order is not important!
                        for (int j = 0; j < _meteo.MetMasts[index].MetDataSorted.Count; j++)
                        {
                            if (_meteo.MetMasts[index].MetDataSorted[j].TimeStamp.Year == _year)
                            {
                                DataRow _add = _thisTable.NewRow();

                                // add the sample value to the column but also check for what height samples
                                if (_meteo.MetrHeader.Dircs.Measured == MeteoData.MeteoSample.HeightInfo.MeasuringHeight.M_10)
                                {
                                    _add["Direction"] = _meteo.MetMasts[index].MetDataSorted[j].Dircs.Metres10.Mean.ToString("#.0");
                                }
                                else if (_meteo.MetrHeader.Dircs.Measured == MeteoData.MeteoSample.HeightInfo.MeasuringHeight.ROT ||
                                    _meteo.MetrHeader.Dircs.Measured == MeteoData.MeteoSample.HeightInfo.MeasuringHeight.BOTH)
                                {
                                    _add["Direction"] = _meteo.MetMasts[index].MetDataSorted[j].Dircs.MetresRt.Mean.ToString("#.0");
                                }

                                // add the sample value to the column but also check for what height samples
                                if (_meteo.MetrHeader.Speed.Measured == MeteoData.MeteoSample.HeightInfo.MeasuringHeight.M_10)
                                {
                                    _add["Speed"] = _meteo.MetMasts[index].MetDataSorted[j].Speed.Metres10.Mean.ToString("#.00");
                                }
                                else if (_meteo.MetrHeader.Speed.Measured == MeteoData.MeteoSample.HeightInfo.MeasuringHeight.ROT ||
                                    _meteo.MetrHeader.Speed.Measured == MeteoData.MeteoSample.HeightInfo.MeasuringHeight.BOTH)
                                {
                                    _add["Speed"] = _meteo.MetMasts[index].MetDataSorted[j].Speed.MetresRt.Mean.ToString("#.00");
                                }

                                // lastly, definitely helps to add the row to the table
                                _thisTable.Rows.Add(_add);
                            }

                            count++;

                            if (count % 1000 == 0)
                            {
                                if (progress != null)
                                {
                                    progress.Report((int)((double)100 / _assetList.Count * i +
                                     (double)j * 100 / _meteo.MetMasts[index].MetDataSorted.Count / _assetList.Count));
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            return _tableList;
        }

        #endregion

        #region Event Data Retrieval Tasks

        public void EventData(ScadaData _scadaFile, EventData _thisEvent, bool _averagesComputed)
        {
            // get index of the asset and get index of the event time in the asset
            // the index of the asset to be used below
            int assetIndex = _scadaFile.WindFarm.FindIndex(x => x.UnitID == _thisEvent.SourceAsset);

            // public accessor method: first get generic info, then averages
            EventDataRetrieval(_scadaFile.WindFarm[assetIndex], _thisEvent, false);
            if (_averagesComputed) { EventDataRetrieval(_fleetMeans, _thisEvent, true); }            
        }

        private void EventDataRetrieval(ScadaData.TurbineData _turbine, EventData _thisEv, bool _getAverage)
        {
            if (_getAverage)
            {
                _avgEventScadaOnly.Clear();
                _avgWeekBeforeInfo.Clear();
                _avgFullHistory.Clear();
            }
            else
            {
                _eventScadaOnly.Clear();
                _weekBeforeInfo.Clear();
                _fullHistory.Clear();
            }

            // do something part of the method
            // this one needs to take the event details and send it to another listbox plus graph

            // thisEvent has the event data only -- for the actual data to display, we'll need to find 
            // the datapoints from the source data
            // the index of the timestamp a week before the event began or otherwise the 
            // first timestamp in the series - long conditional but should work
            TimeSpan stepBack = new TimeSpan(-24 * 7, 0, 0);
            int weekIndex = _turbine.DataSorted
                .FindIndex(x => x.TimeStamp == _thisEv.EvTimes[0].Add(stepBack)) != -1 ? 
                _turbine.DataSorted.FindIndex(x => x.TimeStamp == _thisEv.EvTimes[0].Add(stepBack)) : 0;

            int timeIndex = _turbine.DataSorted.FindIndex(x => x.TimeStamp == _thisEv.EvTimes[0]);

            for (int i = 0; i < _thisEv.EvTimes.Count; i++)
            {
                if (_getAverage) { _avgEventScadaOnly.Add(_turbine.DataSorted[timeIndex + i]); }
                else { _eventScadaOnly.Add(_turbine.DataSorted[timeIndex + i]); }
            }

            for (int i = weekIndex; i < (timeIndex + _thisEv.EvTimes.Count); i++)
            {
                if (_getAverage) { _avgWeekBeforeInfo.Add(_turbine.DataSorted[i]); }
                else { _weekBeforeInfo.Add(_turbine.DataSorted[i]); }                
            }

            for (int i = 0; i < (timeIndex + _thisEv.EvTimes.Count); i++)
            {
                if (_getAverage) { _avgFullHistory.Add(_turbine.DataSorted[i]); }
                else { _fullHistory.Add(_turbine.DataSorted[i]); }                
            }
        }
        
        #endregion

        #region Data Processing

        #region Cross-type Event Tasks

        /// <summary>
        /// Method looks for matching events based on timestamps in the loaded power and wind datasets
        /// </summary>
        /// <param name="progress"></param>
        public void AssociateEvents(IProgress<int> progress)
        {
            try
            {
                _allPwrEvts.Clear();

                _noPwEvents = AssociateEvents(_noPwEvents, progress);
                _hiPwEvents = AssociateEvents(_hiPwEvents, progress, 50);

                foreach (EventData singleEvent in _noPwEvents)
                {
                    _allPwrEvts.Add(singleEvent);
                }

                foreach (EventData singleEvent in _hiPwEvents)
                {
                    _allPwrEvts.Add(singleEvent);
                }

                _allPwrEvts.OrderBy(o => o.Start);
            }
            catch { throw; }
        }

        private List<EventData> AssociateEvents(List<EventData> currentEvents, IProgress<int> progress, int start = 0)
        {
            try
            {
                int count = 0;

                // check all events that have been found
                for (int i = 0; i < currentEvents.Count; i++)
                {
                    // this boolean prevents the second loop from running if culprit is found already
                    bool goIntoHiSpEvents = true;

                    // start by checking in low speed events list
                    for (int j = 0; j < _loSpEvents.Count; j++)
                    {
                        // this code checks whether the asset these two events are from is the same
                        // and if it is, whether any of the datetimes also belong to the second list
                        if (currentEvents[i].SourceAsset == _loSpEvents[j].SourceAsset && 
                            currentEvents[i].EvTimes.Intersect(_loSpEvents[j].EvTimes).Any())
                        {
                            // if they do, we have confirmed the association, and we can break this loop
                            currentEvents[i].AssocEv = scada_analyst.EventData.EventAssoct.LO_SP;
                            goIntoHiSpEvents = false; break;
                        }
                    }

                    if (goIntoHiSpEvents)
                    {
                        // if we did not confirm the association before, we are in this loop

                        // check all the high speed events as well
                        for (int k = 0; k < _hiSpEvents.Count; k++)
                        {
                            // this code checks whether the asset these two events are from is the same
                            // and if it is, whether any of the datetimes also belong to the second list
                            if (currentEvents[i].SourceAsset == _hiSpEvents[k].SourceAsset && 
                                currentEvents[i].EvTimes.Intersect(_hiSpEvents[k].EvTimes).Any())
                            {
                                // and if the association is confirmed, we can break out from this loop
                                currentEvents[i].AssocEv = scada_analyst.EventData.EventAssoct.HI_SP;
                                break;
                            }
                        }
                    }

                    // if an association was not found, we need to classify it as 'OTHER'
                    if (currentEvents[i].AssocEv == scada_analyst.EventData.EventAssoct.NONE)
                    { currentEvents[i].AssocEv = scada_analyst.EventData.EventAssoct.OTHER; }

                    count++;

                    if (count % 50 == 0)
                    {
                        if (progress != null)
                        {
                            progress.Report((int)(start + 0.5 * i / currentEvents.Count * 100.0));
                        }
                    }
                }

                return currentEvents;
            }
            catch { throw; }
        }

        /// <summary>
        /// Removes events which have been matched based on their windspeed -- calls the OC method
        /// </summary>
        /// <param name="progress"></param>
        public void RemoveMatchedEvents(IProgress<int> progress)
        {
            try
            {
                _noPwEvents = RemovedMatchedEvents(_noPwEvents, progress);
                _hiPwEvents = RemovedMatchedEvents(_hiPwEvents, progress, 50);
            }
            catch { throw; }
        }

        private List<EventData> RemovedMatchedEvents(List<EventData> currentEvents, IProgress<int> progress, int start = 0)
        {
            try
            {
                int count = 0;

                for (int i = currentEvents.Count - 1; i >= 0; i--)
                {
                    if (currentEvents[i].AssocEv == scada_analyst.EventData.EventAssoct.LO_SP ||
                        currentEvents[i].AssocEv == scada_analyst.EventData.EventAssoct.HI_SP)
                    {
                        currentEvents.RemoveAt(i);
                    }

                    count++;

                    if (count % 10 == 0)
                    {
                        if (progress != null)
                        {
                            progress.Report((int)(start + 0.5 * i / currentEvents.Count * 100.0));
                        }
                    }
                }

                return currentEvents;
            }
            catch { throw; }
        }

        #endregion

        #region Day-time Tasks

        /// <summary>
        /// Removes events which have been qualified as likely to be not during the right time of day. Extension of this method
        /// also checks that the removed time is not between likely work hours in order to account for the early morning and 
        /// evening sunshine in the respective hemispheric summers.
        /// </summary>
        /// <param name="progress"></param>

        /// <summary>
        /// Processes loaded power events and returns the collection with the respective time-of-day fields
        /// </summary>
        /// <param name="currentEvents"></param>
        /// <param name="progress"></param>
        /// <returns></returns>
        public void AddDaytimesToEvents(IProgress<int> progress)
        {
            try
            {
                _allPwrEvts.Clear();

                _noPwEvents = AddDaytimesToEvents(_noPwEvents, progress);
                _hiPwEvents = AddDaytimesToEvents(_hiPwEvents, progress, 50);

                foreach (EventData singleEvent in _noPwEvents)
                {
                    _allPwrEvts.Add(singleEvent);
                }

                foreach (EventData singleEvent in _hiPwEvents)
                {
                    _allPwrEvts.Add(singleEvent);
                }

                _allPwrEvts.OrderBy(o => o.Start);
            }
            catch { throw; }
        }

        private List<EventData> AddDaytimesToEvents(List<EventData> _currentEvents, IProgress<int> progress, int start = 0)
        {
            // this method will contain the search for whether a non power production
            // event took place during the day or during the night by calculating the 
            // relevant sunrise and sunset for that day

            int count = 0;

            // check all events that have been found
            for (int i = 0; i < _currentEvents.Count; i++)
            {
                // find out which structure we are working with
                Structure _asset = _assetList.Where(x => x.UnitID == _currentEvents[i].SourceAsset).FirstOrDefault();

                // assign the time of day to this event
                _currentEvents[i].DayTime = scada_analyst.EventData.GetEventDayTime(_currentEvents[i], _asset);

                count++;

                if (count % 10 == 0)
                {
                    if (progress != null)
                    {
                        progress.Report((int)(start + 0.5 * i / _currentEvents.Count * 100.0));
                    }
                }
            }

            return _currentEvents;
        }

        public void RemoveProcessedDaytimes(IProgress<int> progress)
        {
            try
            {
                _noPwEvents = RemoveProcessedDaytimes(_noPwEvents, progress);
                _hiPwEvents = RemoveProcessedDaytimes(_hiPwEvents, progress, 50);
            }
            catch { throw; }
        }

        private List<EventData> RemoveProcessedDaytimes(List<EventData> currentEvents, IProgress<int> progress, int start = 0)
        {
            int count = 0;

            for (int i = currentEvents.Count - 1; i >= 0; i--)
            {
                // condition here checks whether the event started before or after the likely working hours of that day
                // and does not proceed into the method if it did not
                if (currentEvents[i].Start.TimeOfDay > WorkHoursMorning &&
                    currentEvents[i].Start.TimeOfDay < WorkHoursEvening)
                {
                    if (currentEvents[i].DayTime == scada_analyst.EventData.TimeOfEvent.NIGHTTM && MainWindow.Mnt_Night)
                    {
                        currentEvents.RemoveAt(i);
                    }
                    else if (currentEvents[i].DayTime == scada_analyst.EventData.TimeOfEvent.AS_DAWN && MainWindow.Mnt_AstDw)
                    {
                        currentEvents.RemoveAt(i);
                    }
                    else if (currentEvents[i].DayTime == scada_analyst.EventData.TimeOfEvent.NA_DAWN && MainWindow.Mnt_NauDw)
                    {
                        currentEvents.RemoveAt(i);
                    }
                    else if (currentEvents[i].DayTime == scada_analyst.EventData.TimeOfEvent.CI_DAWN && MainWindow.Mnt_CivDw)
                    {
                        currentEvents.RemoveAt(i);
                    }
                    else if (currentEvents[i].DayTime == scada_analyst.EventData.TimeOfEvent.DAYTIME && MainWindow.Mnt_Daytm)
                    {
                        currentEvents.RemoveAt(i);
                    }
                    else if (currentEvents[i].DayTime == scada_analyst.EventData.TimeOfEvent.CI_DUSK && MainWindow.Mnt_CivDs)
                    {
                        currentEvents.RemoveAt(i);
                    }
                    else if (currentEvents[i].DayTime == scada_analyst.EventData.TimeOfEvent.NA_DUSK && MainWindow.Mnt_NauDs)
                    {
                        currentEvents.RemoveAt(i);
                    }
                    else if (currentEvents[i].DayTime == scada_analyst.EventData.TimeOfEvent.AS_DUSK && MainWindow.Mnt_AstDs)
                    {
                        currentEvents.RemoveAt(i);
                    }
                }

                count++;

                if (count % 10 == 0)
                {
                    if (progress != null)
                    {
                        progress.Report((int)(start + 0.5 * i / currentEvents.Count * 100.0));
                    }
                }
            }

            return currentEvents;
        }

        #endregion 

        #region Event Comparison Tasks

        /// <summary>
        /// A set of methods to investigate the events and to compare them to normal behaviour in one turbine to begin with
        /// and then after that in a set of turbines based on their proximity.
        /// </summary>
        public void TurbineEventComparison(EventCheckType task)
        {
            // now I need to decide how to go about this -- first option should be to check against events from the same turbine
            // to see whether the temperatures were similar say one year before and one month before at the same power level
            //
            // then an option should enable checking whether other turbines in the vicinity experienced any downtimes in a 
            // similar timeframe

            if (task == EventCheckType.SAME_TURBINE)
            {
                // this side should check the performance of the same turbine temperature-wise at similar power outputs in the past
                TurbinePastPerformanceCheck();
            }
            else if (task == EventCheckType.NEARBY_TRBNS)
            {
                // this side should go and check whether any nearby turbines experienced similar behaviour under conditions
                TurbineNeighbourPerformanceCheck();
            }
        }

        public void TurbinePastPerformanceCheck()
        {
            // investigate same power outputs at different times in the past

        }

        public void TurbineNeighbourPerformanceCheck()
        {
            // investigate turbine events at nearby neighbours around the same time of the event

        }

        public enum EventCheckType
        {
            // keys to check what's going on and which type of comparison we want to go into
            SAME_TURBINE,
            NEARBY_TRBNS
        }

        #endregion

        #region Event Duration Tasks

        /// <summary>
        /// Duration filter takes a timespan and uses it to remove shorter events from loaded events list. In this implementation
        /// this works on both no power production and high power production events
        /// </summary>
        /// <param name="progress"></param>
        public void RemoveByDuration(IProgress<int> progress)
        {
            try
            {
                _noPwEvents = RemoveByDuration(_noPwEvents, progress);
                _hiPwEvents = RemoveByDuration(_hiPwEvents, progress, 50);
            }
            catch { throw; }
        }

        private List<EventData> RemoveByDuration(List<EventData> currentEvents, IProgress<int> progress, int start = 0)
        {
            try
            {
                int count = 0;

                for (int i = currentEvents.Count - 1; i >= 0; i--)
                {
                    if (currentEvents[i].Durat < _duratFilter)
                    {
                        currentEvents.RemoveAt(i);
                    }

                    count++;

                    if (count % 10 == 0)
                    {
                        if (progress != null)
                        {
                            progress.Report((int)(start + 0.5 * i / currentEvents.Count * 100.0));
                        }
                    }
                }

                return currentEvents;
            }
            catch { throw; }
        }

        #endregion 
        
        #region Fleet-wise Means and Difference

        public ScadaData FleetStats(ScadaData _inputData, IProgress<int> progress)
        {
            ScadaData _holdingFile = new ScadaData(_inputData);

            try
            {
                // create new scadaData instance to contain the average variables and assign it 
                // to the declared variable; create a new turbine data within that
                _fleetMeans = new ScadaData.TurbineData();

                // the two methods below get the actual average values and populate the file in this class
                // lastly these get sorted to prevent trouble
                FleetTotalValues(_holdingFile, progress);
                GetFleetAverages(progress, 50);
                _fleetMeans.DataSorted = _fleetMeans.Data.OrderBy(o => o.TimeStamp).ToList();

                // this computes the delta values for all separate samples
                CalculateDeltas(_holdingFile, progress, 55);

                // re-sorts in case something is amiss
                _holdingFile = SortScada(_holdingFile);
            }
            catch { }

            // we return the scadaFile because we have populated the Delta fields in all relevant areas
            return _holdingFile;
        }

        /// <summary>
        /// This function calculates the fleet-wise average of several variables.
        /// </summary>
        /// <param name="scadaFile"></param>
        private void FleetTotalValues(ScadaData scadaFile, IProgress<int> progress)
        {
            int count = 0;

            // need to decide which variables we are using for this process; thinking main bearing only for now and if needed
            // the method is easily extendable

            // we need some sort of timeaverage for every sample without having the guarantee that every sample exists
            // for every turbine

            // need code to iterate through every turbine to find the specific value 
            
            //this needs to happen for every windfarm we have
            
            for (int i = 0; i < scadaFile.WindFarm.Count; i++)
            {
                // and for every sample in every windfarm
                for (int j = 0; j < scadaFile.WindFarm[i].DataSorted.Count; j++)
                {                
                    if (!_fleetMeans.InclSamples.Contains(scadaFile.WindFarm[i].DataSorted[j].TimeStamp))
                    {
                        // if the averages' file already does not contain this, we can add a new DateTime to it
                        _fleetMeans.Data.Add(new ScadaData.ScadaSample());
                        _fleetMeans.InclSamples.Add(scadaFile.WindFarm[i].DataSorted[j].TimeStamp);

                        _fleetMeans.Data[_fleetMeans.Data.Count - 1].TimeStamp = scadaFile.WindFarm[i].DataSorted[j].TimeStamp;
                    }

                    // get index as the first thing
                    // the index allows to determine where in the mean file the new value should be input
                    int index = _fleetMeans.Data
                        .IndexOf(_fleetMeans.Data.Where(x => x.TimeStamp == scadaFile.WindFarm[i].DataSorted[j].TimeStamp)
                        .FirstOrDefault());

                    ProcessAverageDataValues(scadaFile.WindFarm[i].DataSorted[j], index);

                    count++;

                    if (count % 1000 == 0)
                    {
                        if (progress != null)
                        {
                            progress.Report((int)((scadaFile.WindFarm[i].DataSorted.Count * i + j) * 50.0 / (scadaFile.WindFarm.Count * scadaFile.WindFarm[i].DataSorted.Count)));
                        }
                    }
                }
            }
        }

        private void ProcessAverageDataValues(ScadaData.ScadaSample thisSample, int index)
        {
            // this tuple should return the required values for every input option
            #region Main Variables

            Tuple<double, double> a01 = IncrementAverage(_fleetMeans.Data[index].AmbTemps.Mean, _fleetMeans.Data[index].AmbTemps.Maxm, thisSample.AmbTemps.Mean);
            _fleetMeans.Data[index].AmbTemps.Mean = a01.Item1;
            _fleetMeans.Data[index].AmbTemps.Maxm = a01.Item2;

            Tuple<double, double> a02 = IncrementAverage(_fleetMeans.Data[index].Power.Mean, _fleetMeans.Data[index].Power.Maxm, thisSample.Power.Mean);
            _fleetMeans.Data[index].Power.Mean = a02.Item1;
            _fleetMeans.Data[index].Power.Maxm = a02.Item2;

            Tuple<double, double> a03 = IncrementAverage(_fleetMeans.Data[index].Nacel.Temp.Mean, _fleetMeans.Data[index].Nacel.Temp.Maxm, thisSample.Nacel.Temp.Mean);
            _fleetMeans.Data[index].Nacel.Temp.Mean = a03.Item1;
            _fleetMeans.Data[index].Nacel.Temp.Maxm = a03.Item2;

            #endregion

            #region Gearbox

            Tuple<double, double> c00 = IncrementAverage(_fleetMeans.Data[index].Gearbox.OilTemp.Mean, _fleetMeans.Data[index].Gearbox.OilTemp.Maxm, thisSample.Gearbox.OilTemp.Mean);
            _fleetMeans.Data[index].Gearbox.OilTemp.Mean = c00.Item1;
            _fleetMeans.Data[index].Gearbox.OilTemp.Maxm = c00.Item2;
            Tuple<double, double> c01 = IncrementAverage(_fleetMeans.Data[index].Gearbox.HsGen.Mean, _fleetMeans.Data[index].Gearbox.HsGen.Maxm, thisSample.Gearbox.HsGen.Mean);
            _fleetMeans.Data[index].Gearbox.HsGen.Mean = c01.Item1;
            _fleetMeans.Data[index].Gearbox.HsGen.Maxm = c01.Item2;
            Tuple<double, double> c02 = IncrementAverage(_fleetMeans.Data[index].Gearbox.HsRot.Mean, _fleetMeans.Data[index].Gearbox.HsRot.Maxm, thisSample.Gearbox.HsRot.Mean);
            _fleetMeans.Data[index].Gearbox.HsRot.Mean = c02.Item1;
            _fleetMeans.Data[index].Gearbox.HsRot.Maxm = c02.Item2;
            Tuple<double, double> c03 = IncrementAverage(_fleetMeans.Data[index].Gearbox.ImsGen.Mean, _fleetMeans.Data[index].Gearbox.ImsGen.Maxm, thisSample.Gearbox.ImsGen.Mean);
            _fleetMeans.Data[index].Gearbox.ImsGen.Mean = c03.Item1;
            _fleetMeans.Data[index].Gearbox.ImsGen.Maxm = c03.Item2;
            Tuple<double, double> c04 = IncrementAverage(_fleetMeans.Data[index].Gearbox.ImsRot.Mean, _fleetMeans.Data[index].Gearbox.ImsRot.Maxm, thisSample.Gearbox.ImsRot.Mean);
            _fleetMeans.Data[index].Gearbox.ImsRot.Mean = c04.Item1;
            _fleetMeans.Data[index].Gearbox.ImsRot.Maxm = c04.Item2;

            #endregion

            #region Generator

            Tuple<double, double> b00 = IncrementAverage(_fleetMeans.Data[index].Genny.BearingR.Mean, _fleetMeans.Data[index].Genny.BearingR.Maxm, thisSample.Genny.BearingR.Mean);
            _fleetMeans.Data[index].Genny.BearingR.Mean = b00.Item1;
            _fleetMeans.Data[index].Genny.BearingR.Maxm = b00.Item2;
            Tuple<double, double> b01 = IncrementAverage(_fleetMeans.Data[index].Genny.BearingG.Mean, _fleetMeans.Data[index].Genny.BearingG.Maxm, thisSample.Genny.BearingG.Mean);
            _fleetMeans.Data[index].Genny.BearingG.Mean = b01.Item1;
            _fleetMeans.Data[index].Genny.BearingG.Maxm = b01.Item2;
            Tuple<double, double> b02 = IncrementAverage(_fleetMeans.Data[index].Genny.G1u1.Mean, _fleetMeans.Data[index].Genny.G1u1.Maxm, thisSample.Genny.G1u1.Mean);
            _fleetMeans.Data[index].Genny.G1u1.Mean = b02.Item1;
            _fleetMeans.Data[index].Genny.G1u1.Maxm = b02.Item2;
            Tuple<double, double> b03 = IncrementAverage(_fleetMeans.Data[index].Genny.G1v1.Mean, _fleetMeans.Data[index].Genny.G1v1.Maxm, thisSample.Genny.G1v1.Mean);
            _fleetMeans.Data[index].Genny.G1v1.Mean = b03.Item1;
            _fleetMeans.Data[index].Genny.G1v1.Maxm = b03.Item2;
            Tuple<double, double> b04 = IncrementAverage(_fleetMeans.Data[index].Genny.G1w1.Mean, _fleetMeans.Data[index].Genny.G1w1.Maxm, thisSample.Genny.G1w1.Mean);
            _fleetMeans.Data[index].Genny.G1w1.Mean = b04.Item1;
            _fleetMeans.Data[index].Genny.G1w1.Maxm = b04.Item2;
            Tuple<double, double> b05 = IncrementAverage(_fleetMeans.Data[index].Genny.G2u1.Mean, _fleetMeans.Data[index].Genny.G2u1.Maxm, thisSample.Genny.G2u1.Mean);
            _fleetMeans.Data[index].Genny.G2u1.Mean = b05.Item1;
            _fleetMeans.Data[index].Genny.G2u1.Maxm = b05.Item2;
            Tuple<double, double> b06 = IncrementAverage(_fleetMeans.Data[index].Genny.G2v1.Mean, _fleetMeans.Data[index].Genny.G2v1.Maxm, thisSample.Genny.G2v1.Mean);
            _fleetMeans.Data[index].Genny.G2v1.Mean = b06.Item1;
            _fleetMeans.Data[index].Genny.G2v1.Maxm = b06.Item2;
            Tuple<double, double> b07 = IncrementAverage(_fleetMeans.Data[index].Genny.G2w1.Mean, _fleetMeans.Data[index].Genny.G2w1.Maxm, thisSample.Genny.G2w1.Mean);
            _fleetMeans.Data[index].Genny.G2w1.Mean = b07.Item1;
            _fleetMeans.Data[index].Genny.G2w1.Maxm = b07.Item2;
            Tuple<double, double> b08 = IncrementAverage(_fleetMeans.Data[index].Genny.RPMs.Mean, _fleetMeans.Data[index].Genny.RPMs.Maxm, thisSample.Genny.RPMs.Mean);
            _fleetMeans.Data[index].Genny.RPMs.Mean = b08.Item1;
            _fleetMeans.Data[index].Genny.RPMs.Maxm = b08.Item2;

            #endregion

            #region Main Bearing

            Tuple<double, double> a10 = IncrementAverage(_fleetMeans.Data[index].MainBear.Gs.Mean, _fleetMeans.Data[index].MainBear.Gs.Maxm, thisSample.MainBear.Gs.Mean);
            _fleetMeans.Data[index].MainBear.Gs.Mean = a10.Item1;
            _fleetMeans.Data[index].MainBear.Gs.Maxm = a10.Item2;
            Tuple<double, double> a11 = IncrementAverage(_fleetMeans.Data[index].MainBear.Hs.Mean, _fleetMeans.Data[index].MainBear.Hs.Maxm, thisSample.MainBear.Hs.Mean);
            _fleetMeans.Data[index].MainBear.Hs.Mean = a11.Item1;
            _fleetMeans.Data[index].MainBear.Hs.Maxm = a11.Item2;
            Tuple<double, double> a12 = IncrementAverage(_fleetMeans.Data[index].MainBear.Main.Mean, _fleetMeans.Data[index].MainBear.Main.Maxm, thisSample.MainBear.Main.Mean);
            _fleetMeans.Data[index].MainBear.Main.Mean = a12.Item1;
            _fleetMeans.Data[index].MainBear.Main.Maxm = a12.Item2;

            #endregion
        }

        private Tuple<double, double> IncrementAverage(double oldValue, double incrementor, double incrementingValue)
        {
            // this should only work though if the value !IsNaN and the previous value !IsNaN
            if (double.IsNaN(oldValue)) { oldValue = 0; }

            // if the incrementor IsNaN, then also give back 0
            if (double.IsNaN(incrementor)) { incrementor = 0; }

            if (!double.IsNaN(incrementingValue))
            {
                // this adds the code *but* we also need to keep track of how many times we have done this
                oldValue += incrementingValue;
                incrementor++;
            }
            // no else needed, if it is NaN then we can ignore it without problem

            return new Tuple<double, double>(oldValue, incrementor);
        }

        private void GetFleetAverages(IProgress<int> progress, int start = 0)
        {
            int count = 0;

            // lastly the incrementor needs to be used to get the average for all of the timestamps
            for (int i = 0; i < _fleetMeans.Data.Count; i++)
            {
                _fleetMeans.Data[i].AmbTemps.Mean = _fleetMeans.Data[i].AmbTemps.Mean / _fleetMeans.Data[i].AmbTemps.Maxm;
                _fleetMeans.Data[i].Power.Mean = _fleetMeans.Data[i].Power.Mean / _fleetMeans.Data[i].Power.Maxm;
                _fleetMeans.Data[i].Nacel.Temp.Mean = _fleetMeans.Data[i].Nacel.Temp.Mean / _fleetMeans.Data[i].Nacel.Temp.Maxm;

                _fleetMeans.Data[i].Gearbox.OilTemp.Mean = _fleetMeans.Data[i].Gearbox.OilTemp.Mean / _fleetMeans.Data[i].Gearbox.OilTemp.Maxm;
                _fleetMeans.Data[i].Gearbox.HsGen.Mean = _fleetMeans.Data[i].Gearbox.HsGen.Mean / _fleetMeans.Data[i].Gearbox.HsGen.Maxm;
                _fleetMeans.Data[i].Gearbox.HsRot.Mean = _fleetMeans.Data[i].Gearbox.HsRot.Mean / _fleetMeans.Data[i].Gearbox.HsRot.Maxm;
                _fleetMeans.Data[i].Gearbox.ImsGen.Mean = _fleetMeans.Data[i].Gearbox.ImsGen.Mean / _fleetMeans.Data[i].Gearbox.ImsGen.Maxm;
                _fleetMeans.Data[i].Gearbox.ImsRot.Mean = _fleetMeans.Data[i].Gearbox.ImsRot.Mean / _fleetMeans.Data[i].Gearbox.ImsRot.Maxm;

                _fleetMeans.Data[i].Genny.BearingG.Mean = _fleetMeans.Data[i].Genny.BearingG.Mean / _fleetMeans.Data[i].Genny.BearingG.Maxm;
                _fleetMeans.Data[i].Genny.BearingR.Mean = _fleetMeans.Data[i].Genny.BearingR.Mean / _fleetMeans.Data[i].Genny.BearingR.Maxm;
                _fleetMeans.Data[i].Genny.RPMs.Mean = _fleetMeans.Data[i].Genny.RPMs.Mean / _fleetMeans.Data[i].Genny.RPMs.Maxm;
                _fleetMeans.Data[i].Genny.G1u1.Mean = _fleetMeans.Data[i].Genny.G1u1.Mean / _fleetMeans.Data[i].Genny.G1u1.Maxm;
                _fleetMeans.Data[i].Genny.G1v1.Mean = _fleetMeans.Data[i].Genny.G1v1.Mean / _fleetMeans.Data[i].Genny.G1v1.Maxm;
                _fleetMeans.Data[i].Genny.G1w1.Mean = _fleetMeans.Data[i].Genny.G1w1.Mean / _fleetMeans.Data[i].Genny.G1w1.Maxm;
                _fleetMeans.Data[i].Genny.G2u1.Mean = _fleetMeans.Data[i].Genny.G2u1.Mean / _fleetMeans.Data[i].Genny.G2u1.Maxm;
                _fleetMeans.Data[i].Genny.G2v1.Mean = _fleetMeans.Data[i].Genny.G2v1.Mean / _fleetMeans.Data[i].Genny.G2v1.Maxm;
                _fleetMeans.Data[i].Genny.G2w1.Mean = _fleetMeans.Data[i].Genny.G2w1.Mean / _fleetMeans.Data[i].Genny.G2w1.Maxm;

                _fleetMeans.Data[i].MainBear.Gs.Mean = _fleetMeans.Data[i].MainBear.Gs.Mean / _fleetMeans.Data[i].MainBear.Gs.Maxm;
                _fleetMeans.Data[i].MainBear.Hs.Mean = _fleetMeans.Data[i].MainBear.Hs.Mean / _fleetMeans.Data[i].MainBear.Hs.Maxm;
                _fleetMeans.Data[i].MainBear.Main.Mean = _fleetMeans.Data[i].MainBear.Main.Mean / _fleetMeans.Data[i].MainBear.Main.Maxm;

                count++;

                if (count % 500 == 0)
                {
                    if (progress != null)
                    {
                        progress.Report((int)(start + 0.05 * i / _fleetMeans.Data.Count * 100.0));
                    }
                }
            }
        }

        /// <summary>
        /// This functions calculates the specific deviation for every datapoint for whom the 
        /// average value was calculated.
        /// </summary>
        /// <param name="scadaFile"></param>
        private void CalculateDeltas(ScadaData scadaFile, IProgress<int> progress, int start = 0)
        {
            int count = 0;

            // for this to happen well, the full dataset needs to be made into a local copy 
            // here that contains the relevant information for everything

            for (int i = 0; i < scadaFile.WindFarm.Count; i++)
            {
                for (int j = 0; j < scadaFile.WindFarm[i].DataSorted.Count; j++)
                {
                    // get index as the first thing
                    int index = _fleetMeans.DataSorted
                        .IndexOf(_fleetMeans.DataSorted.Where(x => x.TimeStamp == scadaFile.WindFarm[i].DataSorted[j].TimeStamp)
                        .FirstOrDefault());

                    ScadaData.ScadaSample thisSample = scadaFile.WindFarm[i].DataSorted[j];
                    ScadaData.ScadaSample flytSample = _fleetMeans.DataSorted[index];

                    // doing the calculation this way round means that a negative difference is equal to a spec value
                    // which is lower than the fleet average, and a positive difference is above the fleet average
                    thisSample.AmbTemps.Dlta = thisSample.AmbTemps.Mean - flytSample.AmbTemps.Mean;
                    thisSample.Power.Dlta = thisSample.Power.Mean - flytSample.Power.Mean;
                    thisSample.Nacel.Temp.Dlta = thisSample.Nacel.Temp.Mean - flytSample.Nacel.Temp.Mean;

                    thisSample.Gearbox.OilTemp.Dlta = thisSample.Gearbox.OilTemp.Mean - flytSample.Gearbox.OilTemp.Mean;
                    thisSample.Gearbox.HsGen.Dlta = thisSample.Gearbox.HsGen.Mean - flytSample.Gearbox.HsGen.Mean;
                    thisSample.Gearbox.HsRot.Dlta = thisSample.Gearbox.HsRot.Mean - flytSample.Gearbox.HsRot.Mean;
                    thisSample.Gearbox.ImsGen.Dlta = thisSample.Gearbox.ImsGen.Mean - flytSample.Gearbox.ImsGen.Mean;
                    thisSample.Gearbox.ImsRot.Dlta = thisSample.Gearbox.ImsRot.Mean - flytSample.Gearbox.ImsRot.Mean;

                    thisSample.Genny.BearingG.Dlta = thisSample.Genny.BearingG.Mean - flytSample.Genny.BearingG.Mean;
                    thisSample.Genny.BearingR.Dlta = thisSample.Genny.BearingR.Mean - flytSample.Genny.BearingR.Mean;
                    thisSample.Genny.RPMs.Dlta = thisSample.Genny.RPMs.Mean - flytSample.Genny.RPMs.Mean;
                    thisSample.Genny.G1u1.Dlta = thisSample.Genny.G1u1.Mean - flytSample.Genny.G1u1.Mean;
                    thisSample.Genny.G1v1.Dlta = thisSample.Genny.G1v1.Mean - flytSample.Genny.G1v1.Mean;
                    thisSample.Genny.G1w1.Dlta = thisSample.Genny.G1w1.Mean - flytSample.Genny.G1w1.Mean;
                    thisSample.Genny.G2u1.Dlta = thisSample.Genny.G2u1.Mean - flytSample.Genny.G2u1.Mean;
                    thisSample.Genny.G2v1.Dlta = thisSample.Genny.G2v1.Mean - flytSample.Genny.G2v1.Mean;
                    thisSample.Genny.G2w1.Dlta = thisSample.Genny.G2w1.Mean - flytSample.Genny.G2w1.Mean;

                    thisSample.MainBear.Gs.Dlta = thisSample.MainBear.Gs.Mean - flytSample.MainBear.Gs.Mean;
                    thisSample.MainBear.Hs.Dlta = thisSample.MainBear.Hs.Mean - flytSample.MainBear.Hs.Mean;
                    thisSample.MainBear.Main.Dlta = thisSample.MainBear.Main.Mean - flytSample.MainBear.Main.Mean;

                    count++;

                    if (count % 1000 == 0)
                    {
                        if (progress != null)
                        {
                            progress.Report((int)(start + (scadaFile.WindFarm[i].DataSorted.Count * i + j)*45.0 / (scadaFile.WindFarm.Count * scadaFile.WindFarm[i].DataSorted.Count)));
                        }
                    }
                }
            }
        }

        private ScadaData SortScada(ScadaData scadaFile)
        {
            // note that this needs to work on the Sorted info as already all changes have taken place there
            for (int i = 0; i < scadaFile.WindFarm.Count; i++)
            {
                scadaFile.WindFarm[i].DataSorted = scadaFile.WindFarm[i].DataSorted.OrderBy(o => o.TimeStamp).ToList();
            }

            return scadaFile;
        }

        #endregion

        #region Site Geography

        /// <summary>
        /// Bool on whether locations could be added to loaded assets or not
        /// </summary>
        /// <returns></returns>
        public bool AddStructureLocations(GeoData geoFile, MeteoData meteoFile, ScadaData scadaFile,
            bool scadaLoaded, bool meteoLoaded, bool geoLoaded)
        {
            try
            {
                if (geoLoaded && (meteoLoaded || scadaLoaded))
                {
                    for (int i = 0; i < geoFile.GeoInfo.Count; i++)
                    {
                        if (meteoLoaded)
                        {
                            // if we dealing with metmasts, go in here
                            for (int ij = 0; ij < meteoFile.MetMasts.Count; ij++)
                            {
                                //if their IDs match, they are the same units
                                if (geoFile.GeoInfo[i].AssetID == meteoFile.MetMasts[ij].UnitID)
                                {
                                    // match positions from one to other
                                    meteoFile.MetMasts[ij].Position = geoFile.GeoInfo[i].Position;

                                    // get index for the loaded assets overview
                                    int assetIndex = _assetList.IndexOf(
                                        _assetList.Where(x => x.UnitID == meteoFile.MetMasts[ij].UnitID).FirstOrDefault());

                                    // assign position based on the index above and also assign the true value
                                    _assetList[assetIndex].Position = geoFile.GeoInfo[i].Position;
                                    _assetList[assetIndex].PositionsLoaded = meteoFile.MetMasts[ij].PositionsLoaded = true;

                                    break;
                                }
                            }
                        }

                        if (scadaLoaded)
                        {
                            // if we have turbines, use this method - should be an exact copy of the above
                            for (int ik = 0; ik < scadaFile.WindFarm.Count; ik++)
                            {
                                if (geoFile.GeoInfo[i].AssetID == scadaFile.WindFarm[ik].UnitID)
                                {
                                    scadaFile.WindFarm[ik].Position = geoFile.GeoInfo[i].Position;

                                    int assetIndex = _assetList.IndexOf(
                                        _assetList.Where(x => x.UnitID == scadaFile.WindFarm[ik].UnitID).FirstOrDefault());

                                    _assetList[assetIndex].Position = geoFile.GeoInfo[i].Position;
                                    _assetList[assetIndex].PositionsLoaded = scadaFile.WindFarm[ik].PositionsLoaded = true;

                                    break;
                                }
                            }
                        }
                    }

                    return true;
                }
                else { return false; }

            }
            catch { throw; }
        }

        #endregion

        #region Structure Distance Tasks

        /// <summary>
        /// Get distances between all of the various loaded in positions; doesn't use the full extent
        /// of the geographic data file, but only loaded assets with meteorological or SCADA data.
        /// </summary>
        /// <param name="theseAssets"></param>
        public void GetDistances(List<Structure> theseAssets)
        {
            GetDistancesToEachAsset(theseAssets);
        }

        private void GetDistancesToEachAsset(List<Structure> theseAssets)
        {
            _intervals = new List<Distances>();

            // this method will take the loaded assets and create a resultant object for distances
            // from each one to each other one

            // for each asset there will be n-1 distances to other assets, hence a list of results
            // the object must hold both the originating asset and all the ones originating from it

            for (int i = 0; i < theseAssets.Count; i++)
            {
                // for every asset we have, the same thing needs to happen but we also need to ignore this asset
                for (int j = 0; j < theseAssets.Count; j++)
                {
                    if (i != j)
                    {
                        // then get geographic distance using the suitable formula
                        double thisDistance = GetGeographicDistance(theseAssets[i].Position, theseAssets[j].Position);

                        // add this to the list of distances
                        _intervals.Add(new Distances(theseAssets[i].UnitID, theseAssets[j].UnitID, thisDistance));
                    }
                }
            }
        }

        private double GetGeographicDistance(GridPosition position1, GridPosition position2)
        {
            double latMid, m_per_deg_lat, m_per_deg_lon, deltaLat, deltaLon;

            latMid = (position1.Latitude + position2.Latitude) / 2.0;

            m_per_deg_lat = 111132.954 - 559.822 * Math.Cos(2.0 * latMid) + 1.175 * Math.Cos(4.0 * latMid);
            m_per_deg_lon = (Math.PI / 180) * 6367449 * Math.Cos(latMid);

            deltaLat = Math.Abs(position1.Latitude - position2.Latitude);
            deltaLon = Math.Abs(position1.Longitude - position2.Longitude);

            return Math.Round(Math.Sqrt(Math.Pow(deltaLat * m_per_deg_lat, 2) + Math.Pow(deltaLon * m_per_deg_lon, 2)), 3);
        }

        #endregion

        #endregion
        
        #region Support Classes

        public class AnalyticLimit : ObservableObject
        {
            #region Variables

            private double _maxVars;
            private double _minVars;
            private string _varName;
            private Equipment _type;

            #endregion

            #region Constructor

            public AnalyticLimit(Equipment type, string variable, double minimum, double maximum)
            {
                _maxVars = maximum;
                _minVars = minimum;
                _varName = variable;
                _type = type;
            }

            #endregion 

            public enum Equipment
            {
                GEARBOX,
                GENERATOR,
                MAIN_BEAR
            }

            #region Properties

            public double MaxVars { get { return _maxVars; } set { _maxVars = value; } }
            public double MinVars { get { return _minVars; } set { _minVars = value; } }
            public string VarName { get { return _varName; } set { _varName = value; } }

            public Equipment Type { get { return _type; } set { _type = value; } }

            #endregion
        }

        public class Distances : ObservableObject
        {
            #region Variables

            private int _from;
            private int _to;

            private double _interval;

            #endregion

            #region Constructor

            public Distances() { }

            public Distances(int from, int to, double distance)
            {
                _from = from;
                _to = to;
                _interval = distance;
            }

            #endregion

            #region Properties

            public int From { get { return _from; } set { _from = value; } }
            public int To { get { return _to; } set { _to = value; } }

            public double Intervals { get { return _interval; } set { _interval = value; } }

            #endregion
        }

        public class StructureSmry : Structure
        {
            /// <summary>
            /// This class should be able to count for each set of events what the number of 
            /// specific events of various durations and types exist for it.
            /// </summary>

            #region Variables
                
            private EventsCounter _hiWinds;
            private EventsCounter _loWinds;
            private EventsCounter _noPower;
            private EventsCounter _hiPower;

            private MetaDataCounter _bearInfo;
            private MetaDataCounter _capaInfo;
            private MetaDataCounter _windData;

            #endregion

            #region Constructor

            public StructureSmry(Structure thisAsset, List<EventData> loWindEvents, List<EventData> hiWindEvents,
                List<EventData> noPowrEvents, List<EventData> hiPowrEvents)
            {
                this.Type = thisAsset.Type;
                this.UnitID = thisAsset.UnitID;

                this.Bearings = thisAsset.Bearings;
                this.Capacity = thisAsset.Capacity;
                this.WindInfo = thisAsset.WindInfo;

                //the below also needs to take into account the right asset ID only
                _hiWinds = new EventsCounter(this.UnitID, scada_analyst.EventData.WeatherType.HI_SPD, hiWindEvents);
                _loWinds = new EventsCounter(this.UnitID, scada_analyst.EventData.WeatherType.LO_SPD, loWindEvents);

                if (this.Type == Types.TURBINE)
                {
                    _hiPower = new EventsCounter(this.UnitID, scada_analyst.EventData.PwrProdType.HI_PWR, hiPowrEvents);
                    _noPower = new EventsCounter(this.UnitID, scada_analyst.EventData.PwrProdType.NO_PWR, noPowrEvents);
                }
            }

            public StructureSmry(Structure thisAsset, int _year)
            {
                this.Type = thisAsset.Type;
                this.UnitID = thisAsset.UnitID;

                this.Bearings = thisAsset.Bearings;
                this.Capacity = thisAsset.Capacity;
                this.WindInfo = thisAsset.WindInfo;

                for (int i = 0; i < Bearings.Years.Count; i++)
                {
                    if (Bearings.Years[i].YearName == _year)
                    {
                        _bearInfo = new MetaDataCounter(Bearings.Years[i].MonthlyData);
                        _bearInfo.Overall = Bearings.Years[i].ValStr;
                        break;
                    }
                }

                for (int i = 0; i < Capacity.Years.Count; i++)
                {
                    if (Capacity.Years[i].YearName == _year)
                    {
                        _capaInfo = new MetaDataCounter(Capacity.Years[i].MonthlyData);
                        _capaInfo.Overall = Capacity.Years[i].ValStr;
                        break;
                    }
                }

                for (int i = 0; i < WindInfo.Years.Count; i++)
                {
                    if (WindInfo.Years[i].YearName == _year)
                    {
                        _windData = new MetaDataCounter(WindInfo.Years[i].MonthlyData);
                        _windData.Overall = WindInfo.Years[i].ValStr;
                        break;
                    }
                }
            }

            #endregion 

            #region Support Classes

            public class EventsCounter : ObservableObject
            {
                #region Variables

                private int _shortEvs = 0;
                private int _deciMins = 0;
                private int _hourLong = 0;
                private int _manyHors = 0;
                private int _daysLong = 0;

                private EventType _cntrType = EventType.UNKNOWN;

                #endregion

                #region Constructor

                public EventsCounter(int asset, EventData.PwrProdType thisType, List<EventData> theseEvents)
                {
                    _cntrType = thisType == scada_analyst.EventData.PwrProdType.HI_PWR ? EventType.HI_PWR : EventType.LO_PWR;

                    AssignCounters(asset, theseEvents);
                }

                public EventsCounter(int asset, EventData.WeatherType thisType, List<EventData> theseEvents)
                {
                    _cntrType = thisType == scada_analyst.EventData.WeatherType.HI_SPD ? EventType.HI_SPD : EventType.LO_SPD;

                    AssignCounters(asset, theseEvents);
                }

                #endregion 

                private void AssignCounters(int asset, List<EventData> theseEvents)
                {
                    _shortEvs = AssessEvents(asset, theseEvents, scada_analyst.EventData.EvtDuration.SHORT);
                    _deciMins = AssessEvents(asset, theseEvents, scada_analyst.EventData.EvtDuration.DECIMINS);
                    _hourLong = AssessEvents(asset, theseEvents, scada_analyst.EventData.EvtDuration.HOURS);
                    _manyHors = AssessEvents(asset, theseEvents, scada_analyst.EventData.EvtDuration.MANYHOURS);
                    _daysLong = AssessEvents(asset, theseEvents, scada_analyst.EventData.EvtDuration.DAYS);
                }

                private int AssessEvents(int asset, List<EventData> theseEvents, EventData.EvtDuration counter)
                {
                    return theseEvents.Where(id => id.SourceAsset == asset).Count(x => x.EvtDrtn == counter);
                }

                #region Support Classes

                public enum EventType
                {
                    UNKNOWN,
                    LO_SPD,
                    HI_SPD,
                    LO_PWR,
                    HI_PWR
                }

                #endregion

                #region Properties

                public int ShortEvs { get { return _shortEvs; } set { _shortEvs = value; } }
                public int DeciMins { get { return _deciMins; } set { _deciMins = value; } }
                public int HourLong { get { return _hourLong; } set { _hourLong = value; } }
                public int ManyHors { get { return _manyHors; } set { _manyHors = value; } }
                public int DaysLong { get { return _daysLong; } set { _daysLong = value; } }

                public EventType CntrType { get { return _cntrType; } set { _cntrType = value; } }

                #endregion
            }
            
            public class MetaDataCounter : ObservableObject
            {
                #region Variables

                private string _overall = "";

                private string _jan = "";
                private string _feb = "";
                private string _mar = "";
                private string _apr = "";
                private string _may = "";
                private string _jun = "";
                private string _jul = "";
                private string _aug = "";
                private string _sep = "";
                private string _oct = "";
                private string _nov = "";
                private string _dec = "";

                #endregion

                #region Constructor

                public MetaDataCounter(DataTable _input)
                {
                    _jan = _input.Rows[0].ItemArray[0].ToString();
                    _feb = _input.Rows[0].ItemArray[1].ToString();
                    _mar = _input.Rows[0].ItemArray[2].ToString();
                    _apr = _input.Rows[0].ItemArray[3].ToString();
                    _may = _input.Rows[0].ItemArray[4].ToString();
                    _jun = _input.Rows[0].ItemArray[5].ToString();
                    _jul = _input.Rows[0].ItemArray[6].ToString();
                    _aug = _input.Rows[0].ItemArray[7].ToString();
                    _sep = _input.Rows[0].ItemArray[8].ToString();
                    _oct = _input.Rows[0].ItemArray[9].ToString();
                    _nov = _input.Rows[0].ItemArray[10].ToString();
                    _dec = _input.Rows[0].ItemArray[11].ToString();
                }

                #endregion

                #region Properties

                public string Overall { get { return _overall; } set { _overall = value; } }

                public string Jan { get { return _jan; } set { _jan = value; } }
                public string Feb { get { return _feb; } set { _feb = value; } }
                public string Mar { get { return _mar; } set { _mar = value; } }
                public string Apr { get { return _apr; } set { _apr = value; } }
                public string May { get { return _may; } set { _may = value; } }
                public string Jun { get { return _jun; } set { _jun = value; } }
                public string Jul { get { return _jul; } set { _jul = value; } }
                public string Aug { get { return _aug; } set { _aug = value; } }
                public string Sep { get { return _sep; } set { _sep = value; } }
                public string Oct { get { return _oct; } set { _oct = value; } }
                public string Nov { get { return _nov; } set { _nov = value; } }
                public string Dec { get { return _dec; } set { _dec = value; } }

                #endregion
            }

            #endregion

            #region Properties

            public EventsCounter HiWinds { get { return _hiWinds; } set { _hiWinds = value; } }
            public EventsCounter LoWinds { get { return _loWinds; } set { _loWinds = value; } }
            public EventsCounter NoPower { get { return _noPower; } set { _noPower = value; } }
            public EventsCounter HiPower { get { return _hiPower; } set { _hiPower = value; } }

            public MetaDataCounter BearInfo { get { return _bearInfo; } set { _bearInfo = value; } }
            public MetaDataCounter CapaInfo { get { return _capaInfo; } set { _capaInfo = value; } }
            public MetaDataCounter WindData { get { return _windData; } set { _windData = value; } }

            #endregion
        }

        #endregion

        #region Properties

        public double CutIn { get { return _cutIn; } set { _cutIn = value; } }
        public double CutOut { get { return _cutOut; } set { _cutOut = value; } }
        public double PowerLim { get { return _powerLim; } set { _powerLim = value; } }
        public double RatedPwr { get { return _ratedPwr; } set { _ratedPwr = value; } }

        public string DurationString
        {
            get { return "Duration Filter: " + DuratFilter.ToString(); }
            set
            {
                if (DurationString != value)
                {
                    DurationString = value;
                    OnPropertyChanged(nameof(DurationString));
                }
            }
        }

        public TimeSpan ScadaSeprtr { get { return _scadaSeprtr; } set { _scadaSeprtr = value; } }
        public TimeSpan DuratFilter { get { return _duratFilter; } set { _duratFilter = value; } }
        public TimeSpan WorkHoursMorning { get { return _workHrsMorning; } set { _workHrsMorning = value; } }
        public TimeSpan WorkHoursEvening { get { return _workHrsEvening; } set { _workHrsEvening = value; } }

        public ScadaData.TurbineData FleetMeans { get { return _fleetMeans; } set { _fleetMeans = value; } }

        public List<EventData> AllWtrEvts { get { return _allWtrEvts; } set { _allWtrEvts = value; } }
        public List<EventData> LoSpEvents { get { return _loSpEvents; } set { _loSpEvents = value; } }
        public List<EventData> HiSpEvents { get { return _hiSpEvents; } set { _hiSpEvents = value; } }
        public List<EventData> NoPwEvents { get { return _noPwEvents; } set { _noPwEvents = value; } }
        public List<EventData> RtPwEvents { get { return _hiPwEvents; } set { _hiPwEvents = value; } }
        public List<EventData> AllPwrEvts { get { return _allPwrEvts; } set { _allPwrEvts = value; } }
        public List<EventData> ThresEvnts { get { return _thresEvnts; } set { _thresEvnts = value; } }
        public List<EventData> RChngEvnts { get { return _rChngEvnts; } set { _rChngEvnts = value; } }

        public List<ScadaData.ScadaSample> ThisEvScada { get { return _eventScadaOnly; } set { _eventScadaOnly = value; } }
        public List<ScadaData.ScadaSample> WeekHistory { get { return _weekBeforeInfo; } set { _weekBeforeInfo = value; } }
        public List<ScadaData.ScadaSample> HistEventData { get { return _fullHistory; } set { _fullHistory = value; } }

        public List<ScadaData.ScadaSample> AvgThisEvScada { get { return _avgEventScadaOnly; } set { _avgEventScadaOnly = value; } }
        public List<ScadaData.ScadaSample> AvgWeekHistory { get { return _avgWeekBeforeInfo; } set { _avgWeekBeforeInfo = value; } }
        public List<ScadaData.ScadaSample> AvgHistEventData { get { return _avgFullHistory; } set { _avgFullHistory = value; } }

        public List<AnalyticLimit> Thresholds { get { return _thresholds; } set { _thresholds = value; } }
        public List<AnalyticLimit> RateChange { get { return _rateChange; } set { _rateChange = value; } }
        public List<Distances> Intervals { get { return _intervals; } set { _intervals = value; } }
        public List<Structure> AssetList { get { return _assetList; } set { _assetList = value; } }

        public List<StructureSmry> Summary()
        {
            List<StructureSmry> _temp = new List<StructureSmry>();

            for (int i = 0; i < _assetList.Count; i++)
            {
                _temp.Add(new StructureSmry(_assetList[i], _loSpEvents, _hiSpEvents, _noPwEvents, _hiPwEvents));
            }

            return _temp;
        }

        public List<StructureSmry> GeneralSummary(int _year)
        {
            List<StructureSmry> _temp = new List<StructureSmry>();

            for (int i = 0; i < _assetList.Count; i++)
            {
                _temp.Add(new StructureSmry(_assetList[i], _year));
            }

            return _temp;
        }

        #endregion
    }
}
