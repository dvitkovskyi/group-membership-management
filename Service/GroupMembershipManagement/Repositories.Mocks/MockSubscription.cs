// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using Microsoft.Azure.ServiceBus;
using System.Collections.Generic;

namespace Repositories.Mocks
{
    public class MockSubscription
    {
        public string Name { get; set; }
        public List<Message> Messages { get; set; }
    }
}
