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

        private bool _isFault = false;

        private double _avergPower = 0;
        private double _extrmPower = 0; // used for noting the maximum power at some other extreme variable moment
        private double _extrmValue = 0; // used for all calculations of a maximum value, including powers
        private double _extrmSpeed = 0;
        private double _maxValChng = 0;
        private double _meanNclTmp = 0;
        private double _rpmValChng = 0; // used for taking the RPM delta between first and last event timestep

        private TimeSpan _sampleLen = new TimeSpan(0, 9, 59);

        private AnomalyType _anomaly = AnomalyType.NOANOMALY;
        private EventAssoct _assocEv = EventAssoct.NONE;
        private EventSource _eSource = EventSource.UNKNOWN;
        private EvtDuration _evtDrtn = EvtDuration.UNKNOWN;
        private PwrProdType _pwrProd = PwrProdType.NORMAL;
        private TimeOfEvent _dayTime = TimeOfEvent.UNKNOWN;
        private WeatherType _weather = WeatherType.NORMAL;

        #endregion

        #region Constructor

        public EventData() { }

        #endregion

        #region Create Events

        public EventData(List<MeteoData.MeteoSample> data, WeatherType input)
        {
            FromAsset = data[0].AssetID;

            Start = data[0].TimeStamp;
            Finit = data[data.Count - 1].TimeStamp.Add(_sampleLen);

            Durat = Finit - Start;

            _eSource = EventSource.METMAST;
            Type = Types.WEATHER;
            _weather = input;

            for (int i = 0; i < data.Count; i++)
            {
                if (input == WeatherType.LO_SPD)
                {
                    if (i == 0) { _extrmSpeed = data[i].WSpdR.Mean; }

                    if (data[i].WSpdR.Mean < _extrmSpeed) { _extrmSpeed = data[i].WSpdR.Mean; }
                }
                else if (input == WeatherType.HI_SPD)
                {
                    if (i == 0) { _extrmSpeed = data[i].WSpdR.Mean; }

                    if (data[i].WSpdR.Mean > _extrmSpeed) { _extrmSpeed = data[i].WSpdR.Mean; }
                }

                EvTimes.Add(data[i].TimeStamp);
            }

            SetEventDuration();
        }

        public EventData(List<ScadaData.ScadaSample> data, WeatherType input)
        {
            FromAsset = data[0].AssetID;

            Start = data[0].TimeStamp;
            Finit = data[data.Count - 1].TimeStamp.Add(_sampleLen);

            Durat = Finit - Start;

            _eSource = EventSource.TURBINE;
            Type = Types.WEATHER;
            _weather = input;

            for (int i = 0; i < data.Count; i++)
            {
                if (input == WeatherType.LO_SPD)
                {
                    if (i == 0) { _extrmSpeed = data[i].AnemoM.ActWinds.Mean; }

                    if (data[i].AnemoM.ActWinds.Mean < _extrmSpeed) { _extrmSpeed = data[i].AnemoM.ActWinds.Mean; }
                }
                else if (input == WeatherType.HI_SPD)
                {
                    if (i == 0) { _extrmSpeed = data[i].AnemoM.ActWinds.Mean; }

                    if (data[i].AnemoM.ActWinds.Mean > _extrmSpeed) { _extrmSpeed = data[i].AnemoM.ActWinds.Mean; }
                }

                EvTimes.Add(data[i].TimeStamp);
            }

            SetEventDuration();
        }

        public EventData(List<ScadaData.ScadaSample> data, PwrProdType input)
        {
            FromAsset = data[0].AssetID;

            Start = data[0].TimeStamp;
            Finit = data[data.Count - 1].TimeStamp.Add(_sampleLen);
            
            _pwrProd = input;
            _eSource = EventSource.TURBINE;
            Type = Types.NOPOWER;

            for (int i = 0; i < data.Count; i++)
            {
                if (_pwrProd == PwrProdType.NO_PWR)
                {
                    if (i == 0) { _extrmValue = data[i].Powers.Mean; }

                    if (data[i].Powers.Mean < _extrmValue) { _extrmValue = data[i].Powers.Mean; }
                }
                else if (_pwrProd == PwrProdType.HI_PWR)
                {
                    if (i == 0) { _extrmValue = data[i].Powers.Mean; }

                    if (data[i].Powers.Mean > _extrmValue) { _extrmValue = data[i].Powers.Mean; }
                }

                EvTimes.Add(data[i].TimeStamp);
            }

            SetEventDuration();
        }

        public EventData(List<ScadaData.ScadaSample> data, AnomalyType input)
        {
            FromAsset = data[0].AssetID;

            Start = data[0].TimeStamp;
            Finit = data[data.Count - 1].TimeStamp.Add(_sampleLen);

            _anomaly = input;
            _eSource = EventSource.TURBINE;
            Type = Types.UNKNOWN;

            // Get the change in RPMs between the first and last values
            _rpmValChng = data[data.Count - 1].Genny.Rpms.Mean - data[0].Genny.Rpms.Mean;
            
            for (int i = 1; i < data.Count; i++)
            {
                if (_anomaly == AnomalyType.THRS_BEAR || _anomaly == AnomalyType.ROC_BEAR)
                {
                    if (i == 1) { _extrmValue = data[0].MainBear.Main.Mean; _extrmPower = data[0].Powers.Mean; }

                    _maxValChng = Math.Abs(data[i].MainBear.Main.Mean - data[i - 1].MainBear.Main.Mean) > _maxValChng ? 
                        Math.Abs(data[i].MainBear.Main.Mean - data[i - 1].MainBear.Main.Mean) : _maxValChng;
                    if (data[i].MainBear.Main.Mean > _extrmValue) { _extrmValue = data[i].MainBear.Main.Mean; _extrmPower = data[i].Powers.Mean; }
                }
                else if (_anomaly == AnomalyType.THRS_BEAR_GS || _anomaly == AnomalyType.ROC_BEAR_GS)
                {
                    if (i == 1) { _extrmValue = data[0].MainBear.Gs.Mean; _extrmPower = data[0].Powers.Mean; }

                    _maxValChng = Math.Abs(data[i].MainBear.Gs.Mean - data[i - 1].MainBear.Gs.Mean) > _maxValChng ? 
                        Math.Abs(data[i].MainBear.Gs.Mean - data[i - 1].MainBear.Gs.Mean) : _maxValChng;
                    if (data[i].MainBear.Gs.Mean > _extrmValue) { _extrmValue = data[i].MainBear.Gs.Mean; _extrmPower = data[i].Powers.Mean; }
                }
                else if (_anomaly == AnomalyType.THRS_BEAR_HS || _anomaly == AnomalyType.ROC_BEAR_HS)
                {
                    if (i == 1) { _extrmValue = data[0].MainBear.Hs.Mean; _extrmPower = data[0].Powers.Mean; }

                    _maxValChng = Math.Abs(data[i].MainBear.Hs.Mean - data[i - 1].MainBear.Hs.Mean) > _maxValChng ? 
                        Math.Abs(data[i].MainBear.Hs.Mean - data[i - 1].MainBear.Hs.Mean) : _maxValChng;
                    if (data[i].MainBear.Hs.Mean > _extrmValue) { _extrmValue = data[i].MainBear.Hs.Mean; _extrmPower = data[i].Powers.Mean; }
                }
                else if (_anomaly == AnomalyType.THRS_GEAR_HS_GENS || _anomaly == AnomalyType.ROC_GEAR_HS_GENS)
                {
                    if (i == 1) { _extrmValue = data[0].Gearbox.Hs.Gens.Mean; _extrmPower = data[0].Powers.Mean; }

                    _maxValChng = Math.Abs(data[i].Gearbox.Hs.Gens.Mean - data[i - 1].Gearbox.Hs.Gens.Mean) > _maxValChng ? 
                        Math.Abs(data[i].Gearbox.Hs.Gens.Mean - data[i - 1].Gearbox.Hs.Gens.Mean) : _maxValChng;
                    if (data[i].Gearbox.Hs.Gens.Mean > _extrmValue) { _extrmValue = data[i].Gearbox.Hs.Gens.Mean; _extrmPower = data[i].Powers.Mean; }
                }
                else if (_anomaly == AnomalyType.THRS_GEAR_HS_ROTS || _anomaly == AnomalyType.ROC_GEAR_HS_ROTS)
                {
                    if (i == 1) { _extrmValue = data[0].Gearbox.Hs.Rots.Mean; _extrmPower = data[0].Powers.Mean; }

                    _maxValChng = Math.Abs(data[i].Gearbox.Hs.Rots.Mean - data[i - 1].Gearbox.Hs.Rots.Mean) > _maxValChng ? 
                        Math.Abs(data[i].Gearbox.Hs.Rots.Mean - data[i - 1].Gearbox.Hs.Rots.Mean) : _maxValChng;
                    if (data[i].Gearbox.Hs.Rots.Mean > _extrmValue) { _extrmValue = data[i].Gearbox.Hs.Rots.Mean; _extrmPower = data[i].Powers.Mean; }
                }
                else if (_anomaly == AnomalyType.THRS_GEAR_IM_GENS || _anomaly == AnomalyType.ROC_GEAR_IM_GENS)
                {
                    if (i == 1) { _extrmValue = data[0].Gearbox.Ims.Gens.Mean; _extrmPower = data[0].Powers.Mean; }

                    _maxValChng = Math.Abs(data[i].Gearbox.Ims.Gens.Mean - data[i - 1].Gearbox.Ims.Gens.Mean) > _maxValChng ? 
                        Math.Abs(data[i].Gearbox.Ims.Gens.Mean - data[i - 1].Gearbox.Ims.Gens.Mean) : _maxValChng;
                    if (data[i].Gearbox.Ims.Gens.Mean > _extrmValue) { _extrmValue = data[i].Gearbox.Ims.Gens.Mean; _extrmPower = data[i].Powers.Mean; }
                }
                else if (_anomaly == AnomalyType.THRS_GEAR_IM_ROTS || _anomaly == AnomalyType.ROC_GEAR_IM_ROTS)
                {
                    if (i == 1) { _extrmValue = data[0].Gearbox.Ims.Rots.Mean; _extrmPower = data[0].Powers.Mean; }

                    _maxValChng = Math.Abs(data[i].Gearbox.Ims.Rots.Mean - data[i - 1].Gearbox.Ims.Rots.Mean) > _maxValChng ? 
                        Math.Abs(data[i].Gearbox.Ims.Rots.Mean - data[i - 1].Gearbox.Ims.Rots.Mean) : _maxValChng;
                    if (data[i].Gearbox.Ims.Rots.Mean > _extrmValue) { _extrmValue = data[i].Gearbox.Ims.Rots.Mean; _extrmPower = data[i].Powers.Mean; }
                }
                else if (_anomaly == AnomalyType.THRS_GEAR_OIL || _anomaly == AnomalyType.ROC_GEAR_OIL)
                {
                    if (i == 1) { _extrmValue = data[0].Gearbox.Oils.Mean; _extrmPower = data[0].Powers.Mean; }

                    _maxValChng = Math.Abs(data[i].Gearbox.Oils.Mean - data[i - 1].Gearbox.Oils.Mean) > _maxValChng ? 
                        Math.Abs(data[i].Gearbox.Oils.Mean - data[i - 1].Gearbox.Oils.Mean) : _maxValChng;
                    if (data[i].Gearbox.Oils.Mean > _extrmValue) { _extrmValue = data[i].Gearbox.Oils.Mean; _extrmPower = data[i].Powers.Mean; }
                }
                else if (_anomaly == AnomalyType.THRS_GNNY_G || _anomaly == AnomalyType.ROC_GNNY_G)
                {
                    if (i == 1) { _extrmValue = data[0].Genny.BearingG.Mean; _extrmPower = data[0].Powers.Mean; }

                    _maxValChng = Math.Abs(data[i].Genny.BearingG.Mean - data[i - 1].Genny.BearingG.Mean) > _maxValChng ? 
                        Math.Abs(data[i].Genny.BearingG.Mean - data[i - 1].Genny.BearingG.Mean) : _maxValChng;
                    if (data[i].Genny.BearingG.Mean > _extrmValue) { _extrmValue = data[i].Genny.BearingG.Mean; _extrmPower = data[i].Powers.Mean; }
                }
                else if (_anomaly == AnomalyType.THRS_GNNY_R || _anomaly == AnomalyType.ROC_GNNY_R)
                {
                    if (i == 1) { _extrmValue = data[0].Genny.BearingR.Mean; _extrmPower = data[0].Powers.Mean; }

                    _maxValChng = Math.Abs(data[i].Genny.BearingR.Mean - data[i - 1].Genny.BearingR.Mean) > _maxValChng ? 
                        Math.Abs(data[i].Genny.BearingR.Mean - data[i - 1].Genny.BearingR.Mean) : _maxValChng;
                    if (data[i].Genny.BearingR.Mean > _extrmValue) { _extrmValue = data[i].Genny.BearingR.Mean; _extrmPower = data[i].Powers.Mean; }
                }
                else if (_anomaly == AnomalyType.THRS_GNNY_RPM || _anomaly == AnomalyType.ROC_GNNY_RPM)
                {
                    if (i == 1) { _extrmValue = data[0].Genny.Rpms.Mean; _extrmPower = data[0].Powers.Mean; }

                    _maxValChng = Math.Abs(data[i].Genny.Rpms.Mean - data[i - 1].Genny.Rpms.Mean) > _maxValChng ? 
                        Math.Abs(data[i].Genny.Rpms.Mean - data[i - 1].Genny.Rpms.Mean) : _maxValChng;
                    if (data[i].Genny.Rpms.Mean > _extrmValue) { _extrmValue = data[i].Genny.Rpms.Mean; _extrmPower = data[i].Powers.Mean; }
                }

                _avergPower += data[i].Powers.Mean;
                _meanNclTmp += data[i].Nacel.Mean;

                EvTimes.Add(data[i].TimeStamp);
            }

            _avergPower = _avergPower / data.Count;
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

            if (Durat.TotalMinutes < (int)EvtDuration.DECIMINS) { _evtDrtn = EvtDuration.SHORT; }
            else if (Durat.TotalMinutes < (int)EvtDuration.HOURS) { _evtDrtn = EvtDuration.DECIMINS; }
            else if (Durat.TotalMinutes < (int)EvtDuration.MANYHOURS) { _evtDrtn = EvtDuration.HOURS; }
            else if (Durat.TotalMinutes < (int)EvtDuration.DAYS) { _evtDrtn = EvtDuration.MANYHOURS; }
            else if (Durat.TotalMinutes >= (int)EvtDuration.DAYS) { _evtDrtn = EvtDuration.DAYS; }
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
            THRS_GNNY_RPM,
            ROC_BEAR,
            ROC_BEAR_GS,
            ROC_BEAR_HS,
            ROC_GEAR_OIL,
            ROC_GEAR_HS_GENS,
            ROC_GEAR_HS_ROTS,
            ROC_GEAR_IM_GENS,
            ROC_GEAR_IM_ROTS,
            ROC_GNNY_G,
            ROC_GNNY_R,
            ROC_GNNY_RPM,
        }

        #endregion

        #region Properties

        public bool IsFault { get { return _isFault; } set { _isFault = value; } }

        public double AvergPower { get { return _avergPower; } set { _avergPower = value; } }
        public double ExtrmPower { get { return _extrmPower; } set { _extrmPower = value; } }
        public double ExtrmSpeed { get { return _extrmSpeed; } set { _extrmSpeed = value; } }
        public double ExtrmValue { get { return _extrmValue; } set { _extrmValue = value; } }
        public double MaxValChng { get { return _maxValChng; } set { _maxValChng = value; } }
        public double MeanNclTmp { get { return _meanNclTmp; } set { _meanNclTmp = value; } }
        public double RpmValChng { get { return _rpmValChng; } set { _rpmValChng = value; } }

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

        public string FaultString
        {
            get { if (IsFault) { return "Yes"; } else { return "No"; } }
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
                else if (Anomaly == AnomalyType.ROC_BEAR) { return "Main bearing"; }
                else if (Anomaly == AnomalyType.ROC_BEAR_GS) { return "Main bearing GS"; }
                else if (Anomaly == AnomalyType.ROC_BEAR_HS) { return "Main bearing HS"; }
                else if (Anomaly == AnomalyType.ROC_GEAR_HS_GENS) { return "Gearbox HS Gen. Side"; }
                else if (Anomaly == AnomalyType.ROC_GEAR_HS_ROTS) { return "Gearbox HS Rot. Side"; }
                else if (Anomaly == AnomalyType.ROC_GEAR_IM_GENS) { return "Gearbox IMS Gen. Side"; }
                else if (Anomaly == AnomalyType.ROC_GEAR_IM_ROTS) { return "Gearbox IMS Rot. Side"; }
                else if (Anomaly == AnomalyType.ROC_GEAR_OIL) { return "Gearbox Oil"; }
                else if (Anomaly == AnomalyType.ROC_GNNY_G) { return "Generator G-Bearing"; }
                else if (Anomaly == AnomalyType.ROC_GNNY_R) { return "Generator R-Bearing"; }
                else if (Anomaly == AnomalyType.ROC_GNNY_RPM) { return "Generator RPMs"; }
                else { return "Unknown"; }
            }
        }

        public AnomalyType Anomaly { get { return _anomaly; } set { _anomaly = value; } }
        public EventAssoct AssocEv { get { return _assocEv; } set { _assocEv = value; } }
        public EventSource ESource { get { return _eSource; } set { _eSource = value; } }
        public EvtDuration EvtDrtn { get { return _evtDrtn; } set { _evtDrtn = value; } }
        public PwrProdType PwrProd { get { return _pwrProd; } set { _pwrProd = value; } }
        public TimeOfEvent DayTime { get { return _dayTime; } set { _dayTime = value; } }
        public WeatherType Weather { get { return _weather; } set { _weather = value; } }

        #endregion
    }
}
