using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Data;
using System.Windows.Media;

namespace scada_analyst.Shared
{
    public static class Common
    {
        #region Variables

        private static NumberFormatInfo numberFormat = new CultureInfo("en-US", false).NumberFormat;

        #endregion

        /// <summary>
        /// Returns the grid bearing between two grid positions.
        /// </summary>
        /// <param name="position1">The start grid position.</param>
        /// <param name="position2">The end grid position.</param>
        /// <returns>The grid bearing between the two grid positions.</returns>
        private static float Bearing(double dX, double dY)
        {
            if (dX == 0 && dY == 0)
            {
                return 0;
            }
            else
            {
                double bearing = Math.Atan2(dX, dY) * (180 / Math.PI);
                return (float)(bearing + 360) % 360;
            }
        }

        public static float Bearing(GridPosition position1, GridPosition position2)
        {
            return Bearing(position2.Easting - position1.Easting, position2.Northing - position1.Northing);
        }

        public static float Bearing(double easting1, double northing1,
            double easting2, double northing2)
        {
            return Bearing(easting1 - easting2, northing1 - northing2);
        }

        public static float Bearing(Point p1, Point p2)
        {
            return Bearing(p1.X - p2.X, p1.Y - p2.Y);
        }

        private static float Bearing90(double dX, double dY)
        {
            if (dX == 0 && dY == 0)
            {
                return 0;
            }
            else
            {
                double bearing = Math.Atan2(dX, dY) * (180 / Math.PI);
                return (float)(bearing + 360) % 90;
            }
        }

        public static float Bearing90(double easting1, double northing1,
            double easting2, double northing2)
        {
            return Bearing90(easting1 - easting2, northing1 - northing2);
        }

        public static string BearingStringConversion(float bearing)
        {
            string direction = "";

            bearing = bearing % 360;

            if (bearing < 11.25)
            {
                direction = "North";
            }
            else if (bearing < 33.75)
            {
                direction = "NNE";
            }
            else if (bearing < 56.25)
            {
                direction = "Northeast";
            }
            else if (bearing < 78.75)
            {
                direction = "ENE";
            }
            else if (bearing < 101.25)
            {
                direction = "East";
            }
            else if (bearing < 123.75)
            {
                direction = "ESE";
            }
            else if (bearing < 146.25)
            {
                direction = "Southeast";
            }
            else if (bearing < 168.75)
            {
                direction = "SSE";
            }
            else if (bearing < 191.25)
            {
                direction = "South";
            }
            else if (bearing < 213.75)
            {
                direction = "SSW";
            }
            else if (bearing < 236.25)
            {
                direction = "Southwest";
            }
            else if (bearing < 258.75)
            {
                direction = "WSW";
            }
            else if (bearing < 281.25)
            {
                direction = "West";
            }
            else if (bearing < 303.75)
            {
                direction = "WNW";
            }
            else if (bearing < 326.25)
            {
                direction = "Northwest";
            }
            else if (bearing < 348.75)
            {
                direction = "NNW";
            }
            else
            {
                direction = "North";
            }

            return direction;
        }

        public static double CalculateDistance(GridPosition pos1, GridPosition pos2)
        {
            return CalculateDistance(pos1.Easting, pos1.Northing, pos2.Easting, pos2.Northing);
        }

        public static double CalculateDistance(double x1, double y1, double x2, double y2)
        {
            //get the distance between 2 points, using Pythagoras theorem:
            return Math.Sqrt(Math.Pow(x1 - x2, 2) + Math.Pow(y1 - y2, 2));
        }

        public static bool CanConvert<T>(string data)
        {
            TypeConverter converter = TypeDescriptor.GetConverter(typeof(T));
            return converter.IsValid(data);
        }

        public static bool ContainsAny(this string haystack, params string[] needles)
        {
            foreach (string needle in needles)
            {
                if (haystack.Contains(needle))
                    return true;
            }

            return false;
        }

