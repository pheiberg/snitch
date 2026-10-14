using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using Spectre.Console;

namespace Snitch.Analysis
{
    internal sealed class ProjectFileReporter
    {
        private static readonly JsonSerializerOptions _options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        private readonly IAnsiConsole _console;

        public ProjectFileReporter(IAnsiConsole console)
        {
            _console = console ?? throw new ArgumentNullException(nameof(console));
        }

        public void WriteToFile(IReadOnlyList<ProjectAnalyzerResult> results, string path, bool noPreRelease)
        {
            File.WriteAllText(path, Serialize(results, noPreRelease));

            _console.WriteLine();
            _console.MarkupLine($"[green]Results written to {path.EscapeMarkup()}[/]");
            _console.WriteLine();
        }

        internal static string Serialize(IReadOnlyList<ProjectAnalyzerResult> results, bool noPreRelease)
        {
            var projects = results
                .Where(x => !x.NoPackagesToRemove || (noPreRelease && x.HasPreReleases))
                .Select(x => new ProjectReport(
                    x.Project,
                    x.CanBeRemoved.Select(y => new RemovablePackage(
                        y.Package.Name,
                        GetVersion(y.Package),
                        y.Original.Project.Name)).ToList(),
                    x.MightBeRemoved.Select(y => new MismatchedPackage(
                        y.Package.Name,
                        GetVersion(y.Package),
                        y.Original.Project.Name,
                        GetVersion(y.Original.Package),
                        y.MismatchReason.ToString())).ToList(),
                    GetPreReleases(x, noPreRelease)));

            return JsonSerializer.Serialize(projects, _options);
        }

        private static List<PreReleasePackage> GetPreReleases(ProjectAnalyzerResult result, bool noPreRelease)
        {
            return noPreRelease
                ? result.PreReleasePackages.Select(x => new PreReleasePackage(x.Name, GetVersion(x))).ToList()
                : new List<PreReleasePackage>();
        }

        private static string? GetVersion(Package package)
        {
            return package.Version == null && package.Range == null ? null : package.GetVersionString();
        }

        private sealed record ProjectReport(
            string Project,
            IReadOnlyList<RemovablePackage> CanBeRemoved,
            IReadOnlyList<MismatchedPackage> MightBeRemoved,
            IReadOnlyList<PreReleasePackage> PreRelease);

        private sealed record RemovablePackage(string PackageName, string? PackageVersion, string ReferencedBy);

        private sealed record MismatchedPackage(
            string PackageName,
            string? PackageVersion,
            string ReferencedBy,
            string? ReferencePackageVersion,
            string Reason);

        private sealed record PreReleasePackage(string PackageName, string? PackageVersion);
    }
}