﻿/*
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
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Client.Models;
using SonarQube.Client.RoslynExporterAdapter;

namespace SonarQube.Client.Tests.RoslynExporterAdapter
{
    [TestClass]
    public class NuGetPackageInfoGeneratorTests
    {
        private static readonly IEnumerable<SonarQubeRule> ValidCSharpRules = new SonarQubeRule[]
        {
            CreateRule("csharpsquid", "rule1"),
            CreateRule("csharpsquid", "rule2")
        };

        [TestMethod]
        public void Get_NoActiveRules_NoPackageInfo()
        {
            var properties = new Dictionary<string, string>
            {
                { "sonaranalyzer-cs.analyzerId", "my.analyzer" },
                { "sonaranalyzer-cs.pluginVersion", "1.2.3" }
            };

            var actual = NuGetPackageInfoGenerator.GetNuGetPackageInfos(Array.Empty<SonarQubeRule>(), properties);

            actual.Should().BeEmpty();
        }

        [TestMethod]
        public void Get_NoSonarProperties_NoPackageInfo()
        {
            var rules = new SonarQubeRule[]
            {
                CreateRule("csharpsquid", "rule1"),
                CreateRule("csharpsquid", "rule2")
            };

            var actual = NuGetPackageInfoGenerator.GetNuGetPackageInfos(rules, new Dictionary<string, string>());

            actual.Should().BeEmpty();
        }

        [TestMethod]
        public void Get_MissingVersionProperty_NoPackageInfo()
        {
            var properties = new Dictionary<string, string> { { "sonaranalyzer-cs.analyzerId", "my.analyzer" } };

            var actual = NuGetPackageInfoGenerator.GetNuGetPackageInfos(ValidCSharpRules, properties);

            actual.Should().BeEmpty();
        }

        [TestMethod]
        public void Get_MissingPluginVerionProperty_NoPackageInfo()
        {
            var properties = new Dictionary<string, string> { { "sonaranalyzer-cs.pluginVersion", "1.2.3" } };

            var actual = NuGetPackageInfoGenerator.GetNuGetPackageInfos(ValidCSharpRules, properties);

            actual.Should().BeEmpty();
        }

        [TestMethod]
        public void Get_CSharp_WithPropertiesAndMatchingRules_ExpectedPackageReturned()
        {
            var properties = new Dictionary<string, string>
            {
                { "sonaranalyzer-cs.analyzerId", "my.analyzer" },
                { "sonaranalyzer-cs.pluginVersion", "3.2.1" }
            };

            var rules = new SonarQubeRule[]
            {
                CreateRule("csharpsquid", "rule2")
            };

            var actual = NuGetPackageInfoGenerator.GetNuGetPackageInfos(rules, properties);

            actual.Count().Should().Be(1);
            actual.First().Id.Should().Be("my.analyzer");
            actual.First().Version.Should().Be("3.2.1");
        }

        [TestMethod]
        public void Get_VBNet_WithPropertiesAndMatchingRules_ExpectedPackageReturned()
        {
            var properties = new Dictionary<string, string>
            {
                { "sonaranalyzer-vbnet.analyzerId", "sonarVB" },
                { "sonaranalyzer-vbnet.pluginVersion", "1.2" }
            };

            var rules = new SonarQubeRule[]
            {
                CreateRule("vbnet", "rule1")
            };

            var actual = NuGetPackageInfoGenerator.GetNuGetPackageInfos(rules, properties);

            actual.Count().Should().Be(1);
            actual.First().Id.Should().Be("sonarVB");
            actual.First().Version.Should().Be("1.2");
        }

        [TestMethod]
        public void Get_MultiplePlugins_WithPropertiesAndMatchingRules_ExpectedPackageReturned()
        {
            var properties = new Dictionary<string, string>
            {
                // Valid CSharp properties
                { "sonaranalyzer-cs.analyzerId", "analyzer.csharp" },
                { "sonaranalyzer-cs.pluginVersion", "version.csharp" },

                // Valid VBNet properties
                { "sonaranalyzer-vbnet.analyzerId", "analyzer.vb" },
                { "sonaranalyzer-vbnet.pluginVersion", "version.vb" },

                // First valid third-party analyzer properties
                { "myanalyzer1.analyzerId", "analyzer.myanalyzer1" },
                { "myanalyzer1.pluginVersion", "version.myanalyzer1" },

                // Second valid third-party analyzer properties
                { "wintellect.analyzerId", "analyzer.wintellect" },
                { "wintellect.pluginVersion", "version.wintellect" },

                // Invalid third-party analyzer properties - missing version
                { "invalid.analyzerId", "analyzer.invalid" }
            };

            var rules = new SonarQubeRule[]
            {
                CreateRule("csharpsquid", "rule1"),
                CreateRule("vbnet", "rule2"),
                CreateRule("roslyn.myanalyzer1", "rule3"),
                // no wintellect rule, so it should not be invluded
                CreateRule("roslyn.invalid", "rule3"),
            };

            var actual = NuGetPackageInfoGenerator.GetNuGetPackageInfos(rules, properties);

            actual.Count().Should().Be(3);
            var packages = actual.ToArray();

            packages[0].Id.Should().Be("analyzer.csharp");
            packages[0].Version.Should().Be("version.csharp");

            packages[1].Id.Should().Be("analyzer.vb");
            packages[1].Version.Should().Be("version.vb");

            packages[2].Id.Should().Be("analyzer.myanalyzer1");
            packages[2].Version.Should().Be("version.myanalyzer1");
        }


        private static SonarQubeRule CreateRule(string repoKey, string ruleKey) =>
            new SonarQubeRule(ruleKey, repoKey, true, SonarQubeIssueSeverity.Critical, null);
    }
}
