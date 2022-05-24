/*
 * SonarQube Client
 * Copyright (C) 2016-2022 SonarSource SA
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
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SonarQube.Client.Api.V7_20;
using SonarQube.Client.Tests.Infra;
using static SonarQube.Client.Tests.Infra.MocksHelper;

namespace SonarQube.Client.Tests.Requests.Api.V7_20
{
    [TestClass]
    public class GetExclusionsRequestTests
    {
        [TestMethod]
        [DataRow("sonar.sources")]
        [DataRow("sonar.exclusions")]
        [DataRow("sonar.global.exclusions")]
        [DataRow("sonar.inclusions")]
        [DataRow("sonar.global.inclusions")]
        public async Task InvokeAsync_MultiValueSetting(string keyName)
        {
            const string projectKey = "myproject";

            var testSubject = CreateTestSubject(projectKey);

            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            var httpClient = new HttpClient(handlerMock.Object)
            {
                BaseAddress = new Uri(ValidBaseAddress)
            };

            var request = $"api/settings/values?component={projectKey}&keys=sonar.exclusions%2Csonar.global.exclusions%2Csonar.inclusions%2Csonar.global.inclusions%2Csonar.sources";
            var response = $@"{{
	""settings"": [
		{{
			""key"": {keyName},
			""values"": [
				""**/value1"",
				""value2"",
				""some/value/3"",
			]
		}}
	]
}}";

            SetupHttpRequest(handlerMock, request, response);

            var result = await testSubject.InvokeAsync(httpClient, CancellationToken.None);
            result.Should().NotBeNull();

            result.Exclusions.Should().BeEquivalentTo("apps/backend/db/newsletters");
            result.GlobalExclusions.Should().BeEquivalentTo("**/build-wrapper-dump.json");
        }
        

        private static GetExclusionsRequest CreateTestSubject(string projectKey)
        {
            var testSubject = new GetExclusionsRequest
            {
                Logger = new TestLogger(),
                ProjectKey = projectKey
            };

            return testSubject;
        }
    }
}
