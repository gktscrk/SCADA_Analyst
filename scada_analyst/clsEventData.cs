using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using scada_analyst.Shared;

namespace scada_analyst
{
    public class EventData : BaseEventData
    {
        #region Variables

        private double _averagePwr = 0;
        private double _meanNclTmp = 0;
        private double _extrmPwr = 0;
        private double _extrmSpd = 0;

        private TimeSpan sampleLen = new TimeSpan(0, 9, 59);

        private AnomalyType anomaly = AnomalyType.NOANOMALY;
        private EventAssoct assocEv = EventAssoct.NONE;
        private EventSource eSource = EventSource.UNKNOWN;
        private EvtDuration evtDrtn = EvtDuration.UNKNOWN;
        private PwrProdType pwrProd = PwrProdType.NORMAL;
        private TimeOfEvent dayTime = TimeOfEvent.UNKNOWN;
        private WeatherType weather = WeatherType.NORMAL;

        #endregion

        #region Constructor

        public EventData() { }

        #endregion

        #region Create Events

        public EventData(List<MeteoData.MeteoSample> data, WeatherType input)
        {
            FromAsset = data[0].AssetID;

            Start = data[0].TimeStamp;
            Finit = data[data.Count - 1].TimeStamp.Add(sampleLen);

            Durat = Finit - Start;

            eSource = EventSource.METMAST;
            Type = Types.WEATHER;
            weather = input;

            for (int i = 0; i < data.Count; i++)
            {
                if (input == WeatherType.LO_SPD)
                {
                    if (i == 0) { _extrmSpd = data[i].WSpdR.Mean; }

                    if (data[i].WSpdR.Mean < _extrmSpd) { _extrmSpd = data[i].WSpdR.Mean; }
                }
                else if (input == WeatherType.HI_SPD)
                {
                    if (i == 0) { _extrmSpd = data[i].WSpdR.Mean; }

                    if (data[i].WSpdR.Mean > _extrmSpd) { _extrmSpd = data[i].WSpdR.Mean; }
                }

                EvTimes.Add(data[i].TimeStamp);
            }

            SetEventDuration();
        }

        public EventData(List<ScadaData.ScadaSample> data, WeatherType input)
        {
            FromAsset = data[0].AssetID;

            Start = data[0].TimeStamp;
            Finit = data[data.Count - 1].TimeStamp.Add(sampleLen);

            Durat = Finit - Start;

            eSource = EventSource.TURBINE;
            Type = Types.WEATHER;
            weather = input;

            for (int i = 0; i < data.Count; i++)
            {
                if (input == WeatherType.LO_SPD)
                {
                    if (i == 0) { _extrmSpd = data[i].AnemoM.ActWinds.Mean; }

                    if (data[i].AnemoM.ActWinds.Mean < _extrmSpd) { _extrmSpd = data[i].AnemoM.ActWinds.Mean; }
                }
                else if (input == WeatherType.HI_SPD)
                {
                    if (i == 0) { _extrmSpd = data[i].AnemoM.ActWinds.Mean; }

                    if (data[i].AnemoM.ActWinds.Mean > _extrmSpd) { _extrmSpd = data[i].AnemoM.ActWinds.Mean; }
                }

                EvTimes.Add(data[i].TimeStamp);
            }

            SetEventDuration();
        }

        public EventData(List<ScadaData.ScadaSample> data, PwrProdType input)
        {
            FromAsset = data[0].AssetID;

            Start = data[0].TimeStamp;
            Finit = data[data.Count - 1].TimeStamp.Add(sampleLen);
            
            pwrProd = input;
            eSource = EventSource.TURBINE;
            Type = Types.NOPOWER;

            for (int i = 0; i < data.Count; i++)
            {
                if (pwrProd == PwrProdType.NO_PWR)
                {
                    if (i == 0) { _extrmPwr = data[i].Powers.Mean; }

                    if (data[i].Powers.Mean < _extrmPwr) { _extrmPwr = data[i].Powers.Mean; }
                }
                else if (pwrProd == PwrProdType.HI_PWR)
                {
                    if (i == 0) { _extrmPwr = data[i].Powers.Mean; }

                    if (data[i].Powers.Mean > _extrmPwr) { _extrmPwr = data[i].Powers.Mean; }
                }

                EvTimes.Add(data[i].TimeStamp);
            }

            SetEventDuration();
        }

