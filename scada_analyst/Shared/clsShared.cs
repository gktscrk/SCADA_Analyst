using System;
using System.Collections.Generic;
using System.ComponentModel;    
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace scada_analyst
{
    static class Common
    {
        public static bool CanConvert<T>(string data)
        {
            TypeConverter converter = TypeDescriptor.GetConverter(typeof(T));
            return converter.IsValid(data);
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

        public static DateTime StringToDateTime(string[] dateinfo)
        {
            DateTime result = new DateTime();

            string[] tempDate = Common.GetSplits(dateinfo[0], new char[] { '-' });
            string[] tempTime = Common.GetSplits(dateinfo[1], new char[] { ':', '.' });

            result = new DateTime(Convert.ToInt16(tempDate[0]), Convert.ToInt16(tempDate[1]),
                Convert.ToInt16(tempDate[2]), Convert.ToInt16(tempTime[0]), Convert.ToInt16(tempTime[1]),
                Convert.ToInt16(tempTime[2]));

            return result;
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
    }
}
