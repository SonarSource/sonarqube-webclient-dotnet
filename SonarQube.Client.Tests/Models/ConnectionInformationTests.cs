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
using SonarQube.Client.Models;

namespace SonarQube.Client.Tests.Models
{
    [TestClass]
    public class ConnectionInformationTests
    {
        [TestMethod]
        [DataRow("http://localhost", "http://localhost/")]
        [DataRow("http://localhost/", "http://localhost/")]
        [DataRow("http://localhost:9000", "http://localhost:9000/")]
        [DataRow("https://localhost:9000/", "https://localhost:9000/")]
        [DataRow("https://local.sonarcloud.io", "https://local.sonarcloud.io/")]
        public void Ctor_SonarQubeUrl_IsProcessedCorrectly(string inputUrl, string expectedUrl)
        {
            var testSubject = new ConnectionInformation(new Uri(inputUrl));

            testSubject.ServerUri.ToString().Should().Be(expectedUrl);
            testSubject.IsSonarCloud.Should().BeFalse();
        }

        [TestMethod]
        [DataRow("http://sonarcloud.io") ]
        [DataRow("http://sonarcloud.io/") ]
        [DataRow("https://sonarcloud.io") ]
        [DataRow("https://sonarcloud.io/") ]
        [DataRow("http://SONARCLOUD.IO") ]
        [DataRow("http://www.sonarcloud.io") ]
        [DataRow("https://www.sonarcloud.io/") ]
        [DataRow("http://sonarcloud.io:9999") ]
        public void Ctor_SonarCloudUrl_IsProcessedCorrectly(string inputUrl)
        {
            var testSubject = new ConnectionInformation(new Uri(inputUrl));

            testSubject.ServerUri.Should().Be(ConnectionInformation.FixedSonarCloudUri);
            testSubject.IsSonarCloud.Should().BeTrue();
        }
    }
}
