using System.Threading;
using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SonarQube.Client.Models.ServerSentEvents.ClientContract;
using SonarQube.Client.Models.ServerSentEvents.ServerContract;
using System.Collections.Generic;
using System.Threading.Channels;

namespace SonarQube.Client.Models.ServerSentEvents
{
    public interface ISSEStreamReader
    {
        /// <summary>
        /// Will block the calling thread until an event exists or the connection is closed.
        /// Can throw an exception if the event is not a valid <see cref="IServerEvent"/>
        /// </summary>
        Task<IServerEvent> GetNextEventOrNullAsync();
    }

    /// <summary>
    /// Returns <see cref="IServerEvent"/> deserialized from <see cref="ISqServerEvent"/>
    /// Code on the java side: https://github.com/SonarSource/sonarlint-core/blob/4f34c7c844b12e331a61c63ad7105acac41d2efd/server-api/src/main/java/org/sonarsource/sonarlint/core/serverapi/push/PushApi.java
    /// </summary>
    internal class SSEStreamReader : ISSEStreamReader
    {
        private readonly ChannelReader<ISqServerEvent> sqEventsChannel;
        private readonly CancellationToken cancellationToken;

        private IDictionary<string, Type> eventConverters = new Dictionary<string, Type>
        {
            {"IssueChanged", typeof(IssueChangedServerEvent)},
            // todo: support later
            // {"TaintVulnerabilityClosed", typeof(TaintVulnerabilityClosedServerEvent)},
            // {"TaintVulnerabilityRaised", typeof(TaintVulnerabilityRaisedServerEvent)}
        };

        public SSEStreamReader(ChannelReader<ISqServerEvent> sqEventsChannel, CancellationToken cancellationToken)
        {
            this.cancellationToken = cancellationToken;
            this.sqEventsChannel = sqEventsChannel;
        }

        public async Task<IServerEvent> GetNextEventOrNullAsync()
        {
            var sqEvent = await sqEventsChannel.ReadAsync(cancellationToken);

            if (sqEvent == null)
            {
                return null;
            }

            if (!eventConverters.ContainsKey(sqEvent.Type))
            {
                throw new NotSupportedException($"Unknown ServerEventType: {sqEvent.Type}");
            }

            var deserializedEvent = JsonConvert.DeserializeObject(sqEvent.Data, eventConverters[sqEvent.Type]);

            return (IServerEvent)deserializedEvent;
        }
    }
}
