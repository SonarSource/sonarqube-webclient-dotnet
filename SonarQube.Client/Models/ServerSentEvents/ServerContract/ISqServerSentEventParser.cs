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

using System.IO;
using System.Text;

namespace SonarQube.Client.Models.ServerSentEvents.ServerContract
{
    internal interface ISqServerSentEventParser
    {
        ISqServerEvent Parse(string eventString);
    }

    /// <summary>
    /// Parses the given string into <see cref="ISqServerEvent"/>
    /// </summary>
    /// <remarks>
    /// Parser code on the java side: https://github.com/SonarSource/sonarlint-core/blob/4f34c7c844b12e331a61c63ad7105acac41d2efd/server-api/src/main/java/org/sonarsource/sonarlint/core/serverapi/stream/EventParser.java#L24\
    /// </remarks>
    internal class SqServerSentEventParser : ISqServerSentEventParser
    {
        private const string EventTypeFieldPrefix = "event: ";
        private const string DataFieldPrefix = "data: ";

        public ISqServerEvent Parse(string eventString)
        {
            if (string.IsNullOrEmpty(eventString))
            {
                return null;
            }

            var parsedEventType = "";
            var parsedEventData = new StringBuilder();

            var textReader = new StringReader(eventString);
            string line;
            while ((line = textReader.ReadLine()) != null)
            {
                if (line.StartsWith(EventTypeFieldPrefix))
                {
                    parsedEventType = line.Substring(EventTypeFieldPrefix.Length);
                }
                else if (line.StartsWith(DataFieldPrefix))
                {
                    parsedEventData.Append(line.Substring(DataFieldPrefix.Length));
                }
            }

            if (string.IsNullOrEmpty(parsedEventType) || string.IsNullOrEmpty(parsedEventData.ToString()))
            {
                return null;
            }

            return new SqServerEvent(parsedEventType, parsedEventData.ToString());
        }
    }
}
