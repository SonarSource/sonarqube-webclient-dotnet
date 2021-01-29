/*
 * SonarQube Client
 * Copyright (C) 2016-2020 SonarSource SA
 * mailto:info AT sonarsource DOT com
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SonarQube.Client.Models;

namespace SonarQube.Client.Helpers
{
    /// <summary>
    /// Sets the hashes for any secondary locations in the supplied list of issues
    /// </summary>
    /// <remarks>
    /// Currently secondary location hashes are not stored server-side, so we have to
    /// calculate them ourselves. This means fetching the source code for each file
    /// so we can get the line text and calculate the hash.
    /// </remarks>
    internal class SecondaryIssueHashUpdater
    {
        private readonly IChecksumCalculator checksumCalculator;
        private Dictionary<string, string[]> moduleKeyToSourceLinesMap;


        public SecondaryIssueHashUpdater()
            : this(new ChecksumCalculator())
        {
        }

        internal /* for testing */ SecondaryIssueHashUpdater(IChecksumCalculator checksumCalculator)
        {
            this.checksumCalculator = checksumCalculator;
        }

        internal async Task UpdateHashesAsync(IEnumerable<SonarQubeIssue> issues,
            ISonarQubeService sonarQubeService,
            CancellationToken cancellationToken)
        {
            var secondaryLocations = GetSecondaryLocations(issues);

            var uniqueKeys = GetUniqueSecondaryLocationKeys(secondaryLocations);

            if (!uniqueKeys.Any())
            {
                // This will be the normal case: most issues don't have secondary locations
                return;
            }

            moduleKeyToSourceLinesMap = new Dictionary<string, string[]>();
            foreach (var key in uniqueKeys)
            {
                var sourceCode = await sonarQubeService.GetSourceCodeAsync(key, cancellationToken);
                Debug.Assert(sourceCode != null, "Not expecting the file contents to be null");
                AddSourceCodeToMap(key, sourceCode);
            }

            foreach (var location in GetSecondaryLocations(issues))
            {
                SetLineHash(location);
            }
        }

        private static IEnumerable<IssueLocation> GetSecondaryLocations(IEnumerable<SonarQubeIssue> issues) =>
            issues.SelectMany(
                issue => issue.Flows.SelectMany(
                    flow => flow.Locations))
            .ToArray();

        private static IEnumerable<string> GetUniqueSecondaryLocationKeys(IEnumerable<IssueLocation> locations) =>
            locations
                .Select(loc => loc.ModuleKey)
                .Distinct()
                .ToArray();

        private void AddSourceCodeToMap(string moduleKey, string source) =>
            moduleKeyToSourceLinesMap.Add(moduleKey, source.Split('\n'));

        private void SetLineHash(IssueLocation location)
        {
            // Issue locations can span multiple lines, but only the first line is used
            // when calculating the hash
            var firstLineOfIssue = GetLineText(location.ModuleKey, location.TextRange.StartLine);

            if (firstLineOfIssue != null)
            {
                location.Hash = checksumCalculator.Calculate(firstLineOfIssue);
            }
        }

        private string GetLineText(string moduleKey, int oneBasedLineNumber)
        {
            Debug.Assert(moduleKeyToSourceLinesMap.ContainsKey(moduleKey), "Unexpected module key requested");

            var lines = moduleKeyToSourceLinesMap[moduleKey];

            if (oneBasedLineNumber > lines.Length)
            {
                return null;
            }

            return lines[oneBasedLineNumber - 1];
        }
    }
}
