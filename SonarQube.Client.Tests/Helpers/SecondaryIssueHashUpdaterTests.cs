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

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SonarQube.Client.Models;

namespace SonarQube.Client.Helpers.Tests
{
    [TestClass]
    public class SecondaryIssueHashUpdaterTests
    {
        private const string PrimaryIssueHash = "the primary issue hash should not be affected";
        private readonly IChecksumCalculator checksumCalculator = new ChecksumCalculator();

        [TestMethod]
        public async Task Populate_NoIssues_NoOp()
        {
            var serviceMock = new Mock<ISonarQubeService>();

            var testSubject = new SecondaryIssueHashUpdater();
            await testSubject.UpdateHashesAsync(Array.Empty<SonarQubeIssue>(), serviceMock.Object, CancellationToken.None);

            serviceMock.Invocations.Count.Should().Be(0);
        }

        [TestMethod]
        public async Task Populate_NoSecondaryLocations_NoOp()
        {
            var serviceMock = new Mock<ISonarQubeService>();
            var issues = new[]
            {
                CreateIssue("project1:key1", AddFlow()),
                CreateIssue("project2:key2", AddFlow())
            };

            var testSubject = new SecondaryIssueHashUpdater();
            await testSubject.UpdateHashesAsync(issues, serviceMock.Object, CancellationToken.None);

            serviceMock.Invocations.Count.Should().Be(0);
        }

        [TestMethod]
        public async Task Populate_HasSecondaryLocations_UniqueModulesFetched()
        {
            var issues = new[]
            {
                CreateIssue("primary_only_should_not_be_fetched_1",
                    AddFlow(
                        CreateLocation("duplicate"),
                        CreateLocation("duplicate"),
                        CreateLocation("unique1"),
                        CreateLocation("unique2")
                        )),
                CreateIssue("unique2",
                    AddFlow(
                        CreateLocation("unique3")
                        )),
                CreateIssue("primary_only_should_not_be_fetched_2",
                    AddFlow(
                        CreateLocation("unique4")
                        ))
            };

            // Only expecting the unique set of secondary locations to be requested
            var serviceMock = new Mock<ISonarQubeService>();
            AddSourceFile(serviceMock, "duplicate");
            AddSourceFile(serviceMock, "unique1");
            AddSourceFile(serviceMock, "unique2");
            AddSourceFile(serviceMock, "unique3");
            AddSourceFile(serviceMock, "unique4");

            var testSubject = new SecondaryIssueHashUpdater();
            await testSubject.UpdateHashesAsync(issues, serviceMock.Object, CancellationToken.None);

            serviceMock.VerifyAll();
            serviceMock.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task Populate_HasSecondaryLocations_ExpectedHashesSet()
        {
            const string line1Contents = "line one contents";
            const string line2Contents = " line two XXX ";
            const string line3Contents = "  LINE THREE!\"£$% ";

            var file1Contents = $"{line1Contents}\n{line2Contents}\nfoo foo foo";

            var file2Contents = $"111\n222\n{line3Contents}\n\n\n\n\n";

            var issues = new[]
            {
                CreateIssue("primary_only_should_not_be_fetched_1",
                    AddFlow(
                        CreateLocation("file1", startLine: 1),
                        CreateLocation("file1", startLine: 2)
                        )),
                CreateIssue("primary_only_should_not_be_fetched_2",
                    AddFlow(
                        CreateLocation("file2", startLine: 9999), // beyond the end of the file -> should be ignored
                        CreateLocation("file2", startLine: 3)
                        ))
            };

            var serviceMock = new Mock<ISonarQubeService>();
            AddSourceFile(serviceMock, "file1", file1Contents);
            AddSourceFile(serviceMock, "file2", file2Contents);

            var testSubject = new SecondaryIssueHashUpdater();

            // Act
            await testSubject.UpdateHashesAsync(issues, serviceMock.Object, CancellationToken.None);

            issues[0].Hash.Should().Be(PrimaryIssueHash);
            issues[1].Hash.Should().Be(PrimaryIssueHash);

            issues[0].Flows[0].Locations[0].Hash.Should().Be(checksumCalculator.Calculate(line1Contents));
            issues[0].Flows[0].Locations[1].Hash.Should().Be(checksumCalculator.Calculate(line2Contents));

            issues[1].Flows[0].Locations[0].Hash.Should().Be(null);
            issues[1].Flows[0].Locations[1].Hash.Should().Be(checksumCalculator.Calculate(line3Contents));
        }

        private static SonarQubeIssue CreateIssue(string moduleKey, params IssueFlow[] flows) =>
            new SonarQubeIssue("any", "any", PrimaryIssueHash, "any", moduleKey, "any", true,
                SonarQubeIssueSeverity.Blocker, DateTimeOffset.Now, DateTimeOffset.Now, null, flows.ToList());

        private static IssueFlow AddFlow(params IssueLocation[] locations) =>
            new IssueFlow(locations.ToList());

        private static IssueLocation CreateLocation(string moduleKey, int startLine = 1) =>
            new IssueLocation("any", moduleKey,
                new IssueTextRange(startLine, int.MaxValue, int.MaxValue, int.MaxValue),
                "any");

        private static void AddSourceFile(Mock<ISonarQubeService> serviceMock, string moduleKey, string data = "") =>
            serviceMock.Setup(x => x.GetSourceCodeAsync(moduleKey, It.IsAny<CancellationToken>())).Returns(Task.FromResult(data));
    }
}