        public static string[] GetSplits(string line, char[] delimiters, bool useEmpty, bool trim)
        {
            string[] splits = line.Split(delimiters);

            List<string> result = new List<string>();

            for (int i = 0; i < splits.Length; i++)
            {
                if (trim) { splits[i] = splits[i].Trim(); }
                if (useEmpty)
                {
                    result.Add(splits[i]);
                }
                else if (!splits[i].Equals(""))
                {
                    result.Add(splits[i]);
                }
            }

            return result.ToArray();
        }
        
        public static string[] GetSplits(string line, char[] delimiters)
        {
            return GetSplits(line, delimiters, false, false);
        }

        public static string[] GetSplits(string line, char delimiter)
        {
            return GetSplits(line, new char[] { delimiter }, false, false);
        }

        public static string[] GetSplits(string line, char[] delimiters, bool useEmpty)
        {
            return GetSplits(line, delimiters, false, false);
        }
        
        public static string GetStringDecimals(double number, int dp)
        {
            numberFormat.NumberDecimalDigits = dp;
            numberFormat.NumberGroupSeparator = "";

            return number.ToString("N", numberFormat);
        }
        
        public static string RemoveAllSpaces(string input)
        {
            while (input.Contains(" "))
            {
                input = input.Replace(" ", "");
            }

            return input;
        }

        public static string RemoveExcessSpaces(string input)
        {
            while (input.Contains("  "))
            {
                input = input.Replace("  ", " ");
            }

            return input;
        }

        public static DateTime StringToDateTime(string dateInfo, bool correctYearOrder = true)
        {
            return StringToDateTime(GetSplits(dateInfo, ' '), correctYearOrder);
        }

        public static DateTime StringToDateTime(string[] dateInfo, bool correctYearOrder = true)
        {
            if (correctYearOrder)
            {
                return StringToDateTime(dateInfo, DateFormat.DMY);
            }
            else
            {
                return StringToDateTime(dateInfo, DateFormat.MDY);
            }
        }

        public static DateTime StringToDateTime(string[] dateInfo, DateFormat dtf)
        {
            try
            {
                            DateTime result = new DateTime();

            string[] tempDate = Common.GetSplits(dateInfo[0], new char[] { '-', '/' });
            string[] tempTime = Common.GetSplits(dateInfo[1], new char[] { ':', '.' });

            if (dtf == DateFormat.DMY)
            {
                if (tempTime.Length == 2)
                {
                    result = new DateTime(Convert.ToInt16(tempDate[2]), Convert.ToInt16(tempDate[1]), Convert.ToInt16(tempDate[0]),
                        Convert.ToInt16(tempTime[0]), Convert.ToInt16(tempTime[1]), Convert.ToInt16(tempTime[2]));
                }
                else
                {
                    result = new DateTime(Convert.ToInt16(tempDate[2]), Convert.ToInt16(tempDate[1]), Convert.ToInt16(tempDate[0]),
                        Convert.ToInt16(tempTime[0]), Convert.ToInt16(tempTime[1]), 0);
                }
            }
            else if (dtf == DateFormat.MDY)
            {
                if (tempTime.Length == 2)
                {
                    result = new DateTime(Convert.ToInt16(tempDate[0]), Convert.ToInt16(tempDate[1]), Convert.ToInt16(tempDate[2]),
                        Convert.ToInt16(tempTime[0]), Convert.ToInt16(tempTime[1]), Convert.ToInt16(tempTime[2]));
                }
                else
                {
                    result = new DateTime(Convert.ToInt16(tempDate[0]), Convert.ToInt16(tempDate[1]), Convert.ToInt16(tempDate[2]),
                        Convert.ToInt16(tempTime[0]), Convert.ToInt16(tempTime[1]), 0);
                }
            }
            else if (dtf == DateFormat.YDM)
            {
                if (tempTime.Length == 2)
                {
                    result = new DateTime(Convert.ToInt16(tempDate[0]), Convert.ToInt16(tempDate[2]), Convert.ToInt16(tempDate[1]),
                        Convert.ToInt16(tempTime[0]), Convert.ToInt16(tempTime[1]), Convert.ToInt16(tempTime[2]));
                }
                else
                {
                    result = new DateTime(Convert.ToInt16(tempDate[0]), Convert.ToInt16(tempDate[2]), Convert.ToInt16(tempDate[1]),
                        Convert.ToInt16(tempTime[0]), Convert.ToInt16(tempTime[1]), 0);
                }
            }
            else
            {
                if (tempTime.Length == 2)
                {
                    result = new DateTime(Convert.ToInt16(tempDate[0]), Convert.ToInt16(tempDate[1]), Convert.ToInt16(tempDate[2]),
                        Convert.ToInt16(tempTime[0]), Convert.ToInt16(tempTime[1]), Convert.ToInt16(tempTime[2]));
                }
                else
                {
                    result = new DateTime(Convert.ToInt16(tempDate[0]), Convert.ToInt16(tempDate[1]), Convert.ToInt16(tempDate[2]),
                        Convert.ToInt16(tempTime[0]), Convert.ToInt16(tempTime[1]), 0);
                }
            }

            return result;
            }
            catch
            {
                throw new WrongDateTimeException();
            }
        }

