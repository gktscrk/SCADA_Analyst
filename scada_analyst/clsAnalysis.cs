using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using scada_analyst.Shared;

namespace scada_analyst
{
    class Analysis : ObservableObject
    {
        #region Variables

        private List<EventData> _allWtrEvts = new List<EventData>();
        private List<EventData> _loSpEvents = new List<EventData>();
        private List<EventData> _hiSpEvents = new List<EventData>();
        private List<EventData> _noPwEvents = new List<EventData>();
        private List<EventData> _rtPwEvents = new List<EventData>();
        private List<EventData> _allPwrEvts = new List<EventData>();

        private List<Structure> _assetList = new List<Structure>();
        private List<Distances> _intervals = new List<Distances>();

        #endregion

        #region Constructor

        public Analysis() { }

        #endregion 

        #region Reset Events

        public void ResetEventList()
        {
            //this method resets the eventlist in case the user so wishes
            for (int i = 0; i < AllPwrEvts.Count; i++)
            {
                if (AllPwrEvts[i].PwrProd == EventData.PwrProdType.NOPROD)
                {
                    NoPwEvents.Add(AllPwrEvts[i]);
                }
                else if (AllPwrEvts[i].PwrProd == EventData.PwrProdType.RATEDP)
                {
                    RtPwEvents.Add(AllPwrEvts[i]);
                }
            }
        }

        #endregion

        #region Analysis Methods

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
                _rtPwEvents = AssociateEvents(_rtPwEvents, progress, 50);

                foreach (EventData singleEvent in _noPwEvents)
                {
                    _allPwrEvts.Add(singleEvent);
                }

                foreach (EventData singleEvent in _rtPwEvents)
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

