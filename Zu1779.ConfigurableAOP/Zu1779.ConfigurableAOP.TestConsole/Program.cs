namespace Zu1779.ConfigurableAOP.TestConsole
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;

    using LightInject;
    using LightInject.Interception;

    class Program
    {
        static async Task Main()
        {
            ServiceContainer serviceContainer = new ServiceContainer();
            serviceContainer.RegisterFrom<CompositionRoot>();

            Program prg = serviceContainer.GetInstance<Program>();
            await prg.ProgramTask();

            Console.WriteLine("\r\nPress any key");
            Console.ReadKey();
        }

        public Program(IOuterService outerService)
        {
            this.outerService = outerService;
        }
        private readonly IOuterService outerService;

        public async Task ProgramTask()
        {
            await outerService.Task();
        }
    }

    class CompositionRoot : ICompositionRoot
    {
        public void Compose(IServiceRegistry serviceRegistry)
        {
            serviceRegistry.Register<Program>();
            serviceRegistry.Register<IOuterService, OuterService>();
            serviceRegistry.Register<IInnerService, InnerService>();
            serviceRegistry.Register<DebugInterceptor>();
            serviceRegistry.Register<DataTraceInterceptor>();
            serviceRegistry.Register<PerformanceInterceptor>();
            //serviceRegistry.Intercept(sr => sr.ServiceType == typeof(IInnerService), sf => sf.GetInstance<DebugInterceptor>());
            //serviceRegistry.Intercept(sr => sr.ServiceType == typeof(IInnerService), sf => sf.GetInstance<DataTraceInterceptor>());
            //serviceRegistry.Intercept(sr => sr.ServiceType == typeof(IInnerService), sf => sf.GetInstance<PerformanceInterceptor>());

            // Method interception
            Action<IServiceFactory, ProxyDefinition> defineProxy = (sf, pd) =>
            {
                pd.Implement(() => sf.GetInstance<DataTraceInterceptor>(), m => m.Name == "Design");
            };
            serviceRegistry.Intercept(sr => sr.ServiceType == typeof(IInnerService), (sf, pd) => defineProxy(sf, pd));
        }
    }

    class DebugInterceptor : HybridInterceptor
    {
        protected override object BeforeInvoke(IInvocationInfo invocationInfo, Dictionary<string, object> additionalData)
        {
            Console.WriteLine($"* Before {invocationInfo.TargetMethod.Name}");
            var response = base.BeforeInvoke(invocationInfo, additionalData);
            return response;
        }
        protected override object AfterInvoke(IInvocationInfo invocationInfo, object response, Dictionary<string, object> additionalData)
        {
            Console.WriteLine($"* After {invocationInfo.TargetMethod.Name}");
            return response;
        }
    }

    class DataTraceInterceptor : HybridInterceptor
    {
        protected override object AfterInvoke(IInvocationInfo invocationInfo, object response, Dictionary<string, object> _)
        {
            Console.WriteLine($"CALL => {invocationInfo.Signature()} => RETURN => {response}");
            return response;
        }
    }
    class PerformanceInterceptor : HybridInterceptor
    {
        protected override object BeforeInvoke(IInvocationInfo invocationInfo, Dictionary<string, object> additionalData)
        {
            additionalData.Add("stopwatch", Stopwatch.StartNew());
            return base.BeforeInvoke(invocationInfo, additionalData);
        }
        protected override object AfterInvoke(IInvocationInfo invocationInfo, object response, Dictionary<string, object> additionalData)
        {
            Stopwatch sw = additionalData["stopwatch"] as Stopwatch;
            Console.WriteLine($"CALL => {invocationInfo.Signature()} => TOOK => {sw.ElapsedMilliseconds} ms");
            return base.AfterInvoke(invocationInfo, response, additionalData);
        }
    }

    static class InvocationExtension
    {
        public static string Signature(this IInvocationInfo invocationInfo)
        {
            string returnType = invocationInfo.Method.ReturnType.ToString();
            string methodName = invocationInfo.Method.Name;
            string arguments = string.Join(", ", invocationInfo.Method.GetParameters().Select((c, i) => $"{c.ParameterType} {c.Name} = {invocationInfo.Arguments[i]}"));
            return $"{returnType} {methodName}({arguments})";
        }
    }

    public interface IOuterService
    {
        Task Task();
    }
    class OuterService : IOuterService
    {
        public OuterService(IInnerService innerService)
        {
            this.innerService = innerService;
        }
        private readonly IInnerService innerService;

        public async Task Task()
        {
            Console.WriteLine("Going to make some tasks");
            innerService.Analyse();
            innerService.Design();
            innerService.Develope();
            await innerService.Log();
            await innerService.GetTime();
            //await innerService.Await2sec();
        }
    }

    public interface IInnerService
    {
        void Analyse();
        void Design(int layers = 3);
        void Develope();
        Task Log();
        Task<DateTime> GetTime();
        Task Await2sec();
    }
    class InnerService : IInnerService
    {
        public void Analyse() => Console.WriteLine("I'm going to analyse");
        public void Design(int layers = 3) => Console.WriteLine($"I'm going to design {layers} layers");
        public void Develope() => Console.WriteLine("I'm going to develope");

        public async Task Log()
        {
            await Task.Delay(1);
            Console.WriteLine($"Time to log something");
        }

        public async Task Await2sec()
        {
            await Task.Delay(4000);
        }

        public async Task<DateTime> GetTime()
        {
            var now = await Task.Run(() => DateTime.Now);
            Console.WriteLine($"Well ... think it's better to check the clock");
            return now;
        }
    }
}
