﻿namespace PCSX2_Configurator.Settings
{
    public class VersionSettings
    {
        public string Name { get; set; }
        public string ArchiveName { get; set; }
        public string DownloadLink { get; set; }
        public string Directory { get; set; }
        public string Executable { get; set; } = "pcsx2.exe";
        public string InisDirectory { get; set; } = "inis";
        public bool IsDevBuild { get; set; }
        public bool ShouldUpdate { get; set; }
    }
}
