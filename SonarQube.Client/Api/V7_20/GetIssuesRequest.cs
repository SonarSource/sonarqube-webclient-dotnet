﻿/*
 * SonarQube Client
 * Copyright (C) 2016-2021 SonarSource SA
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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SonarQube.Client.Helpers;
using SonarQube.Client.Models;
using SonarQube.Client.Requests;

namespace SonarQube.Client.Api.V7_20
{
    public class GetIssuesRequest : PagedRequestBase<SonarQubeIssue>, IGetIssuesRequest
    {
        [JsonProperty("projects")]
        public virtual string ProjectKey { get; set; }

        [JsonProperty("statuses")]
        public string Statuses { get; set; }

        // This property is not present in the IGetIssuesRequest interface, it is meant to be
        // set by the GetIssuesRequestWrapper to add additional parameters to the API calls.
        [JsonProperty("types")]
        public string Types { get; set; }

        protected override string Path => "api/issues/search";

        protected override SonarQubeIssue[] ParseResponse(string response)
        {
            var root = JObject.Parse(response);

            // This is a paged request so ParseResponse will be called once for each "page"
            // of the response. However, we expect each page to be self-contained, so we want
            // to rebuild the lookup each time.
            componentKeyPathLookup = GetComponentKeyPathLookup(root);

            return root["issues"]
                .ToObject<ServerIssue[]>()
                .Select(ToSonarQubeIssue)
                .ToArray();
        }

        #region Json data classes -> public read-only class conversion methods

        /// <summary>
        /// Lookup component key -> path for files. Each response contains normalized data, containing
        /// issues and components, where each issue's "component" property points to a component with
        /// the same "key". We obtain the FilePath of each issue from its corresponding component.
        /// </summary>
        private ILookup<string, string> componentKeyPathLookup;

        private static ILookup<string, string> GetComponentKeyPathLookup(JObject root)
        {
            var components = root["components"] == null
                ? Array.Empty<ServerComponent>()
                : root["components"].ToObject<ServerComponent[]>();

            return components
                .Where(c => c.IsFile)
                .ToLookup(c => c.Key, c => c.Path); // Using a Lookup because it does not throw, unlike the Dictionary
        }

        private SonarQubeIssue ToSonarQubeIssue(ServerIssue issue) =>
            new SonarQubeIssue(issue.IssueKey, ComputePath(issue.Component), issue.Hash, issue.Message, ComputeModuleKey(issue),
                GetRuleKey(issue.CompositeRuleKey), issue.Status == "RESOLVED",
                SonarQubeIssueSeverityConverter.Convert(issue.Severity),
                issue.CreationDate,
                issue.UpdateDate,
                ToIssueTextRange(issue.TextRange),
                ToIssueFlows(issue.Flows));

        private string ComputePath(string component) =>
            FilePathNormalizer.NormalizeSonarQubePath(componentKeyPathLookup[component].FirstOrDefault() ?? string.Empty);

        private static string ComputeModuleKey(ServerIssue issue) =>
            issue.SubProject ?? issue.Component;

        private static string GetRuleKey(string compositeRuleKey) =>
            // ruleKey is "csharpsqid:S1234" or "vbnet:S1234" but we need S1234
            compositeRuleKey.Replace("vbnet:", string.Empty).Replace("csharpsquid:", string.Empty);

        private List<IssueFlow> ToIssueFlows(ServerIssueFlow[] serverIssueFlows) =>
            serverIssueFlows?.Select(ToIssueFlow).ToList();

        private IssueFlow ToIssueFlow(ServerIssueFlow serverIssueFlow) =>
            new IssueFlow(serverIssueFlow.Locations?.Select(ToIssueLocation).ToList());

        private IssueLocation ToIssueLocation(ServerIssueLocation serverIssueLocation) =>
            new IssueLocation(ComputePath(serverIssueLocation.Component), serverIssueLocation.Component, ToIssueTextRange(serverIssueLocation.TextRange), serverIssueLocation.Message);

        private static IssueTextRange ToIssueTextRange(ServerIssueTextRange serverIssueTextRange)
        {
            return serverIssueTextRange == null
                ? null
                : new IssueTextRange(serverIssueTextRange.StartLine,
                    serverIssueTextRange.EndLine,
                    serverIssueTextRange.StartOffset,
                    serverIssueTextRange.EndOffset);
        }

        #endregion Json data classes -> public read-only class conversion methods

        #region JSON data classes

        private class ServerIssue
        {
            [JsonProperty("key")]
            public string IssueKey { get; set; }

            [JsonProperty("rule")]
            public string CompositeRuleKey { get; set; }

            [JsonProperty("component")]
            public string Component { get; set; }

            [JsonProperty("subProject")]
            public string SubProject { get; set; }

            [JsonProperty("hash")]
            public string Hash { get; set; }

            [JsonProperty("message")]
            public string Message { get; set; }

            [JsonProperty("status")]
            public string Status { get; set; }

            [JsonProperty("severity")]
            public string Severity { get; set; }

            [JsonProperty("textRange")]
            public ServerIssueTextRange TextRange { get; set; }

            [JsonProperty("creationDate")]
            public virtual DateTimeOffset CreationDate { get; set; }

            [JsonProperty("updateDate")]
            public virtual DateTimeOffset UpdateDate { get; set; }

            [JsonProperty("flows")]
            public ServerIssueFlow[] Flows { get; set; }
        }

        private class ServerComponent
        {
            [JsonProperty("key")]
            public string Key { get; set; }
            [JsonProperty("qualifier")]
            public string Qualifier { get; set; }
            [JsonProperty("path")]
            public string Path { get; set; }

            public bool IsFile
            {
                get { return Qualifier == "FIL"; }
            }
        }

        private class ServerIssueFlow
        {
            [JsonProperty("locations")]
            public ServerIssueLocation[] Locations { get; set; }
        }

        private class ServerIssueLocation
        {
            [JsonProperty("component")]
            public string Component { get; set; }
            [JsonProperty("textRange")]
            public ServerIssueTextRange TextRange { get; set; }
            [JsonProperty("msg")]
            public string Message { get; set; }
        }

        private class ServerIssueTextRange
        {
            [JsonProperty("startLine")]
            public int StartLine { get; set; }
            [JsonProperty("endLine")]
            public int EndLine { get; set; }
            [JsonProperty("startOffset")]
            public int StartOffset { get; set; }
            [JsonProperty("endOffset")]
            public int EndOffset { get; set; }
        }

        #endregion // JSON data classes
    }
}
