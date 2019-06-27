﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Build.Tasks.SourceControl;

namespace Microsoft.Build.Tasks.Git
{
    internal sealed class GitEnvironment
    {
        private const string LocalConfigurationScopeName = "local";
        private const string GitRepositoryConfigurationScopeName = "GitRepositoryConfigurationScope";

        public static readonly GitEnvironment Empty = new GitEnvironment();

        public string HomeDirectory { get; }
        public string XdgConfigHomeDirectory { get; }
        public string ProgramDataDirectory { get; }
        public string SystemDirectory { get; }

        // TODO: https://github.com/dotnet/sourcelink/issues/301
        // consider adding environment variables: GIT_DIR, GIT_DISCOVERY_ACROSS_FILESYSTEM, GIT_CEILING_DIRECTORIES
        // https://git-scm.com/docs/git#Documentation/git.txt-codeGITDIRcode
        // https://git-scm.com/docs/git#Documentation/git.txt-codeGITCEILINGDIRECTORIEScode
        // https://git-scm.com/docs/git#Documentation/git.txt-codeGITDISCOVERYACROSSFILESYSTEMcode
        //
        // if GIT_COMMON_DIR is set config worktree is ignored
        // https://git-scm.com/docs/git#Documentation/git.txt-codeGITCOMMONDIRcode
        // 
        // GIT_WORK_TREE overrides all other work tree settings:
        // https://git-scm.com/docs/git#Documentation/git.txt-codeGITWORKTREEcode

        public GitEnvironment(
            string homeDirectory = null,
            string xdgConfigHomeDirectory = null,
            string programDataDirectory = null,
            string systemDirectory = null)
        {
            if (!string.IsNullOrWhiteSpace(homeDirectory))
            {
                HomeDirectory = homeDirectory;
            }

            if (!string.IsNullOrWhiteSpace(xdgConfigHomeDirectory))
            {
                XdgConfigHomeDirectory = xdgConfigHomeDirectory;
            }

            if (!string.IsNullOrWhiteSpace(programDataDirectory))
            {
                ProgramDataDirectory = programDataDirectory;
            }

            if (!string.IsNullOrWhiteSpace(systemDirectory))
            {
                SystemDirectory = systemDirectory;
            }
        }

        public static GitEnvironment Create(string configurationScope)
        {
            if (string.IsNullOrEmpty(configurationScope))
            {
                return CreateFromProcessEnvironment();
            }

            if (string.Equals(configurationScope, LocalConfigurationScopeName, StringComparison.OrdinalIgnoreCase))
            {
                return Empty;
            }

            throw new NotSupportedException(string.Format(Resources.ValueOfIsNotValidConfigurationScope, GitRepositoryConfigurationScopeName, configurationScope));
        }

        public static GitEnvironment CreateFromProcessEnvironment()
        {
            return new GitEnvironment(
                homeDirectory: Environment.GetFolderPath(Environment.SpecialFolder.UserProfile, Environment.SpecialFolderOption.DoNotVerify),
                xdgConfigHomeDirectory: Environment.GetEnvironmentVariable("XDG_CONFIG_HOME"),
                programDataDirectory: Environment.GetEnvironmentVariable("PROGRAMDATA"),
                systemDirectory: FindSystemDirectory());
        }

        private static string FindSystemDirectory()
        {
            return PathUtils.IsUnixLikePlatform ? 
                (Environment.GetEnvironmentVariable("MICROSOFT_SOURCELINK_TEST_ENVIRONMENT_ETC_DIR") ?? "/etc") : 
                Path.Combine(FindWindowsGitInstallation(), "etc");
        }

        private static string FindWindowsGitInstallation()
        {
            Debug.Assert(!PathUtils.IsUnixLikePlatform);

            string[] paths;
            try
            {
                paths = Environment.GetEnvironmentVariable("PATH").Split(Path.PathSeparator);
            }
            catch
            {
                paths = Array.Empty<string>();
            }

            var gitExe = paths.FirstOrDefault(dir => File.Exists(Path.Combine(dir, "git.exe")));
            if (gitExe != null)
            {
                return Path.GetDirectoryName(gitExe);
            }

            var gitCmd = paths.FirstOrDefault(dir => File.Exists(Path.Combine(dir, "git.cmd")));
            if (gitCmd != null)
            {
                return Path.GetDirectoryName(gitCmd);
            }

#if REGISTRY // TODO
            string registryInstallLocation;
            try
            {
                using var regKey = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\Git_is1");
                registryInstallLocation = regKey?.GetValue("InstallLocation") as string;
            }
            catch
            {
                registryInstallLocation = null;
            }

            if (registryInstallLocation != null)
            {
                yield return Path.Combine(registryInstallLocation, subdirectory);
            }
#endif
            return null;
        }

        internal string GetHomeDirectoryForPathExpansion(string path)
            => HomeDirectory ?? throw new NotSupportedException(
                string.Format(Resources.HomeRelativePathsAreNotAllowed, GitRepositoryConfigurationScopeName, LocalConfigurationScopeName, path));
    }
}
