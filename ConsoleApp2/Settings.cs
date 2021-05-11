using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace ConsoleApp2
{
    public class Settings
    {
        private List<string> blackList;
        public string ErrorPageAdress;

        public Settings()
        {
            GetSettings();
        }

        private void GetSettings()
        {
            var settingsFile = new XmlDocument();
            settingsFile.Load(Environment.CurrentDirectory + Path.DirectorySeparatorChar + "Settings.xml");

            // Getting a list of banned sites
            var forbidList = settingsFile.SelectNodes("Settings/BlockedWebsites/Website");

            var resultList = new List<string>();

            if (forbidList == null) return;
            for (var i = 0; i < forbidList.Count; ++i)
            {
                resultList.Add(forbidList[i].FirstChild.Value);
            }

            blackList = resultList;

            // Getting the path to the error page
            var node = settingsFile.SelectNodes("Settings/WrongPageAdress");
            if (node != null) ErrorPageAdress = node[0].FirstChild.Value;
        }

        public bool BlockedSite(string host)
        {
            foreach (var key in blackList)
            {
                if (host.Equals(key))
                {
                    return true;
                }
            }

            return false;
        }
    }
}