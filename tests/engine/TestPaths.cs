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
        private const string Solution = "Broodline.sln";

        /// The repository root: the directory holding Broodline.sln.
        internal static string RepoRoot() => Above(Solution);

        /// The test project's own folder, for the files it owns as SOURCE
        /// rather than as build output.
        internal static string ProjectDir() => Above("Broodline.Sim.Tests.csproj");

        /// Null rather than an assertion, for the one caller that wants to
        /// report the miss in its own words.
        ///
        /// Calls the same walk rather than repeating it. The first version of
        /// this file wrote the loop twice with the sentinel hardcoded in the
        /// second copy - in the file whose opening line is "found once instead
        /// of four times" - so renaming the solution would have left the
        /// asserting path working and the null-returning path silently
        /// answering null.
        internal static string RepoRootOrNull() => AboveOrNull(Solution);

        private static string Above(string sentinel)
        {
            var found = AboveOrNull(sentinel);
            Assert.True(found != null,
                "could not find " + sentinel + " above " + AppContext.BaseDirectory);
            return found;
        }

        private static string AboveOrNull(string sentinel)
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, sentinel)))
                dir = dir.Parent;
            return dir?.FullName;
        }
    }
}
