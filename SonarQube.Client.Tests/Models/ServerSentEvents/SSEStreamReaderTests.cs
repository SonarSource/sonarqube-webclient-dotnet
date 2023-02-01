﻿/*
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
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SonarQube.Client.Models.ServerSentEvents;
using SonarQube.Client.Models.ServerSentEvents.ClientContract;
using SonarQube.Client.Models.ServerSentEvents.ServerContract;
using Newtonsoft.Json;

namespace SonarQube.Client.Tests.Models.ServerSentEvents
{
    [TestClass]
    public class SSEStreamReaderTests
    {
        [TestMethod]
        public async Task GetNextEventOrNullAsync_Null_NullReturned()
        {
            var channel = CreateChannel((ISqServerEvent) null);

            var testSubject = CreateTestSubject(sqEventsChannel: channel);

            var result = await testSubject.GetNextEventOrNullAsync();

            result.Should().BeNull();
        }

        [TestMethod]
        public void GetNextEventOrNullAsync_UnrecognizedEventType_NotSupportedException()
        {
            var channel = CreateChannel(new SqServerEvent("some type 111", "some data"));

            var testSubject = CreateTestSubject(sqEventsChannel: channel);

            Func<Task<IServerEvent>> func = async () => await testSubject.GetNextEventOrNullAsync();;

            func.Should().ThrowExactly<NotSupportedException>().And.Message.Should().Contain("some type 111");
        }

        [TestMethod]
        public void GetNextEventOrNullAsync_FailureToDeserializeTheEventData_Exception()
        {
            var channel = CreateChannel(new SqServerEvent("IssueChanged", "some invalid data"));

            var testSubject = CreateTestSubject(sqEventsChannel: channel);

            Func<Task<IServerEvent>> func = async () => await testSubject.GetNextEventOrNullAsync(); ;

            func.Should().ThrowExactly<JsonReaderException>();
        }

        [TestMethod]
        public async Task GetNextEventOrNullAsync_IssueChangedEventType_DeserializedEvent()
        {
            const string serializedIssueChangedEvent =
                "{\"projectKey\": \"projectKey1\",\"issues\": [{\"issueKey\": \"key1\",\"branchName\": \"master\"}],\"resolved\": \"true\"}";

            var channel = CreateChannel(new SqServerEvent("IssueChanged", serializedIssueChangedEvent));

            var testSubject = CreateTestSubject(sqEventsChannel: channel);

            var result = await testSubject.GetNextEventOrNullAsync();

            result.Should().NotBeNull();
            result.Should().BeOfType<IIssueChangedServerEvent>();
        }

        private Channel<ISqServerEvent> CreateChannel(params ISqServerEvent[] events)
        {
            var channel = Channel.CreateUnbounded<ISqServerEvent>();

            foreach (var sqServerEvent in events)
            {
                channel.Writer.TryWrite(sqServerEvent);
            }

            return channel;
        }

        private SSEStreamReader CreateTestSubject(
            ISqServerSentEventParser sqServerSentEventParser = null,
            Stream serverStream = null,
            CancellationToken? cancellationToken = null,
            Channel<ISqServerEvent> sqEventsChannel = null)
        {
            sqServerSentEventParser ??= Mock.Of<ISqServerSentEventParser>();
            serverStream ??= Mock.Of<Stream>();
            cancellationToken ??= CancellationToken.None;
            sqEventsChannel ??= Mock.Of<Channel<ISqServerEvent>>();

            return new SSEStreamReader(serverStream, cancellationToken.Value, sqServerSentEventParser, sqEventsChannel);
        }
    }
}
