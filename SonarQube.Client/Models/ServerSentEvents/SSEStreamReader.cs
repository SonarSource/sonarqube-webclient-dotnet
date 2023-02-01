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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SonarQube.Client.Models.ServerSentEvents.ClientContract;
using SonarQube.Client.Models.ServerSentEvents.ServerContract;

namespace SonarQube.Client.Models.ServerSentEvents
{
    /// <summary>
    /// Returns <see cref="IServerEvent"/> deserialized from <see cref="ISqServerEvent"/>
    /// Code on the java side: https://github.com/SonarSource/sonarlint-core/blob/4f34c7c844b12e331a61c63ad7105acac41d2efd/server-api/src/main/java/org/sonarsource/sonarlint/core/serverapi/push/PushApi.java
    /// </summary>
    internal class SSEStreamReader : ISSEStreamReader
    {
        private readonly Stream serverStream;
        private readonly CancellationToken cancellationToken;
        private readonly ISqServerSentEventParser sqServerSentEventParser;
        private readonly Channel<ISqServerEvent> sqEventsChannel;

        private IDictionary<string, Type> eventConverters = new Dictionary<string, Type>
        {
            {"IssueChanged", typeof(IIssueChangedServerEvent)},
            {"TaintVulnerabilityClosed", typeof(ITaintVulnerabilityClosedServerEvent)},
            {"TaintVulnerabilityRaised", typeof(ITaintVulnerabilityRaisedServerEvent)}
        };

        public SSEStreamReader(Stream serverStream, CancellationToken cancellationToken)
            : this(serverStream, cancellationToken, new SqServerSentEventParser())
        {
        }

        internal SSEStreamReader(Stream serverStream,
            CancellationToken cancellationToken,
            ISqServerSentEventParser sqServerSentEventParser)
        {
            this.serverStream = serverStream;
            this.cancellationToken = cancellationToken;
            this.sqServerSentEventParser = sqServerSentEventParser;

            sqEventsChannel = Channel.CreateUnbounded<ISqServerEvent>();
        }

        public async Task BeginListening()
        {
            var eventLines = new List<string>();

            using (var streamReader = new StreamReader(serverStream))
            {
                while (!streamReader.EndOfStream && !cancellationToken.IsCancellationRequested)
                {
                    var line = await streamReader.ReadLineAsync();
                    var isPreviousLineALineBreak = eventLines.Last() == "\n";
                    var isEventEnd = line == "\n" && isPreviousLineALineBreak;

                    eventLines.Add(line);

                    if (isEventEnd)
                    {
                        var parsedEvent = sqServerSentEventParser.Parse(eventLines);
                        await sqEventsChannel.Writer.WriteAsync(parsedEvent, cancellationToken);
                        eventLines.Clear();
                    }
                }
            }
        }

        public async Task<IServerEvent> GetNextEventOrNullAsync()
        {
            var sqEvent = await sqEventsChannel.Reader.ReadAsync(cancellationToken);

            if (sqEvent == null)
            {
                return null;
            }

            if (!eventConverters.ContainsKey(sqEvent.Type))
            {
                throw new NotSupportedException($"Unknown ServerEventType: {sqEvent.Type}");
            }

            var deserializedEvent = JsonConvert.DeserializeObject(sqEvent.Data, eventConverters[sqEvent.Type]);

            return (IServerEvent) deserializedEvent;
        }

        public void Dispose()
        {
            sqEventsChannel?.Writer.Complete();
        }
    }
}