        public static DateTime StringToDateTime(string lengthSix, string lengthEight)
        {
            return StringToDateTime(lengthSix, lengthEight, true, true);
        }

        public static DateTime StringToDateTime(string lengthSix, string lengthEight, bool hasTime, bool hasDate)
        {
            return StringToDateTime(lengthSix, lengthEight, true, true, new DateTime(2001, 01, 01, 12, 00, 00));
        }

        public static DateTime StringToDateTime(string lengthSix, string lengthEight, bool hasTime, bool hasDate,
            DateTime defaultDT)
        {
            DateTime result = new DateTime();

            string[] substring = new string[6];
            if (hasTime)
            {
                substring[0] = lengthSix.Substring(0, 2); //HH
                substring[1] = lengthSix.Substring(2, 2); //MM
                substring[2] = lengthSix.Substring(4, 2); //SS
            }
            if (hasDate)
            {
                substring[3] = lengthEight.Substring(0, 2); //DD
                substring[4] = lengthEight.Substring(2, 2); //MM
                substring[5] = lengthEight.Substring(4, 4); //YYYY
            }

            if (hasTime && hasDate)
            {
                result = new DateTime(Convert.ToInt16(substring[5]), Convert.ToInt16(substring[4]),
                    Convert.ToInt16(substring[3]), Convert.ToInt16(substring[0]), Convert.ToInt16(substring[1]),
                    Convert.ToInt16(substring[2]));
            }
            else if (hasTime)
            {
                result = new DateTime(defaultDT.Year, defaultDT.Month, defaultDT.Day, Convert.ToInt16(substring[0]), Convert.ToInt16(substring[1]),
                    Convert.ToInt16(substring[2]));
            }
            else if (hasDate)
            {
                result = new DateTime(Convert.ToInt16(substring[5]), Convert.ToInt16(substring[4]),
                    Convert.ToInt16(substring[3]), defaultDT.Hour, defaultDT.Minute, defaultDT.Second);
            }

            return result;
        }

        public static DateTime ToDateTime(this int dosDateTime)
        {
            var date = (dosDateTime & 0xFFFF0000) >> 16;
            var time = (dosDateTime & 0x0000FFFF);

            var year = (date >> 9) + 1980;
            var month = (date & 0x01e0) >> 5;
            var day = date & 0x1F;
            var hour = time >> 11;
            var minute = (time & 0x07e0) >> 5;
            var second = (time & 0x1F) * 2;

            return new DateTime((int)year, (int)month, (int)day, (int)hour, (int)minute, (int)second);
        }

