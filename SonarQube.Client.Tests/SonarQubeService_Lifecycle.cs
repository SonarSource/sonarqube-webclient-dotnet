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
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Moq.Protected;
using SonarQube.Client.Api;
using SonarQube.Client.Helpers;
using SonarQube.Client.Logging;
using SonarQube.Client.Models;
using SonarQube.Client.Requests;

namespace SonarQube.Client.Tests
{
    [TestClass]
    public class SonarQubeService_Lifecycle : SonarQubeService_TestBase
    {
        [TestMethod]
        public async Task Connect_To_SonarQube_Valid_Credentials()
        {
            // The earliest version that supports authentication
            SetupRequest("api/server/version", "3.3.0.0");
            SetupRequest("api/authentication/validate", "{ \"valid\": true }");

            service.IsConnected.Should().BeFalse();
            service.SonarQubeVersion.Should().BeNull();

            await service.ConnectAsync(
                new Models.ConnectionInformation(new Uri("http://localhost"), "user", "pass".ToSecureString()),
                CancellationToken.None);

            service.IsConnected.Should().BeTrue();
            service.SonarQubeVersion.Should().Be(new Version("3.3.0.0"));
        }

        [TestMethod]
        public void Connect_To_SonarQube_Invalid_Credentials()
        {
            // The earliest version that supports authentication
            SetupRequest("api/server/version", "3.3.0.0");
            SetupRequest("api/authentication/validate", "{ \"valid\": false }");

            service.IsConnected.Should().BeFalse();
            service.SonarQubeVersion.Should().BeNull();

            Func<Task> action = async () => await service.ConnectAsync(
                new Models.ConnectionInformation(new Uri("http://localhost"), "user", "pass".ToSecureString()),
                CancellationToken.None);

            action.Should().ThrowExactly<InvalidOperationException>()
                .And.Message.Should().Be("Invalid credentials");

            service.IsConnected.Should().BeFalse();
            service.SonarQubeVersion.Should().BeNull();
        }

        [TestMethod]
        public async Task Disconnect_Does_Not_Dispose_MessageHandler()
        {
            // Regression test for #689 - LoggingMessageHandler is disposed on disconnect

            // Arrange
            messageHandler.Protected().Setup("Dispose", true);
            await ConnectToSonarQube();

            // Act. Disconnect should not throw
            service.Disconnect();

            // Assert
            service.IsConnected.Should().BeFalse();
            service.SonarQubeVersion.Should().BeNull();
            messageHandler.Protected().Verify("Dispose", Times.Never(), true);
        }

        [TestMethod]
        public async Task Dispose_Does_Dispose_MessageHandler()
        {
            // Arrange
            messageHandler.Protected().Setup("Dispose", true);
            await ConnectToSonarQube();

            // Act
            service.Dispose();

            // Assert
            service.IsConnected.Should().BeFalse();
            service.SonarQubeVersion.Should().BeNull();
            messageHandler.Protected().Verify("Dispose", Times.Once(), true);
        }

        [TestMethod]
        public async Task InvokeRequest_PassesExpectedParameters()
        {
            // Arrange
            var token = new CancellationToken(true);
            var expectedPlugins = new SonarQubePlugin[] { new SonarQubePlugin("key1", "version1") };
            DummyGetPluginsRequest.SetInvokeResponse(expectedPlugins);

            // Register a dummy request with a high-enough version to make sure it's
            // the dummy request that is used
            requestFactory.RegisterRequest<IGetPluginsRequest, DummyGetPluginsRequest>("999.9.9");
            await ConnectToSonarQube(version: "999.9.9", "https://myserver");

            // Act
            var result = await service.GetAllPluginsAsync(token);

            // Assert
            // Strictly speaking the logger is set on creation not invocation, but
            // we'll check it anyway
            DummyGetPluginsRequest.SuppliedLogger.Should().BeSameAs(logger);

            DummyGetPluginsRequest.InvocationCount.Should().Be(1);
            DummyGetPluginsRequest.SuppliedHttpClient.BaseAddress.AbsoluteUri.Should().Be("https://myserver/");
            DummyGetPluginsRequest.SuppliedService.Should().BeSameAs(service);
            DummyGetPluginsRequest.SuppliedCancellationToken.Should().Be(token);

            result.Should().BeSameAs(expectedPlugins);
        }

        private class DummyGetPluginsRequest : IGetPluginsRequest
        {
            // We don't control the creation of instances of this class
            // so we have to use statics to capture the data from the InvokeAsync calls.
            public static int InvocationCount { get; private set; }
            public static ILogger SuppliedLogger { get; private set; }
            public static HttpClient SuppliedHttpClient { get; private set; }
            public static ISonarQubeService SuppliedService { get; private set; }
            public static CancellationToken SuppliedCancellationToken { get; private set; }

            private static SonarQubePlugin[] pluginsToReturn;

            public static void SetInvokeResponse(SonarQubePlugin[] plugins) => pluginsToReturn = plugins;

            ILogger IRequest.Logger { get => throw new NotImplementedException(); set { SuppliedLogger = value; } }

            Task<SonarQubePlugin[]> IRequest<SonarQubePlugin[]>.InvokeAsync(HttpClient httpClient, ISonarQubeService service, CancellationToken token)
            {
                InvocationCount++;
                SuppliedHttpClient = httpClient;
                SuppliedService = service;
                SuppliedCancellationToken = token;
                return Task.FromResult<SonarQubePlugin[]>(pluginsToReturn);
            }
        }
    }
}