        public EventData(List<ScadaData.ScadaSample> data, AnomalyType input)
        {
            FromAsset = data[0].AssetID;

            Start = data[0].TimeStamp;
            Finit = data[data.Count - 1].TimeStamp.Add(sampleLen);

            anomaly = input;
            eSource = EventSource.TURBINE;
            Type = Types.UNKNOWN;
            
            for (int i = 0; i < data.Count; i++)
            {
                if (anomaly == AnomalyType.THRS_BEAR)
                {
                    if (i == 0) { _extrmPwr = data[i].MainBear.Standards.Mean; }

                    if (data[i].MainBear.Standards.Mean > _extrmPwr) { _extrmPwr = data[i].MainBear.Standards.Mean; }
                }
                else if (anomaly == AnomalyType.THRS_BEAR_GS)
                {
                    if (i == 0) { _extrmPwr = data[i].MainBear.Gs.Mean; }

                    if (data[i].MainBear.Gs.Mean > _extrmPwr) { _extrmPwr = data[i].MainBear.Gs.Mean; }
                }
                else if (anomaly == AnomalyType.THRS_BEAR_HS)
                {
                    if (i == 0) { _extrmPwr = data[i].MainBear.Hs.Mean; }

                    if (data[i].MainBear.Hs.Mean > _extrmPwr) { _extrmPwr = data[i].MainBear.Hs.Mean; }
                }
                else if (anomaly == AnomalyType.THRS_GEAR_HS_GENS)
                {
                    if (i == 0) { _extrmPwr = data[i].Gearbox.Hs.Gens.Mean; }

                    if (data[i].Gearbox.Hs.Gens.Mean > _extrmPwr) { _extrmPwr = data[i].Gearbox.Hs.Gens.Mean; }
                }
                else if (anomaly == AnomalyType.THRS_GEAR_HS_ROTS)
                {
                    if (i == 0) { _extrmPwr = data[i].Gearbox.Hs.Rots.Mean; }

                    if (data[i].Gearbox.Hs.Rots.Mean > _extrmPwr) { _extrmPwr = data[i].Gearbox.Hs.Rots.Mean; }
                }
                else if (anomaly == AnomalyType.THRS_GEAR_IM_GENS)
                {
                    if (i == 0) { _extrmPwr = data[i].Gearbox.Ims.Gens.Mean; }

                    if (data[i].Gearbox.Ims.Gens.Mean > _extrmPwr) { _extrmPwr = data[i].Gearbox.Ims.Gens.Mean; }
                }
                else if (anomaly == AnomalyType.THRS_GEAR_IM_ROTS)
                {
                    if (i == 0) { _extrmPwr = data[i].Gearbox.Ims.Rots.Mean; }

                    if (data[i].Gearbox.Ims.Rots.Mean > _extrmPwr) { _extrmPwr = data[i].Gearbox.Ims.Rots.Mean; }
                }
                else if (anomaly == AnomalyType.THRS_GEAR_OIL)
                {
                    if (i == 0) { _extrmPwr = data[i].Gearbox.Oils.Mean; }

                    if (data[i].Gearbox.Oils.Mean > _extrmPwr) { _extrmPwr = data[i].Gearbox.Oils.Mean; }
                }
                else if (anomaly == AnomalyType.THRS_GNNY_G)
                {
                    if (i == 0) { _extrmPwr = data[i].Genny.bearingG.Mean; }

                    if (data[i].Genny.bearingG.Mean > _extrmPwr) { _extrmPwr = data[i].Genny.bearingG.Mean; }
                }
                else if (anomaly == AnomalyType.THRS_GNNY_R)
                {
                    if (i == 0) { _extrmPwr = data[i].Genny.bearingR.Mean; }

                    if (data[i].Genny.bearingR.Mean > _extrmPwr) { _extrmPwr = data[i].Genny.bearingR.Mean; }
                }
                else if (anomaly == AnomalyType.THRS_GNNY_RPM)
                {
                    if (i == 0) { _extrmPwr = data[i].Genny.Rpms.Mean; }

                    if (data[i].Genny.Rpms.Mean > _extrmPwr) { _extrmPwr = data[i].Genny.Rpms.Mean; }
                }

                _averagePwr += data[i].Powers.Mean;
                _meanNclTmp += data[i].Nacel.Mean;

                EvTimes.Add(data[i].TimeStamp);
            }

            _averagePwr = _averagePwr / data.Count;
            _meanNclTmp = _meanNclTmp / data.Count;

            SetEventDuration();
        }

