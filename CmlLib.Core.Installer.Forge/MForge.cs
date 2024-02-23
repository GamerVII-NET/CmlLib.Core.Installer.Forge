﻿using System.ComponentModel;
using CmlLib.Core.Downloader;
using CmlLib.Core.Installer.Forge.Versions;
using CmlLib.Core.Version;

namespace CmlLib.Core.Installer.Forge;

public class MForge
{
    public static readonly string ForgeAdUrl =
        "https://adfoc.us/serve/sitelinks/?id=271228&url=https://maven.minecraftforge.net/";

    private readonly IForgeInstallerVersionMapper _installerMapper;

    private readonly CMLauncher _launcher;
    private readonly ForgeVersionLoader _versionLoader;

    public MForge(CMLauncher launcher)
    {
        _launcher = launcher;
        _installerMapper = new ForgeInstallerVersionMapper();
        _versionLoader = new ForgeVersionLoader(new HttpClient());
    }

    public event DownloadFileChangedHandler? FileChanged;
    public event EventHandler<ProgressChangedEventArgs>? ProgressChanged;
    public event EventHandler<string>? InstallerOutput;

    private ForgeInstallOptions createDefaultOptions()
    {
        return new ForgeInstallOptions(_launcher.MinecraftPath)
        {
            Downloader = new SequenceDownloader()
        };
    }

    public Task<string> Install(string mcVersion, bool forceUpdate = false)
    {
        return Install(mcVersion, createDefaultOptions(), forceUpdate);
    }

    public async Task<string> Install(
        string mcVersion,
        ForgeInstallOptions options,
        bool forceUpdate = false)
    {
        var minecraftVersion = mcVersion;
        if (mcVersion.Contains("-")) minecraftVersion = mcVersion.Split("-").First();
        var versions = await _versionLoader.GetForgeVersions(minecraftVersion);

        ForgeVersion bestVersion = null;
        if (mcVersion.Contains("-"))
            bestVersion = versions.First(c => c.ForgeVersionName == mcVersion.Split("-forge-").Last());
        else
            bestVersion =
                versions.FirstOrDefault(v => v.IsRecommendedVersion) ??
                versions.FirstOrDefault(v => v.IsLatestVersion) ??
                versions.FirstOrDefault() ??
                throw new InvalidOperationException("Cannot find any version");

        return await Install(bestVersion, options, forceUpdate);
    }

    public Task<string> Install(string mcVersion, string forgeVersion, bool forceUpdate = false)
    {
        return Install(mcVersion, forgeVersion, createDefaultOptions(), forceUpdate);
    }

    public async Task<string> Install(
        string mcVersion,
        string forgeVersion,
        ForgeInstallOptions options,
        bool forceUpdate = false)
    {
        var versions = await _versionLoader.GetForgeVersions(mcVersion);

        var foundVersion = versions.FirstOrDefault(v => v.ForgeVersionName == forgeVersion) ??
                           throw new InvalidOperationException("Cannot find version name " + forgeVersion);
        return await Install(foundVersion, options, forceUpdate);
    }

    public async Task<string> Install(
        ForgeVersion forgeVersion,
        ForgeInstallOptions options,
        bool forceUpdate)
    {
        var installer = _installerMapper.CreateInstaller(forgeVersion);

        if (await checkVersionInstalled(installer.VersionName) && !forceUpdate)
            return installer.VersionName;

        var version = await checkAndDownloadVanillaVersion(forgeVersion.MinecraftVersionName);
        if (string.IsNullOrEmpty(options.JavaPath))
            options.JavaPath = getJavaPath(version);

        installer.FileChanged += e => FileChanged?.Invoke(e);
        installer.ProgressChanged += (s, e) => ProgressChanged?.Invoke(this, e);
        installer.InstallerOutput += (s, e) => InstallerOutput?.Invoke(this, e);
        await installer.Install(options);

        await _launcher.GetAllVersionsAsync();
        return installer.VersionName;
    }

    private async Task<MVersion> checkAndDownloadVanillaVersion(string mcVersion)
    {
        var version = await _launcher.GetVersionAsync(mcVersion);
        await _launcher.CheckAndDownloadAsync(version);
        return version;
    }

    private async Task<bool> checkVersionInstalled(string versionName)
    {
        try
        {
            await _launcher.GetVersionAsync(versionName);
            return true;
        }
        catch (KeyNotFoundException)
        {
            return false;
        }
    }

    private string getJavaPath(MVersion version)
    {
        var javaPath = _launcher.GetJavaPath(version);


        if (string.IsNullOrEmpty(javaPath) || !File.Exists(javaPath))
            javaPath = _launcher.GetDefaultJavaPath();
        if (string.IsNullOrEmpty(javaPath) || !File.Exists(javaPath))
            throw new InvalidOperationException("Cannot find any java binary. Set java binary path");


        if (File.Exists("/proc/self/cgroup"))
            javaPath = javaPath.Replace("w.exe", string.Empty);

        return javaPath;
    }
}