        public static int ToDOSDate(this DateTime dateTime)
        {
            var years = dateTime.Year - 1980;
            var months = dateTime.Month;
            var days = dateTime.Day;
            var hours = dateTime.Hour;
            var minutes = dateTime.Minute;
            var seconds = dateTime.Second;

            var date = (years << 9) | (months << 5) | days;
            var time = (hours << 11) | (minutes << 5) | (seconds >> 1);

            return (date << 16) | time;
        }

        public enum DateFormat
        {
            YMD,
            YDM,
            MDY,
            DMY,
            EMPTY
        }
    }

    public static class ExtensionMethods
    {
        public static string GetColourName(Color colour)
        {
            string colourName = colour.GetColorName();

            if (colourName.Equals(""))
            {
                //no match found, so do a more fuzzy check:
                int acceptableMargin = 15;

                foreach (var v in typeof(Colors).GetProperties())
                {
                    //in .NET4.0 this should be Color c = (Color)v.GetValue(v, null), and so I'm doing the same here
                    //so that I don't have to keep changing it every time I recompile for .NET4.0:
                    Color c = (Color)v.GetValue(v, null);

                    if ((colour.A >= c.A - acceptableMargin && colour.A <= c.A + acceptableMargin)
                         && (colour.R >= c.R - acceptableMargin && colour.R <= c.R + acceptableMargin)
                         && (colour.G >= c.G - acceptableMargin && colour.G <= c.G + acceptableMargin)
                         && (colour.B >= c.B - acceptableMargin && colour.B <= c.B + acceptableMargin))
                    {
                        return c.GetColorName();
                    }
                }
            }

            return colourName;
        }

        public static string GetColorName(this Color color)
        {
            string result = knownColors
                .Where(kvp => kvp.Value.Equals(color))
                .Select(kvp => kvp.Key)
                .FirstOrDefault();

            return result == null ? "" : result;
        }

        public static Color GetInverse(this Color c)
        {
            return Color.FromArgb(255, (byte)(255 - c.R), (byte)(255 - c.G), (byte)(255 - c.B));
        }

        static Dictionary<string, Color> GetKnownColors()
        {
            var colorProperties = typeof(Colors).GetProperties(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
            return colorProperties
                .ToDictionary(
                    p => p.Name,
                    p => (Color)p.GetValue(null, null));
        }

        public static System.Drawing.Color GetNonWPFColour(this Color colour, bool includeAlpha = true)
        {
            return System.Drawing.Color.FromArgb(includeAlpha ? colour.A : 255, colour.R, colour.G, colour.B);
        }
        
        private static readonly Dictionary<string, Color> knownColors = GetKnownColors();
        
        public static string Substring(this string s, int firstNo, int lastNo, bool range)
        {
            return s.Substring(firstNo, lastNo - firstNo);
        }
    }

    public class TabControlViewModel : INotifyPropertyChanged
    {
        private bool _tabHeaderVisible = false;

        public ICommand ToggleHeader
        {
            get;
            private set;
        }

        public bool TabHeaderVisible
        {
            get { return _tabHeaderVisible; }
            set
            {
                _tabHeaderVisible = value;
                OnPropertyChanged("TabHeaderVisible");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string name)
        {
            var changed = PropertyChanged;
            if (changed != null)
            {
                changed(this, new PropertyChangedEventArgs(name));
            }
        }
    }

    [ValueConversion(typeof(bool), typeof(bool))]
    public class BoolToOppositeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter,
            System.Globalization.CultureInfo culture)
        {
            return !(bool)value;
        }

        public object ConvertBack(object value, Type targetType, object parameter,
            System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    [ValueConversion(typeof(Color), typeof(Brush))]
    public class ColourToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter,
            System.Globalization.CultureInfo culture)
        {
            Color colour = (Color)value;

            return new SolidColorBrush(colour);
        }

        public object ConvertBack(object value, Type targetType, object parameter,
            System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
