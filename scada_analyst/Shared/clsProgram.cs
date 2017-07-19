using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace scada_analyst.Shared
{
    public static class VersionHistory
    {
        private static List<ProgramVersion> GetAllChanges()
        {
            List<ProgramVersion> results = new List<ProgramVersion>(25);

            #region Version 1.400 (Romax Visit)

            ProgramVersion v1405 = new ProgramVersion(1405, new DateTime(2017, 07, 19));
            v1405.AddChange("Remove events possible for multiple events at a time in the low power production area.");
            results.Add(v1405);

            ProgramVersion v1404 = new ProgramVersion(1404, new DateTime(2017, 07, 19));
            v1404.AddChange("Loading SCADA data the turbine ID checks both Asset ID and Station ID to see which one to use.");
            results.Add(v1404);

            ProgramVersion v1403 = new ProgramVersion(1403, new DateTime(2017, 07, 19));
            v1403.AddChange("All errors should come up as a similar black screen and fade in.");
            results.Add(v1403);

            ProgramVersion v1402 = new ProgramVersion(1402, new DateTime(2017, 07, 19));
            v1402.AddChange("Can pick date format for input files.");
            results.Add(v1402);

            ProgramVersion v1401 = new ProgramVersion(1401, new DateTime(2017, 07, 19));
            v1401.AddChange("Can use Station ID to load data.");
            results.Add(v1401);

            ProgramVersion v1400 = new ProgramVersion(1400, new DateTime(2017, 07, 19));
            v1400.AddChange("Added option to view max power value at max threshold crossover.");
            v1400.AddChange("Added option to view RPM change over the rate of change period.");
            v1400.AddChange("Fixed change fault status option workings.");
            results.Add(v1400);

            #endregion

            #region Version 1.000
            
            ProgramVersion v1300 = new ProgramVersion(1300, new DateTime(2017, 07, 10));
            v1300.AddChange("Added option for displaying rate of change of variables above an user-defined limit.");
            v1300.AddChange("User can now certify certain events as 'faults' by right-clicking on them in the 'No Power Production' event list.");
            results.Add(v1300);

            ProgramVersion v1201 = new ProgramVersion(1201, new DateTime(2017, 07, 08));
            v1201.AddChange("Fixed averages calculating to take into account the first asset as well.");
            results.Add(v1201);

            ProgramVersion v1200 = new ProgramVersion(1200, new DateTime(2017, 07, 07));
            v1200.AddChange("Added option to remove power events one by one.");
            results.Add(v1200);

            ProgramVersion v1100 = new ProgramVersion(1100, new DateTime(2017, 07, 04));
            v1100.AddChange("Thresholding is now possible based on user-specified values.");
            results.Add(v1100);

            ProgramVersion v1000 = new ProgramVersion(1000, new DateTime(2017, 06, 29));
            v1000.AddChange("Redefined program version number as v1.000.");
            results.Add(v1000);

            #endregion

            #region Version 0.200

            ProgramVersion v0280 = new ProgramVersion(0280, new DateTime(2017, 06, 29));
            v0280.AddChange("Defining constant wind speed directions to enable assessing their relation to power events' locations.");
            v0280.AddChange("Events summary implementation to see the distribution of power events by asset and duration.");
            results.Add(v0280);

            ProgramVersion v0270 = new ProgramVersion(0270, new DateTime(2017, 06, 28));
            v0270.AddChange("Implemented fleet-wde temperature comparison outputting a delta temperature for certain variables.");
            results.Add(v0270);

            ProgramVersion v0260 = new ProgramVersion(0260, new DateTime(2017, 06, 27));
            v0260.AddChange("Redefined programming objectives for future functions.");
            v0260.AddChange("Improvment: Manual working hours defineable to avoid excluding low power events at say 4 AM in the summer.");
            results.Add(v0260);

            ProgramVersion v0251 = new ProgramVersion(0251, new DateTime(2017, 06, 26));
            v0251.AddChange("Day-time processing takes into account minimum and maximum working hours to account for a long summer day.");
            results.Add(v0251);

            ProgramVersion v0250 = new ProgramVersion(0250, new DateTime(2017, 06, 26));
            v0250.AddChange("Can display charts of basic event temperatures for a duration of a week before it happened.");
            v0250.AddChange("Events and charts both show the same information though the chart only has one variable.");
            results.Add(v0250);

            ProgramVersion v0240 = new ProgramVersion(0240, new DateTime(2017, 06, 23));
            v0240.AddChange("Display of events based lists.");
            v0240.AddChange("Slowly working towards chart display with the same datasets.");
            results.Add(v0240);

            ProgramVersion v0230 = new ProgramVersion(0230, new DateTime(2017, 06, 22));
            v0230.AddChange("Implemented MVVM style systems for a bit to improve the GUI.");
            results.Add(v0230);

            ProgramVersion v0220 = new ProgramVersion(0220, new DateTime(2017, 06, 21));
            v0220.AddChange("Implemented MVVM style systems for a bit to improve the GUI.");
            v0220.AddChange("General GUI redesign.");
            results.Add(v0220);

            ProgramVersion v0210 = new ProgramVersion(0210, new DateTime(2017, 06, 20));
            v0210.AddChange("Added time of day comparison to remove events during the day in case they are scheduled maintenance events.");
            v0210.AddChange("User can choose when time of day events are most likely to be maintenance. Should probably add a time filter as well.");
            results.Add(v0210);

            ProgramVersion v0200 = new ProgramVersion(0200, new DateTime(2017, 06, 19));
            v0200.AddChange("Radical changes to UI design (MahApps Modern).");
            v0200.AddChange("Implementation of visualisation library (LiveGraphs).");
            results.Add(v0200);

            #endregion 

            #region Version 0.100

            ProgramVersion v0160 = new ProgramVersion(0160, new DateTime(2017, 06, 19));
            v0160.AddChange("Events - associates power and wind events.");
            v0160.AddChange("Events - filter by duration.");
            results.Add(v0160);

            ProgramVersion v0151 = new ProgramVersion(0151, new DateTime(2017, 06, 18));
            v0151.AddChange("Tweaked loading of files to make it programmatically better.");
            results.Add(v0151);

            ProgramVersion v0150 = new ProgramVersion(0150, new DateTime(2017, 06, 16));
            v0150.AddChange("Event detection - displays all found events at the end of the method.");
            v0150.AddChange("Event detection - notices larger timesteps than 10min and breaks events.");
            v0150.AddChange("Event detection - notices end of series of file and breaks events appropriately.");
            v0150.AddChange("Event detection - assigns durations to failure events based on length.");
            v0150.AddChange("Event detection - events with values similar to 'no value' are ignored.");
            v0150.AddChange("Export option - meteorology data.");
            results.Add(v0150);

            ProgramVersion v0140 = new ProgramVersion(0140, new DateTime(2017, 06, 16));
            v0140.AddChange("Began event detection work - failures and weather events included.");
            results.Add(v0140);

            ProgramVersion v0131 = new ProgramVersion(0130, new DateTime(2017, 06, 16));
            v0131.AddChange("Affected improvements on the listview for data display.");
            results.Add(v0131);

            ProgramVersion v0130 = new ProgramVersion(0130, new DateTime(2017, 06, 15));
            v0130.AddChange("Visualised list with information on basic loaded data.");
            results.Add(v0130);

            ProgramVersion v0123 = new ProgramVersion(0123, new DateTime(2017, 06, 15));
            v0123.AddChange("Export option - feeds back and forth the export options.");
            v0123.AddChange("Export option - fixed TimeStamp properties for the export and load.");
            results.Add(v0123);

            ProgramVersion v0122 = new ProgramVersion(0122, new DateTime(2017, 06, 15));
            v0122.AddChange("Export option - added gearbox options.");
            v0122.AddChange("Export option - save file with properties.");
            results.Add(v0122);

            ProgramVersion v0121 = new ProgramVersion(0121, new DateTime(2017, 06, 14));
            v0121.AddChange("Export option beginning - choose which variables are useful.");
            results.Add(v0121);

            ProgramVersion v0120 = new ProgramVersion(0120, new DateTime(2017, 06, 14));
            v0120.AddChange("Task.Run methodology now implements proper progress tracking.");
            results.Add(v0120);

            ProgramVersion v0101 = new ProgramVersion(0101, new DateTime(2017, 06, 07));
            v0101.AddChange("Improved loading methodology for meteorology files.");
            v0101.AddChange("Improving turbine data loading with regards to empty variables and unknown info.");
            results.Add(v0101);

            ProgramVersion v0100 = new ProgramVersion(0100, new DateTime(2017,06,07));
            v0100.AddChange("Improved loading methodology for SCADA files.");
            v0100.AddChange("More variables loaded from turbine and temperature files.");
            results.Add(v0100);

            ProgramVersion v0014 = new ProgramVersion(0014, new DateTime(2017, 06, 06));
            v0014.AddChange("Added version history.");
            results.Add(v0014);

            #endregion

            return results;
        }

        public static List<ProgramVersion> GetChanges(int sinceVersion = -1)
        {
            return GetAllChanges().FindAll(v => v.Version > sinceVersion);
        }

        #region Support classes

        public class ProgramVersion : IComparable
        {
            #region Variables

            private DateTime releaseDate;
            private int version;
            private List<string> changes = new List<string>();

            #endregion

            public ProgramVersion(int version, DateTime releaseDate)
            {
                this.version = version;
                this.releaseDate = releaseDate;
            }

            public void AddChange(string change)
            {
                changes.Add(string.Format("- {0}", change));
            }

            public int CompareTo(object obj)
            {
                if (obj is ProgramVersion)
                {
                    return version.CompareTo(((ProgramVersion)obj).version);
                }

                return 0;
            }

            private string GetVersionString()
            {
                string versionString = version.ToString();
                string result = "";

                for (int i = 0; i < versionString.Length - 1; i++)
                {
                    result = result + versionString[i] + ".";
                }

                return result + versionString[versionString.Length - 1];
            }

            #region Accessor methods

            public string Changes
            {
                get
                {
                    StringBuilder sB = new StringBuilder();

                    for (int i = 0; i < changes.Count; i++)
                    {
                        if (i < changes.Count - 1)
                        {
                            sB.AppendLine(changes[i]);
                        }
                        else
                        {
                            sB.Append(changes[i]);
                        }
                    }

                    return sB.ToString();
                }
            }

            public int Version
            {
                get { return version; }
            }

            public string VersionAndDate
            {
                get
                {
                    return string.Format("{0} ({1})", GetVersionString(), releaseDate.ToShortDateString());
                }
            }

            #endregion
        }

        #endregion
    }
}
