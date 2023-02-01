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

using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SonarQube.Client.Models.ServerSentEvents;
using SonarQube.Client.Models.ServerSentEvents.ClientContract;

namespace SonarQube.Client.Tests.Models.ServerSentEvents
{
    [TestClass]
    public class SSEStreamTests
    {
        [TestMethod]
        public void BeginListening_CallsWriter()
        {
            var writerTask = Task.FromResult(5);
            var writer = new Mock<ISSEStreamWriter>();
            writer.Setup(x => x.BeginListening()).Returns(writerTask);

            var testSubject = CreateTestSubject(writer: writer.Object);

            var result = testSubject.BeginListening();

            result.Should().Be(writerTask);
        }

        [TestMethod]
        public void GetNextEventOrNullAsync_CallsReader()
        {
            var readerTask = Task.FromResult(Mock.Of<IServerEvent>());
            var reader = new Mock<ISSEStreamReader>();
            reader.Setup(x => x.GetNextEventOrNullAsync()).Returns(readerTask);

            var testSubject = CreateTestSubject(reader: reader.Object);

            var result = testSubject.GetNextEventOrNullAsync();

            result.Should().Be(readerTask);
        }

        [TestMethod]
        public void Dispose_DisposesWriter()
        {
            var writer = new Mock<ISSEStreamWriter>();

            var testSubject = CreateTestSubject(writer: writer.Object);

            writer.Verify(x=> x.Dispose(), Times.Never);

            testSubject.Dispose();

            writer.Verify(x => x.Dispose(), Times.Once);
        }

        private SSEStream CreateTestSubject(ISSEStreamReader reader = null, ISSEStreamWriter writer = null) 
            => new(reader, writer);
    }
}
