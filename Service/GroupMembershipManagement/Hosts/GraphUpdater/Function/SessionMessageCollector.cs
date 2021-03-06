// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using Entities.ServiceBus;
using Microsoft.Azure.ServiceBus;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Hosts.GraphUpdater
{
	public class SessionMessageCollector
	{
		private readonly IGraphUpdater _graphUpdater;

		// at the moment, each instance of the function only handles one message a time for memory usage reasons
		// but this is good to have for later if things change
        private static readonly ConcurrentDictionary<string, List<GroupMembershipMessage>> _receivedMessages = new ConcurrentDictionary<string, List<GroupMembershipMessage>>();

		public SessionMessageCollector(IGraphUpdater graphUpdater)
		{
			_graphUpdater = graphUpdater;
		}

		public async Task HandleNewMessage(GroupMembershipMessage body, IMessageSession messageSession)
		{
			var sessionId = messageSession.SessionId;
			_receivedMessages.AddOrUpdate(sessionId, new List<GroupMembershipMessage> { body }, (key, messages) => {
				messages.Add(body); return messages;
			});

			//Console.WriteLine($"Session {sessionId} got {body.Body.SourceMembers.Count} users.");
            if (body.Body.IsLastMessage)
			{
				if (!_receivedMessages.TryRemove(sessionId, out var allReceivedMessages))
				{
					// someone else got to it first. shouldn't happen, but it's good to be prepared.
					return;
				}

				// remove this message-renewing logic once we can verify that the new, longer message leases work.
				var source = new CancellationTokenSource();
				var cancellationToken = source.Token;
				var renew = RenewMessages(messageSession, allReceivedMessages, cancellationToken);

                var received = GroupMembership.Merge(allReceivedMessages.Select(x => x.Body));

				try
				{
					await _graphUpdater.CalculateDifference(received);

					// If it succeeded, complete all the messages and close the session
					await messageSession.CompleteAsync(allReceivedMessages.Select(x => x.LockToken));

					await messageSession.CloseAsync();
				}
				finally
				{
					source.Cancel();
				}
			}

		}

		private static readonly TimeSpan _waitBetweenRenew = TimeSpan.FromMinutes(4);
		private async Task RenewMessages(IMessageSession messageSession, List<GroupMembershipMessage> messages, CancellationToken cancellationToken)
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				await messageSession.RenewSessionLockAsync();
				foreach (var message in messages)
				{
					await messageSession.RenewLockAsync(message.LockToken);
				}
				await Task.Delay(_waitBetweenRenew);
			}
		}
	}
}
