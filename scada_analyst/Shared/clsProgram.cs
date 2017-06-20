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

            #region Version 0.200

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
