using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameOfCardsCsharp
{
    /// <summary>
    /// Event publisher template
    /// </summary>
    public class EventPublisher<TEventArgs>
    {
        private class HandlerEntry
        {
            public long Id { get; set; }
            public Action<TEventArgs>? Handler { get; set; }
        }

        private readonly List<HandlerEntry> handlers = new();
        private long nextId = 0;

        public long Subscribe(Action<TEventArgs> handler)
        {
            long id = nextId++;
            handlers.Add(new HandlerEntry { Id = id, Handler = handler });
            return id;
        }

        public void Unsubscribe(long id)
        {
            handlers.RemoveAll(entry => entry.Id == id);
        }

        public void Publish(TEventArgs args)
        {
            foreach (var entry in handlers)
            {
                entry.Handler?.Invoke(args);
            }
        }

        public void Clear()
        {
            handlers.Clear();
        }

        public int SubscriberCount() => handlers.Count;
    }
}