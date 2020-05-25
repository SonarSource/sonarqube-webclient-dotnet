﻿/*
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

using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace SonarQube.Client.Requests
{
    /// <summary>
    /// Abstract implementation of IPagedRequest<typeparamref name="TResponseItem"/> that automatically downloads all pages
    /// from the server.
    /// </summary>
    /// <typeparam name="TResponseItem">The type of the items returned by this request.</typeparam>
    public abstract class PagedRequestBase<TResponseItem> : RequestBase<TResponseItem[]>, IPagedRequest<TResponseItem>
    {
        private const int FirstPage = 1;
        private const int MaximumPageSize = 500;

        [JsonIgnore]
        public virtual int? MaxPageNumber { get; set; }

        [JsonProperty("p")]
        public virtual int Page { get; set; } = FirstPage;

        [JsonProperty("ps")]
        public virtual int PageSize { get; set; } = MaximumPageSize;

        public override async Task<TResponseItem[]> InvokeAsync(HttpClient httpClient, CancellationToken token)
        {
            var allResponseItems = new List<TResponseItem>();

            Result<TResponseItem[]> pageResult;
            do
            {
                pageResult = await InvokeUncheckedAsync(httpClient, token);
                ValidateResult(pageResult, allResponseItems);

                if (pageResult.Value != null)
                {
                    allResponseItems.AddRange(pageResult.Value);
                    Logger.Debug($"Received {pageResult.Value.Length} items.");
                }

                Page++;
            }
            while (!ReachedMaxPageNumber() &&
                pageResult.Value != null &&
                // Continue paging until we get a partial page of results i.e. fewer than requested.
                // NB there is a bug here: should be comparing against the request page size, not the
                // maximum allowed size. See https://github.com/SonarSource/sonarqube-webclient-dotnet/issues/8
                pageResult.Value.Length >= MaximumPageSize);

            return allResponseItems.ToArray();
        }

        private bool ReachedMaxPageNumber()
        {
            return MaxPageNumber.HasValue && Page > MaxPageNumber.Value;
        }

        protected virtual void ValidateResult(Result<TResponseItem[]> pageResult, List<TResponseItem> allResponseItems) =>
            pageResult.EnsureSuccess();
    }
}
