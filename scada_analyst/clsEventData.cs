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

        private double extrmSpd = 0;
        private double extrmPwr = 0;
        private string displayDayTime = "";
        private string displayAssoctn = "";

        private TimeSpan sampleLen = new TimeSpan(0, 9, 59);

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
                    if (i == 0) { extrmSpd = data[i].WSpdR.Mean; }

                    if (data[i].WSpdR.Mean < extrmSpd) { extrmSpd = data[i].WSpdR.Mean; }
                }
                else if (input == WeatherType.HI_SPD)
                {
                    if (i == 0) { extrmSpd = data[i].WSpdR.Mean; }

                    if (data[i].WSpdR.Mean > extrmSpd) { extrmSpd = data[i].WSpdR.Mean; }
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
                    if (i == 0) { extrmSpd = data[i].AnemoM.ActWinds.Mean; }

                    if (data[i].AnemoM.ActWinds.Mean < extrmSpd) { extrmSpd = data[i].AnemoM.ActWinds.Mean; }
                }
                else if (input == WeatherType.HI_SPD)
                {
                    if (i == 0) { extrmSpd = data[i].AnemoM.ActWinds.Mean; }

                    if (data[i].AnemoM.ActWinds.Mean > extrmSpd) { extrmSpd = data[i].AnemoM.ActWinds.Mean; }
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
                if (pwrProd == PwrProdType.NOPROD)
                {
                    if (i == 0) { extrmPwr = data[i].Powers.Mean; }

                    if (data[i].Powers.Mean < extrmPwr) { extrmPwr = data[i].Powers.Mean; }
                }
                else if (pwrProd == PwrProdType.RATEDP)
                {
                    if (i == 0) { extrmPwr = data[i].Powers.Mean; }

                    if (data[i].Powers.Mean > extrmPwr) { extrmPwr = data[i].Powers.Mean; }
                }

                EvTimes.Add(data[i].TimeStamp);
            }

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
            NOPROD, // for no power production events
            RATEDP  // for rated power
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

        #endregion

        #region Properties

        public double ExtrmSpd { get { return extrmSpd; } set { extrmSpd = value; } }
        public double ExtrmPow { get { return extrmPwr; } set { extrmPwr = value; } }

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
            set { displayDayTime = value; }
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
            set { displayAssoctn = value; }
        }

        public EventAssoct AssocEv { get { return assocEv; } set { assocEv = value; } }
        public EventSource ESource { get { return eSource; } set { eSource = value; } }
        public EvtDuration EvtDrtn { get { return evtDrtn; } set { evtDrtn = value; } }
        public PwrProdType PwrProd { get { return pwrProd; } set { pwrProd = value; } }
        public TimeOfEvent DayTime { get { return dayTime; } set { dayTime = value; } }
        public WeatherType Weather { get { return weather; } set { weather = value; } }

        #endregion
    }
}
