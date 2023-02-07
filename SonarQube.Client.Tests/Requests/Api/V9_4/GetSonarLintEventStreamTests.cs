/*
 * SonarQube Client
 * Copyright (C) 2016-2023 SonarSource SA
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
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Moq.Protected;
using SonarQube.Client.Api.V9_4;
using SonarQube.Client.Logging;

namespace SonarQube.Client.Tests.Requests.Api.V9_4
{
    [TestClass]
    public class GetSonarLintEventStreamTests
    {
        private const string ProjectKey = "someproj";
        private static readonly Uri BaseUrl = new ("http://localhost");
        private static readonly string RelativeUrl = $"api/push/sonarlint_events?languages=cs%2Cvbnet%2Ccpp%2Cc%2Cjs%2Cts&projectKeys={ProjectKey}";

        [TestMethod]
        public async Task InvokeAsync_ReturnsCorrectStream()
        {
            using var responseStream = new MemoryStream(Encoding.UTF8.GetBytes("hello this is a test"));
            var messageHandler = SetupMessageHandler(GetHttpResponseMessage(responseStream));

            using var httpClient = new HttpClient(messageHandler.Object) { BaseAddress = BaseUrl };
            var testSubject = CreateTestSubject();
            using var response = await testSubject.InvokeAsync(httpClient, CancellationToken.None);

            response.Should().NotBeNull();
            messageHandler.VerifyAll();
            await VerifyStreamContent(response, "hello this is a test");
;        }

        [TestMethod]
        [DataRow(HttpStatusCode.Unauthorized)]
        [DataRow(HttpStatusCode.NotFound)]
        [DataRow(HttpStatusCode.Forbidden)]
        public void InvokeAsync_ApiIsInaccessible_NonRecoverableStatusCode_ThrowsHttpExceptionAndDoesNotTryAgain(HttpStatusCode statusCode)
        {
            var messageHandler = SetupMessageHandler(GetHttpResponseMessage(httpStatusCode: statusCode));

            using var httpClient = new HttpClient(messageHandler.Object) { BaseAddress = BaseUrl };
            var testSubject = CreateTestSubject();

            Func<Task<Stream>> func = async () => await testSubject.InvokeAsync(httpClient, CancellationToken.None);

            func.Should().ThrowExactly<HttpRequestException>().And.Message.Should().Contain(((int)statusCode).ToString());

            messageHandler.Invocations.Count.Should().Be(1);
        }

        [TestMethod]
        public async Task InvokeAsync_ApiIsInaccessible_RecoverableStatusCode_TriesAgain()
        {
            using var responseStream = new MemoryStream(Encoding.UTF8.GetBytes("hello this is a test"));

            var messageHandler = SetupMessageHandler(
                GetHttpResponseMessage(httpStatusCode: HttpStatusCode.BadGateway),
                GetHttpResponseMessage(httpStatusCode: HttpStatusCode.GatewayTimeout),
                GetHttpResponseMessage(responseStream));

            using var httpClient = new HttpClient(messageHandler.Object) { BaseAddress = BaseUrl };
            var testSubject = CreateTestSubject();
            using var response = await testSubject.InvokeAsync(httpClient, CancellationToken.None);

            response.Should().NotBeNull();
            messageHandler.VerifyAll();
            messageHandler.Invocations.Count.Should().Be(3);

            await VerifyStreamContent(response, "hello this is a test");
        }

        [TestMethod]
        public void InvokeAsync_ApiIsInaccessible_TooManyRetries_ThrowsHttpExceptionAndDoesNotTryAgain()
        {
            var failedResponse = GetHttpResponseMessage(httpStatusCode: HttpStatusCode.BadGateway);
            var messageHandler = SetupMessageHandler(Enumerable.Repeat(failedResponse, 20).ToArray());

            using var httpClient = new HttpClient(messageHandler.Object) { BaseAddress = BaseUrl };
            var testSubject = CreateTestSubject();
            Func<Task<Stream>> func = async () => await testSubject.InvokeAsync(httpClient, CancellationToken.None);

            func.Should().ThrowExactly<HttpRequestException>().And.Message.Should().Contain(((int)HttpStatusCode.BadGateway).ToString());

            const int maxNumberOfAttempts = 10;
            messageHandler.Invocations.Count.Should().Be(maxNumberOfAttempts);
        }


        private static HttpResponseMessage GetHttpResponseMessage(
            Stream responseStream = null,
            HttpStatusCode httpStatusCode = HttpStatusCode.OK) =>
            new(httpStatusCode) {Content = new StreamContent(responseStream ?? Stream.Null)};

        private static Mock<HttpMessageHandler> SetupMessageHandler(params HttpResponseMessage[] responses)
        {
            var headers = new[] {MediaTypeHeaderValue.Parse("text/event-stream") };
            var messageHandler = new Mock<HttpMessageHandler>();

            var sendAsyncMock = messageHandler.Protected()
                .SetupSequence<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.Is<HttpRequestMessage>(m =>
                        m.RequestUri == new Uri(BaseUrl, RelativeUrl) &&
                        headers.All(header => m.Headers.Accept.Contains(header))),
                    ItExpr.IsAny<CancellationToken>());

            foreach (var httpResponseMessage in responses)
            {
                sendAsyncMock.ReturnsAsync(httpResponseMessage);
            }

            return messageHandler;
        }

        private static GetSonarLintEventStream CreateTestSubject() => 
            new() { ProjectKey = ProjectKey, Logger = Mock.Of<ILogger>() };

        private static async Task VerifyStreamContent(Stream response, string expectedContent)
        {
            using var reader = new StreamReader(response, Encoding.UTF8);
            var responseString = await reader.ReadToEndAsync();
            responseString.Should().Be(expectedContent);
        }

    }
}
