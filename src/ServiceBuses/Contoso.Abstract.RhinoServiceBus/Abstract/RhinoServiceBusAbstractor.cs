#region License
/*
The MIT License

Copyright (c) 2008 Sky Morey

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
*/
#endregion
using System;
using System.Linq;
using System.Abstract;
using Contoso.Abstract.RhinoServiceBus;
using Rhino.ServiceBus;
using Rhino.ServiceBus.Config;
using Rhino.ServiceBus.Impl;
using IServiceBus = Rhino.ServiceBus.IServiceBus;
using IMessageConsumer = Rhino.ServiceBus.Internal.IMessageConsumer;
using IOccasionalMessageConsumer = Rhino.ServiceBus.Internal.IOccasionalMessageConsumer;
using System.Reflection;
// [Main] http://hibernatingrhinos.com/open-source/rhino-service-bus
namespace Contoso.Abstract
{
    /// <summary>
    /// IRhinoServiceBus
    /// </summary>
    public interface IRhinoServiceBus : IPublishingServiceBus
    {
        /// <summary>
        /// Gets the bus.
        /// </summary>
        IServiceBus Bus { get; }
    }

    /// <summary>
    /// RhinoServiceBusAbstractor
    /// </summary>
    public partial class RhinoServiceBusAbstractor : IRhinoServiceBus, ServiceBusManager.ISetupRegistration
    {
        private IServiceLocator _serviceLocator;

        static RhinoServiceBusAbstractor() { ServiceBusManager.EnsureRegistration(); }
        /// <summary>
        /// Initializes a new instance of the <see cref="RhinoServiceBusAbstractor"/> class.
        /// </summary>
        public RhinoServiceBusAbstractor()
            : this(ServiceLocatorManager.Current, DefaultBusCreator(null, null, null)) { }
        /// <summary>
        /// Initializes a new instance of the <see cref="RhinoServiceBusAbstractor"/> class.
        /// </summary>
        /// <param name="serviceLocator">The service locator.</param>
        public RhinoServiceBusAbstractor(IServiceLocator serviceLocator)
            : this(serviceLocator, DefaultBusCreator(serviceLocator, null, null)) { }
        /// <summary>
        /// Initializes a new instance of the <see cref="RhinoServiceBusAbstractor"/> class.
        /// </summary>
        /// <param name="busConfiguration">The bus configuration.</param>
        public RhinoServiceBusAbstractor(BusConfigurationSection busConfiguration)
            : this(ServiceLocatorManager.Current, DefaultBusCreator(null, busConfiguration, null)) { }
        /// <summary>
        /// Initializes a new instance of the <see cref="RhinoServiceBusAbstractor"/> class.
        /// </summary>
        /// <param name="serviceLocator">The service locator.</param>
        /// <param name="busConfiguration">The bus configuration.</param>
        public RhinoServiceBusAbstractor(IServiceLocator serviceLocator, BusConfigurationSection busConfiguration)
            : this(serviceLocator, DefaultBusCreator(serviceLocator, busConfiguration, null)) { }
        /// <summary>
        /// Initializes a new instance of the <see cref="RhinoServiceBusAbstractor"/> class.
        /// </summary>
        /// <param name="serviceLocator">The service locator.</param>
        /// <param name="bus">The bus.</param>
        public RhinoServiceBusAbstractor(IServiceLocator serviceLocator, IStartableServiceBus bus)
            : this(serviceLocator, (IServiceBus)bus) { bus.Start(); }
        /// <summary>
        /// Initializes a new instance of the <see cref="RhinoServiceBusAbstractor"/> class.
        /// </summary>
        /// <param name="serviceLocator">The service locator.</param>
        /// <param name="bus">The bus.</param>
        public RhinoServiceBusAbstractor(IServiceLocator serviceLocator, IServiceBus bus)
        {
            if (serviceLocator == null)
                throw new ArgumentNullException("serviceLocator");
            if (bus == null)
                throw new ArgumentNullException("bus", "The specified bus cannot be null.");
            _serviceLocator = serviceLocator;
            Bus = bus;
            CommonLoggingFactoryAdapter.EnsureRegistration(); 
        }

        Action<IServiceLocator, string> ServiceBusManager.ISetupRegistration.DefaultServiceRegistrar
        {
            get { return (locator, name) => ServiceBusManager.RegisterInstance<IRhinoServiceBus>(this, locator, name); }
        }

        /// <summary>
        /// Gets the service object of the specified type.
        /// </summary>
        /// <param name="serviceType">An object that specifies the type of service object to get.</param>
        /// <returns>
        /// A service object of type <paramref name="serviceType"/>.
        /// -or-
        /// null if there is no service object of type <paramref name="serviceType"/>.
        /// </returns>
        public object GetService(Type serviceType) { throw new NotImplementedException(); }

        /// <summary>
        /// Creates the message.
        /// </summary>
        /// <typeparam name="TMessage">The type of the message.</typeparam>
        /// <param name="messageBuilder">The message builder.</param>
        /// <returns></returns>
        public TMessage CreateMessage<TMessage>(Action<TMessage> messageBuilder)
            where TMessage : class
        {
            var message = _serviceLocator.Resolve<TMessage>();
            if (messageBuilder != null)
                messageBuilder(message);
            return message;
        }

