using System.ComponentModel;
using CmlLib.Core.Downloader;
using CmlLib.Core.Installer.Forge.Versions;

namespace CmlLib.Core.Installer.Forge;

public interface IForgeInstaller
{
    string VersionName { get; }
    ForgeVersion ForgeVersion { get; }
    event DownloadFileChangedHandler? FileChanged;
    event EventHandler<ProgressChangedEventArgs> ProgressChanged;
    event EventHandler<string>? InstallerOutput;
    Task Install(ForgeInstallOptions options);
}
