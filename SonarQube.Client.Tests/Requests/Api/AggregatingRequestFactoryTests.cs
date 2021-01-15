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
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SonarQube.Client.Requests;

namespace SonarQube.Client.Tests.Requests.Api
{
    [TestClass]
    public class AggregatingRequestFactoryTests
    {
        [TestMethod]
        public void Create_ReturnsFirstMatch()
        {
            var serverInfo = new ServerInfo(new Version(1, 2), ServerType.SonarCloud);
            var request = new DummyRequest();

            var factory1 = CreateFactory(serverInfo, null);
            var factory2 = CreateFactory(serverInfo, request);
            var factory3 = CreateFactory(serverInfo, null);

            var testSubject = new AggregatingRequestFactory(factory1.Object, factory2.Object, factory3.Object);

            var actual = testSubject.Create<DummyRequest>(serverInfo);

            actual.Should().BeSameAs(request);

            factory1.Invocations.Count.Should().Be(1);
            factory2.Invocations.Count.Should().Be(1);
            factory3.Invocations.Count.Should().Be(0);
        }

        [TestMethod]
        public void Create_NoMatches_ReturnsNull()
        {
            var serverInfo = new ServerInfo(new Version(1, 2), ServerType.SonarCloud);
            var factory1 = CreateFactory(serverInfo, null);

            var testSubject = new AggregatingRequestFactory(factory1.Object);

            var actual = testSubject.Create<DummyRequest>(serverInfo);

            actual.Should().BeNull();
        }

        private static Mock<IRequestFactory> CreateFactory(ServerInfo serverInfo, DummyRequest response)
        {
            var factory = new Mock<IRequestFactory>();
            factory.Setup(x => x.Create<DummyRequest>(serverInfo)).Returns(response);
            return factory;
        }

        public class DummyRequest : IRequest
        {
            public Logging.ILogger Logger { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        }
    }
}
