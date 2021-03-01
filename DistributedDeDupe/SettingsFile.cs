using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using File = Google.Apis.Drive.v3.Data.File;

namespace DistributedDeDupe
{
    [Serializable()]
    public class SettingsData
    {
        public int iterations = 10000;
        public string salt; //base64
        public int keySize = 256;
        public SerializableDictionary<string, string> locations = new SerializableDictionary<string, string>();
    }

    public class Settings
    {
        public static bool Testing;
    }
    public class SettingsFile
    {
        public static void Write(SettingsData data, string file)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(SettingsData));
            string xml = "";
            using (var sww = new StringWriter())
            {
                using (XmlWriter writer = XmlWriter.Create(sww))
                {
                    serializer.Serialize(writer, data);
                    xml = sww.ToString();
                }
            }
            
            System.IO.File.WriteAllText(file, xml);
        }

        public static SettingsData Read(string file)
        {
            SettingsData data = null;
            
            XmlSerializer serializer = new XmlSerializer(typeof(SettingsData));
            using (StreamReader read = new StreamReader(file))
            {
                data = (SettingsData) serializer.Deserialize(read);
            }
            
            return data;
        }
    }
}