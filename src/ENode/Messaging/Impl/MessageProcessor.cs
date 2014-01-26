﻿using System;
using System.Collections.Generic;
using ECommon.IoC;
using ECommon.Logging;
using ECommon.Scheduling;

namespace ENode.Messaging.Impl
{
    /// <summary>The abstract implementation of IMessageProcessor.
    /// </summary>
    public abstract class MessageProcessor<TQueue, TMessageExecutor, TMessagePayload> : IMessageProcessor<TQueue, TMessagePayload>
        where TQueue : class, IMessageQueue<TMessagePayload>
        where TMessageExecutor : class, IMessageHandler<TMessagePayload>
    {
        private readonly IList<Worker> _workers;
        private readonly TQueue _bindingQueue;
        private readonly ILogger _logger;
        private bool _started;

        /// <summary>The binding queue of the message processor.
        /// </summary>
        public TQueue BindingQueue
        {
            get { return _bindingQueue; }
        }

        /// <summary>Parameterized constructor.
        /// </summary>
        /// <param name="bindingQueue"></param>
        /// <param name="messageExecutorCount"></param>
        /// <param name="messageDequeueIntervalMilliseconds"></param>
        protected MessageProcessor(TQueue bindingQueue, int messageExecutorCount, int messageDequeueIntervalMilliseconds)
        {
            if (bindingQueue == null)
            {
                throw new ArgumentNullException("bindingQueue");
            }
            if (messageExecutorCount <= 0)
            {
                throw new ArgumentException(string.Format("There must at least one message executor for {0}.", GetType().Name));
            }

            _bindingQueue = bindingQueue;
            _workers = new List<Worker>();

            for (var index = 0; index < messageExecutorCount; index++)
            {
                var messageExecutor = ObjectContainer.Resolve<TMessageExecutor>();
                Worker worker;
                if (messageDequeueIntervalMilliseconds > 0)
                {
                    worker = new Worker(() => ProcessMessage(messageExecutor), messageDequeueIntervalMilliseconds);
                }
                else
                {
                    worker = new Worker(() => ProcessMessage(messageExecutor));
                }
                _workers.Add(worker);
            }

            _logger = ObjectContainer.Resolve<ILoggerFactory>().Create(GetType().Name);
            _started = false;
        }

        /// <summary>Start the message processor.
        /// </summary>
        public void Start()
        {
            if (_started) return;

            foreach (var worker in _workers)
            {
                worker.Start();
            }
            _started = true;
            _logger.InfoFormat("[0] started, binding queue {1}, worker count:{2}.", GetType().Name, _bindingQueue.Name, _workers.Count);
        }

        private void ProcessMessage(TMessageExecutor messageExecutor)
        {
            var message = _bindingQueue.Dequeue();
            if (message == null) return;
            try
            {
                messageExecutor.Handle(message);
            }
            catch (Exception ex)
            {
                _logger.Error(string.Format("Exception raised when executing message:{0}.", message), ex);
            }
        }
    }
}