        /// <summary>
        /// Sends the specified endpoint.
        /// </summary>
        /// <param name="endpoint">The endpoint.</param>
        /// <param name="messages">The messages.</param>
        /// <returns></returns>
        public virtual IServiceBusCallback Send(IServiceBusEndpoint endpoint, params object[] messages)
        {
            if (messages == null || messages.Length == 0 || messages[0] == null)
                throw new ArgumentNullException("messages", "Please include at least one message.");
            try
            {
                if (endpoint == null) Bus.Send(messages);
                else if (endpoint != ServiceBus.SelfEndpoint) Bus.Send(RhinoServiceBusTransport.Cast(endpoint), messages);
                else Bus.SendToSelf(messages);
            }
            catch (Exception ex) { throw new ServiceBusMessageException(messages[0].GetType(), ex); }
            return null;
        }

        /// <summary>
        /// Replies the specified messages.
        /// </summary>
        /// <param name="messages">The messages.</param>
        public virtual void Reply(params object[] messages)
        {
            if (messages == null || messages.Length == 0 || messages[0] == null)
                throw new ArgumentNullException("messages", "Please include at least one message.");
            try { Bus.Reply(messages); }
            catch (Exception ex) { throw new ServiceBusMessageException(messages[0].GetType(), ex); }
        }

        #region Publishing ServiceBus

        /// <summary>
        /// Publishes the specified messages.
        /// </summary>
        /// <param name="messages">The messages.</param>
        public virtual void Publish(params object[] messages)
        {

            try { Bus.Publish(messages); }
            catch (Exception ex) { throw new ServiceBusMessageException(messages[0].GetType(), ex); }
        }

        /// <summary>
        /// Subscribes the specified message type.
        /// </summary>
        /// <param name="messageType">Type of the message.</param>
        /// <param name="condition">The condition.</param>
        public virtual void Subscribe(Type messageType, Predicate<object> condition)
        {
            if (messageType == null)
                throw new ArgumentNullException("messageType");
            if (condition != null)
                throw new ArgumentException("condition", "Must be null.");
            try { Bus.Subscribe(messageType); }
            catch (Exception ex) { throw new ServiceBusMessageException(messageType, ex); }
        }

        /// <summary>
        /// Unsubscribes the specified message type.
        /// </summary>
        /// <param name="messageType">Type of the message.</param>
        public virtual void Unsubscribe(Type messageType)
        {
            if (messageType == null)
                throw new ArgumentNullException("messageType");
            try { Bus.Unsubscribe(messageType); }
            catch (Exception ex) { throw new ServiceBusMessageException(messageType, ex); }
        }

        #endregion

        #region Domain-specific

        /// <summary>
        /// Gets the bus.
        /// </summary>
        public IServiceBus Bus { get; private set; }

        #endregion

        /// <summary>
        /// Defaults the bus creator.
        /// </summary>
        /// <param name="serviceLocator">The service locator.</param>
        /// <param name="busConfiguration">The bus configuration.</param>
        /// <param name="configurator">The configurator.</param>
        /// <returns></returns>
        public static IStartableServiceBus DefaultBusCreator(IServiceLocator serviceLocator, BusConfigurationSection busConfiguration, Action<AbstractRhinoServiceBusConfiguration> configurator)
        {
            if (serviceLocator == null)
                serviceLocator = ServiceLocatorManager.Current;
            var configuration = new RhinoServiceBusConfiguration()
                .UseMessageSerializer<RhinoServiceBus.Serializers.XmlMessageSerializer>()
                .UseAbstractServiceLocator(serviceLocator);
            if (busConfiguration != null)
                configuration.UseConfiguration(busConfiguration);
            if (configurator != null)
                configurator(configuration);
            configuration.Configure();
            return serviceLocator.Resolve<IStartableServiceBus>();
        }

        /// <summary>
        /// Configures the consumers.
        /// </summary>
        /// <param name="serviceLocator">The service locator.</param>
        /// <param name="assemblyToScan">The assembly to scan.</param>
        /// <param name="condition">The condition.</param>
        public static void ConfigureConsumers(IServiceLocator serviceLocator, Assembly assemblyToScan, Predicate<Type> condition)
        {
            if (serviceLocator == null)
                serviceLocator = ServiceLocatorManager.Current;
            if (assemblyToScan == null)
                throw new ArgumentNullException("assemblyToScan");
            var types = assemblyToScan.GetTypes()
                .Where(type => typeof(IMessageConsumer).IsAssignableFrom(type) &&
                    !typeof(IOccasionalMessageConsumer).IsAssignableFrom(type) &&
                    (condition == null || condition(type)));
            var r = serviceLocator.Registrar;
            foreach (var type in types)
                ConfigureConsumer(r, type);
        }

        /// <summary>
        /// Configures the consumer.
        /// </summary>
        /// <param name="r">The r.</param>
        /// <param name="type">The type.</param>
        public static void ConfigureConsumer(IServiceRegistrar r, Type type)
        {
            r.Register<IMessageConsumer>(type, type.FullName);
        }
    }
}
