﻿using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using IniParser;
using IniParser.Model;
using Newtonsoft.Json;
using LibGit2Sharp;
using PCSX2_Configurator.Common;
using PCSX2_Configurator.Helpers;
using PCSX2_Configurator.Settings;

namespace PCSX2_Configurator.Services
{
    internal sealed class RemoteConfigService : IRemoteConfigService
    {
        private readonly string remoteConfigsPath;
        private readonly string remote = "https://github.com/Zombeaver/PCSX2-Configs";
        private readonly IConfigurationService configurationService;
        private readonly IEmulationService emulationService;
        private readonly IFileHelpers fileHelpers;
        private readonly XmlDocument remoteIndex;
        private readonly FileIniDataParser iniParser;

        public RemoteConfigService(AppSettings appSettings, FileIniDataParser iniParser, IConfigurationService configurationService, IEmulationService emulationService, IFileHelpers fileHelpers)
        {
            remoteConfigsPath = appSettings.RemoteConfigsPath ?? "Remote";
            this.iniParser = iniParser;
            this.configurationService = configurationService;
            this.emulationService = emulationService;
            this.fileHelpers = fileHelpers;
            remoteIndex = new XmlDocument();

            Task.Run(UpdateFromRemote).ContinueWith(task => remoteIndex.Load($"{remoteConfigsPath}\\RemoteIndex.xml"));
        }

        public void ImportConfig(string gameId, string emulatorPath)
        {
            if (!(remoteIndex.SelectSingleNode($"//Config[GameIds/GameId = contains(., '{gameId}')]") is XmlElement configElement)) return;
            var configDirectory = configElement.GetAttribute("Name");
            var configPath = $"{remoteConfigsPath}\\Game Configs\\{configDirectory}";
            var configName = Regex.Replace(configDirectory, "id#\\d+", "").Trim().ToLowerInvariant().Replace(" ", "-");
            var inisPath = emulationService.GetInisPath(emulatorPath);

            var importedConfigPath = configurationService.CreateConfig(configName, inisPath, ConfigOptions.DefaultForRemote);

            // PCSX2_ui.ini
            var targetUiFile = $"{importedConfigPath}\\{ConfiguratorConstants.UiFileName}";
            var targetUiConfig = iniParser.ReadFile(targetUiFile);
            var sourceUiConfig = new IniData();

            if (File.Exists($"{configPath}\\{ConfiguratorConstants.UiFileName}")) MergeUiConfig(sourceUiConfig, iniParser.ReadFile($"{configPath}\\{ConfiguratorConstants.UiFileName}"));
            if (File.Exists($"{configPath}\\pcsx2_ui-tweak.ini")) MergeUiConfig(sourceUiConfig, iniParser.ReadFile($"{configPath}\\pcsx2_ui-tweak.ini"));

            targetUiConfig.Merge(sourceUiConfig);
            iniParser.WriteFile(targetUiFile, targetUiConfig, Encoding.UTF8);

            // SPU2-X.ini
            var sourceSpu2xConfigFile = $"{configPath}\\{ConfiguratorConstants.Spu2xFileName}";
            if (File.Exists(sourceSpu2xConfigFile))
            {
                var targetSpu2xFile = $"{importedConfigPath}\\{ConfiguratorConstants.Spu2xFileName}";
                var targetSpu2xConfig = File.Exists(targetSpu2xFile) ? iniParser.ReadFile(targetSpu2xFile) : new IniData();
                var sourceSpu2xConfig = iniParser.ReadFile(sourceSpu2xConfigFile);

                MergeSpu2xConfig(targetSpu2xConfig, sourceSpu2xConfig);
                iniParser.WriteFile(targetSpu2xFile, targetSpu2xConfig, Encoding.UTF8);
            }

            // PCSX2_vm.ini
            var sourceVmConfigFile = $"{configPath}\\{ConfiguratorConstants.VmFileName}";
            if (File.Exists(sourceVmConfigFile))
            {
                var targetVmFile = $"{importedConfigPath}\\{ConfiguratorConstants.VmFileName}";
                var targetVmConfig = File.Exists(targetVmFile) ? iniParser.ReadFile(targetVmFile) : new IniData();
                var sourceVmConfig = iniParser.ReadFile(sourceVmConfigFile);

                MergeVmConfig(targetVmConfig, sourceVmConfig);
                iniParser.WriteFile(targetVmFile, targetVmConfig, Encoding.UTF8);
            }

            // GSdx.ini
            fileHelpers.CopyWithoutException($"{configPath}\\{ConfiguratorConstants.GsdxFileName}", $"{importedConfigPath}\\{ConfiguratorConstants.GsdxFileName}");

            // Cheats and Widescreen Patches
            foreach (var file in Directory.GetFiles(configPath, "*.pnach"))
            {
                var fileName = Path.GetFileName(file);
                var destination = $"{Path.GetDirectoryName(emulatorPath)}\\" + (fileName.EndsWith("_ws.pnach") ? $"cheats_ws\\{fileName.Replace("_ws", "")}" : $"cheats\\{fileName}");
                File.Copy(file, destination, overwrite: true);
            }

            // Game Ids
            var gameIds = configElement.SelectNodes("GameIds/GameId").Cast<XmlNode>().Select(x => Regex.Match(x.InnerText, "[A-Z]{4}-[0-9]{5}").Value);
            if(gameIds.Count() > 0) File.WriteAllText($"{importedConfigPath}\\gameids", string.Join(';', gameIds), Encoding.UTF8);

            // Remote File
            var remoteJson = JsonConvert.SerializeObject(new { status = configElement.SelectSingleNode("Status")?.InnerText, notes = configElement.SelectSingleNode("Notes")?.InnerText });
            File.WriteAllText($"{importedConfigPath}\\remote", remoteJson, Encoding.UTF8);
        }

        private static void MergeUiConfig(IniData target, IniData source)
        {
            target.Global.Merge(source.Global);
            target["Folders"].Merge(source["Folders"]);
            target["Filenames"].Merge(source["Filenames"]);
            target["GSWindow"].Merge(source["GSWindow"]);
        }

        private static void MergeSpu2xConfig(IniData target, IniData source)
        {
            target["MIXING"].Merge(source["MIXING"]);
            target["OUTPUT"].Merge(source["OUTPUT"]);
            target["WAVEOUT"].Merge(source["WAVEOUT"]);
            target["DSOUNDOUT"].Merge(source["DSOUNDOUT"]);
            target["PORTAUDIO"].Merge(source["PORTAUDIO"]);
            target["SOUNDTOUCH"].Merge(source["SOUNDTOUCH"]);
        }

        private static void MergeVmConfig(IniData target, IniData source)
        {
            target["EmuCore"].Merge(source["EmuCore"]);
            target["EmuCore/Speedhacks"].Merge(source["EmuCore/Speedhacks"]);
            target["EmuCore/CPU"].Merge(source["EmuCore/CPU"]);
            target["EmuCore/CPU/Recompiler"].Merge(source["EmuCore/CPU/Recompiler"]);
            target["EmuCore/GS"].Merge(source["EmuCore/GS"]);
            target["EmuCore/Gamefixes"].Merge(source["EmuCore/Gamefixes"]);
        }

        private void UpdateFromRemote()
        {
            if (Directory.Exists(remoteConfigsPath))
            {
                using var repo = new Repository(remoteConfigsPath);
                repo.Reset(ResetMode.Hard);
                Commands.Pull(repo, new Signature("MERGE_USER_NAME", "MERGE_USER_EMAIL", DateTimeOffset.Now), null);
            }
            else Repository.Clone(remote, remoteConfigsPath);
        }
    }
}
