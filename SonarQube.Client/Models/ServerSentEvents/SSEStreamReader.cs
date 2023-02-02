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

using System.Threading;
using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SonarQube.Client.Models.ServerSentEvents.ClientContract;
using SonarQube.Client.Models.ServerSentEvents.ServerContract;
using System.Collections.Generic;
using System.Threading.Channels;

namespace SonarQube.Client.Models.ServerSentEvents
{
    public interface ISSEStreamReader
    {
        /// <summary>
        /// Will block the calling thread until an event exists or the connection is closed.
        /// Can throw an exception if the event is not a valid <see cref="IServerEvent"/>
        /// </summary>
        Task<IServerEvent> GetNextEventOrNullAsync();
    }

    /// <summary>
    /// Returns <see cref="IServerEvent"/> deserialized from <see cref="ISqServerEvent"/>
    /// Code on the java side: https://github.com/SonarSource/sonarlint-core/blob/4f34c7c844b12e331a61c63ad7105acac41d2efd/server-api/src/main/java/org/sonarsource/sonarlint/core/serverapi/push/PushApi.java
    /// </summary>
    internal class SSEStreamReader : ISSEStreamReader
    {
        private readonly ChannelReader<ISqServerEvent> sqEventsChannel;
        private readonly CancellationToken cancellationToken;

        private readonly IDictionary<string, Type> eventConverters = new Dictionary<string, Type>
        {
            {"IssueChanged", typeof(IssueChangedServerEvent)},
            // todo: support later
            // {"TaintVulnerabilityClosed", typeof(TaintVulnerabilityClosedServerEvent)},
            // {"TaintVulnerabilityRaised", typeof(TaintVulnerabilityRaisedServerEvent)}
        };

        public SSEStreamReader(ChannelReader<ISqServerEvent> sqEventsChannel, CancellationToken cancellationToken)
        {
            this.cancellationToken = cancellationToken;
            this.sqEventsChannel = sqEventsChannel;
        }

        public async Task<IServerEvent> GetNextEventOrNullAsync()
        {
            var sqEvent = await sqEventsChannel.ReadAsync(cancellationToken);

            if (sqEvent == null)
            {
                return null;
            }

            if (!eventConverters.ContainsKey(sqEvent.Type))
            {
                return null;
            }

            var deserializedEvent = JsonConvert.DeserializeObject(sqEvent.Data, eventConverters[sqEvent.Type]);

            return (IServerEvent)deserializedEvent;
        }
    }
}
