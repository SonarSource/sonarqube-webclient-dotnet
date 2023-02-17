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
using System.Threading;
using System.Threading.Tasks;

namespace SonarQube.Client.Models.ServerSentEvents.ServerContract
{
    /// <summary>
    /// Reads lines from the network stream and aggregates them into <see cref="ISqServerEvent"/>.
    /// </summary>
    /// <returns>
    /// Returns aggregated <see cref="ISqServerEvent"/> or null if the stream ended or the task was cancelled.
    /// Will throw if there was a problem reading from the underlying stream.
    /// </returns>
    internal interface ISqSSEStreamReader : IDisposable
    {
        Task<ISqServerEvent> ReadAsync();
    }

    /// <summary>
    /// Code on the java side: https://github.com/SonarSource/sonarlint-core/blob/171ca4d75c24033e115a81bd7481427cd1f39f4c/server-api/src/main/java/org/sonarsource/sonarlint/core/serverapi/stream/EventBuffer.java
    /// </summary>
    internal sealed class SqSSEStreamReader : ISqSSEStreamReader
    {
        private readonly string projectKey;
        private readonly ISSEConnectionFactory sseConnectionFactory;
        private const int MaxReconnectionsCount = 5; // possibly needs to be configurable
        private readonly CancellationToken disposalToken;
        private readonly ISqServerSentEventParser sqServerSentEventParser;
        private StreamReader currentNetworkStreamReader;
        private int reconnectionsCount = -1;

        public SqSSEStreamReader(string projectKey, ISSEConnectionFactory sseConnectionFactory, CancellationToken disposalToken)
            : this(projectKey, sseConnectionFactory, disposalToken, new SqServerSentEventParser())
        {
        }

        internal SqSSEStreamReader(
            string projectKey,
            ISSEConnectionFactory sseConnectionFactory,
            CancellationToken disposalToken,
            ISqServerSentEventParser sqServerSentEventParser)
        {
            this.projectKey = projectKey;
            this.sseConnectionFactory = sseConnectionFactory;
            this.disposalToken = disposalToken;
            this.sqServerSentEventParser = sqServerSentEventParser;
        }

        public async Task<ISqServerEvent> ReadAsync()
        {
            var eventLines = new List<string>();

            while (!disposalToken.IsCancellationRequested
                   && await CheckCurrentStreamReaderAndReconnect() 
                   && !currentNetworkStreamReader.EndOfStream /*I recently discovered that this tries to read the underlying stream synchronously*/)
            {
                var (isReadSuccessful, line) = await TryReadLine();

                if (isReadSuccessful)
                {
                    continue;
                }

                var isEventEnd = string.IsNullOrEmpty(line);

                if (isEventEnd)
                {
                    var parsedEvent = sqServerSentEventParser.Parse(eventLines);

                    eventLines.Clear();

                    if (parsedEvent != null)
                    {
                        return parsedEvent;
                    }
                }
                else
                {
                    eventLines.Add(line);
                }
            }

            return null;
        }

        public void Dispose()
        {
            currentNetworkStreamReader?.Dispose();
        }

        private async Task<bool> CheckCurrentStreamReaderAndReconnect()
        {
            if (currentNetworkStreamReader != null)
            {
                return true;
            }

            return await TryReconnect();
        }

        private async Task<bool> TryReconnect()
        {
            if (reconnectionsCount++ > MaxReconnectionsCount)
            {
                return false;
            }
            
            // possibly add await Task.Delay(reconnectionsCount * baseRetryDelayMs)

            // possibly need to wrap this into try/catch as well
            var sseStream = await sseConnectionFactory.CreateSSEConnectionAsync(projectKey, disposalToken);

            if (sseStream == null)
            {
                return false;
            }

            currentNetworkStreamReader = new StreamReader(sseStream);
            return true;
        }

        private async Task<(bool isSuccessful, string line)> TryReadLine()
        {
            try
            {
                return (true, await currentNetworkStreamReader.ReadLineAsync());
            }
            catch (Exception)
            {
                currentNetworkStreamReader.Dispose();
                currentNetworkStreamReader = null;
                return (false, null);
            }
        }
    }
}
