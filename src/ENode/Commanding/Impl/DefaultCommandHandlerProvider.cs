﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ECommon.Components;
using ENode.Infrastructure;

namespace ENode.Commanding.Impl
{
    public class DefaultCommandHandlerProvider : ICommandHandlerProvider, IAssemblyInitializer
    {
        private readonly IDictionary<Type, IList<ICommandHandler>> _handlerDict = new Dictionary<Type, IList<ICommandHandler>>();

        public void Initialize(params Assembly[] assemblies)
        {
            foreach (var handlerType in assemblies.SelectMany(assembly => assembly.GetTypes().Where(IsHandlerType)))
            {
                if (!TypeUtils.IsComponent(handlerType))
                {
                    throw new Exception(string.Format("Handler [type={0}] should be marked as component.", handlerType.FullName));
                }
                RegisterHandler(handlerType);
            }
        }
        public IEnumerable<ICommandHandler> GetHandlers(Type commandType)
        {
            var handlers = new List<ICommandHandler>();
            foreach (var key in _handlerDict.Keys.Where(key => key.IsAssignableFrom(commandType)))
            {
                handlers.AddRange(_handlerDict[key]);
            }
            return handlers;
        }

        private bool IsHandlerType(Type type)
        {
            return type != null && type.IsClass && !type.IsAbstract && ScanHandlerInterfaces(type).Any();
        }
        private void RegisterHandler(Type handlerType)
        {
            foreach (var handlerInterfaceType in ScanHandlerInterfaces(handlerType))
            {
                var argumentType = handlerInterfaceType.GetGenericArguments().Single();
                var handlerProxyType = typeof(CommandProxyHandler<>).MakeGenericType(argumentType);
                IList<ICommandHandler> handlers;
                if (!_handlerDict.TryGetValue(argumentType, out handlers))
                {
                    handlers = new List<ICommandHandler>();
                    _handlerDict.Add(argumentType, handlers);
                }

                if (handlers.Any(x => x.GetInnerHandler().GetType() == handlerType))
                {
                    continue;
                }

                handlers.Add(Activator.CreateInstance(handlerProxyType, new[] { ObjectContainer.Resolve(handlerType) }) as ICommandHandler);
            }
        }
        private IEnumerable<Type> ScanHandlerInterfaces(Type type)
        {
            return type.GetInterfaces().Where(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(ICommandHandler<>));
        }
    }
}
