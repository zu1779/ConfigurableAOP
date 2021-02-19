namespace Zu1779.ConfigurableAOP
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Concurrent;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;

    using LightInject.Interception;

    public abstract class HybridInterceptor : IInterceptor
    {
        static HybridInterceptor()
        {
            OpenGenericInvokeAsyncMethod = typeof(HybridInterceptor).GetTypeInfo().DeclaredMethods
                .FirstOrDefault(m => m.IsGenericMethod && m.Name == "InvokeAsync");
        }
        private static readonly MethodInfo OpenGenericInvokeAsyncMethod;
        private static readonly ConcurrentDictionary<Type, TaskType> TaskTypes = new ConcurrentDictionary<Type, TaskType>();
        private static readonly ConcurrentDictionary<Type, Func<object, object[], object>> InvokeAsyncDelegates =
            new ConcurrentDictionary<Type, Func<object, object[], object>>();
        private readonly IMethodBuilder methodBuilder = new DynamicMethodBuilder();

        public object Invoke(IInvocationInfo invocationInfo)
        {
            Type returnType = invocationInfo.Method.ReturnType;
            TaskType taskType = GetTaskType(returnType);

            if (taskType == TaskType.Task) return InvokeAsync(invocationInfo);
            else if (taskType == TaskType.TaskOfT) return GetInvokeAsyncDelegate(returnType)(this, new object[] { invocationInfo });
            else return InnerInvoke(invocationInfo);
        }

        /// <summary>
        /// Remember to return base method to return response of intercepted method or just return a value to avoid call.
        /// </summary>
        protected virtual object BeforeInvoke(IInvocationInfo invocationInfo, Dictionary<string, object> additionalData) => invocationInfo.Proceed();
        /// <summary>
        /// Base implementation just return response parameter (don't need to call that if you override)
        /// </summary>
        protected virtual object AfterInvoke(IInvocationInfo invocationInfo, object response, Dictionary<string, object> additionalData) => response;
        private object InnerInvoke(IInvocationInfo invocationInfo)
        {
            Dictionary<string, object> additionalData = new Dictionary<string, object>();
            object response = BeforeInvoke(invocationInfo, additionalData);
            return AfterInvoke(invocationInfo, response, additionalData);
        }
        protected virtual async Task InvokeAsync(IInvocationInfo invocationInfo)
        {
            Dictionary<string, object> additionalData = new Dictionary<string, object>();
            await (Task)BeforeInvoke(invocationInfo, additionalData);
            AfterInvoke(invocationInfo, null, additionalData);
        }
        protected virtual async Task<T> InvokeAsync<T>(IInvocationInfo invocationInfo)
        {
            Dictionary<string, object> additionalData = new Dictionary<string, object>();
            var response = await (Task<T>)BeforeInvoke(invocationInfo, additionalData);
            return await Task.FromResult((T)AfterInvoke(invocationInfo, response, additionalData));
        }
        private Func<object, object[], object> GetInvokeAsyncDelegate(Type returnType)
        {
            Func<object, object[], object> response = InvokeAsyncDelegates.GetOrAdd(returnType, CreateInvokeAsyncDelegate);
            return response;
        }
        private Func<object, object[], object> CreateInvokeAsyncDelegate(Type returnType)
        {
            var closedGenericInvokeMethod = CreateClosedGenericInvokeMethod(returnType);
            return methodBuilder.GetDelegate(closedGenericInvokeMethod);
        }
        private static MethodInfo CreateClosedGenericInvokeMethod(Type returnType)
        {
            MethodInfo response = OpenGenericInvokeAsyncMethod.MakeGenericMethod(returnType.GetTypeInfo().GenericTypeArguments);
            return response;
        }

        private static TaskType GetTaskType(Type returnType) => TaskTypes.GetOrAdd(returnType, ResolveTaskType);
        private static TaskType ResolveTaskType(Type returnType)
        {
            if (IsTask(returnType)) return TaskType.Task;
            else if (IsTaskOfT(returnType)) return TaskType.TaskOfT;
            else return TaskType.None;
        }
        private static bool IsTask(Type returnType) => returnType == typeof(Task);
        private static bool IsTaskOfT(Type returnType) =>
            returnType.GetTypeInfo().IsGenericType && returnType.GetTypeInfo().GetGenericTypeDefinition() == typeof(Task<>);

        private enum TaskType { None, Task, TaskOfT }
    }
}
