﻿/*
 * SonarQube Client
 * Copyright (C) 2016-2022 SonarSource SA
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
using Newtonsoft.Json;

namespace SonarQube.Client.Models
{
    public class ServerExclusions
    {
        private static readonly string[] EmptyValues = Array.Empty<string>();

        public ServerExclusions()
            : this(null, null, null)
        {
        }

        public ServerExclusions(string[] exclusions,
            string[] globalExclusions,
            string[] inclusions)
        {
            Exclusions = exclusions ?? EmptyValues;
            GlobalExclusions = globalExclusions ?? EmptyValues;
            Inclusions = inclusions ?? EmptyValues;
        }

        [JsonProperty("sonar.exclusions")]
        public string[] Exclusions { get; set; }

        [JsonProperty("sonar.global.exclusions")]
        public string[] GlobalExclusions { get; set; }

        [JsonProperty("sonar.inclusions")]
        public string[] Inclusions { get; set; }

        public override string ToString()
        {
            return "Server Exclusions: " +
                   "\n    Inclusions: " + string.Join(",", Inclusions) +
                   "\n    Exclusions: " + string.Join(",", Exclusions) +
                   "\n    Global Exclusions: " + string.Join(",", GlobalExclusions);
        }

        public override bool Equals(object obj)
        {
            return Equals((ServerExclusions)obj);
        }

        public bool Equals(ServerExclusions other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;

            return ToString().Equals(other.ToString());
        }
    }
}
