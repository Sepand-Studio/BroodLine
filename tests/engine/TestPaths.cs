using System;
using System.IO;
using Xunit;

namespace Broodline.Sim.Tests
{
    /// Where things are, found once instead of four times.
    ///
    /// Three separate files walked up from AppContext.BaseDirectory looking for
    /// Broodline.sln, with three different failure messages, and a fourth
    /// walked up looking for the .csproj - which reads like a typo and is not:
    /// the emitter wants the PROJECT folder so it rewrites the tracked baseline
    /// rather than the copy in bin/. Naming both here is what keeps that
    /// distinction visible instead of looking like drift.
    ///
    /// Walking rather than hardcoding a relative depth, so this survives a
    /// different TargetFramework or output layout.
    internal static class TestPaths
    {
        /// The repository root: the directory holding Broodline.sln.
        internal static string RepoRoot() => Above("Broodline.sln");

        /// The test project's own folder, for the files it owns as SOURCE
        /// rather than as build output.
        internal static string ProjectDir() => Above("Broodline.Sim.Tests.csproj");

        private static string Above(string sentinel)
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, sentinel)))
                dir = dir.Parent;

            Assert.True(dir != null,
                "could not find " + sentinel + " above " + AppContext.BaseDirectory);
            return dir.FullName;
        }

        /// Null rather than an assertion, for the one caller that wants to
        /// report the miss in its own words.
        internal static string RepoRootOrNull()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Broodline.sln")))
                dir = dir.Parent;
            return dir?.FullName;
        }
    }
}
