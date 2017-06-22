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

        private TimeSpan sampleLen = new TimeSpan(0, 9, 59);

        private EventAssoct assocEv = EventAssoct.NONE;
        private EventSource eSource;
        private EvtDuration evtDrtn = EvtDuration.UNKNOWN;
        private PwrProdType pwrProd = PwrProdType.NORMAL;
        private TimeOfEvent dayTime = TimeOfEvent.UNKNOWN;
        private WeatherType weather = WeatherType.NORMAL;

        #endregion

        public EventData() { }

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
                if (input == WeatherType.LOW_SP)
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
                if (input == WeatherType.LOW_SP)
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

        private void SetEventDuration()
        {
            // the definitions for the different no power production event
            // duration can be easily and accessibly changed here

            Durat = Finit - Start;

            if (Durat.TotalMinutes < 30) { evtDrtn = EvtDuration.DMNS; }
            else if (Durat.TotalMinutes < 60 * 5) { evtDrtn = EvtDuration.HORS; }
            else if (Durat.TotalMinutes < 60 * 10) { evtDrtn = EvtDuration.DHRS; }
            else if (Durat.TotalMinutes < 60 * 24) { evtDrtn = EvtDuration.DAYS; }
        }

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

            UNKNOWN,
            NONE,
            DMNS, // deciminutes
            HORS, // hours
            DHRS, // decihours
            DAYS // days
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
            LOW_SP, // below cutin
            HI_SPD  // above cutout
        }

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

        public EventAssoct AssocEv { get { return assocEv; } set { assocEv = value; } }
        public EventSource ESource { get { return eSource; } set { eSource = value; } }
        public EvtDuration EvtDrtn { get { return evtDrtn; } set { evtDrtn = value; } }
        public PwrProdType PwrProd { get { return pwrProd; } set { pwrProd = value; } }
        public TimeOfEvent DayTime { get { return dayTime; } set { dayTime = value; } }
        public WeatherType Weather { get { return weather; } set { weather = value; } }

        #endregion
    }
}
