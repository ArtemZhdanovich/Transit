// Copyright 2007-2016 Chris Patterson, Dru Sellers, Travis Smith, et. al.
//  
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use
// this file except in compliance with the License. You may obtain a copy of the 
// License at 
// 
//     http://www.apache.org/licenses/LICENSE-2.0 
// 
// Unless required by applicable law or agreed to in writing, software distributed
// under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR 
// CONDITIONS OF ANY KIND, either express or implied. See the License for the 
// specific language governing permissions and limitations under the License.
namespace MassTransit.EndpointConfigurators
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Builders;
    using BusConfigurators;
    using GreenPipes;
    using GreenPipes.Builders;
    using GreenPipes.Configurators;
    using GreenPipes.Validation;
    using Pipeline;
    using Pipeline.Filters;
    using Pipeline.Pipes;
    using Transports;


    public abstract class ReceiveEndpointSpecification :
        IConsumePipeConfigurator,
        ISendPipelineConfigurator,
        IPublishPipelineConfigurator
    {
        readonly IConsumePipe _consumePipe;
        readonly ConsumePipeConfigurator _consumePipeSpecification;
        readonly Lazy<Uri> _deadLetterAddress;
        readonly Lazy<Uri> _errorAddress;
        readonly Lazy<Uri> _inputAddress;
        readonly IList<string> _lateConfigurationKeys;
        readonly PublishPipeConfigurator _publishPipeConfigurator;
        readonly IBuildPipeConfigurator<ReceiveContext> _receiveConfigurator;
        readonly SendPipeConfigurator _sendPipeConfigurator;
        readonly IList<IReceiveEndpointSpecification> _specifications;

        protected ReceiveEndpointSpecification(IConsumePipe consumePipe)
        {
            _consumePipe = consumePipe;

            _specifications = new List<IReceiveEndpointSpecification>();
            _consumePipeSpecification = new ConsumePipeConfigurator();
            _sendPipeConfigurator = new SendPipeConfigurator();
            _publishPipeConfigurator = new PublishPipeConfigurator();
            _receiveConfigurator = new PipeConfigurator<ReceiveContext>();
            _lateConfigurationKeys = new List<string>();

            _inputAddress = new Lazy<Uri>(GetInputAddress);
            _errorAddress = new Lazy<Uri>(GetErrorAddress);
            _deadLetterAddress = new Lazy<Uri>(GetDeadLetterAddress);
        }

        public Uri InputAddress => _inputAddress.Value;

        void IPipeConfigurator<ConsumeContext>.AddPipeSpecification(IPipeSpecification<ConsumeContext> specification)
        {
            _consumePipeSpecification.AddPipeSpecification(specification);
        }

        void IConsumePipeConfigurator.AddPipeSpecification<T>(IPipeSpecification<ConsumeContext<T>> specification)
        {
            _consumePipeSpecification.AddPipeSpecification(specification);
        }

        public void ConfigurePublish(Action<IPublishPipeConfigurator> callback)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            callback(_publishPipeConfigurator);
        }

        public void ConfigureSend(Action<ISendPipeConfigurator> callback)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            callback(_sendPipeConfigurator);
        }

        public virtual IEnumerable<ValidationResult> Validate()
        {
            return _specifications.SelectMany(x => x.Validate())
                .Concat(_consumePipeSpecification.Validate())
                .Concat(_sendPipeConfigurator.Validate())
                .Concat(
                    _lateConfigurationKeys.Select(
                        x => new ConfigurationValidationResult(ValidationResultDisposition.Failure, x, "was configured after being used")));
        }

        public void AddEndpointSpecification(IReceiveEndpointSpecification configurator)
        {
            _specifications.Add(configurator);
        }

        protected IReceivePipe CreateReceivePipe(IReceiveEndpointBuilder builder)
        {
            foreach (var specification in _specifications)
                specification.Configure(builder);

            AddDeadLetterFilter(builder);

            AddRescueFilter(builder);

            _receiveConfigurator.UseFilter(new DeserializeFilter(builder.MessageDeserializer, builder.ConsumePipe));

            IPipe<ReceiveContext> receivePipe = _receiveConfigurator.Build();

            return new ReceivePipe(receivePipe, builder.ConsumePipe);
        }

        protected IConsumePipe CreateConsumePipe(IBusBuilder builder)
        {
            return _consumePipe ?? builder.CreateConsumePipe(_consumePipeSpecification);
        }

        protected ISendEndpointProvider CreateSendEndpointProvider(IReceiveEndpointBuilder builder)
        {
            return builder.CreateSendEndpointProvider(InputAddress, _sendPipeConfigurator);
        }

        protected IPublishEndpointProvider CreatePublishEndpointProvider(IReceiveEndpointBuilder builder)
        {
            return builder.CreatePublishEndpointProvider(InputAddress, _publishPipeConfigurator);
        }

        void AddDeadLetterFilter(IReceiveEndpointBuilder builder)
        {
            IPipe<ReceiveContext> moveToDeadLetterPipe = Pipe.New<ReceiveContext>(x =>
            {
                Func<Task<ISendTransport>> getDeadLetterTransport = () => builder.SendTransportProvider.GetSendTransport(_deadLetterAddress.Value);

                x.UseFilter(new MoveToTransportFilter(_deadLetterAddress.Value, getDeadLetterTransport, "dead-letter"));
            });

            _receiveConfigurator.UseDeadLetterQueue(moveToDeadLetterPipe);
        }

        void AddRescueFilter(IReceiveEndpointBuilder builder)
        {
            IPipe<ExceptionReceiveContext> moveToErrorPipe = Pipe.New<ExceptionReceiveContext>(x =>
            {
                Func<Task<ISendTransport>> getErrorTransport = () => builder.SendTransportProvider.GetSendTransport(_errorAddress.Value);

                var publishEndpointProvider = builder.CreatePublishEndpointProvider(InputAddress, _publishPipeConfigurator);

                x.UseFilter(new MoveExceptionToTransportFilter(publishEndpointProvider, _errorAddress.Value, getErrorTransport));
            });

            _receiveConfigurator.UseRescue(moveToErrorPipe);
        }

        protected virtual void Changed(string key)
        {
            if (IsAlreadyConfigured())
            {
                _lateConfigurationKeys.Add(key);
            }
        }

        protected virtual bool IsAlreadyConfigured()
        {
            return _inputAddress.IsValueCreated
                || _errorAddress.IsValueCreated
                || _deadLetterAddress.IsValueCreated;
        }

        protected abstract Uri GetInputAddress();

        protected abstract Uri GetErrorAddress();

        protected abstract Uri GetDeadLetterAddress();
    }
}