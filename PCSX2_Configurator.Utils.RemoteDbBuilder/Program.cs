using IniParser.Parser;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;

namespace PCSX2_Configurator.Utils.RemoteDbBuilder
{
    class Program
    {
        static SortedDictionary<string, List<string>> gamesAndIds;

        static void Main()
        {
            Console.WriteLine("Db Builder For PCSX2 Configuartor Remote Configs, Please enter the working directory (of pcsx2 configurator)");

            SetGamesWithIds();

            var workingDirectory = Console.ReadLine().Replace("\"", "");
            const string filename = "RemoteIndex.xml";
            const string rootNode = "RemoteConfigs";

            Directory.SetCurrentDirectory(workingDirectory);

            var xmlDocument = new XmlDocument();
            if (!File.Exists(filename))
            {
                xmlDocument.AppendChild(xmlDocument.CreateElement(rootNode));
                xmlDocument.Save(filename);
            }
            else throw new Exception("file already exists");

            foreach (var configDir in Directory.GetDirectories("remote\\Game Configs"))
            {
                var configName = configDir.Substring(configDir.LastIndexOf("\\") + 1).Split("id#")[0].Trim();
                var configNode = xmlDocument.CreateElement("Config");

                var nameAttribute = xmlDocument.CreateAttribute("Name");
                nameAttribute.Value = configName;
                configNode.Attributes.Append(nameAttribute);

                var keyName = configName.Replace("-", "");
                if (gamesAndIds.ContainsKey(keyName))
                {
                    var gameIdsNode = xmlDocument.CreateElement("GameIds");
                    foreach (var id in gamesAndIds[keyName])
                    {
                        var gameIdNode = xmlDocument.CreateElement("GameId");
                        gameIdNode.InnerText = id;
                        gameIdsNode.AppendChild(gameIdNode);
                    }
                    configNode.AppendChild(gameIdsNode);
                }

                var launchboxIdNode = xmlDocument.CreateElement("LaunchboxId");
                launchboxIdNode.InnerText = configDir.Substring(configDir.IndexOf("id#") + "id#".Length);
                configNode.AppendChild(launchboxIdNode);

                xmlDocument.DocumentElement.AppendChild(configNode);
                xmlDocument.Save(filename);
            }
        }

        static void SetGamesWithIds()
        {
            var database = File.ReadAllText("GameIndex.dbf");

            var gameList = database.Split("-- Game List")[1];
            gameList = Regex.Replace(gameList, "\\[patches(.|\n)*?\\[/patches\\]", string.Empty);
            gameList = Regex.Replace(gameList, "//.*", string.Empty);
            var games = gameList.Split("---------------------------------------------").Skip(1).ToArray();

            var iniParser = new IniDataParser();
            foreach(var game in games)
            {
                var data = iniParser.Parse(game).Global;
                if (data["Name"] == null || data["Serial"] == null) continue;
                data["Name"] = Regex.Replace(data["Name"], "\\[.*?\\]", string.Empty);
                data["Name"] = Regex.Replace(data["Name"], "[<>:\"/\\\\|?\\*-]", string.Empty);
                data["Name"] = data["Name"].Replace("  ", " ").Trim();
                gamesAndIds ??= new SortedDictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                if(gamesAndIds.ContainsKey(data["Name"])) gamesAndIds[data["Name"]].Add(data["Serial"]);
                else gamesAndIds.Add(data["Name"], new List<string> { data["Serial"] });
            }
        }
    }
}
