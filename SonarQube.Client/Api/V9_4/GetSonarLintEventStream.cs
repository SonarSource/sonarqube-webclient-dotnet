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
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SonarQube.Client.Models;
using SonarQube.Client.Requests;

namespace SonarQube.Client.Api.V9_4
{
    internal class GetSonarLintEventStream : RequestBase<Stream>, IGetSonarLintEventStream
    {
        private static readonly string AllKnownLanguages = string.Join(",", SonarQubeLanguage.AllLanguages.Select(x => x.Key));

        private const int MaxNumberOfRequestAttempts = 10;

        protected override string Path => "api/push/sonarlint_events";

        protected override MediaTypeWithQualityHeaderValue[] AllowedMediaTypeHeaders =>
            new[]
            {
                MediaTypeWithQualityHeaderValue.Parse("text/event-stream")
            };

        public override async Task<Stream> InvokeAsync(HttpClient httpClient, CancellationToken token)
        {
            var requestAttempts = 0;

            return await InvokeWithRetriesAsync();

            // Code on the java side: https://github.com/SonarSource/sonarlint-core/blob/4f34c7c844b12e331a61c63ad7105acac41d2efd/server-api/src/main/java/org/sonarsource/sonarlint/core/serverapi/stream/EventStream.java#L101
            async Task<Stream> InvokeWithRetriesAsync()
            {
                do
                {
                    requestAttempts++;

                    var result = await InvokeUncheckedAsync(httpClient, token);

                    if (ShouldRetry(result) && requestAttempts < MaxNumberOfRequestAttempts)
                    {
                        continue;
                    }

                    result.EnsureSuccess();

                    return result.Value;
                } while (true);
            }
        }

        private static bool ShouldRetry(Result<Stream> result)
        {
            var isRecoverableStatus = result.StatusCode != HttpStatusCode.Forbidden &&
                                      result.StatusCode != HttpStatusCode.Unauthorized &&
                                      result.StatusCode != HttpStatusCode.NotFound;

            return !result.IsSuccess && isRecoverableStatus;
        }

        protected override async Task<Result<Stream>> ReadResponseAsync(HttpResponseMessage httpResponse)
        {
            var stream = await httpResponse.Content.ReadAsStreamAsync();

            return new Result<Stream>(httpResponse, stream);
        }

        protected override Stream ParseResponse(string response)
        {
            // should not be called
            throw new InvalidOperationException();
        }

        [JsonProperty("languages")] 
        public string Languages { get; set; } = AllKnownLanguages;

        [JsonProperty("projectKeys")]
        public string ProjectKey { get; set; }
    }
}