        /// <summary>
        /// Sets the enum "TimeOfEvent" for every event
        /// </summary>
        /// <param name="thisEvent"></param>
        /// <param name="thisStructure"></param>
        /// <returns></returns>
        public static TimeOfEvent GetEventDayTime(EventData thisEvent, Structure thisStructure)
        {
            double tsunrise, tsunsets, civcrise, civcsets, astrrise, astrsets, nautrise, nautsets;

            Sunriset.AstronomicalTwilight(thisEvent.Start.Year, thisEvent.Start.Month,
                thisEvent.Start.Day, thisStructure.Position.Latitude, thisStructure.Position.Longitude,
                out astrrise, out astrsets);

            TimeSpan astriseTime = TimeSpan.FromHours(astrrise);
            TimeSpan astsetsTime = TimeSpan.FromHours(astrsets);

            Sunriset.NauticalTwilight(thisEvent.Start.Year, thisEvent.Start.Month,
                thisEvent.Start.Day, thisStructure.Position.Latitude, thisStructure.Position.Longitude,
                out nautrise, out nautsets);

            TimeSpan nauriseTime = TimeSpan.FromHours(nautrise);
            TimeSpan nausetsTime = TimeSpan.FromHours(nautsets);

            Sunriset.CivilTwilight(thisEvent.Start.Year, thisEvent.Start.Month,
                thisEvent.Start.Day, thisStructure.Position.Latitude, thisStructure.Position.Longitude,
                out civcrise, out civcsets);

            TimeSpan civriseTime = TimeSpan.FromHours(civcrise);
            TimeSpan civsetsTime = TimeSpan.FromHours(civcsets);

            Sunriset.SunriseSunset(thisEvent.Start.Year, thisEvent.Start.Month,
                thisEvent.Start.Day, thisStructure.Position.Latitude, thisStructure.Position.Longitude,
                out tsunrise, out tsunsets);

            TimeSpan sunriseTime = TimeSpan.FromHours(tsunrise);
            TimeSpan sunsetsTime = TimeSpan.FromHours(tsunsets);

            if (thisEvent.Start.TimeOfDay <= astriseTime)
            {
                return EventData.TimeOfEvent.NIGHTTM;
            }
            else if (thisEvent.Start.TimeOfDay <= nauriseTime)
            {
                return EventData.TimeOfEvent.AS_DAWN;
            }
            else if (thisEvent.Start.TimeOfDay <= civriseTime)
            {
                return EventData.TimeOfEvent.NA_DAWN;
            }
            else if (thisEvent.Start.TimeOfDay <= sunriseTime)
            {
                return EventData.TimeOfEvent.CI_DAWN;
            }
            else if (thisEvent.Start.TimeOfDay <= sunsetsTime)
            {
                return EventData.TimeOfEvent.DAYTIME;
            }
            else if (thisEvent.Start.TimeOfDay <= civsetsTime)
            {
                return EventData.TimeOfEvent.CI_DUSK;
            }
            else if (thisEvent.Start.TimeOfDay <= nausetsTime)
            {
                return EventData.TimeOfEvent.NA_DUSK;
            }
            else if (thisEvent.Start.TimeOfDay <= astsetsTime)
            {
                return EventData.TimeOfEvent.AS_DUSK;
            }
            else
            {
                return EventData.TimeOfEvent.NIGHTTM;
            }
        }

        /// <summary>
        /// Get event durations and assign appropriate enumerators
        /// </summary>
        private void SetEventDuration()
        {
            // the definitions for the different no power production event
            // duration can be easily and accessibly changed here

            Durat = Finit - Start;

            if (Durat.TotalMinutes < (int)EvtDuration.DECIMINS) { evtDrtn = EvtDuration.SHORT; }
            else if (Durat.TotalMinutes < (int)EvtDuration.HOURS) { evtDrtn = EvtDuration.DECIMINS; }
            else if (Durat.TotalMinutes < (int)EvtDuration.MANYHOURS) { evtDrtn = EvtDuration.HOURS; }
            else if (Durat.TotalMinutes < (int)EvtDuration.DAYS) { evtDrtn = EvtDuration.MANYHOURS; }
            else if (Durat.TotalMinutes >= (int)EvtDuration.DAYS) { evtDrtn = EvtDuration.DAYS; }
        }

        #endregion

        #region Support Classes

        public enum EventAssoct
        {
            // an enum to list whether the power event is associated with a wind 
            // speed event or not -- other for not

            NONE,
            LO_SP,
            HI_SP,
            OTHER
        }

        public enum EventSource
        {
            // this enum is for marking the source which created this event

            UNKNOWN,
            METMAST,
            TURBINE
        }

        public enum EvtDuration
        {
            // this event marks the duration of the downtime for the event

            UNKNOWN = -1,
            SHORT = 0, // short for things below 30 minutes which are quite probably not worth thinking about
            DECIMINS = 30, // deciminutes
            HOURS = 2*60, // hours
            MANYHOURS = 8*60, // decihours
            DAYS = 2*24*60 // days
        }

        public enum PwrProdType
        {
            NORMAL,
            NO_PWR, // for no power production events
            HI_PWR  // for rated power
        }

        public enum TimeOfEvent
        {
            // an enum to list what time of day the event took place at

