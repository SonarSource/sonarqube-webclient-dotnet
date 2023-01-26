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

using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Client.Models.ServerSentEvents.ServerContract;

namespace SonarQube.Client.Tests.Models.ServerSentEvents
{
    [TestClass]
    public class SqServerSentEventParserTests
    {
        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        public void Parse_EmptyString_Null(string eventString)
        {
            var testSubject = CreateTestSubject();

            var result = testSubject.Parse(eventString);

            result.Should().BeNull();
        }

        [TestMethod]
        [DataRow(@"
data: some data
        ")] // no event type field
        [DataRow(@"
event: 
data: some data
        ")] // empty event type field
        [DataRow(@"
event : aaa
data: some data
        ")] // "event :" and not "event:"
        public void Parse_InvalidEventType_Null(string eventString)
        {
            var testSubject = CreateTestSubject();

            var result = testSubject.Parse(eventString);

            result.Should().BeNull();
        }

        [TestMethod]
        [DataRow(@"
event: some type
        ")] // no event data field
        [DataRow(@"
event: some type
data: 
        ")] // empty data field
        [DataRow(@"
event: some type
data : some data
        ")] // "data :" and not "data:"
        public void Parse_InvalidEventData_Null(string eventString)
        {
            var testSubject = CreateTestSubject();

            var result = testSubject.Parse(eventString);

            result.Should().BeNull();
        }

        [TestMethod]
        public void Parse_CorrectEventString_ParsedEvent()
        {
            const string eventString = @"
event: some event type
data: some event data
            ";

            var testSubject = CreateTestSubject();

            var result = testSubject.Parse(eventString);

            result.Should().NotBeNull();
            result.Type.Should().Be("some event type");
            result.Data.Should().Be("some event data");
        }

        [TestMethod]
        public void Parse_CorrectEventString_MultilineData_ParsedEvent()
        {
            const string eventString = @"
event: some event type
data: some event data1
data: 
data: some event data2
            ";

            var testSubject = CreateTestSubject();

            var result = testSubject.Parse(eventString);

            result.Should().NotBeNull();
            result.Type.Should().Be("some event type");
            result.Data.Should().Be("some event data1some event data2");
        }

        [TestMethod]
        public void Parse_HasJunkFields_JunkFieldsIgnored()
        {
            const string eventString = @"
junk1: junk field1
EVENT: junk event type
event:
event: some event type
data: 
data: some event data1
junk2: junk field2
DATA: junk data2
data: some event data2
junk3: junk field3
            ";

            var testSubject = CreateTestSubject();

            var result = testSubject.Parse(eventString);

            result.Should().NotBeNull();
            result.Type.Should().Be("some event type");
            result.Data.Should().Be("some event data1some event data2");
        }

        private static SqServerSentEventParser CreateTestSubject() => new();
    }
}
