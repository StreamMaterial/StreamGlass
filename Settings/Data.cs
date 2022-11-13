﻿using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;

namespace StreamGlass.Settings
{
    public class Data
    {
        private Dictionary<string, Dictionary<string, string>> m_Settings = new();

        public void Load()
        {
            if (File.Exists("settings.json"))
            {
                JObject response = JObject.Parse(File.ReadAllText("settings.json"));
                foreach (var item in response)
                {
                    string key = item.Key;
                    var value = item.Value;
                    if (value is JObject obj)
                    {
                        Dictionary<string, string> settings = new();
                        foreach (var settingToken in obj)
                        {
                            if (settingToken.Value != null)
                                settings.Add(settingToken.Key, settingToken.Value.ToString());
                        }
                        m_Settings[key] = settings;
                    }
                }
            }
        }

        public string Get(string section, string name)
        {
            if (m_Settings.TryGetValue(section, out var settings))
            {
                if (settings.TryGetValue(name, out var value))
                    return value;
            }
            return "";
        }

        public void Set(string section, string name, string value)
        {
            if (!m_Settings.ContainsKey(section))
                m_Settings.Add(section, new());
            m_Settings[section][name] = value;
        }

        public void Create(string section, string name, string value)
        {
            if (!m_Settings.ContainsKey(section))
            {
                m_Settings.Add(section, new());
                m_Settings[section][name] = value;
            }
        }

        public void Save()
        {
            JObject json = new();
            foreach (var setting in m_Settings)
            {
                JObject section = new();
                foreach (var values in setting.Value)
                    section[values.Key] = values.Value;
                json.Add(setting.Key, section);
            }
            File.WriteAllText("settings.json", json.ToString());
        }
    }
}