            UNKNOWN,
            AS_DAWN, // 18 deg to 12 deg sun below horizon
            NA_DAWN, // 12 deg to 6 deg sun below horizon
            CI_DAWN, // 6 deg below to sun at horizon 
            DAYTIME,
            CI_DUSK, // sun from horizon to 6 deg below
            NA_DUSK, // sun from 6 deg below to 12 deg below horizon
            AS_DUSK, // sun from 12 deg below to 18 deg below horizon
            NIGHTTM
        }

        public enum WeatherType
        {
            NORMAL,
            LO_SPD, // below cutin
            HI_SPD  // above cutout
        }

        public enum AnomalyType
        {
            NOANOMALY,
            THRS_BEAR,
            THRS_BEAR_GS,
            THRS_BEAR_HS,
            THRS_GEAR_OIL,
            THRS_GEAR_HS_GENS,
            THRS_GEAR_HS_ROTS,
            THRS_GEAR_IM_GENS,
            THRS_GEAR_IM_ROTS,
            THRS_GNNY_G,
            THRS_GNNY_R,
            THRS_GNNY_RPM
        }

        #endregion

        #region Properties

        public double AveragePwr { get { return _averagePwr; } set { _averagePwr = value; } }
        public double MeanNclTmp { get { return _meanNclTmp; } set { _meanNclTmp = value; } }
        public double ExtrmSpd { get { return _extrmSpd; } set { _extrmSpd = value; } }
        public double ExtrmPow { get { return _extrmPwr; } set { _extrmPwr = value; } }

        public string DisplayDayTime
        {
            get
            {
                if (DayTime == TimeOfEvent.NIGHTTM) { return "Night"; }
                else if (DayTime == TimeOfEvent.AS_DAWN) { return "Astronomical dawn"; }
                else if (DayTime == TimeOfEvent.NA_DAWN) { return "Nautical dawn"; }
                else if (DayTime == TimeOfEvent.CI_DAWN) { return "Civic dawn"; }
                else if (DayTime == TimeOfEvent.DAYTIME) { return "Day"; }
                else if (DayTime == TimeOfEvent.CI_DUSK) { return "Civic dusk"; }
                else if (DayTime == TimeOfEvent.NA_DUSK) { return "Nautical dusk"; }
                else if (DayTime == TimeOfEvent.AS_DUSK) { return "Astronomical dusk"; }
                else { return "Unknown"; }
            }
        }

        public string DisplayAssoctn
        {
            get
            {
                if (AssocEv == EventAssoct.NONE) { return "Unknown"; }
                else if (AssocEv == EventAssoct.LO_SP) { return "Low wind speeds"; }
                else if (AssocEv == EventAssoct.HI_SP) { return "High wind speeds"; }
                else { return "Other"; }
            }
        }

        public string TriggerVar
        {
            get
            {
                if (Anomaly == AnomalyType.NOANOMALY) { return "No anomaly"; }
                else if (Anomaly == AnomalyType.THRS_BEAR) { return "Main bearing"; }
                else if (Anomaly == AnomalyType.THRS_BEAR_GS) { return "Main bearing GS"; }
                else if (Anomaly == AnomalyType.THRS_BEAR_HS) { return "Main bearing HS"; }
                else if (Anomaly == AnomalyType.THRS_GEAR_HS_GENS) { return "Gearbox HS Gen. Side"; }
                else if (Anomaly == AnomalyType.THRS_GEAR_HS_ROTS) { return "Gearbox HS Rot. Side"; }
                else if (Anomaly == AnomalyType.THRS_GEAR_IM_GENS) { return "Gearbox IMS Gen. Side"; }
                else if (Anomaly == AnomalyType.THRS_GEAR_IM_ROTS) { return "Gearbox IMS Rot. Side"; }
                else if (Anomaly == AnomalyType.THRS_GEAR_OIL) { return "Gearbox Oil"; }
                else if (Anomaly == AnomalyType.THRS_GNNY_G) { return "Generator G-Bearing"; }
                else if (Anomaly == AnomalyType.THRS_GNNY_R) { return "Generator R-Bearing"; }
                else if (Anomaly == AnomalyType.THRS_GNNY_RPM) { return "Generator RPMs"; }
                else { return "Unknown"; }
            }
        }

        public AnomalyType Anomaly { get { return anomaly; } set { anomaly = value; } }
        public EventAssoct AssocEv { get { return assocEv; } set { assocEv = value; } }
        public EventSource ESource { get { return eSource; } set { eSource = value; } }
        public EvtDuration EvtDrtn { get { return evtDrtn; } set { evtDrtn = value; } }
        public PwrProdType PwrProd { get { return pwrProd; } set { pwrProd = value; } }
        public TimeOfEvent DayTime { get { return dayTime; } set { dayTime = value; } }
        public WeatherType Weather { get { return weather; } set { weather = value; } }

        #endregion
    }
}
