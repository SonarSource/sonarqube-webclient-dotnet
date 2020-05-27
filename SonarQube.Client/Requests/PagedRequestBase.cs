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
using System.Collections.Generic;
using System.Linq;
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
        public const int MaximumPageSize = 500;
        public const int MaximumItemsCount = 10000;

        [JsonIgnore] public virtual int ItemsLimit { get; set; } = MaximumItemsCount;

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
                pageResult.Value.Length >= PageSize);

            return allResponseItems.Take(ItemsLimit).ToArray();
        }

        private bool ReachedMaxPageNumber()
        {
            var maxPageNumber = (int)Math.Ceiling(a: ItemsLimit / (double)PageSize);
            var reachedMaxPage = Page > maxPageNumber;

            if (reachedMaxPage)
            {
                Logger.Warning("The SonarQube maximum API response limit reached");
            }

            return reachedMaxPage;
        }

        protected virtual void ValidateResult(Result<TResponseItem[]> pageResult, List<TResponseItem> allResponseItems) =>
            pageResult.EnsureSuccess();
    }
}