                for (int i = 0; i < currentEvents.Count; i++)
                {
                    bool goIntoHiSpEvents = true;

                    for (int j = 0; j < _loSpEvents.Count; j++)
                    {
                        if (currentEvents[i].EvTimes.Intersect(_loSpEvents[j].EvTimes).Any())
                        {
                            currentEvents[i].AssocEv = EventData.EventAssoct.LO_SP;

                            goIntoHiSpEvents = false;
                            break;
                        }
                    }

                    if (goIntoHiSpEvents)
                    {
                        for (int k = 0; k < _hiSpEvents.Count; k++)
                        {
                            if (currentEvents[i].EvTimes.Intersect(_hiSpEvents[k].EvTimes).Any())
                            {
                                currentEvents[i].AssocEv = EventData.EventAssoct.HI_SP;

                                break;
                            }
                        }
                    }

                    if (currentEvents[i].AssocEv == EventData.EventAssoct.NONE)
                    { currentEvents[i].AssocEv = EventData.EventAssoct.OTHER; }

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

        /// <summary>
        /// Removes events which have been matched based on their windspeed -- calls the OC method
        /// </summary>
        /// <param name="progress"></param>
        public void RemoveMatchedEvents(IProgress<int> progress)
        {
            try
            {
                _noPwEvents = RemovedMatchedEvents(_noPwEvents, progress);
                _rtPwEvents = RemovedMatchedEvents(_rtPwEvents, progress, 50);
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
                    if (currentEvents[i].AssocEv == EventData.EventAssoct.LO_SP ||
                        currentEvents[i].AssocEv == EventData.EventAssoct.HI_SP)
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
                _rtPwEvents = AddDaytimesToEvents(_rtPwEvents, progress, 50);

                foreach (EventData singleEvent in _noPwEvents)
                {
                    _allPwrEvts.Add(singleEvent);
                }

                foreach (EventData singleEvent in _rtPwEvents)
                {
                    _allPwrEvts.Add(singleEvent);
                }

                _allPwrEvts.OrderBy(o => o.Start);
            }
            catch { throw; }
        }

        private List<EventData> AddDaytimesToEvents(List<EventData> currentEvents, IProgress<int> progress, int start = 0)
        {
            // this method will contain the search for whether a non power production
            // event took place during the day or during the night by calculating the 
            // relevant sunrise and sunset for that day

            int count = 0;

            for (int i = 0; i < currentEvents.Count; i++)
            {
                Structure asset = _assetList.Where(x => x.UnitID == currentEvents[i].FromAsset).FirstOrDefault();

                currentEvents[i].DayTime = EventData.GetEventDayTime(currentEvents[i], asset);

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

        public void RemoveProcessedDaytimes(IProgress<int> progress)
        {
            try
            {
                _noPwEvents = RemoveProcessedDaytimes(_noPwEvents, progress);
                _rtPwEvents = RemoveProcessedDaytimes(_rtPwEvents, progress, 50);
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
                if (currentEvents[i].Start.TimeOfDay > MainWindow.WorkHoursMorning &&
                    currentEvents[i].Start.TimeOfDay < MainWindow.WorkHoursEvening)
                {
                    if (currentEvents[i].DayTime == EventData.TimeOfEvent.NIGHTTM && MainWindow.Mnt_Night)
                    {
                        currentEvents.RemoveAt(i);
                    }
                    else if (currentEvents[i].DayTime == EventData.TimeOfEvent.AS_DAWN && MainWindow.Mnt_AstDw)
                    {
                        currentEvents.RemoveAt(i);
                    }
                    else if (currentEvents[i].DayTime == EventData.TimeOfEvent.NA_DAWN && MainWindow.Mnt_NauDw)
                    {
                        currentEvents.RemoveAt(i);
                    }
                    else if (currentEvents[i].DayTime == EventData.TimeOfEvent.CI_DAWN && MainWindow.Mnt_CivDw)
                    {
                        currentEvents.RemoveAt(i);
                    }
                    else if (currentEvents[i].DayTime == EventData.TimeOfEvent.DAYTIME && MainWindow.Mnt_Daytm)
                    {
                        currentEvents.RemoveAt(i);
                    }
                    else if (currentEvents[i].DayTime == EventData.TimeOfEvent.CI_DUSK && MainWindow.Mnt_CivDs)
                    {
                        currentEvents.RemoveAt(i);
                    }
                    else if (currentEvents[i].DayTime == EventData.TimeOfEvent.NA_DUSK && MainWindow.Mnt_NauDs)
                    {
                        currentEvents.RemoveAt(i);
                    }
                    else if (currentEvents[i].DayTime == EventData.TimeOfEvent.AS_DUSK && MainWindow.Mnt_AstDs)
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
                _rtPwEvents = RemoveByDuration(_rtPwEvents, progress, 50);
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
                    if (currentEvents[i].Durat < MainWindow.DuratFilter)
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

        #region Finding Events

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
                            if (scadaFile.WindFarm[i].DataSorted[j].Powers.Mean < MainWindow.PowerLim &&
                                scadaFile.WindFarm[i].DataSorted[j].Powers.Mean != -9999)
                            {
                                List<ScadaData.ScadaSample> thisEvent = new List<ScadaData.ScadaSample>();
                                thisEvent.Add(scadaFile.WindFarm[i].DataSorted[j]);

                                for (int k = j + 1; k < scadaFile.WindFarm[i].DataSorted.Count; k++)
                                {
                                    if (scadaFile.WindFarm[i].DataSorted[k].DeltaTime > MainWindow.ScadaSeprtr)
                                    {
                                        j = k; break;
                                    }

                                    if (scadaFile.WindFarm[i].DataSorted[k].Powers.Mean > MainWindow.PowerLim)
                                    {
                                        j = k; break;
                                    }
                                    else if (k == scadaFile.WindFarm[i].DataSorted.Count - 1) { j = k; }

                                    thisEvent.Add(scadaFile.WindFarm[i].DataSorted[k]);
                                }

                                _noPwEvents.Add(new EventData(thisEvent, EventData.PwrProdType.NOPROD));
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
                            if (scadaFile.WindFarm[i].DataSorted[j].Powers.Mean >= MainWindow.RatedPwr &&
                                scadaFile.WindFarm[i].DataSorted[j].Powers.Mean != -9999)
                            {
                                List<ScadaData.ScadaSample> thisEvent = new List<ScadaData.ScadaSample>();
                                thisEvent.Add(scadaFile.WindFarm[i].DataSorted[j]);

                                for (int k = j + 1; k < scadaFile.WindFarm[i].DataSorted.Count; k++)
                                {
                                    if (scadaFile.WindFarm[i].DataSorted[k].DeltaTime > MainWindow.ScadaSeprtr)
                                    {
                                        j = k; break;
                                    }

                                    if (scadaFile.WindFarm[i].DataSorted[k].Powers.Mean < MainWindow.RatedPwr)
                                    {
                                        j = k; break;
                                    }
                                    else if (k == scadaFile.WindFarm[i].DataSorted.Count - 1) { j = k; }

                                    thisEvent.Add(scadaFile.WindFarm[i].DataSorted[k]);
                                }

                                _rtPwEvents.Add(new EventData(thisEvent, EventData.PwrProdType.RATEDP));
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
                            if (meteoFile.MetMasts[i].MetDataSorted[j].WSpdR.Mean < MainWindow.CutIn &&
                                meteoFile.MetMasts[i].MetDataSorted[j].WSpdR.Mean >= 0)
                            {
                                List<MeteoData.MeteoSample> thisEvent = new List<MeteoData.MeteoSample>();
                                thisEvent.Add(meteoFile.MetMasts[i].MetDataSorted[j]);

                                for (int k = j + 1; k < meteoFile.MetMasts[i].MetDataSorted.Count; k++)
                                {
                                    if (meteoFile.MetMasts[i].MetDataSorted[k].DeltaTime > new TimeSpan(0, 10, 0))
                                    {
                                        j = k; break;
                                    }

                                    if (meteoFile.MetMasts[i].MetDataSorted[k].WSpdR.Mean > MainWindow.CutIn)
                                    {
                                        j = k; break;
                                    }
                                    else if (k == meteoFile.MetMasts[i].MetDataSorted.Count - 1) { j = k; }

                                    thisEvent.Add(meteoFile.MetMasts[i].MetDataSorted[k]);
                                }

                                _loSpEvents.Add(new EventData(thisEvent, EventData.WeatherType.LOW_SP));
                                _allWtrEvts.Add(new EventData(thisEvent, EventData.WeatherType.LOW_SP));
                            }
                            else if (meteoFile.MetMasts[i].MetDataSorted[j].WSpdR.Mean > MainWindow.CutOut)
                            {
                                List<MeteoData.MeteoSample> thisEvent = new List<MeteoData.MeteoSample>();
                                thisEvent.Add(meteoFile.MetMasts[i].MetDataSorted[j]);

                                for (int k = j + 1; k < meteoFile.MetMasts[i].MetDataSorted.Count; k++)
                                {
                                    if (meteoFile.MetMasts[i].MetDataSorted[k].DeltaTime > new TimeSpan(0, 10, 0))
                                    {
                                        j = k; break;
                                    }

                                    if (meteoFile.MetMasts[i].MetDataSorted[k].WSpdR.Mean < MainWindow.CutOut)
                                    {
                                        j = k; break;
                                    }
                                    else if (k == meteoFile.MetMasts[i].MetDataSorted.Count - 1) { j = k; }

                                    thisEvent.Add(meteoFile.MetMasts[i].MetDataSorted[k]);
                                }

                                _hiSpEvents.Add(new EventData(thisEvent, EventData.WeatherType.HI_SPD));
                                _allWtrEvts.Add(new EventData(thisEvent, EventData.WeatherType.HI_SPD));
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
                            if (scadaFile.WindFarm[i].DataSorted[j].AnemoM.ActWinds.Mean < MainWindow.CutIn &&
                                scadaFile.WindFarm[i].DataSorted[j].AnemoM.ActWinds.Mean >= 0)
                            {
                                List<ScadaData.ScadaSample> thisEvent = new List<ScadaData.ScadaSample>();
                                thisEvent.Add(scadaFile.WindFarm[i].DataSorted[j]);

                                for (int k = j + 1; k < scadaFile.WindFarm[i].DataSorted.Count; k++)
                                {
                                    if (scadaFile.WindFarm[i].DataSorted[k].DeltaTime > new TimeSpan(0, 10, 0))
                                    {
                                        j = k; break;
                                    }

                                    if (scadaFile.WindFarm[i].DataSorted[k].AnemoM.ActWinds.Mean > MainWindow.CutIn)
                                    {
                                        j = k; break;
                                    }
                                    else if (k == scadaFile.WindFarm[i].DataSorted.Count - 1) { j = k; }

                                    thisEvent.Add(scadaFile.WindFarm[i].DataSorted[k]);
                                }

                                _loSpEvents.Add(new EventData(thisEvent, EventData.WeatherType.LOW_SP));
                                _allWtrEvts.Add(new EventData(thisEvent, EventData.WeatherType.LOW_SP));
                            }
                            else if (scadaFile.WindFarm[i].DataSorted[j].AnemoM.ActWinds.Mean > MainWindow.CutOut)
                            {
                                List<ScadaData.ScadaSample> thisEvent = new List<ScadaData.ScadaSample>();
                                thisEvent.Add(scadaFile.WindFarm[i].DataSorted[j]);

                                for (int k = j + 1; k < scadaFile.WindFarm[i].DataSorted.Count; k++)
                                {
                                    if (scadaFile.WindFarm[i].DataSorted[k].DeltaTime > new TimeSpan(0, 10, 0))
                                    {
                                        j = k; break;
                                    }

                                    if (scadaFile.WindFarm[i].DataSorted[k].AnemoM.ActWinds.Mean < MainWindow.CutOut)
                                    {
                                        j = k; break;
                                    }
                                    else if (k == scadaFile.WindFarm[i].DataSorted.Count - 1) { j = k; }

                                    thisEvent.Add(scadaFile.WindFarm[i].DataSorted[k]);
                                }

                                _hiSpEvents.Add(new EventData(thisEvent, EventData.WeatherType.HI_SPD));
                                _allWtrEvts.Add(new EventData(thisEvent, EventData.WeatherType.HI_SPD));
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

        public class Distances : ObservableObject
        {
            #region Variables

            private int _from;
            private int _to;

            private double _interval;

            #endregion

            public Distances() { }

            public Distances(int from, int to, double distance)
            {
                _from = from;
                _to = to;
                _interval = distance;
            }

            #region Properties

            public int From { get { return _from; } set { _from = value; } }
            public int To { get { return _to; } set { _to = value; } }

            public double Intervals { get { return _interval; } set { _interval = value; } }

            #endregion
        }

        #endregion

        #region Properties

        public List<EventData> AllWtrEvts { get { return _allWtrEvts; } set { _allWtrEvts = value; } }
        public List<EventData> LoSpEvents { get { return _loSpEvents; } set { _loSpEvents = value; } }
        public List<EventData> HiSpEvents { get { return _hiSpEvents; } set { _hiSpEvents = value; } }
        public List<EventData> NoPwEvents { get { return _noPwEvents; } set { _noPwEvents = value; } }
        public List<EventData> RtPwEvents { get { return _rtPwEvents; } set { _rtPwEvents = value; } }
        public List<EventData> AllPwrEvts { get { return _allPwrEvts; } set { _allPwrEvts = value; } }

        public List<Structure> AssetsList { get { return _assetList; } set { _assetList = value; } }
        public List<Distances> Intervals { get { return _intervals; } set { _intervals = value; } }

        #endregion
    }
}
