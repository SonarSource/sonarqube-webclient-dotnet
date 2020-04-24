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

using SonarQube.Client.Models;

namespace SonarQube.Client.RoslynExporterAdapter
{
    public static class RoslynPluginRuleKeyExtensions
    {
        private const string ROSLYN_REPOSITORY_PREFIX = "roslyn.";

        private const string CSharpRepositoryKey = "csharpsquid";
        private const string CSharpPropertyPrefix = "sonaranalyzer-cs";

        private const string VBNetRepositoryKey = "vbnet";
        private const string VBNetPropertyPrefix = "sonaranalyzer-vbnet";

        public static string TryGetRoslynPluginPropertyPrefix(this SonarQubeRule rule)
        {
            // The SonarC# and Sonar VBNet repositories don't following the same prefix-naming rules
            // as third-party plugins - they have their own custom prefixes.
            if (CSharpRepositoryKey.Equals(rule.RepositoryKey))
            {
                return CSharpPropertyPrefix;
            }

            if (VBNetRepositoryKey.Equals(rule.RepositoryKey))
            {
                return VBNetPropertyPrefix;
            }

            // For plugins created by the SonarQube Roslyn SDK, the repository key is in the form
            // "roslyn.MyCustomAnalyzer", and the Sonar properties added by the custom plugin will
            // start with "MyCustomAnalyzer." e.g.
            // * MyCustomAnalyzer.analyzerId
            // * MyCustomAnalyzer.pluginVersion
            if (rule.RepositoryKey.Length > ROSLYN_REPOSITORY_PREFIX.Length &&
                rule.RepositoryKey.StartsWith(ROSLYN_REPOSITORY_PREFIX))
            {
                return rule.RepositoryKey.Substring(ROSLYN_REPOSITORY_PREFIX.Length);
            }

            return null; // not a Roslyn-based rule
        }
    }
}
